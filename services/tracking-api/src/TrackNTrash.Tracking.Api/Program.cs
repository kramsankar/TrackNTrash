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
    builder.Services.AddSingleton(new SqlMasterStore(sqlCs!));
    builder.Services.AddSingleton(new SqlRbacStore(sqlCs!));
    builder.Services.AddSingleton<ITrayProjection>(new SqlTrayProjection(sqlCs!));
}
else
{
    builder.Services.AddSingleton<IEventStore, InMemoryEventStore>();
    builder.Services.AddSingleton<IShipmentStateStore, InMemoryShipmentStateStore>();
    builder.Services.AddSingleton<IExceptionStore, InMemoryExceptionStore>();
    builder.Services.AddSingleton<IManifestStore, InMemoryManifestStore>();
    builder.Services.AddSingleton<ITrayProjection, NoOpTrayProjection>();
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

        // A camera sits unattended in a warehouse, so its credentials are the ones most
        // likely to walk. A device account is therefore refused everywhere by default and
        // allowed only on the handful of endpoints it genuinely needs (DevicePolicy).
        o.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(schemes.ToArray())
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => !DeviceRoles.IsDeviceOnly(ctx.User))
            .Build();

        o.AddPolicy(DeviceRoles.DevicePolicy, p => p
            .AddAuthenticationSchemes(schemes.ToArray())
            .RequireAuthenticatedUser());
    });
}

// ---- Console + SignalR (Module 12) ----
builder.Services.AddSignalR();
// The console used to read a private in-memory list, so a restart showed an empty board
// while ops.Exception still held every unactioned row.
if (useSql) builder.Services.AddSingleton<IConsoleExceptionStore>(new SqlConsoleExceptionStore(sqlCs!));
else builder.Services.AddSingleton<IConsoleExceptionStore, InMemoryConsoleExceptionStore>();

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
    sp.GetRequiredService<IConsoleExceptionStore>(),
    sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub>>()));

// ---- Exception rules (register concrete rules; add more by adding a line) ----
builder.Services.AddSingleton<IIngestExceptionRule, CountMismatchAtDockRule>();
builder.Services.AddSingleton<ISweepExceptionRule, NoReceiveWithinSlaRule>();

// ---- Services ----
builder.Services.AddSingleton<IngestionService>();
builder.Services.AddSingleton(_ => SweepOptions.Default);
builder.Services.AddSingleton<SweepService>();

// ---- Trips (Module 7) ----
// Trips were in-memory only, so an App Service recycle silently discarded them.
if (useSql) builder.Services.AddSingleton<ITripStore>(new SqlTripStore(sqlCs!));
else builder.Services.AddSingleton<ITripStore, InMemoryTripStore>();
builder.Services.AddSingleton<TripService>();

// ---- Receiving (Module 8) ----
// ASNs in memory meant a recycle stranded an inbound tray at the store door with no
// expected-carton list, so every scan read as an over-scan.
if (useSql) builder.Services.AddSingleton<IAsnStore>(new SqlAsnStore(sqlCs!));
else builder.Services.AddSingleton<IAsnStore, InMemoryAsnStore>();
builder.Services.AddSingleton<ReceivingService>();
// Receiving sessions are per-tray/store; held server-side keyed by a session id.
// In memory they did not survive a recycle, so a colleague mid-tray had to start again.
if (useSql)
    builder.Services.AddSingleton<IReceivingSessionStore>(sp =>
        new SqlReceivingSessionStore(sqlCs!, sp.GetRequiredService<IAsnStore>()));
else
    builder.Services.AddSingleton<IReceivingSessionStore, InMemoryReceivingSessionStore>();

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
.WithTags("Orders").WithName("CreateOrder").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/orders", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlOrderStore>();
    if (store is null) return Results.Ok(Array.Empty<object>());   // in-memory mode has no order master
    return Results.Ok(await store.ListAsync(500, ct));
})
.WithTags("Orders").WithName("ListOrders").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Generic master data (product, store, zone, rack, vehicle, device, role) ----------
app.MapGet("/masters", () => Results.Ok(SqlMasterStore.Masters.Values.Select(m => new { m.Key, m.Label })))
    .WithTags("Masters").WithName("ListMasterTypes").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/masters/{key}", async (string key, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlMasterStore>();
    if (store is null) return Results.Ok(Array.Empty<object>());
    try { return Results.Ok(await store.ListAsync(key, ct)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithTags("Masters").WithName("ListMaster").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/masters/{key}", async (string key, System.Text.Json.JsonElement body, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlMasterStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    try { return Results.Ok(new { id = await store.CreateAsync(key, body, ct) }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
    { return Results.BadRequest(new { error = "That code already exists — pick a different one." }); }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 547)
    { return Results.BadRequest(new { error = "A referenced record does not exist, or a value breaks a data rule." }); }
}).WithTags("Masters").WithName("CreateMaster").RequireAuthorizationWhenConfigured(authOptions);

