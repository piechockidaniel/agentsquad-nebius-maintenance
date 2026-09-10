using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentSquad.Agents.Maintenance;

/// <summary>
/// Retrieves supplementary, bounded security evidence. Its output is deliberately never used by
/// the deterministic manifest/advisory policy that authorizes the v1 patch.
/// </summary>
public sealed class TavilySecurityResearch(IHttpClientFactory clients, IOptions<AgentSquadOptions> options)
    : ITavilySecurityResearch
{
    private const int MaxSources = 3;
    private const int MaxTitleLength = 180;
    private const int MaxExcerptLength = 600;

    private static readonly string[] ApprovedDomains = ["github.com", "nuget.org", "learn.microsoft.com"];

    public Task<TavilySecurityResearchPreflight> GetPreflightAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(string.IsNullOrWhiteSpace(ApiKey)
            ? new TavilySecurityResearchPreflight(false,
                "Tavily research is not configured. Set TAVILY_API_KEY to include supplementary security evidence.")
            : new TavilySecurityResearchPreflight(true,
                "Tavily supplementary security research is configured; it runs only after policy confirms an advisory."));
    }

    public async Task<TavilySecurityResearchResult> ResearchAsync(PackageSecurityAdvisory advisory,
        CancellationToken cancellationToken = default)
    {
        var key = ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return new(false,
                "Tavily research is not configured; the policy-verified repair continues without supplementary web evidence.",
                []);
        }

        try
        {
            var body = new
            {
                query = $"{advisory.Id} {MaintenancePolicy.PackageId} {MaintenancePolicy.VulnerableVersion} " +
                        $"{MaintenancePolicy.PatchedVersion} security advisory remediation",
                topic = "general",
                search_depth = "basic",
                max_results = MaxSources,
                include_answer = false,
                include_raw_content = false,
                include_images = false,
                include_domains = ApprovedDomains
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            var client = clients.CreateClient("TavilySecurityResearch");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(false, $"Tavily security research returned HTTP {(int)response.StatusCode}; " +
                                  "repair policy remains unchanged.", []);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var sources = ParseSources(document.RootElement);
            var requestId = ReadString(document.RootElement, "request_id");
            var credits = ReadInt(document.RootElement, "usage", "credits");
            var detail = $"Tavily searched approved security domains and retained {sources.Count} bounded evidence source" +
                         (sources.Count == 1 ? "." : "s.");

            return new(true, detail, sources, requestId, credits);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return new(false, $"Tavily security research was unavailable: {EvidenceSanitizer.Clean(ex.Message)}", []);
        }
    }

    private string? ApiKey => options.Value.Tavily.ApiKey ?? Environment.GetEnvironmentVariable("TAVILY_API_KEY");

    private static IReadOnlyList<TavilyEvidenceSource> ParseSources(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return [];

        return results.EnumerateArray()
            .Select(result => new
            {
                Title = ReadString(result, "title"),
                Url = ReadString(result, "url"),
                Content = ReadString(result, "content")
            })
            .Where(result => result.Url is not null && IsApprovedSource(result.Url))
            .Take(MaxSources)
            .Select(result => new TavilyEvidenceSource(
                Clip(EvidenceSanitizer.Clean(result.Title) ?? "Untitled source", MaxTitleLength),
                result.Url!,
                Clip(EvidenceSanitizer.Clean(result.Content) ?? "No excerpt returned.", MaxExcerptLength)))
            .ToArray();
    }

    private static bool IsApprovedSource(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        ApprovedDomains.Any(domain => string.Equals(uri.Host, domain, StringComparison.OrdinalIgnoreCase) ||
                                       uri.Host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));

    private static string? ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? ReadInt(JsonElement element, string parent, string property) =>
        element.TryGetProperty(parent, out var container) && container.ValueKind == JsonValueKind.Object &&
        container.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static string Clip(string value, int limit) => value.Length <= limit ? value : value[..limit] + "…";
}
