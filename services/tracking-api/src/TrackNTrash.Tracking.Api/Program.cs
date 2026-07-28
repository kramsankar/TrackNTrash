using TrackNTrash.Tracking.Api;
using TrackNTrash.Tracking.Api.Infrastructure;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;
using TrackNTrash.Tracking.Core.Trips;
using TrackNTrash.Tracking.Core.Receiving;
using TrackNTrash.Tracking.Api.Console;

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Serialize enums as strings for readable API payloads (Picked, IllegalTransition, …).
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Core singletons ----
builder.Services.AddSingleton<ShipmentStateMachine>();
builder.Services.AddSingleton<ExceptionSeverityMatrix>(_ => new ExceptionSeverityMatrix());

// ---- Stores: SQL when a connection string is present, else in-memory ----
var sqlCs = builder.Configuration.GetConnectionString("TrackNTrash");
if (!string.IsNullOrWhiteSpace(sqlCs))
    builder.Services.AddSingleton<IEventStore>(new SqlEventStore(sqlCs));
else
    builder.Services.AddSingleton<IEventStore, InMemoryEventStore>();

// State / exception / manifest stores: in-memory by default (SQL variants follow the same shape).
builder.Services.AddSingleton<IShipmentStateStore, InMemoryShipmentStateStore>();
builder.Services.AddSingleton<IExceptionStore, InMemoryExceptionStore>();
builder.Services.AddSingleton<IManifestStore, InMemoryManifestStore>();

// ---- Console + SignalR (Module 12) ----
builder.Services.AddSignalR();
builder.Services.AddSingleton<ConsoleExceptionStore>();

// ---- Notifications: Service Bus when configured, else logging; wrapped by the SignalR relay ----
var sbCs = builder.Configuration["ServiceBus:ConnectionString"];
var sbTopic = builder.Configuration["ServiceBus:Topic"] ?? "exceptions";
INotificationPublisher innerPublisher = !string.IsNullOrWhiteSpace(sbCs)
    ? new ServiceBusNotificationPublisher(sbCs, sbTopic)
    : new LoggingNotificationPublisher(
        LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LoggingNotificationPublisher>());
builder.Services.AddSingleton(innerPublisher);
builder.Services.AddSingleton<INotificationPublisher>(sp => new SignalRExceptionRelay(
    innerPublisher,
    sp.GetRequiredService<ConsoleExceptionStore>(),
    sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub>>()));

// ---- Exception rules (register concrete rules; add more by adding a line) ----
builder.Services.AddSingleton<IIngestExceptionRule, CountMismatchAtDockRule>();
builder.Services.AddSingleton<ISweepExceptionRule, NoReceiveWithinSlaRule>();

// ---- Services ----
builder.Services.AddSingleton<IngestionService>();
builder.Services.AddSingleton(_ => SweepOptions.Default);
builder.Services.AddSingleton<SweepService>();

// ---- Trips (Module 7) ----
builder.Services.AddSingleton<ITripStore, InMemoryTripStore>();
builder.Services.AddSingleton<TripService>();

// ---- Receiving (Module 8) ----
builder.Services.AddSingleton<IAsnStore, InMemoryAsnStore>();
builder.Services.AddSingleton<ReceivingService>();
// Receiving sessions are per-tray/store; held server-side keyed by a session id for the demo API.
builder.Services.AddSingleton<ReceivingSessionCache>();

// CORS for the exception console (Vite dev server).
builder.Services.AddCors(o => o.AddPolicy("console", p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
app.UseCors("console");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "TrackNTrash.Tracking.Api" }))
   .WithTags("System");

// ---------- Ingestion ----------
app.MapPost("/events/scan", async (ScanEventDto dto, IngestionService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.ClientEventId) || string.IsNullOrWhiteSpace(dto.DeviceId))
        return Results.BadRequest(new { error = "clientEventId and deviceId are required." });
    var result = await svc.IngestAsync(dto.ToInput(), ct);
    return Results.Ok(result);
})
.WithTags("Events").WithName("IngestScan")
.Produces<IngestResult>(200).ProducesProblem(400);

app.MapPost("/events/scan/batch", async (ScanEventDto[] dtos, IngestionService svc, CancellationToken ct) =>
{
    var results = new List<IngestResult>(dtos.Length);
    foreach (var dto in dtos)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientEventId) || string.IsNullOrWhiteSpace(dto.DeviceId))
            continue; // skip malformed; batch stays best-effort + idempotent
        results.Add(await svc.IngestAsync(dto.ToInput(), ct));
    }
    return Results.Ok(new { accepted = results.Count, results });
})
.WithTags("Events").WithName("IngestScanBatch");

// ---------- Manifest sync (edge module pulls expected tray manifests) ----------
app.MapGet("/manifests", async (DateTimeOffset? since, IManifestStore store, CancellationToken ct) =>
{
    var cutoff = since ?? DateTimeOffset.MinValue;
    var manifests = await store.GetChangedSinceAsync(cutoff, ct);
    return Results.Ok(new { since = cutoff, count = manifests.Count, manifests });
})
.WithTags("Manifests").WithName("GetManifestsDelta");