app.MapPut("/masters/{key}/{id:int}", async (string key, int id, System.Text.Json.JsonElement body, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlMasterStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    try { return await store.UpdateAsync(key, id, body, ct) ? Results.Ok(new { id, updated = true }) : Results.NotFound(); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
    { return Results.BadRequest(new { error = "That code already exists — pick a different one." }); }
}).WithTags("Masters").WithName("UpdateMaster").RequireAuthorizationWhenConfigured(authOptions);

app.MapDelete("/masters/{key}/{id:int}", async (string key, int id, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlMasterStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    try { return await store.DeleteAsync(key, id, ct) ? Results.Ok(new { id, deleted = true }) : Results.NotFound(); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 547)
    { return Results.BadRequest(new { error = "This record is still referenced elsewhere and cannot be removed." }); }
}).WithTags("Masters").WithName("DeleteMaster").RequireAuthorizationWhenConfigured(authOptions);

// ---------- RBAC: forms, role mappings, users ----------
app.MapGet("/rbac/forms", async (IServiceProvider sp, CancellationToken ct) =>
{
    var rbac = sp.GetService<SqlRbacStore>();
    return rbac is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await rbac.ListFormsAsync(ct));
}).WithTags("RBAC").WithName("ListForms").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/rbac/mappings", async (int? roleId, IServiceProvider sp, CancellationToken ct) =>
{
    var rbac = sp.GetService<SqlRbacStore>();
    return rbac is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await rbac.ListMappingsAsync(roleId, ct));
}).WithTags("RBAC").WithName("ListMappings").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/rbac/mappings", async (MappingDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var rbac = sp.GetService<SqlRbacStore>();
    if (rbac is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (dto.RoleId <= 0 || string.IsNullOrWhiteSpace(dto.FormId))
        return Results.BadRequest(new { error = "roleId and formId are required." });
    await rbac.SaveMappingAsync(dto.RoleId, dto.FormId, dto.CanView, dto.CanCreate, dto.CanEdit, dto.CanDelete, ct);
    return Results.Ok(new { dto.RoleId, dto.FormId, saved = true });
}).WithTags("RBAC").WithName("SaveMapping").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/rbac/users", async (IServiceProvider sp, CancellationToken ct) =>
{
    var rbac = sp.GetService<SqlRbacStore>();
    return rbac is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await rbac.ListUsersAsync(ct));
}).WithTags("RBAC").WithName("ListUsers").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/rbac/users", async (SaveUserDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var rbac = sp.GetService<SqlRbacStore>();
    if (rbac is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.Username))
        return Results.BadRequest(new { error = "username is required." });
    if (dto.UserId is null && string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { error = "A password is required for a new user." });
    try
    {
        var id = await rbac.SaveUserAsync(dto.UserId, dto.Username.Trim(),
            string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.Username : dto.DisplayName,
            dto.Email, dto.RoleId, dto.SiteCode, dto.Password, dto.IsActive, ct);
        return Results.Ok(new { userId = id, dto.Username });
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
    { return Results.BadRequest(new { error = "That username is already taken." }); }
}).WithTags("RBAC").WithName("SaveUser").RequireAuthorizationWhenConfigured(authOptions);

