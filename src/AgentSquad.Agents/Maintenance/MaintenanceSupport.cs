using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentSquad.Agents.Maintenance;

public sealed class TokenFactoryModelProbe(IHttpClientFactory clients, IOptions<AgentSquadOptions> options) : IMaintenanceModelProbe
{
    public async Task<MaintenanceModelPreflight> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var key = options.Value.TokenFactory.ApiKey ?? Environment.GetEnvironmentVariable("NEBIUS_TOKEN_FACTORY_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return new(false, "NEBIUS_TOKEN_FACTORY_API_KEY is not configured.");
        try
        {
            var client = clients.CreateClient("TokenFactory");
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.tokenfactory.nebius.com/v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, $"Token Factory model listing returned HTTP {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var found = document.RootElement.GetProperty("data").EnumerateArray().Any(x => x.GetProperty("id").GetString() == options.Value.Maintenance.ModelId);
            return found ? new(true, $"Token Factory exposes {options.Value.Maintenance.ModelId}.") : new(false, "Configured NVIDIA model is unavailable to this Token Factory key.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException) { return new(false, EvidenceSanitizer.Clean(ex.Message)!); }
    }
}

public sealed class TokenFactoryNarrator(IHttpClientFactory clients, IOptions<AgentSquadOptions> options) : IMaintenanceNarrator
{
    public async Task<string?> SummarizeAsync(string advisoryId, string severity, string summary, CancellationToken cancellationToken = default)
    {
        var key = options.Value.TokenFactory.ApiKey ?? Environment.GetEnvironmentVariable("NEBIUS_TOKEN_FACTORY_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return null;
        var client = clients.CreateClient("TokenFactory");
        var body = new { model = options.Value.Maintenance.ModelId, messages = new[] { new { role = "system", content = "State the confirmed dependency risk and exact sandbox-only repair in two short sentences. Do not invent facts." }, new { role = "user", content = $"{advisoryId}; severity {severity}; {summary}; repair: Microsoft.Extensions.Caching.Memory 8.0.0 to 8.0.1." } } };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tokenfactory.nebius.com/v1/chat/completions") { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return EvidenceSanitizer.Clean(document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString());
    }
}

public sealed class PackageSecurityLookup(IHttpClientFactory clients) : IPackageSecurityLookup
{
    public async Task<PackageSecurityAssessment> InspectAsync(string ecosystem, string packageName, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = clients.CreateClient("PackageSecurity");
            var registration = await client.GetAsync($"https://api.nuget.org/v3/registration5-semver1/{packageName.ToLowerInvariant()}/{version}.json", cancellationToken);
            if (!registration.IsSuccessStatusCode) return new(false, [], Error: "NuGet registration lookup failed.");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/advisories/{MaintenancePolicy.AdvisoryId}");
            request.Headers.UserAgent.ParseAdd("AgentSquad-maintenance-demo");
            using var advisoryResponse = await client.SendAsync(request, cancellationToken);
            if (!advisoryResponse.IsSuccessStatusCode) return new(false, [], Error: "GitHub advisory lookup failed.");
            using var advisory = JsonDocument.Parse(await advisoryResponse.Content.ReadAsStringAsync(cancellationToken));
            var id = advisory.RootElement.GetProperty("ghsa_id").GetString();
            var summary = advisory.RootElement.GetProperty("summary").GetString();
            var severity = advisory.RootElement.GetProperty("severity").GetString();
            return new(true, [new PackageSecurityAdvisory(id ?? "", summary ?? "", severity ?? "", "<= 8.0.0", MaintenancePolicy.PatchedVersion, $"https://github.com/advisories/{id}")], "NuGet registration and GitHub advisory confirmed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return new(false, [], Error: EvidenceSanitizer.Clean(ex.Message)); }
    }
}

public static partial class EvidenceSanitizer
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? value : Secret().Replace(value, "[redacted]")[..Math.Min(8000, Secret().Replace(value, "[redacted]").Length)];
    [GeneratedRegex("(?i)(Bearer\\s+|sk-|v1\\.)[A-Za-z0-9._-]{12,}")] private static partial Regex Secret();
}
