namespace AgentSquad.Agents.Maintenance;

/// <summary>
/// The only post-repair interactions available in v1. Commands are fixed, run only against
/// loopback in a disposable Sandbox, and are never derived from console input.
/// </summary>
public static class ManualSandboxCheckCatalog
{
    private static readonly IReadOnlyDictionary<string, ManualSandboxCheckDefinition> Checks =
        new Dictionary<string, ManualSandboxCheckDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["health"] = new(
                "health",
                "Health check",
                "Start the repaired fixture privately and confirm its health endpoint responds.",
                "HTTP 200 with status = ok",
                """
                set -eu
                ASPNETCORE_URLS=http://127.0.0.1:8080 dotnet run --project MaintenanceLab.Api.csproj --no-restore > /tmp/agentsquad-manual-check.log 2>&1 &
                app_pid=$!
                cleanup() {
                  kill "$app_pid" 2>/dev/null || true
                }
                trap cleanup EXIT
                ready=false
                for _ in $(seq 1 20); do
                  if curl --fail --silent --show-error --max-time 2 http://127.0.0.1:8080/health >/dev/null 2>&1; then
                    ready=true
                    break
                  fi
                  sleep 1
                done
                if [ "$ready" != true ]; then
                  cat /tmp/agentsquad-manual-check.log >&2
                  exit 1
                fi
                response="$(curl --fail --silent --show-error --max-time 5 http://127.0.0.1:8080/health)"
                printf 'AGENTSQUAD_MANUAL_RESPONSE=%s\n' "$response"
                test "$response" = '{"status":"ok"}'
                """),
            ["work-item-42"] = new(
                "work-item-42",
                "Read work item 42",
                "Start the repaired fixture privately and read one known work item through its HTTP API.",
                "HTTP 200 with item 42 and its cached title",
                """
                set -eu
                ASPNETCORE_URLS=http://127.0.0.1:8080 dotnet run --project MaintenanceLab.Api.csproj --no-restore > /tmp/agentsquad-manual-check.log 2>&1 &
                app_pid=$!
                cleanup() {
                  kill "$app_pid" 2>/dev/null || true
                }
                trap cleanup EXIT
                ready=false
                for _ in $(seq 1 20); do
                  if curl --fail --silent --show-error --max-time 2 http://127.0.0.1:8080/health >/dev/null 2>&1; then
                    ready=true
                    break
                  fi
                  sleep 1
                done
                if [ "$ready" != true ]; then
                  cat /tmp/agentsquad-manual-check.log >&2
                  exit 1
                fi
                response="$(curl --fail --silent --show-error --max-time 5 http://127.0.0.1:8080/work-items/42)"
                printf 'AGENTSQUAD_MANUAL_RESPONSE=%s\n' "$response"
                test "$response" = '{"id":"42","title":"Maintenance item 42"}'
                """)
        };

    public static IReadOnlyList<ManualSandboxCheckDefinition> All => [.. Checks.Values.OrderBy(check => check.Id)];

    public static bool TryGet(string? id, out ManualSandboxCheckDefinition check) =>
        Checks.TryGetValue(id ?? string.Empty, out check!);
}

public sealed record ManualSandboxCheckDefinition(
    string Id,
    string Title,
    string Description,
    string ExpectedResult,
    string Command);

public static class ManualSandboxCheckStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Passed = "Passed";
    public const string Failed = "Failed";

    public static bool IsTerminal(string status) => status is Passed or Failed;
}

public sealed record MaintenanceManualCheckSnapshot(
    string Id,
    string CheckId,
    string Title,
    string Description,
    string ExpectedResult,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Output,
    string? Error);

public sealed record MaintenanceManualCheckStartResult(
    bool Started,
    bool Enabled,
    MaintenanceManualCheckSnapshot? Check,
    string? Error = null,
    int? StatusCode = null);
