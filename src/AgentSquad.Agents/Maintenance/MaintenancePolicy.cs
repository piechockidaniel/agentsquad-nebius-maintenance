using System.Xml.Linq;

namespace AgentSquad.Agents.Maintenance;

/// <summary>
/// The only autonomous write policy in v1. It is intentionally data-free and exact: changing
/// configuration cannot expand it to another package, a source file, or a major/minor release.
/// </summary>
public sealed class MaintenancePolicy
{
    public const string PackageId = "Microsoft.Extensions.Caching.Memory";
    public const string VulnerableVersion = "8.0.0";
    public const string PatchedVersion = "8.0.1";
    public const string AdvisoryId = "GHSA-qj66-m88j-hmgj";
    public const string FixtureProjectFileName = "DependencyMaintenanceFixture.csproj";
    public const string FixtureTestProjectFileName = FixtureProjectFileName;

    public MaintenancePolicyDecision EvaluateManifest(string manifest)
    {
        try
        {
            var document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            var package = document.Descendants()
                .SingleOrDefault(element =>
                    element.Name.LocalName == "PackageReference" &&
                    string.Equals(element.Attribute("Include")?.Value, PackageId, StringComparison.Ordinal));

            if (package is null)
            {
                return MaintenancePolicyDecision.Blocked($"The controlled fixture does not directly reference {PackageId}.");
            }

            var versionAttribute = package.Attribute("Version");
            if (!string.Equals(versionAttribute?.Value, VulnerableVersion, StringComparison.Ordinal))
            {
                return MaintenancePolicyDecision.Blocked(
                    $"Policy only permits {PackageId} {VulnerableVersion}; found '{versionAttribute?.Value ?? "unspecified"}'.");
            }

            versionAttribute!.Value = PatchedVersion;
            return MaintenancePolicyDecision.Permit(
                document.ToString(SaveOptions.DisableFormatting),
                $"Exact patch policy permits {PackageId} {VulnerableVersion} → {PatchedVersion}.");
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            return MaintenancePolicyDecision.Blocked($"Fixture manifest cannot be safely parsed: {ex.Message}");
        }
    }

    public MaintenancePolicyDecision EvaluateAdvisories(
        MaintenancePolicyDecision manifestDecision,
        PackageSecurityAssessment assessment)
    {
        if (!manifestDecision.Allowed) return manifestDecision;
        if (!assessment.Available)
        {
            return MaintenancePolicyDecision.Blocked(assessment.Error ?? "Security advisory lookup is unavailable.");
        }

        var advisory = assessment.Advisories.SingleOrDefault(item =>
            string.Equals(item.Id, AdvisoryId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.FirstPatchedVersion, PatchedVersion, StringComparison.Ordinal));

        return advisory is null
            ? MaintenancePolicyDecision.Blocked(
                $"The required advisory {AdvisoryId} with fixed version {PatchedVersion} was not confirmed.")
            : manifestDecision with { Advisory = advisory };
    }

    public static string BuildDiff(string originalManifest, string patchedManifest) =>
        $"--- a/{FixtureProjectFileName}\n+++ b/{FixtureProjectFileName}\n" +
        $"-    <PackageReference Include=\"{PackageId}\" Version=\"{VulnerableVersion}\" />\n" +
        $"+    <PackageReference Include=\"{PackageId}\" Version=\"{PatchedVersion}\" />";
}

public sealed record MaintenancePolicyDecision(
    bool Allowed,
    string Detail,
    string? PatchedManifest,
    PackageSecurityAdvisory? Advisory)
{
    public static MaintenancePolicyDecision Permit(string patchedManifest, string detail) =>
        new(true, detail, patchedManifest, null);

    public static MaintenancePolicyDecision Blocked(string detail) =>
        new(false, detail, null, null);
}
