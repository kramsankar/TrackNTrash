using TrackNTrash.LabelApi.Models;
using TrackNTrash.LabelApi.Options;
using TrackNTrash.LabelApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Options ----
builder.Services.Configure<LabelOptions>(builder.Configuration.GetSection(LabelOptions.SectionName));
var labelOptions = builder.Configuration.GetSection(LabelOptions.SectionName).Get<LabelOptions>() ?? new LabelOptions();

// ---- DI ----
builder.Services.AddSingleton(labelOptions.Sscc);
builder.Services.AddSingleton(labelOptions.Zpl);
builder.Services.AddSingleton<QrImageService>();
builder.Services.AddSingleton<ZplRenderer>();

// Serial provider: SQL sequences in prod, in-memory for dev/test.
if (string.Equals(labelOptions.SerialProvider, "Sql", StringComparison.OrdinalIgnoreCase))
{
    var cs = labelOptions.ConnectionString
             ?? throw new InvalidOperationException("Label:ConnectionString required when SerialProvider=Sql.");
    builder.Services.AddSingleton<ISerialNumberProvider>(new SqlSerialNumberProvider(cs));
}
else
{
    builder.Services.AddSingleton<ISerialNumberProvider, InMemorySerialNumberProvider>();
}

builder.Services.AddSingleton<LabelService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "TrackNTrash.LabelApi" }))
   .WithName("Health").WithTags("System");

// ---- POST /labels/carton ----
app.MapPost("/labels/carton", async (CartonLabelRequest req, LabelService svc, CancellationToken ct) =>
{
    try
    {
        var labels = await svc.CreateCartonLabelsAsync(req, ct);
        return Results.Ok(labels);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
})
.WithName("CreateCartonLabels").WithTags("Labels")
.Produces<IReadOnlyList<CartonLabel>>(200).ProducesProblem(400);

// ---- POST /labels/sscc ----
app.MapPost("/labels/sscc", async (SsccLabelRequest req, LabelService svc, CancellationToken ct) =>
{
    try
    {
        var labels = await svc.CreateSsccLabelsAsync(req, ct);
        return Results.Ok(labels);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
})
.WithName("CreateSsccLabels").WithTags("Labels")
.Produces<IReadOnlyList<SsccLabel>>(200).ProducesProblem(400);

// ---- POST /labels/tray ----
app.MapPost("/labels/tray", async (TrayLabelRequest req, LabelService svc, CancellationToken ct) =>
{
    try
    {
        var labels = await svc.CreateTrayLabelsAsync(req, ct);
        return Results.Ok(labels);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
})
.WithName("CreateTrayLabels").WithTags("Labels")
.Produces<IReadOnlyList<TrayLabel>>(200).ProducesProblem(400);

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