// The console asks for its own permissions to build the menu.
app.MapGet("/rbac/permissions", async (HttpContext http, IServiceProvider sp, CancellationToken ct) =>
{
    var rbac = sp.GetService<SqlRbacStore>();
    if (rbac is null) return Results.Ok(Array.Empty<object>());
    // Always derive the subject from the token. Accepting a ?username= would let any
    // caller read another user's permission set.
    var username = http.User?.Identity?.Name
        ?? http.User?.FindFirst("unique_name")?.Value
        ?? http.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    // Only when no sign-in method is configured (local dev) may the caller name itself.
    if (string.IsNullOrWhiteSpace(username) && !authOptions.LocalEnabled && !authOptions.EntraEnabled)
        username = http.Request.Query["username"].ToString();
    if (string.IsNullOrWhiteSpace(username)) return Results.Ok(Array.Empty<object>());
    return Results.Ok(await rbac.PermissionsForUserAsync(username, ct));
}).WithTags("RBAC").WithName("MyPermissions").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Item-level tracking (units inside a carton) ----------
app.MapGet("/cartons", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlItemStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListCartonsAsync(500, ct));
}).WithTags("Items").WithName("ListCartons").RequireAuthorizationWhenConfigured(authOptions);

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
}).WithTags("Items").WithName("CreateCarton").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/items/counts", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlItemStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListCountsAsync(500, ct));
}).WithTags("Items").WithName("ListItemCounts").RequireAuthorizationWhenConfigured(authOptions);

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
}).WithTags("Items").WithName("RecordItemCount").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Cameras & site mapping ----------
app.MapGet("/cameras", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListAsync(ct));
}).WithTags("Cameras").WithName("ListCameras").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/cameras", async (CameraDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.CameraCode) || string.IsNullOrWhiteSpace(dto.SiteCode))
        return Results.BadRequest(new { error = "cameraCode and siteCode are required." });
    var id = await store.UpsertAsync(dto.CameraCode, string.IsNullOrWhiteSpace(dto.Name) ? dto.CameraCode : dto.Name,
        dto.CameraKind, dto.SiteCode, dto.Zone, dto.Station, dto.Checkpoint, dto.RtspUrl, dto.Purpose, dto.Status, ct);
    return Results.Ok(new { cameraId = id, dto.CameraCode });
}).WithTags("Cameras").WithName("UpsertCamera").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/cameras/{cameraId:int}/placement", async (int cameraId, PlacementDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    await store.PlaceAsync(cameraId, dto.SiteMapId, dto.X, dto.Y, dto.HeadingDeg, ct);
    return Results.Ok(new { cameraId, dto.SiteMapId, dto.X, dto.Y });
}).WithTags("Cameras").WithName("PlaceCamera").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/cameras/{cameraCode}/heartbeat", async (string cameraCode, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    await store.HeartbeatAsync(cameraCode, ct);
    return Results.Ok(new { cameraCode, seen = DateTimeOffset.UtcNow });
}).WithTags("Cameras").WithName("CameraHeartbeat").AllowDevicesWhenConfigured(authOptions);

app.MapGet("/sitemaps", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    return store is null ? Results.Ok(Array.Empty<object>()) : Results.Ok(await store.ListMapsAsync(ct));
}).WithTags("Cameras").WithName("ListSiteMaps").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/sitemaps", async (SiteMapDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlCameraStore>();
    if (store is null) return Results.Problem("Requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.SiteCode)) return Results.BadRequest(new { error = "siteCode is required." });
    var id = await store.UpsertMapAsync(dto.SiteCode, string.IsNullOrWhiteSpace(dto.Name) ? dto.SiteCode : dto.Name,
        dto.ImageUri, dto.Width, dto.Height, ct);
    return Results.Ok(new { siteMapId = id, dto.SiteCode });
}).WithTags("Cameras").WithName("UpsertSiteMap").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Asset master (reusable trays) ----------
app.MapGet("/assets", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlAssetStore>();
    if (store is null) return Results.Ok(Array.Empty<object>());
    return Results.Ok(await store.ListAsync(1000, ct));
})
.WithTags("Assets").WithName("ListAssets").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/assets/summary", async (IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlAssetStore>();
    if (store is null) return Results.Ok(new { total = 0 });
    return Results.Ok(await store.SummaryAsync(ct));
})
.WithTags("Assets").WithName("AssetSummary").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/assets/register", async (RegisterAssetsDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    var store = sp.GetService<SqlAssetStore>();
    if (store is null) return Results.Problem("Asset registry requires SQL persistence.", statusCode: 501);
    if (string.IsNullOrWhiteSpace(dto.SiteCode) || dto.Count < 1)
        return Results.BadRequest(new { error = "siteCode and count (>=1) required." });
    var qrs = await store.RegisterTraysAsync(dto.SiteCode, dto.Count, ct);
    return Results.Ok(new { registered = qrs.Count, trayQrs = qrs });
})
.WithTags("Assets").WithName("RegisterAssets").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Ingestion ----------
app.MapPost("/events/scan", async (ScanEventDto dto, IngestionService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.ClientEventId) || string.IsNullOrWhiteSpace(dto.DeviceId))
        return Results.BadRequest(new { error = "clientEventId and deviceId are required." });
    var result = await svc.IngestAsync(dto.ToInput(), ct);
    return Results.Ok(result);
})
.WithTags("Events").WithName("IngestScan")
.Produces<IngestResult>(200).ProducesProblem(400).RequireAuthorizationWhenConfigured(authOptions);

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
.WithTags("Events").WithName("IngestScanBatch").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Manifest sync (edge module pulls expected tray manifests) ----------
app.MapGet("/manifests", async (DateTimeOffset? since, IManifestStore store, CancellationToken ct) =>
{
    var cutoff = since ?? DateTimeOffset.MinValue;
    var manifests = await store.GetChangedSinceAsync(cutoff, ct);
    return Results.Ok(new { since = cutoff, count = manifests.Count, manifests });
})
.WithTags("Manifests").WithName("GetManifestsDelta").AllowDevicesWhenConfigured(authOptions);

