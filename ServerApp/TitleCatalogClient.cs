using System.Text.Json;
using Serilog;
using ServerShared.Controllers;
using ServerShared.Titles;

namespace ServerApp;

/// <summary>
/// Fetches the catalog's protocol facts from the accounts service and lays
/// them over the host's local titles, so an operator records a title's
/// identifiers once in the admin panel rather than once per host. The local
/// file keeps only what is genuinely this machine's: ports, capture
/// directories, identity files.
/// </summary>
internal static class TitleCatalogClient
{
    private const string ProtocolsPath = "/api/v1/titles/protocols";

    private sealed class ProtocolsResponse
    {
        public List<TitleProtocolFacts> Protocols { get; set; } = [];
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Applies the catalog's facts to <paramref name="titles"/> in place and
    /// reports where they came from. Titles the catalog does not know keep
    /// their local values; catalog titles this host does not list are noted
    /// and ignored, since the host has no ports or captures for them.
    /// </summary>
    public static void Apply(Settings.TitleCatalogSettings settings, IReadOnlyList<TitleDescriptor> titles)
    {
        HashSet<TitleDescriptor> fromCatalog = ApplyCatalog(settings, titles);
        FillFromBuiltIns(titles, fromCatalog);
    }

    /// <summary>
    /// Fills the protocol facts a title is missing from what this host ships
    /// for it (<see cref="DefaultTitles"/>), for every title the catalog did
    /// not answer for. Without it a host with no accounts service, or one
    /// that cannot reach it on a first start, would refuse every title whose
    /// Configs/Titles.json entry leaves its family to the catalog. Only empty
    /// fields are filled: what an operator wrote in the file stands.
    /// </summary>
    internal static void FillFromBuiltIns(IReadOnlyList<TitleDescriptor> titles, IReadOnlySet<TitleDescriptor> fromCatalog)
    {
        foreach (TitleDescriptor title in titles)
        {
            if (fromCatalog.Contains(title) || BuiltInFor(title) is not { } builtIn)
                continue;
            title.Matchers ??= new();
            List<string> filled = [];
            if (string.IsNullOrWhiteSpace(title.ProtocolFamily) && !string.IsNullOrWhiteSpace(builtIn.ProtocolFamily))
            {
                title.ProtocolFamily = builtIn.ProtocolFamily;
                filled.Add("family");
            }
            if (string.IsNullOrWhiteSpace(title.Matchers.AccessKey) && !string.IsNullOrWhiteSpace(builtIn.Matchers.AccessKey))
            {
                title.Matchers.AccessKey = builtIn.Matchers.AccessKey;
                filled.Add("access key");
            }
            if (string.IsNullOrWhiteSpace(title.Matchers.LegacyOnlineConfigId) && !string.IsNullOrWhiteSpace(builtIn.Matchers.LegacyOnlineConfigId))
            {
                title.Matchers.LegacyOnlineConfigId = builtIn.Matchers.LegacyOnlineConfigId;
                filled.Add("online-config id");
            }
            if (filled.Count > 0)
                Log.Information("[titles] {Title}: {Filled} from this host's built-in facts for {BuiltIn}",
                    title, string.Join(", ", filled), builtIn.TitleId);
        }
    }

    /// <summary>
    /// The built-in entry for a title: by slug, else by an identifier only
    /// that game uses, since a hand-written Titles.json may name a title
    /// differently from the catalog.
    /// </summary>
    private static TitleDescriptor? BuiltInFor(TitleDescriptor title) =>
        DefaultTitles.All.FirstOrDefault(d => string.Equals(d.TitleId, title.TitleId, StringComparison.OrdinalIgnoreCase))
        ?? DefaultTitles.All.FirstOrDefault(d => Same(d.Matchers.LegacyOnlineConfigId, title.Matchers?.LegacyOnlineConfigId))
        ?? DefaultTitles.All.FirstOrDefault(d => Same(d.Matchers.AppId, title.Matchers?.AppId));

