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
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            Log.Information("[titles] no TitleCatalog.Url configured; serving Configs/{File} as written", TitleRegistry.ConfigFileName);
            return;
        }

        (List<TitleProtocolFacts>? facts, string source) = Fetch(settings) is { } live
            ? (live, "the accounts service")
            : (ReadCache(settings), "the cached copy");
        if (facts is null)
        {
            Log.Warning("[titles] no protocol facts from {Url} and no cache at Configs/{Cache}; serving Configs/{File} as written",
                settings.Url, settings.CacheFileName, TitleRegistry.ConfigFileName);
            return;
        }

        var byId = new Dictionary<string, TitleProtocolFacts>(StringComparer.OrdinalIgnoreCase);
        foreach (TitleProtocolFacts fact in facts)
            if (!string.IsNullOrWhiteSpace(fact.TitleId))
                byId[fact.TitleId] = fact;

        int applied = 0;
        foreach (TitleDescriptor title in titles)
        {
            if (!byId.Remove(title.TitleId, out TitleProtocolFacts? fact))
                continue;
            title.ApplyFacts(fact);
            applied++;
        }
        Log.Information("[titles] protocol facts for {Applied} of {Hosted} hosted title(s) came from {Source}; {Unhosted} supported title(s) are not hosted here ({Names})",
            applied, titles.Count, source, byId.Count, byId.Count == 0 ? "-" : string.Join(", ", byId.Keys));
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