app.MapPut("/manifests", async (ManifestDto dto, IManifestStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TrayQr))
        return Results.BadRequest(new { error = "trayQr is required." });
    await store.UpsertAsync(dto.ToManifest(), ct);
    return Results.Ok(new { dto.TrayQr, dto.ExpectedCartonCount });
})
.WithTags("Manifests").WithName("UpsertManifest");

// ---------- Read models ----------
app.MapGet("/shipment-lines/{orderLineId:long}/state", async (long orderLineId, IShipmentStateStore store, CancellationToken ct) =>
{
    var rec = await store.GetOrCreateAsync(orderLineId, ct);
    return Results.Ok(rec);
})
.WithTags("State").WithName("GetLineState");

app.MapGet("/exceptions/open", async (IExceptionStore store, CancellationToken ct) =>
    Results.Ok(await store.GetOpenAsync(ct)))
.WithTags("Exceptions").WithName("GetOpenExceptions");

// ---------- Manual sweep trigger (the Functions timer calls SweepService directly) ----------
app.MapPost("/admin/sweep", async (SweepService sweep, CancellationToken ct) =>
    Results.Ok(await sweep.RunAsync(DateTimeOffset.UtcNow, ct)))
.WithTags("Admin").WithName("RunSweep");

// ---------- Trips (Module 7) ----------
app.MapPost("/trips", async (CreateTripDto dto, TripService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.VehicleReg))
        return Results.BadRequest(new { error = "vehicleReg is required." });
    var trip = await svc.CreateAsync(dto.ToRequest(), ct);
    return Results.Ok(new { trip.TripNumber, trip.ManifestQr, trip.Status,
        stops = trip.Stops.Count, trays = trip.Loads.Count });
})
.WithTags("Trips").WithName("CreateTrip");

app.MapGet("/trips/{tripNumber}", async (string tripNumber, TripService svc, CancellationToken ct) =>
{
    var trip = await svc.GetAsync(tripNumber, ct);
    return trip is null ? Results.NotFound() : Results.Ok(trip);
})
.WithTags("Trips").WithName("GetTrip");

// Driver scans a tray at the loading dock (wrong-trip detection happens here).
app.MapPost("/trips/{tripNumber}/load", async (string tripNumber, LoadScanDto dto, TripService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TrayQr))
        return Results.BadRequest(new { error = "trayQr is required." });
    var result = await svc.LoadTrayScanAsync(tripNumber, dto.TrayQr, dto.DeviceId, dto.UserId, ct);
    // Always 200 with an outcome — the app renders red/green from Outcome; a wrong-trip is a
    // business outcome, not an HTTP error.
    return Results.Ok(result);
})
.WithTags("Trips").WithName("LoadTrayScan");

// Telematics / geofence webhook.
app.MapPost("/events/telemetry", async (TelemetryDto dto, TripService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TripNumber))
        return Results.BadRequest(new { error = "tripNumber is required." });
    if (string.Equals(dto.Event, "depart", StringComparison.OrdinalIgnoreCase))
    {
        var ok = await svc.DepartAsync(dto.TripNumber, dto.DeviceId ?? "telematics", ct);
        return ok ? Results.Ok(new { dto.TripNumber, transitioned = "InTransit" })
                  : Results.Conflict(new { error = "Trip not in a loadable/departable state." });
    }
    // "arrive" and other events are recorded but need no line transition here (handled at receiving).
    return Results.Ok(new { dto.TripNumber, recorded = dto.Event });
})
.WithTags("Trips").WithName("Telemetry");

// ---------- Receiving (Module 8) ----------
app.MapPut("/asn", async (AsnDto dto, IAsnStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TrayQr) || string.IsNullOrWhiteSpace(dto.StoreCode))
        return Results.BadRequest(new { error = "trayQr and storeCode are required." });
    await store.UpsertAsync(dto.ToAsn(), ct);
    return Results.Ok(new { dto.TrayQr, dto.StoreCode, expected = dto.ExpectedCartons.Count });
})
.WithTags("Receiving").WithName("UpsertAsn");

app.MapPost("/receiving/start", async (StartReceivingDto dto, ReceivingService svc, ReceivingSessionCache cache, CancellationToken ct) =>
{
    var session = await svc.StartAsync(dto.TrayQr, dto.StoreCode, ct);
    if (session is null) return Results.NotFound(new { error = "No ASN for this tray/store." });
    var id = cache.Add(session);
    return Results.Ok(new { sessionId = id, session.Asn.TrayQr, session.Asn.StoreCode,
        expected = session.ExpectedCount,
        expectedCartons = session.Asn.ExpectedCartons });
})
.WithTags("Receiving").WithName("StartReceiving");

