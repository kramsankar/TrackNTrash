using System.Collections.Concurrent;
using TrackNTrash.Tracking.Core;

namespace TrackNTrash.Tracking.Api.Console;

public sealed record AuditEntry(string Action, string User, DateTimeOffset Utc, string? Note);

public sealed class ConsoleException
{
    public long Id { get; init; }
    public string Type { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Status { get; set; } = "Open";
    public string? Checkpoint { get; init; }
    public long? OrderLineId { get; init; }
    public int? TrayId { get; init; }
    public long? TripId { get; init; }
    public int? StoreId { get; init; }
    public string? Route { get; init; }
    public string Detail { get; init; } = "";
    public string? FrameBlobUri { get; init; }
    public string? PhotoBlobUri { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public List<AuditEntry> Audit { get; } = new();
    public int AgeMinutes => (int)(DateTimeOffset.UtcNow - CreatedUtc).TotalMinutes;
}

/// <summary>In-memory exception store powering the console (SQL-backed in prod over ops.Exception).</summary>
public sealed class ConsoleExceptionStore
{
    private readonly ConcurrentDictionary<long, ConsoleException> _byId = new();
    private long _seq;

    public ConsoleException Add(TrackException ex)
    {
        var id = Interlocked.Increment(ref _seq);
        var record = new ConsoleException
        {
            Id = id,
            Type = ex.Type.ToString(),
            Severity = ex.Severity.ToString(),
            Status = "Open",
            Checkpoint = ex.Checkpoint,
            OrderLineId = ex.OrderLineId,
            TrayId = ex.TrayId,
            TripId = ex.TripId,
            StoreId = ex.StoreId,
            Detail = ex.Detail,
            FrameBlobUri = ex.FrameBlobUri,
            CreatedUtc = ex.CreatedUtc
        };
        _byId[id] = record;
        return record;
    }

    public IEnumerable<ConsoleException> List(string? checkpoint, string? severity, string? status, string? route)
        => _byId.Values
            .Where(e => checkpoint is null || string.Equals(e.Checkpoint, checkpoint, StringComparison.OrdinalIgnoreCase))
            .Where(e => severity is null || string.Equals(e.Severity, severity, StringComparison.OrdinalIgnoreCase))
            .Where(e => status is null || string.Equals(e.Status, status, StringComparison.OrdinalIgnoreCase))
            .Where(e => route is null || string.Equals(e.Route, route, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.CreatedUtc);

    public ConsoleException? Get(long id) => _byId.TryGetValue(id, out var e) ? e : null;

    public bool Apply(long id, string action, string user, string? note, out ConsoleException? updated)
    {
        updated = null;
        if (!_byId.TryGetValue(id, out var e)) return false;
        lock (e)
        {
            e.Status = action switch
            {
                "acknowledge" => "Acknowledged",
                "resolve" => "Resolved",
                "escalate" => "Escalated",
                _ => e.Status
            };
            e.Audit.Add(new AuditEntry(action, user, DateTimeOffset.UtcNow, note));
        }
        updated = e;
        return true;
    }
}
