using TrackNTrash.Tracking.Api;
using TrackNTrash.Tracking.Infrastructure;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;
using TrackNTrash.Tracking.Core.Trips;
using TrackNTrash.Tracking.Core.Receiving;
using TrackNTrash.Tracking.Api.Console;

using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using TrackNTrash.Tracking.Api.Auth;

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
var useSql = !string.IsNullOrWhiteSpace(sqlCs);
if (useSql)
{
    builder.Services.AddSingleton<IEventStore>(new SqlEventStore(sqlCs!));
    builder.Services.AddSingleton<IShipmentStateStore>(new SqlShipmentStateStore(sqlCs!));
    builder.Services.AddSingleton<IExceptionStore>(new SqlExceptionStore(sqlCs!));
    builder.Services.AddSingleton<IManifestStore>(new SqlManifestStore(sqlCs!));
    builder.Services.AddSingleton(new SqlOrderStore(sqlCs!));
    builder.Services.AddSingleton(new SqlAssetStore(sqlCs!));
    builder.Services.AddSingleton(new SqlUserStore(sqlCs!));
    builder.Services.AddSingleton(new SqlItemStore(sqlCs!));
    builder.Services.AddSingleton(new SqlCameraStore(sqlCs!));
}
else
{
    builder.Services.AddSingleton<IEventStore, InMemoryEventStore>();
    builder.Services.AddSingleton<IShipmentStateStore, InMemoryShipmentStateStore>();
    builder.Services.AddSingleton<IExceptionStore, InMemoryExceptionStore>();
    builder.Services.AddSingleton<IManifestStore, InMemoryManifestStore>();
}

// ---- Authentication: local JWT (username/password) and/or Entra ID ----
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<TokenService>();

if (authOptions.LocalEnabled || authOptions.EntraEnabled)
{
    var auth = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

    if (authOptions.LocalEnabled)
        auth.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = authOptions.Issuer,
                ValidateAudience = true, ValidAudience = authOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(2),
            };
            // SignalR sends the token via query string on the websocket handshake.
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var accessToken = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        ctx.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });

    if (authOptions.EntraEnabled)
        auth.AddJwtBearer("Entra", o =>
        {
            o.Authority = $"https://login.microsoftonline.com/{authOptions.EntraTenantId}/v2.0";
            o.Audience = authOptions.EntraAudience;
            o.TokenValidationParameters = new TokenValidationParameters { ValidateLifetime = true };
        });

    builder.Services.AddAuthorization(o =>
    {
        // Accept either scheme wherever [Authorize]/RequireAuthorization is used.
        var schemes = new List<string>();
        if (authOptions.LocalEnabled) schemes.Add(JwtBearerDefaults.AuthenticationScheme);
        if (authOptions.EntraEnabled) schemes.Add("Entra");
        o.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(schemes.ToArray())
            .RequireAuthenticatedUser().Build();
    });
}

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

// CORS for the exception console. Origins are configurable (Cors:Origins, comma-separated)
// so the deployed console URL can be allowed alongside the Vite dev server.
var corsOrigins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy("console", p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
app.UseCors("console");
if (authOptions.LocalEnabled || authOptions.EntraEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.UseSwagger();
app.UseSwaggerUI();

// ---------- Auth ----------
// What sign-in methods this deployment offers (drives the console's login screen).
app.MapGet("/auth/config", () => Results.Ok(new
{
    local = authOptions.LocalEnabled,
    entra = authOptions.EntraEnabled,
    entraTenantId = authOptions.EntraEnabled ? authOptions.EntraTenantId : null,
    entraClientId = authOptions.EntraEnabled ? authOptions.EntraAudience : null,
})).WithTags("Auth").WithName("AuthConfig");

app.MapPost("/auth/login", async (LoginDto dto, IServiceProvider sp, TokenService tokens, CancellationToken ct) =>
{
    if (!authOptions.LocalEnabled) return Results.Problem("Local sign-in is not enabled.", statusCode: 501);
    var users = sp.GetService<SqlUserStore>();
    if (users is null) return Results.Problem("Local sign-in requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { error = "username and password are required." });

    var user = await users.AuthenticateAsync(dto.Username.Trim(), dto.Password, ct);
    if (user is null) return Results.Json(new { error = "Incorrect username or password." }, statusCode: 401);

    var (token, expires) = tokens.Issue(user);
    return Results.Ok(new { token, expiresUtc = expires, name = user.DisplayName, username = user.Username, roles = user.Roles });
}).WithTags("Auth").WithName("Login");

// First-run/admin seeding of local users. Requires the configured setup key.
app.MapPost("/auth/users", async (UpsertUserDto dto, HttpRequest req, IServiceProvider sp, IConfiguration cfg, CancellationToken ct) =>
{
    var setupKey = cfg["Auth:SetupKey"];
    if (string.IsNullOrWhiteSpace(setupKey) || req.Headers["x-setup-key"] != setupKey)
        return Results.Json(new { error = "Invalid setup key." }, statusCode: 403);
    var users = sp.GetService<SqlUserStore>();
    if (users is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { error = "username and password are required." });
    await users.UpsertAsync(dto.Username.Trim(), string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.Username : dto.DisplayName,
        dto.Password, string.IsNullOrWhiteSpace(dto.Roles) ? "Dispatcher" : dto.Roles, ct);
    return Results.Ok(new { dto.Username, seeded = true });
}).WithTags("Auth").WithName("UpsertUser");

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "TrackNTrash.Tracking.Api" }))
   .WithTags("System");

