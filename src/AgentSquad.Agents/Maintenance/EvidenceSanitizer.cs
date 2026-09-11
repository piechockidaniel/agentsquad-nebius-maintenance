using System.Text.RegularExpressions;

namespace AgentSquad.Agents.Maintenance;

public static partial class EvidenceSanitizer
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? value : Secret().Replace(value,
        "[redacted]")[..Math.Min(8000, Secret().Replace(value, "[redacted]").Length)];

    /// <summary>Maps provider failures to a safe, low-cardinality telemetry label without logging their text.</summary>
    public static string TelemetryCategory(string? value)
    {
        var normalized = value?.ToUpperInvariant() ?? string.Empty;
        if (normalized.Contains("NOT CONFIGURED") || normalized.Contains("MISSING")) return "configuration";
        if (normalized.Contains("AUTHORIZ") || normalized.Contains("CREDENTIAL") || normalized.Contains("TOKEN"))
            return "authorization";
        if (normalized.Contains("TIMEOUT") || normalized.Contains("TIMED OUT")) return "timeout";
        if (normalized.Contains("RATE LIMIT")) return "rate-limited";
        return "operation-failed";
    }

    [GeneratedRegex("(?i)(Bearer\\s+|sk-|v1\\.|tvly-)[A-Za-z0-9._-]{12,}")] private static partial Regex Secret();
}
