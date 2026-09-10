using System.Text.RegularExpressions;

namespace AgentSquad.Agents.Maintenance;

public static partial class EvidenceSanitizer
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? value : Secret().Replace(value,
        "[redacted]")[..Math.Min(8000, Secret().Replace(value, "[redacted]").Length)];
    [GeneratedRegex("(?i)(Bearer\\s+|sk-|v1\\.|tvly-)[A-Za-z0-9._-]{12,}")] private static partial Regex Secret();
}
