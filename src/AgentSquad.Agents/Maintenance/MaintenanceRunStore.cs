using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Options;

namespace AgentSquad.Agents.Maintenance;

/// <summary>
/// Bounded, in-memory evidence store for the single-user hackathon demonstration. A separate
/// lock makes "one active run" a server-side invariant rather than a UI convention.
/// </summary>
public sealed class MaintenanceRunStore(IOptions<AgentSquadOptions> options)
{
    private readonly object _gate = new();
    private readonly int _capacity = options.Value.Maintenance.HistoryLimit;
    private readonly Dictionary<string, MaintenanceRunRecord> _records = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = [];
    private string? _activeId;

    public MaintenanceRunSnapshot? TryStart(string trigger)
    {
        lock (_gate)
        {
            if (_activeId is not null)
            {
                return null;
            }

            var record = new MaintenanceRunRecord(Guid.NewGuid().ToString("N"), trigger);
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
            return _order.Take(effective)
                .Where(_records.ContainsKey)
                .Select(id => _records[id].Snapshot())
                .ToArray();
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

public sealed class MaintenanceRunRecord(string id, string trigger)
{
    private readonly List<MaintenanceToolStep> _steps = [];

    public string Id { get; } = id;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string Trigger { get; } = trigger;
    public string Status { get; private set; } = MaintenanceRunStatuses.Queued;
    public string Phase { get; private set; } = "Queued";
    public string? AdvisoryId { get; private set; }
    public string? Summary { get; private set; }
    public string? SandboxSnapshotId { get; private set; }
    public string? Error { get; private set; }

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
        Status,
        Phase,
        AdvisoryId,
        Summary,
        SandboxSnapshotId,
        _steps.ToArray(),
        Error);
}