app.MapPost("/receiving/{sessionId}/scan", async (string sessionId, ScanCartonDto dto, ReceivingService svc, ReceivingSessionCache cache, CancellationToken ct) =>
{
    var session = cache.Get(sessionId);
    if (session is null) return Results.NotFound(new { error = "Unknown session." });
    var result = await svc.ScanAsync(session, dto.Payload, ct);
    return Results.Ok(result);
})
.WithTags("Receiving").WithName("ReceivingScan");

app.MapPost("/receiving/{sessionId}/damaged", (string sessionId, DamagedDto dto, ReceivingService svc, ReceivingSessionCache cache) =>
{
    var session = cache.Get(sessionId);
    if (session is null) return Results.NotFound(new { error = "Unknown session." });
    if (string.IsNullOrWhiteSpace(dto.PhotoBlobUri))
        return Results.BadRequest(new { error = "A damage photo is required." });
    return Results.Ok(svc.FlagDamaged(session, dto.Payload, dto.PhotoBlobUri));
})
.WithTags("Receiving").WithName("ReceivingDamaged");

app.MapPost("/receiving/{sessionId}/complete", async (string sessionId, CompleteReceivingDto dto, ReceivingService svc, ReceivingSessionCache cache, CancellationToken ct) =>
{
    var session = cache.Get(sessionId);
    if (session is null) return Results.NotFound(new { error = "Unknown session." });
    if (string.IsNullOrWhiteSpace(dto.ReceiverName))
        return Results.BadRequest(new { error = "receiverName is required for POD." });
    var summary = await svc.CompleteAsync(session, dto.DeviceId,
        new ProofOfDelivery { ReceiverName = dto.ReceiverName, SignatureBlobUri = dto.SignatureBlobUri, DeliveryPhotoBlobUri = dto.DeliveryPhotoBlobUri }, ct);
    cache.Remove(sessionId);
    return Results.Ok(summary);
})
.WithTags("Receiving").WithName("CompleteReceiving");

app.MapPost("/receiving/return-tray", async (ReturnTrayDto dto, ReceivingService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TrayQr) || string.IsNullOrWhiteSpace(dto.VehicleReg))
        return Results.BadRequest(new { error = "trayQr and vehicleReg are required." });
    await svc.ReturnEmptyTrayAsync(dto.TrayQr, dto.VehicleReg, dto.DeviceId, ct);
    return Results.Ok(new { dto.TrayQr, returnedTo = dto.VehicleReg });
})
.WithTags("Receiving").WithName("ReturnTray");

// ---------- Exception Console (Module 12) ----------
app.MapHub<ExceptionsHub>("/hubs/exceptions");

app.MapGet("/console/exceptions", (string? checkpoint, string? severity, string? status, string? route, ConsoleExceptionStore store) =>
    Results.Ok(store.List(checkpoint, severity, status, route)))
.WithTags("Console").WithName("ListConsoleExceptions");

app.MapGet("/console/exceptions/{id:long}", async (long id, ConsoleExceptionStore store, IEventStore events, CancellationToken ct) =>
{
    var ex = store.Get(id);
    if (ex is null) return Results.NotFound();
    // Attach the affected order line's event timeline.
    var timeline = ex.OrderLineId is null
        ? Array.Empty<object>()
        : (await events.GetByOrderLineAsync(ex.OrderLineId.Value, ct))
            .Select(e => new { e.ScanEventId, e.Input.EventType, e.Input.Verdict, e.Input.EventUtc })
            .Cast<object>().ToArray();
    return Results.Ok(new { exception = ex, timeline });
})
.WithTags("Console").WithName("GetConsoleException");

// One-click actions. In prod these require role auth (Dispatcher / Warehouse Manager / Admin).
app.MapPost("/console/exceptions/{id:long}/acknowledge", async (long id, ActionDto dto, ConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub) =>
    await ApplyAction(id, "acknowledge", dto, store, hub))
.WithTags("Console");

app.MapPost("/console/exceptions/{id:long}/resolve", async (long id, ActionDto dto, ConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub) =>
    await ApplyAction(id, "resolve", dto, store, hub))
.WithTags("Console");

app.MapPost("/console/exceptions/{id:long}/escalate", async (long id, ActionDto dto, ConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub) =>
{
    var result = await ApplyAction(id, "escalate", dto, store, hub);
    // Escalate also emits a Teams post payload (posted by a Service Bus subscriber in prod).
    return result;
})
.WithTags("Console");

static async Task<IResult> ApplyAction(long id, string action, ActionDto dto, ConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub)
{
    if (string.IsNullOrWhiteSpace(dto.User))
        return Results.BadRequest(new { error = "user is required for audit." });
    if (!store.Apply(id, action, dto.User, dto.Note ?? dto.ReasonCode, out var updated))
        return Results.NotFound();
    await hub.Clients.All.SendAsync("exceptionUpdated", updated);
    return Results.Ok(updated);
}

app.Run();

public sealed record ActionDto(string User, string? ReasonCode, string? Note);

public partial class Program { }
