using System.Collections.Concurrent;

namespace TrackNTrash.D365.Integration;

/// <summary>Dedupe posts so a redelivered event never double-posts to F&O.</summary>
public interface IIdempotencyStore
{
    /// <summary>Returns true if this (channel, eventId) was already processed.</summary>
    Task<bool> SeenAsync(string channel, string eventId, CancellationToken ct = default);
    Task MarkAsync(string channel, string eventId, CancellationToken ct = default);
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new();
    private static string Key(string c, string e) => $"{c}::{e}";
    public Task<bool> SeenAsync(string channel, string eventId, CancellationToken ct = default)
        => Task.FromResult(_seen.ContainsKey(Key(channel, eventId)));
    public Task MarkAsync(string channel, string eventId, CancellationToken ct = default)
    { _seen[Key(channel, eventId)] = 1; return Task.CompletedTask; }
}

/// <summary>Posts confirmations back into D365 F&O (OData / custom service).</summary>
public interface ID365Client
{
    Task PostPickingConfirmationAsync(PickingConfirmation c, CancellationToken ct = default);
    Task PostShipmentConfirmationAsync(ShipmentConfirmation c, CancellationToken ct = default);
    Task PostDeliveryNoteAsync(DeliveryNotePosting c, CancellationToken ct = default);
    Task CreateShortageCaseAsync(DeliveryNotePosting c, CancellationToken ct = default);
    Task PostQuantityAdjustmentAsync(DeliveryNotePosting c, CancellationToken ct = default);
}

/// <summary>Posts order intake into the tracking system (creates order/lines/expected cartons).</summary>
public interface ITrackingIntakeClient
{
    Task CreateOrderAsync(OrderIntake order, CancellationToken ct = default);
}

/// <summary>Dead-letter sink for posts that exhausted retries (→ Power Automate repair flow).</summary>
public interface IDeadLetterSink
{
    Task DeadLetterAsync(string channel, string eventId, string payloadJson, string error, CancellationToken ct = default);
}
