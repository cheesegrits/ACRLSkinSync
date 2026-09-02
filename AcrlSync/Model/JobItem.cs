using System.Collections.Generic;
using System.Windows.Forms.VisualStyles;
using WinSCP;

namespace AcrlSync.Model
{
    static public class ConnectionSettings
    {
        static private SessionOptions sessionOptions = new SessionOptions
        {
            Protocol = Protocol.Ftp,
            HostName = "host",
            UserName = "user",
            Password = "pword",
        };

        static public SessionOptions Options { get { return sessionOptions; } }
        static public void SetHost(string ip)
        {
            sessionOptions.HostName = ip;
        }

        /// <summary>
        /// A server-absolute path for a directory under the skins root.
        ///
        /// WinSCP applies WebdavRoot to ITS OWN root probe on connect, but a
        /// path with a leading slash handed to ListDirectory is treated as
        /// server-absolute and sent exactly as given. So "/Download" went to
        /// https://acrlonline.org/Download - Laravel, 404 - while WinSCP's own
        /// probe of /skins/ succeeded a moment earlier. Measured from
        /// session.log, 2026-09-02, after an afternoon of assuming otherwise.
        ///
        /// So the app prefixes the root itself. WebdavRoot stays the single
        /// place the path is configured; this is just where it gets applied.
        /// Paths WinSCP hands BACK (RemoteFileInfo.FullName) are already
        /// server-absolute and must not go through here.
        /// </summary>
        static public string RemotePath(string directory)
        {
            string root = (sessionOptions.WebdavRoot ?? "").TrimEnd('/');
            return root + "/" + directory.TrimStart('/');
        }

        /// <summary>
        /// Where WinSCP writes its session log: next to the exe, overwritten
        /// on every run so it only ever holds the latest attempt.
        /// </summary>
        static public string SessionLogPath
        {
            get { return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "session.log"); }
        }

        /// <summary>
        /// The one place a WinSCP Session is created, so every session logs.
        ///
        /// "Could not connect" used to be all anyone got: the app catches
        /// SessionRemoteException and shows one dialog regardless of cause,
        /// so a wrong password, a bad path, a certificate problem and a dead
        /// server all looked identical. With the log, the answer is in
        /// session.log beside the exe, and "send me your session.log" is
        /// the whole support conversation.
        /// </summary>
        static public Session NewSession()
        {
            Session session = new Session();

            try
            {
                session.SessionLogPath = SessionLogPath;
            }
            catch
            {
                // A log we cannot write must never stop a sync.
            }

            return session;
        }
    }

    public class GeneralSettings
    {
        private string _AcCarsDirectory;
        private string _AccCarsDirectory;
        private string[] _ExcludedSkins;

        public string AcCarsDirectory { get { return _AcCarsDirectory; } set { _AcCarsDirectory = value; } }
        public string AccCarsDirectory { get { return _AccCarsDirectory; } set { _AccCarsDirectory = value; } }
        public string[] ExcludedSkins { get { return _ExcludedSkins; } set { _ExcludedSkins = value; } }

        public GeneralSettings()
        {
            _AcCarsDirectory = "";
            _ExcludedSkins = null;
        }

        public GeneralSettings(string acCarDir, string accCarDir, string ExcludedStr)
        {
            _AcCarsDirectory = acCarDir;
            _AccCarsDirectory = accCarDir;
            _ExcludedSkins = ExcludedStr.Split(':');
        }
    }

    static public class Jobs
    {
        static public string acCarsPath = "acCarPath";
        static public string accCarsPath = "accCarPath";
        static public void SetCarsPath(string path)
        {
            acCarsPath = path;
        }
        static public string acPath = "acPath";
        static public void SetAcPath(string path)
        {
            acPath = path;
        }
        static public string accPath = "accPath";
        static public void SetAccPath(string path)
        {
            accPath = path;
        }
    }

    public class JobItem
    {
        public string Name { get; set; }
        public List<Item> Items { get; }

        public JobItem()
        {
            Items = new List<Item>();
        }
    }

    public class Item
    {
        public string FTPPath { get; set; }
        public string Game { get; set; }

        public Item(string ftpPath, string game)
        {
            FTPPath = ftpPath;
            Game = game;
        }
    }

    public class AnalysisItem
    {
        public int SkinCount { get; set; }
        public int Files { get; set; }
        public long Size { get; set; }

        public List<Skin> Skins { get; set; }
        public AnalysisItem()
        {
            Skins = new List<Skin>();
        }
    }

    public class Skin
    {
        public string Name { get; set; }
        public string Car { get; set; }
        public string Game { get; set; }
        public List<RemoteFileInfo> Files {get; set;}

        public Skin()
        {
            Files = new List<RemoteFileInfo>();
        }
    }
}