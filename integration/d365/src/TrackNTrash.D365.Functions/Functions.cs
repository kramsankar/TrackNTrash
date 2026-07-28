using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TrackNTrash.D365.Integration;

namespace TrackNTrash.D365.Functions;

/// <summary>
/// OUTBOUND from F&O → tracking system.
/// F&O emits a business event (sales order + warehouse work confirmed) to a Service Bus queue.
/// This creates the order/lines/expected-carton records in the tracking DB. Event-driven, no polling.
/// </summary>
public sealed class SalesOrderConfirmedFunction
{
    private readonly ITrackingIntakeClient _intake;
    private readonly IIdempotencyStore _idem;
    private readonly ILogger<SalesOrderConfirmedFunction> _log;

    public SalesOrderConfirmedFunction(ITrackingIntakeClient intake, IIdempotencyStore idem, ILogger<SalesOrderConfirmedFunction> log)
    {
        _intake = intake;
        _idem = idem;
        _log = log;
    }

    [Function("SalesOrderConfirmed")]
    public async Task Run(
        [ServiceBusTrigger("fno-business-events", Connection = "ServiceBusConnection")] string message,
        FunctionContext ctx)
    {
        var e = JsonSerializer.Deserialize<FoSalesOrderConfirmed>(message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (e is null || string.IsNullOrWhiteSpace(e.EventId)) { _log.LogWarning("Unparseable business event"); return; }

        if (await _idem.SeenAsync("fno-outbound", e.EventId, ctx.CancellationToken))
        {
            _log.LogInformation("Duplicate business event {EventId} ignored", e.EventId);
            return;
        }

        var intake = Mapping.ToOrderIntake(e);
        await _intake.CreateOrderAsync(intake, ctx.CancellationToken);   // Function host retries on throw
        await _idem.MarkAsync("fno-outbound", e.EventId, ctx.CancellationToken);
        _log.LogInformation("Created tracking order {Order} from F&O event {EventId}", intake.OrderNumber, e.EventId);
    }
}

/// <summary>
/// INBOUND to F&O ← tracking system.
/// The tracking API publishes ReceivingComplete (and other) events to a Service Bus topic.
/// This posts the delivery note / packing slip confirmation, with idempotency, retry and
/// dead-lettering handled by D365PostingService.
/// </summary>
public sealed class ReceivingCompletedFunction
{
    private readonly D365PostingService _posting;
    private readonly ILogger<ReceivingCompletedFunction> _log;

    public ReceivingCompletedFunction(D365PostingService posting, ILogger<ReceivingCompletedFunction> log)
    {
        _posting = posting;
        _log = log;
    }

    [Function("ReceivingCompleted")]
    public async Task Run(
        [ServiceBusTrigger("tracking-events", "d365-delivery", Connection = "ServiceBusConnection")] string message,
        FunctionContext ctx)
    {
        var e = JsonSerializer.Deserialize<TrackingOutboundEvent>(message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        if (e is null || e.Kind != TrackingEventKind.ReceivingComplete) return;

        var result = await _posting.PostAsync(e, ctx.CancellationToken);
        _log.LogInformation("F&O delivery post for {Order}: {Message}", e.OrderNumber, result.Message);
    }
}