app.MapPut("/manifests", async (ManifestDto dto, IManifestStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TrayQr))
        return Results.BadRequest(new { error = "trayQr is required." });
    await store.UpsertAsync(dto.ToManifest(), ct);
    return Results.Ok(new { dto.TrayQr, dto.ExpectedCartonCount });
})
.WithTags("Manifests").WithName("UpsertManifest").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Read models ----------
app.MapGet("/shipment-lines/{orderLineId:long}/state", async (long orderLineId, IShipmentStateStore store, CancellationToken ct) =>
{
    var rec = await store.GetOrCreateAsync(orderLineId, ct);
    return Results.Ok(rec);
})
.WithTags("State").WithName("GetLineState").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/exceptions/open", async (IExceptionStore store, CancellationToken ct) =>
    Results.Ok(await store.GetOpenAsync(ct)))
.WithTags("Exceptions").WithName("GetOpenExceptions").RequireAuthorizationWhenConfigured(authOptions);

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
.WithTags("Trips").WithName("CreateTrip").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/trips/{tripNumber}", async (string tripNumber, TripService svc, CancellationToken ct) =>
{
    var trip = await svc.GetAsync(tripNumber, ct);
    return trip is null ? Results.NotFound() : Results.Ok(trip);
})
.WithTags("Trips").WithName("GetTrip").RequireAuthorizationWhenConfigured(authOptions);

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
.WithTags("Trips").WithName("LoadTrayScan").RequireAuthorizationWhenConfigured(authOptions);

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
.WithTags("Trips").WithName("Telemetry").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Receiving (Module 8) ----------
app.MapPut("/asn", async (AsnDto dto, IAsnStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TrayQr) || string.IsNullOrWhiteSpace(dto.StoreCode))
        return Results.BadRequest(new { error = "trayQr and storeCode are required." });
    await store.UpsertAsync(dto.ToAsn(), ct);
    return Results.Ok(new { dto.TrayQr, dto.StoreCode, expected = dto.ExpectedCartons.Count });
})
.WithTags("Receiving").WithName("UpsertAsn").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/receiving/start", async (StartReceivingDto dto, ReceivingService svc, IReceivingSessionStore sessions, CancellationToken ct) =>
{
    var session = await svc.StartAsync(dto.TrayQr, dto.StoreCode, ct);
    if (session is null) return Results.NotFound(new { error = "No ASN for this tray/store." });
    var id = await sessions.AddAsync(session, ct);
    return Results.Ok(new { sessionId = id, session.Asn.TrayQr, session.Asn.StoreCode,
        expected = session.ExpectedCount,
        expectedCartons = session.Asn.ExpectedCartons });
})
.WithTags("Receiving").WithName("StartReceiving").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/receiving/{sessionId}/scan", async (string sessionId, ScanCartonDto dto, ReceivingService svc, IReceivingSessionStore sessions, CancellationToken ct) =>
{
    var session = await sessions.GetAsync(sessionId, ct);
    if (session is null) return Results.NotFound(new { error = "Unknown session." });
    var result = await svc.ScanAsync(session, dto.Payload, ct);
    // The scan mutates the session in place, so it has to be written back before the
    // next request rehydrates it from SQL.
    await sessions.SaveAsync(sessionId, session, ct);
    return Results.Ok(result);
})
.WithTags("Receiving").WithName("ReceivingScan").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/receiving/{sessionId}/damaged", async (string sessionId, DamagedDto dto, ReceivingService svc, IReceivingSessionStore sessions, CancellationToken ct) =>
{
    var session = await sessions.GetAsync(sessionId, ct);
    if (session is null) return Results.NotFound(new { error = "Unknown session." });
    if (string.IsNullOrWhiteSpace(dto.PhotoBlobUri))
        return Results.BadRequest(new { error = "A damage photo is required." });
    var result = svc.FlagDamaged(session, dto.Payload, dto.PhotoBlobUri);
    await sessions.SaveAsync(sessionId, session, ct);
    return Results.Ok(result);
})
.WithTags("Receiving").WithName("ReceivingDamaged").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/receiving/{sessionId}/complete", async (string sessionId, CompleteReceivingDto dto, ReceivingService svc, IReceivingSessionStore sessions, CancellationToken ct) =>
{
    var session = await sessions.GetAsync(sessionId, ct);
    if (session is null) return Results.NotFound(new { error = "Unknown session." });
    if (string.IsNullOrWhiteSpace(dto.ReceiverName))
        return Results.BadRequest(new { error = "receiverName is required for POD." });
    var summary = await svc.CompleteAsync(session, dto.DeviceId,
        new ProofOfDelivery { ReceiverName = dto.ReceiverName, SignatureBlobUri = dto.SignatureBlobUri, DeliveryPhotoBlobUri = dto.DeliveryPhotoBlobUri }, ct);
    await sessions.RemoveAsync(sessionId, ct);
    return Results.Ok(summary);
})
.WithTags("Receiving").WithName("CompleteReceiving").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/receiving/return-tray", async (ReturnTrayDto dto, ReceivingService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.TrayQr) || string.IsNullOrWhiteSpace(dto.VehicleReg))
        return Results.BadRequest(new { error = "trayQr and vehicleReg are required." });
    await svc.ReturnEmptyTrayAsync(dto.TrayQr, dto.VehicleReg, dto.DeviceId, ct);
    return Results.Ok(new { dto.TrayQr, returnedTo = dto.VehicleReg });
})
.WithTags("Receiving").WithName("ReturnTray").RequireAuthorizationWhenConfigured(authOptions);

