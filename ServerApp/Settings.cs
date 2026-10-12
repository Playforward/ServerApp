namespace ServerApp;

internal class Settings
{
    public class Server
    {
        public string Name { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool UseUDP { get; set; } = false;
        public bool UseCerts { get; set; }

        /// <summary>
        /// Request size and time limits for a TCP (HTTP) listener; absent means
        /// the defaults. See ServerShared's HttpLimits.
        /// </summary>
        public ServerShared.Server.HttpLimits HttpLimits { get; set; } = new();

        /// <summary>
        /// How many endpoints a UDP listener remembers, and for how long;
        /// absent means the defaults. See ServerShared's UdpLimits.
        /// </summary>
        public ServerShared.Server.UdpLimits UdpLimits { get; set; } = new();
    }

    public class CertDetail
    {
        public string Name { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool IsMainCert { get; set; }
    }

    public List<Server> Servers { get; set; } = [];

    public List<CertDetail> CertDetails { get; set; } = [];

    /// <summary>
    /// Where the title catalog's protocol facts are fetched from at start.
    /// Empty means local only: a LAN server with no accounts service serves
    /// what Configs/Titles.json says and nothing else.
    /// </summary>
    public TitleCatalogSettings TitleCatalog { get; set; } = new();

    public class TitleCatalogSettings
    {
        /// <summary>The accounts service, e.g. http://localhost:8080. Empty disables the fetch.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>How long the fetch may take before the cached copy is used instead.</summary>
        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Where the last successful fetch is kept, under Configs/, so a
        /// start with the accounts service unreachable still has yesterday's
        /// facts rather than none.
        /// </summary>
        public string CacheFileName { get; set; } = "Titles.catalog-cache.json";
    }
}