    private static bool Same(string? builtIn, string? configured) =>
        !string.IsNullOrWhiteSpace(builtIn) && string.Equals(builtIn, configured, StringComparison.OrdinalIgnoreCase);

    /// <summary>Lays the catalog over the titles and returns the ones it answered for.</summary>
    private static HashSet<TitleDescriptor> ApplyCatalog(Settings.TitleCatalogSettings settings, IReadOnlyList<TitleDescriptor> titles)
    {
        HashSet<TitleDescriptor> applied = [];
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            Log.Information("[titles] no TitleCatalog.Url configured; serving Configs/{File} with this host's built-in facts", TitleRegistry.ConfigFileName);
            return applied;
        }

        (List<TitleProtocolFacts>? facts, string source) = Fetch(settings) is { } live
            ? (live, "the accounts service")
            : (ReadCache(settings), "the cached copy");
        if (facts is null)
        {
            Log.Warning("[titles] no protocol facts from {Url} and no cache at Configs/{Cache}; serving Configs/{File} with this host's built-in facts",
                settings.Url, settings.CacheFileName, TitleRegistry.ConfigFileName);
            return applied;
        }

        var byId = new Dictionary<string, TitleProtocolFacts>(StringComparer.OrdinalIgnoreCase);
        foreach (TitleProtocolFacts fact in facts)
            if (!string.IsNullOrWhiteSpace(fact.TitleId))
                byId[fact.TitleId] = fact;

        foreach (TitleDescriptor title in titles)
        {
            if (!byId.Remove(title.TitleId, out TitleProtocolFacts? fact))
                continue;
            title.ApplyFacts(fact);
            applied.Add(title);
        }
        Log.Information("[titles] protocol facts for {Applied} of {Hosted} hosted title(s) came from {Source}; {Unhosted} supported title(s) are not hosted here ({Names})",
            applied.Count, titles.Count, source, byId.Count, byId.Count == 0 ? "-" : string.Join(", ", byId.Keys));
        return applied;
    }

    private static List<TitleProtocolFacts>? Fetch(Settings.TitleCatalogSettings settings)
    {
        if (!Uri.TryCreate(settings.Url.TrimEnd('/') + ProtocolsPath, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Log.Error("[titles] TitleCatalog.Url must be an absolute http(s) URL: {Url}", settings.Url);
            return null;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)) };
            using HttpResponseMessage response = http.GetAsync(uri).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("[titles] {Uri} answered {Status}; using the cached copy if there is one", uri, (int)response.StatusCode);
                return null;
            }
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            List<TitleProtocolFacts> facts = JsonSerializer.Deserialize<ProtocolsResponse>(body, Json)?.Protocols ?? [];
            WriteCache(settings, facts);
            return facts;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            Log.Warning("[titles] could not fetch protocol facts from {Uri} ({Reason}); using the cached copy if there is one",
                uri, exception.Message);
            return null;
        }
    }

    private static string CachePath(Settings.TitleCatalogSettings settings) =>
        Path.Combine(Directory.GetCurrentDirectory(), "Configs", settings.CacheFileName);

    private static List<TitleProtocolFacts>? ReadCache(Settings.TitleCatalogSettings settings)
    {
        string path = CachePath(settings);
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<List<TitleProtocolFacts>>(File.ReadAllText(path), JsonController.SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            Log.Warning("[titles] the cached protocol facts at {Path} could not be read ({Reason})", path, exception.Message);
            return null;
        }
    }

    private static void WriteCache(Settings.TitleCatalogSettings settings, List<TitleProtocolFacts> facts)
    {
        try
        {
            File.WriteAllText(CachePath(settings), JsonSerializer.Serialize(facts, JsonController.SerializerOptions));
        }
        catch (IOException exception)
        {
            Log.Warning("[titles] could not cache the protocol facts ({Reason}); the next offline start will have none", exception.Message);
        }
    }
}
