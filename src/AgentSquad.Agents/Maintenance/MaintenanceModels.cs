namespace AgentSquad.Agents.Maintenance;

public static class MaintenanceRunStatuses
{
    public const string Queued = "Queued";
    public const string Scanning = "Scanning";
    public const string Sandboxing = "Sandboxing";
    public const string Testing = "Testing";
    public const string Remediated = "Remediated";
    public const string Blocked = "Blocked";
    public const string Failed = "Failed";

    public static bool IsTerminal(string status) =>
        status is Remediated or Blocked or Failed;
}

public static class MaintenanceRunTriggers
{
    public const string Manual = "manual";
    public const string Scheduled = "scheduled";
}

public sealed record MaintenanceToolStep(
    string Name,
    string Status,
    DateTimeOffset OccurredAt,
    string Detail,
    string? Output = null);

public sealed record MaintenanceRunSnapshot(
    string Id,
    DateTimeOffset CreatedAt,
    string Trigger,
    string ScenarioId,
    string Status,
    string Phase,
    string? AdvisoryId,
    string? Summary,
    string? SandboxSnapshotId,
    IReadOnlyList<MaintenanceToolStep> ToolSteps,
    string? Error);

public sealed record MaintenanceStartResult(
    bool Started,
    bool Enabled,
    MaintenanceRunSnapshot? Run,
    string? Error = null,
    int? StatusCode = null);

public sealed record MaintenancePreflight(
    bool Ready,
    string ModelId,
    string ModelDetail,
    string SandboxDetail,
    IReadOnlyList<string> SandboxTools);

public sealed record MaintenanceModelPreflight(bool Available, string Detail);

public interface IMaintenanceModelProbe
{
    Task<MaintenanceModelPreflight> ProbeAsync(CancellationToken cancellationToken = default);
}

public interface IMaintenanceNarrator
{
    Task<string?> SummarizeAsync(
        string advisoryId,
        string severity,
        string summary,
        CancellationToken cancellationToken = default);
}