// ---------- Order intake (D365 outbound target; creates master data for SQL FKs) ----------
app.MapPost("/orders", async (OrderDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlOrderStore>();
    if (store is null)
        return Results.Problem("Order intake requires SQL persistence (set ConnectionStrings:TrackNTrash).", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.OrderNumber) || string.IsNullOrWhiteSpace(dto.StoreCode))
        return Results.BadRequest(new { error = "orderNumber and storeCode are required." });
    var lineIds = await store.CreateAsync(dto.ToInput(), ct);
    return Results.Ok(new { dto.OrderNumber, dto.StoreCode, orderLineIds = lineIds });
})
.WithTags("Orders").WithName("CreateOrder");

app.MapGet("/orders", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlOrderStore>();
    if (store is null) return Results.Ok(Array.Empty<object>());   // in-memory mode has no order master
    return Results.Ok(await store.ListAsync(500, ct));
})
.WithTags("Orders").WithName("ListOrders");

// ---------- Item-level tracking (units inside a carton) ----------
app.MapGet("/cartons", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlItemStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListCartonsAsync(500, ct));
}).WithTags("Items").WithName("ListCartons");

app.MapPost("/cartons", async (CartonSetupDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlItemStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.Gtin) || string.IsNullOrWhiteSpace(dto.Serial))
        return Results.BadRequest(new { error = "gtin and serial are required." });

    try
    {
        var cartonId = await store.CreateCartonAsync(dto.OrderLineId, dto.Gtin, dto.Serial,
            dto.ExpectedItemCount, dto.ItemIdentification, ct);
        int added = 0;
        if (dto.Items.Count > 0)
            added = await store.AddItemsAsync(cartonId, dto.Items.Select(i => (i.Barcode, i.Gtin, i.Description)), ct);
        return Results.Ok(new { cartonId, dto.Serial, dto.ExpectedItemCount, dto.ItemIdentification, itemsRegistered = added });
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 547)   // check/FK violation
    {
        return Results.BadRequest(new { error = "Carton rejected by a data rule. Serials allow letters, digits and - . / _ (max 20); the order line must exist.", detail = ex.Message });
    }
}).WithTags("Items").WithName("CreateCarton");

app.MapGet("/items/counts", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlItemStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListCountsAsync(500, ct));
}).WithTags("Items").WithName("ListItemCounts");

// The reconciliation entry point: barcode scans and/or a camera's visual count.
app.MapPost("/items/count", async (ItemCountDto dto, IServiceProvider sp, IngestionService ingestion,
    IExceptionStore exceptions, INotificationPublisher notifier, ExceptionSeverityMatrix severity, CancellationToken ct) =>
{
    var store = sp.GetService<SqlItemStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (dto.CartonId <= 0) return Results.BadRequest(new { error = "cartonId is required." });

    // Record the observation event first so the count can reference it.
    var evt = await ingestion.IngestAsync(new ScanEventInput
    {
        ClientEventId = $"itemcount:{dto.CartonId}:{Guid.NewGuid():N}",
        EventType = dto.VisionCount.HasValue ? "ItemVisionCount" : "ItemScan",
        Checkpoint = dto.Checkpoint,
        DeviceId = dto.DeviceId,
        CartonId = dto.CartonId,
        MetaJson = dto.FrameBlobUri is null ? null : $"{{\"frameRef\":\"{dto.FrameBlobUri}\"}}",
    }, ct);

    var result = await store.RecordCountAsync(dto.CartonId, dto.Checkpoint, dto.ScannedBarcodes,
        dto.VisionCount, dto.CameraId, dto.FrameBlobUri, dto.Confidence, evt.ScanEventId, ct);

    // A mismatch at item level is an exception, same as at carton level.
    if (result.Verdict is "SHORT" or "OVER")
    {
        var type = result.Verdict == "SHORT" ? ExceptionType.MissingCarton : ExceptionType.UnknownCarton;
        var ex = new TrackException
        {
            Type = type,
            Severity = severity.For(type),
            Checkpoint = dto.Checkpoint,
            CartonId = dto.CartonId,
            TriggeringEventId = evt.ScanEventId,
            Detail = $"Item count {result.Verdict} on carton {dto.CartonId}: {result.Detail}",
            FrameBlobUri = dto.FrameBlobUri,
        };
        await exceptions.AddAsync(ex, ct);
        // Publish too, so it reaches the ops console live (same path the ingestion pipeline uses).
        await notifier.PublishAsync(ex, ct);
    }

    return Results.Ok(result);
}).WithTags("Items").WithName("RecordItemCount");

