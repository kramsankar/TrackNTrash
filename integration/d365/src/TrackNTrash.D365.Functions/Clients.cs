using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TrackNTrash.D365.Integration;

namespace TrackNTrash.D365.Functions;

/// <summary>
/// Posts order intake into the tracking system's order-intake endpoint
/// (POST /orders → ops.SalesOrder / OrderLine / Carton in the Module 1 schema).
/// </summary>
public sealed class HttpTrackingIntakeClient : ITrackingIntakeClient
{
    private readonly HttpClient _http;
    public HttpTrackingIntakeClient(HttpClient http) => _http = http;

    public async Task CreateOrderAsync(OrderIntake order, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/orders", order, ct);
        resp.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// D365 F&O client over OData / custom services. Endpoints are illustrative; wire the real
/// entity paths (SalesOrderHeadersV2, WHSWorkLine custom service, etc.) and OAuth token here.
/// </summary>
public sealed class ODataD365Client : ID365Client
{
    private readonly HttpClient _http;
    private readonly ILogger<ODataD365Client> _log;
    public ODataD365Client(HttpClient http, ILogger<ODataD365Client> log) { _http = http; _log = log; }

    public async Task PostPickingConfirmationAsync(PickingConfirmation c, CancellationToken ct = default)
    {
        // Custom service that confirms WHSWorkLine for the InventTransId.
        var resp = await _http.PostAsJsonAsync("/api/services/TntWhsWorkService/confirmPick", c, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task PostShipmentConfirmationAsync(ShipmentConfirmation c, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/data/CustomerPackingSlipHeaders", c, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task PostDeliveryNoteAsync(DeliveryNotePosting c, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/services/TntDeliveryService/postDeliveryNote", c, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task CreateShortageCaseAsync(DeliveryNotePosting c, CancellationToken ct = default)
    {
        _log.LogInformation("Creating F&O case for shortages on {Order}", c.OrderNumber);
        var resp = await _http.PostAsJsonAsync("/data/Cases", new { c.OrderNumber, reason = "DeliveryShortage" }, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task PostQuantityAdjustmentAsync(DeliveryNotePosting c, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/data/InventoryAdjustments",
            new { c.OrderNumber, lines = c.Lines }, ct);
        resp.EnsureSuccessStatusCode();
    }
}
