using System;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WinSCP;

namespace AcrlSync.Model
{
    /// <summary>
    /// The connection details fetched from acrlonline.org at startup.
    ///
    /// This is the whole point of the 2026 rebuild: the sync server can move
    /// and the app follows, without every driver reinstalling. The site is the
    /// source of truth ("remote always wins"); the shipped connection.json is
    /// only the fallback for when the site cannot be reached.
    ///
    /// The one thing hardcoded is the config URL itself, on acrlonline.org -
    /// the league's own domain, steered by DNS. If THAT ever had to change the
    /// app would need reissuing, but a domain the league controls is exactly
    /// the stable point to anchor to, and it is why the old app's mistake
    /// (anchoring to a HOST it did not control, acrl.rackservice.org) was the
    /// problem in the first place.
    /// </summary>
    public class RemoteConfig
    {
        private const string ConfigUrl = "https://acrlonline.org/sync/config.json";
        private const int TimeoutMs = 5000;

        public string MinimumVersion { get; private set; }

        /// <summary>
        /// True when a config was fetched and parsed. False means the site was
        /// unreachable or gave nonsense, and the caller should keep whatever it
        /// loaded locally.
        /// </summary>
        public bool Ok { get; private set; }

        private JObject _connection;

        /// <summary>
        /// Fetch and parse, swallowing every failure into Ok = false. A site
        /// that is down, slow, or serving garbage must never stop the app - it
        /// just falls back to its local connection.json, exactly as it did
        /// before this feature existed.
        /// </summary>
        public static RemoteConfig Fetch()
        {
            RemoteConfig result = new RemoteConfig { Ok = false };

            try
            {
                // TLS 1.2 explicitly. .NET Framework picks a default per the OS
                // and older machines can still default to something the edge
                // rejects; being explicit costs nothing.
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ConfigUrl);
                request.Method = "GET";
                request.Timeout = TimeoutMs;
                request.ReadWriteTimeout = TimeoutMs;
                request.UserAgent = "ACRLSync";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        return result;

                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        JObject root = JObject.Parse(reader.ReadToEnd());

                        JObject connection = root["connection"] as JObject;
                        if (connection == null)
                            return result;

                        result._connection = connection;
                        result.MinimumVersion = (string)root["minimum_version"];
                        result.Ok = true;
                    }
                }
            }
            catch
            {
                // Unreachable, timed out, bad TLS, malformed JSON - all the
                // same to us: no usable remote config, use local.
                result.Ok = false;
            }

            return result;
        }

        /// <summary>
        /// Apply the fetched connection onto the live SessionOptions. Only
        /// fields actually present are touched, so a partial config leaves the
        /// rest as the local file set them.
        /// </summary>
        public void ApplyTo(SessionOptions options)
        {
            if (!Ok || _connection == null)
                return;

            SetString(options, "HostName", v => options.HostName = v);
            SetString(options, "WebdavRoot", v => options.WebdavRoot = v);
            SetString(options, "UserName", v => options.UserName = v);
            SetString(options, "Password", v => options.Password = v);

            JToken port = _connection["PortNumber"];
            if (port != null && port.Type == JTokenType.Integer)
                options.PortNumber = (int)port;

            JToken protocol = _connection["Protocol"];
            if (protocol != null && protocol.Type == JTokenType.Integer)
                options.Protocol = (Protocol)(int)protocol;

            JToken secure = _connection["WebdavSecure"];
            if (secure != null && (secure.Type == JTokenType.Boolean))
                options.WebdavSecure = (bool)secure;
        }

        private void SetString(SessionOptions options, string key, Action<string> set)
        {
            JToken token = _connection[key];
            if (token != null && token.Type == JTokenType.String)
                set((string)token);
        }

        /// <summary>
        /// Is the running app older than the site now requires? A soft signal:
        /// the caller warns, but still tries, because a mis-set minimum must
        /// never brick a working app. Unknown or unparseable versions are
        /// treated as fine.
        /// </summary>
        public bool RequiresNewerApp(Version current)
        {
            Version required;
            if (!Ok || string.IsNullOrWhiteSpace(MinimumVersion) ||
                !Version.TryParse(MinimumVersion, out required))
            {
                return false;
            }

            return current < required;
        }
    }
}
