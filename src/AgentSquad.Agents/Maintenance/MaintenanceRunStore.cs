using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSquad.Agents.Maintenance;

/// <summary>
/// Bounded, in-memory evidence store for the single-user hackathon demonstration. A separate
/// lock makes "one active run" a server-side invariant rather than a UI convention.
/// </summary>
public sealed class MaintenanceRunStore(IOptions<AgentSquadOptions> options)
{
    private readonly Lock _gate = new();
    private readonly int _capacity = options.Value.Maintenance.HistoryLimit;
    private readonly Dictionary<string, MaintenanceRunRecord> _records = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = [];
    private string? _activeId;
    private string? _activeManualCheckId;

    public MaintenanceRunSnapshot? TryStart(string trigger, string scenarioId)
    {
        lock (_gate)
        {
            if (_activeId is not null || _activeManualCheckId is not null)
            {
                return null;
            }

            var record = new MaintenanceRunRecord(Guid.NewGuid().ToString("N"), trigger, scenarioId);
            _records.Add(record.Id, record);
            _order.AddFirst(record.Id);
            _activeId = record.Id;
            TrimTerminalRecords();
            return record.Snapshot();
        }
    }

    public MaintenanceRunSnapshot? Get(string id)
    {
        lock (_gate)
        {
            return _records.TryGetValue(id, out var record) ? record.Snapshot() : null;
        }
    }

    public IReadOnlyList<MaintenanceRunSnapshot> Recent(int? limit)
    {
        lock (_gate)
        {
            var effective = Math.Clamp(limit ?? _capacity, 1, _capacity);
            return [.. _order.Take(effective)
                .Where(_records.ContainsKey)
                .Select(id => _records[id].Snapshot())];
        }
    }

    public void Update(string id, Action<MaintenanceRunRecord> update)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(id, out var record)) return;
            update(record);
            if (MaintenanceRunStatuses.IsTerminal(record.Status) && _activeId == id)
            {
                _activeId = null;
                TrimTerminalRecords();
            }
        }
    }

    public MaintenanceManualCheckStartResult TryStartManualCheck(string runId, ManualSandboxCheckDefinition definition)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(runId, out var record))
            {
                return new(false, true, null, "The maintenance run was not found.", 404);
            }

            if (_activeId is not null || _activeManualCheckId is not null)
            {
                return new(false, true, null, "A maintenance Sandbox operation is already active.", 409);
            }

            if (!string.Equals(record.Status, MaintenanceRunStatuses.Remediated, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(record.SandboxSnapshotId))
            {
                return new(false, true, null,
                    "Manual checks are available only for a remediated run with a Sandbox snapshot.", 409);
            }

            if (record.ManualCheckCount >= 10)
            {
                return new(false, true, null,
                    "This run has reached its limit of ten recorded manual Sandbox checks.", 409);
            }

            var check = record.StartManualCheck(definition);
            _activeManualCheckId = check.Id;
            return new(true, true, check);
        }
    }

    public void UpdateManualCheck(string runId, string checkId, Action<MaintenanceManualCheckRecord> update)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(runId, out var record) || !record.TryGetManualCheck(checkId, out var check))
            {
                return;
            }

            update(check);
            if (ManualSandboxCheckStatuses.IsTerminal(check.Status) && _activeManualCheckId == check.Id)
            {
                _activeManualCheckId = null;
                TrimTerminalRecords();
            }
        }
    }

    private void TrimTerminalRecords()
    {
        while (_order.Count > _capacity)
        {
            var oldest = _order.Last!;
            var id = oldest.Value;
            if (_activeId == id) return;
            _order.RemoveLast();
            _records.Remove(id);
        }
    }
}

public sealed class MaintenanceRunRecord(string id, string trigger, string scenarioId)
{
    private readonly List<MaintenanceToolStep> _steps = [];
    private readonly List<MaintenanceManualCheckRecord> _manualChecks = [];

    public string Id { get; } = id;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string Trigger { get; } = trigger;
    public string ScenarioId { get; } = scenarioId;
    public string Status { get; private set; } = MaintenanceRunStatuses.Queued;
    public string Phase { get; private set; } = "Queued";
    public string? AdvisoryId { get; private set; }
    public string? Summary { get; private set; }
    public string? SandboxSnapshotId { get; private set; }
    public string? Error { get; private set; }
    public int ManualCheckCount => _manualChecks.Count;

    public void Begin(string status, string phase)
    {
        Status = status;
        Phase = phase;
    }

    public void AddStep(string name, string status, string detail, string? output = null) =>
        _steps.Add(new MaintenanceToolStep(name, status, DateTimeOffset.UtcNow, detail, output));

    public void SetAdvisory(string advisoryId) => AdvisoryId = advisoryId;
    public void SetSummary(string? summary) => Summary = summary;
    public void SetSandboxSnapshot(string? sandboxSnapshotId) => SandboxSnapshotId = sandboxSnapshotId;

    public MaintenanceManualCheckSnapshot StartManualCheck(ManualSandboxCheckDefinition definition)
    {
        var check = new MaintenanceManualCheckRecord(Guid.NewGuid().ToString("N"), definition);
        _manualChecks.Add(check);
        return check.Snapshot();
    }

    public bool TryGetManualCheck(string id, out MaintenanceManualCheckRecord check)
    {
        check = _manualChecks.FirstOrDefault(candidate => candidate.Id == id)!;
        return check is not null;
    }

    public void Finish(string status, string phase, string? error = null)
    {
        Status = status;
        Phase = phase;
        Error = error;
    }

    public MaintenanceRunSnapshot Snapshot() => new(
        Id,
        CreatedAt,
        Trigger,
        ScenarioId,
        Status,
        Phase,
        AdvisoryId,
        Summary,
        SandboxSnapshotId,
        [.. _steps],
        [.. _manualChecks.Select(check => check.Snapshot())],
        Error);
}

public sealed class MaintenanceManualCheckRecord(string id, ManualSandboxCheckDefinition definition)
{
    public string Id { get; } = id;
    public string CheckId { get; } = definition.Id;
    public string Title { get; } = definition.Title;
    public string Description { get; } = definition.Description;
    public string ExpectedResult { get; } = definition.ExpectedResult;
    public string Status { get; private set; } = ManualSandboxCheckStatuses.Queued;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Output { get; private set; }
    public string? Error { get; private set; }

    public void Begin() => Status = ManualSandboxCheckStatuses.Running;

    public void Finish(bool succeeded, string? output = null, string? error = null)
    {
        Status = succeeded ? ManualSandboxCheckStatuses.Passed : ManualSandboxCheckStatuses.Failed;
        Output = output;
        Error = error;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public MaintenanceManualCheckSnapshot Snapshot() => new(
        Id,
        CheckId,
        Title,
        Description,
        ExpectedResult,
        Status,
        CreatedAt,
        CompletedAt,
        Output,
        Error);
}