// ---------- Exception Console (Module 12) ----------
app.MapHub<ExceptionsHub>("/hubs/exceptions").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/console/exceptions", async (string? checkpoint, string? severity, string? status, string? route, IConsoleExceptionStore store, CancellationToken ct) =>
    Results.Ok(await store.ListAsync(checkpoint, severity, status, route, ct)))
.WithTags("Console").WithName("ListConsoleExceptions").RequireAuthorizationWhenConfigured(authOptions);

app.MapGet("/console/exceptions/{id:long}", async (long id, IConsoleExceptionStore store, IEventStore events, CancellationToken ct) =>
{
    var ex = await store.GetAsync(id, ct);
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
app.MapPost("/console/exceptions/{id:long}/acknowledge", async (long id, ActionDto dto, IConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub, CancellationToken ct) =>
    await ApplyAction(id, "acknowledge", dto, store, hub, ct))
.WithTags("Console").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/console/exceptions/{id:long}/resolve", async (long id, ActionDto dto, IConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub, CancellationToken ct) =>
    await ApplyAction(id, "resolve", dto, store, hub, ct))
.WithTags("Console").RequireAuthorizationWhenConfigured(authOptions);

app.MapPost("/console/exceptions/{id:long}/escalate", async (long id, ActionDto dto, IConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub, CancellationToken ct) =>
{
    var result = await ApplyAction(id, "escalate", dto, store, hub, ct);
    // Escalate also emits a Teams post payload (posted by a Service Bus subscriber in prod).
    return result;
})
.WithTags("Console").RequireAuthorizationWhenConfigured(authOptions);

static async Task<IResult> ApplyAction(long id, string action, ActionDto dto, IConsoleExceptionStore store, Microsoft.AspNetCore.SignalR.IHubContext<ExceptionsHub> hub, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(dto.User))
        return Results.BadRequest(new { error = "user is required for audit." });
    var updated = await store.ApplyAsync(id, action, dto.User, dto.Note ?? dto.ReasonCode, ct);
    if (updated is null) return Results.NotFound();
    await hub.Clients.All.SendAsync("exceptionUpdated", updated, ct);
    return Results.Ok(updated);
}

app.Run();

public sealed record ActionDto(string User, string? ReasonCode, string? Note);

public partial class Program { }
