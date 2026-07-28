using Microsoft.Extensions.Logging;

namespace TrackNTrash.D365.Integration;

public sealed record PostingOptions
{
    public int MaxAttempts { get; init; } = 4;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public ShortageHandling ShortageHandling { get; init; } = ShortageHandling.CreateCase;
}

public sealed record PostResult(bool Posted, bool Duplicate, bool DeadLettered, string Message);

/// <summary>
/// Inbound-to-F&O posting with idempotency, exponential-backoff retry and dead-lettering.
/// Routes a tracking event to the correct F&O post based on its kind. Shortages at receiving
/// are handled per <see cref="PostingOptions.ShortageHandling"/> (adjustment or case).
/// </summary>
public sealed class D365PostingService
{
    private readonly ID365Client _d365;
    private readonly IIdempotencyStore _idem;
    private readonly IDeadLetterSink _dlq;
    private readonly PostingOptions _opts;
    private readonly ILogger<D365PostingService> _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public D365PostingService(
        ID365Client d365, IIdempotencyStore idem, IDeadLetterSink dlq,
        PostingOptions opts, ILogger<D365PostingService> log,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _d365 = d365;
        _idem = idem;
        _dlq = dlq;
        _opts = opts;
        _log = log;
        _delay = delay ?? Task.Delay;    // injectable so tests don't actually sleep
    }

    private const string Channel = "d365-inbound";

    public async Task<PostResult> PostAsync(TrackingOutboundEvent e, CancellationToken ct = default)
    {
        if (await _idem.SeenAsync(Channel, e.EventId, ct))
        {
            _log.LogInformation("Skipping duplicate F&O post for event {EventId}", e.EventId);
            return new PostResult(false, true, false, "duplicate");
        }

        Func<CancellationToken, Task> action = e.Kind switch
        {
            TrackingEventKind.TrayBuildComplete =>
                c => _d365.PostPickingConfirmationAsync(Mapping.ToPickingConfirmation(e), c),
            TrackingEventKind.ShipmentConfirmed =>
                c => _d365.PostShipmentConfirmationAsync(Mapping.ToShipmentConfirmation(e, DateTimeOffset.UtcNow), c),
            TrackingEventKind.ReceivingComplete =>
                c => PostDeliveryAsync(Mapping.ToDeliveryNote(e, DateTimeOffset.UtcNow), c),
            _ => throw new ArgumentOutOfRangeException(nameof(e.Kind))
        };

        Exception? last = null;
        for (int attempt = 1; attempt <= _opts.MaxAttempts; attempt++)
        {
            try
            {
                await action(ct);
                await _idem.MarkAsync(Channel, e.EventId, ct);
                _log.LogInformation("Posted {Kind} for {Order} (event {EventId}) on attempt {Attempt}",
                    e.Kind, e.OrderNumber, e.EventId, attempt);
                return new PostResult(true, false, false, "posted");
            }
            catch (Exception ex)
            {
                last = ex;
                _log.LogWarning(ex, "F&O post attempt {Attempt}/{Max} failed for {EventId}",
                    attempt, _opts.MaxAttempts, e.EventId);
                if (attempt < _opts.MaxAttempts)
                {
                    // exponential backoff: base * 2^(attempt-1)
                    var wait = TimeSpan.FromMilliseconds(_opts.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    await _delay(wait, ct);
                }
            }
        }

        await _dlq.DeadLetterAsync(Channel, e.EventId, e.OrderNumber, last?.Message ?? "unknown", ct);
        return new PostResult(false, false, true, $"dead-lettered: {last?.Message}");
    }

    private async Task PostDeliveryAsync(DeliveryNotePosting note, CancellationToken ct)
    {
        await _d365.PostDeliveryNoteAsync(note, ct);
        if (note.HasShortages)
        {
            if (_opts.ShortageHandling == ShortageHandling.CreateCase)
                await _d365.CreateShortageCaseAsync(note, ct);
            else
                await _d365.PostQuantityAdjustmentAsync(note, ct);
        }
    }
}
