using System.Text.Json;

namespace AgentSquad.Agents.Maintenance;

public sealed class PackageSecurityLookup(IHttpClientFactory clients) : IPackageSecurityLookup
{
    public async Task<PackageSecurityAssessment> InspectAsync(string ecosystem, string packageName, string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = clients.CreateClient("PackageSecurity");
            var registration = await client.GetAsync($"https://api.nuget.org/v3/registration5-semver1/" +
                $"{packageName.ToLowerInvariant()}/{version}.json", cancellationToken);

            if (!registration.IsSuccessStatusCode) return new(false, [], Error: "NuGet registration lookup failed.");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/advisories/" +
                $"{MaintenancePolicy.AdvisoryId}");

            request.Headers.UserAgent.ParseAdd("AgentSquad-maintenance-demo");

            using var advisoryResponse = await client.SendAsync(request, cancellationToken);
            if (!advisoryResponse.IsSuccessStatusCode) return new(false, [], Error: "GitHub advisory lookup failed.");
            using var advisory = JsonDocument.Parse(await advisoryResponse.Content.ReadAsStringAsync(cancellationToken));

            var id = advisory.RootElement.GetProperty("ghsa_id").GetString();
            var summary = advisory.RootElement.GetProperty("summary").GetString();
            var severity = advisory.RootElement.GetProperty("severity").GetString();

            return new(true, [new PackageSecurityAdvisory(id ?? "", summary ?? "", severity ?? "",
                "<= 8.0.0", MaintenancePolicy.PatchedVersion, $"https://github.com/advisories/{id}")],
                "NuGet registration and GitHub advisory confirmed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, [], Error: EvidenceSanitizer.Clean(ex.Message));
        }
    }
}
