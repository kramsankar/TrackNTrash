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

/// <summary>
/// The read/action model behind the exception console. SQL-backed when a connection string
/// is configured (<see cref="SqlConsoleExceptionStore"/>), in-memory otherwise.
/// </summary>
public interface IConsoleExceptionStore
{
    Task<ConsoleException> AddAsync(TrackException ex, CancellationToken ct = default);
    Task<IReadOnlyList<ConsoleException>> ListAsync(string? checkpoint, string? severity, string? status,
        string? route, CancellationToken ct = default);
    Task<ConsoleException?> GetAsync(long id, CancellationToken ct = default);
    /// <summary>Returns the updated exception, or null when the id is unknown.</summary>
    Task<ConsoleException?> ApplyAsync(long id, string action, string user, string? note,
        CancellationToken ct = default);
}

/// <summary>Maps a console action onto the status it produces. Shared by both stores.</summary>
public static class ConsoleActions
{
    public static string? StatusFor(string action) => action switch
    {
        "acknowledge" => "Acknowledged",
        "resolve" => "Resolved",
        "escalate" => "Escalated",
        _ => null
    };
}

/// <summary>In-memory console store, used for local runs and tests with no database.</summary>
public sealed class InMemoryConsoleExceptionStore : IConsoleExceptionStore
{
    private readonly ConcurrentDictionary<long, ConsoleException> _byId = new();
    private long _seq;

    public Task<ConsoleException> AddAsync(TrackException ex, CancellationToken ct = default)
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
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<ConsoleException>> ListAsync(string? checkpoint, string? severity, string? status,
        string? route, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ConsoleException>>(_byId.Values
            .Where(e => checkpoint is null || string.Equals(e.Checkpoint, checkpoint, StringComparison.OrdinalIgnoreCase))
            .Where(e => severity is null || string.Equals(e.Severity, severity, StringComparison.OrdinalIgnoreCase))
            .Where(e => status is null || string.Equals(e.Status, status, StringComparison.OrdinalIgnoreCase))
            .Where(e => route is null || string.Equals(e.Route, route, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.CreatedUtc)
            .ToList());

    public Task<ConsoleException?> GetAsync(long id, CancellationToken ct = default)
        => Task.FromResult(_byId.TryGetValue(id, out var e) ? e : null);

    public Task<ConsoleException?> ApplyAsync(long id, string action, string user, string? note,
        CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var e)) return Task.FromResult<ConsoleException?>(null);
        lock (e)
        {
            e.Status = ConsoleActions.StatusFor(action) ?? e.Status;
            e.Audit.Add(new AuditEntry(action, user, DateTimeOffset.UtcNow, note));
        }
        return Task.FromResult<ConsoleException?>(e);
    }
}