// ---------- Cameras & site mapping ----------
app.MapGet("/cameras", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListAsync(ct));
}).WithTags("Cameras").WithName("ListCameras");

app.MapPost("/cameras", async (CameraDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.CameraCode) || string.IsNullOrWhiteSpace(dto.SiteCode))
        return Results.BadRequest(new { error = "cameraCode and siteCode are required." });
    var id = await store.UpsertAsync(dto.CameraCode, string.IsNullOrWhiteSpace(dto.Name) ? dto.CameraCode : dto.Name,
        dto.CameraKind, dto.SiteCode, dto.Zone, dto.Station, dto.Checkpoint, dto.RtspUrl, dto.Purpose, dto.Status, ct);
    return Results.Ok(new { cameraId = id, dto.CameraCode });
}).WithTags("Cameras").WithName("UpsertCamera");

app.MapPost("/cameras/{cameraId:int}/placement", async (int cameraId, PlacementDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    await store.PlaceAsync(cameraId, dto.SiteMapId, dto.X, dto.Y, dto.HeadingDeg, ct);
    return Results.Ok(new { cameraId, dto.SiteMapId, dto.X, dto.Y });
}).WithTags("Cameras").WithName("PlaceCamera");

app.MapPost("/cameras/{cameraCode}/heartbeat", async (string cameraCode, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    await store.HeartbeatAsync(cameraCode, ct);
    return Results.Ok(new { cameraCode, seen = DateTimeOffset.UtcNow });
}).WithTags("Cameras").WithName("CameraHeartbeat");

app.MapGet("/sitemaps", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListMapsAsync(ct));
}).WithTags("Cameras").WithName("ListSiteMaps");

app.MapPost("/sitemaps", async (SiteMapDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.SiteCode)) return Results.BadRequest(new { error = "siteCode is required." });
    var id = await store.UpsertMapAsync(dto.SiteCode, string.IsNullOrWhiteSpace(dto.Name) ? dto.SiteCode : dto.Name,
        dto.ImageUri, dto.Width, dto.Height, ct);
    return Results.Ok(new { siteMapId = id, dto.SiteCode });
}).WithTags("Cameras").WithName("UpsertSiteMap");

// ---------- Asset master (reusable trays) ----------
app.MapGet("/assets", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlAssetStore>();
    if (store is null) return Results.Ok(Array.Empty<object>());
    return Results.Ok(await store.ListAsync(1000, ct));
})
.WithTags("Assets").WithName("ListAssets");

app.MapGet("/assets/summary", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlAssetStore>();
    if (store is null) return Results.Ok(new { total = 0 });
    return Results.Ok(await store.SummaryAsync(ct));
})
.WithTags("Assets").WithName("AssetSummary");

app.MapPost("/assets/register", async (RegisterAssetsDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlAssetStore>();
    if (store is null) return Results.Problem("Asset registry requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.SiteCode) || dto.Count < 1)
        return Results.BadRequest(new { error = "siteCode and count (>=1) required." });
    var qrs = await store.RegisterTraysAsync(dto.SiteCode, dto.Count, ct);
    return Results.Ok(new { registered = qrs.Count, trayQrs = qrs });
})
.WithTags("Assets").WithName("RegisterAssets");

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
.WithTags("Admin").WithName("RunSweep").RequireAuthorizationWhenConfigured(authOptions);

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
app.MapHub<ExceptionsHub>("/hubs/exceptions").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/console/exceptions", (string? checkpoint, string? severity, string? status, string? route, ConsoleExceptionStore store) =>
    Results.Ok(store.List(checkpoint, severity, status, route)))
.WithTags("Console").WithName("ListConsoleExceptions").RequireAuthorizationWhenConfigured(authOptions);

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
.WithTags("Console").WithName("GetConsoleException").RequireAuthorizationWhenConfigured(authOptions);

// One-click actions. In prod these require role auth (Dispatcher / Warehouse Manager / Admin).
app.MapPost("/console/exceptions/{id:long}/acknowledge", async (long id, ActionDto dto, ConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub) =>
    await ApplyAction(id, "acknowledge", dto, store, hub))
.WithTags("Console").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/console/exceptions/{id:long}/resolve", async (long id, ActionDto dto, ConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub) =>
    await ApplyAction(id, "resolve", dto, store, hub))
.WithTags("Console").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/console/exceptions/{id:long}/escalate", async (long id, ActionDto dto, ConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub) =>
{
    var result = await ApplyAction(id, "escalate", dto, store, hub);
    // Escalate also emits a Teams post payload (posted by a Service Bus subscriber in prod).
    return result;
})
.WithTags("Console").RequireAuthorizationWhenConfigured(authOptions);

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
