using TrackNTrash.AssetApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cs = builder.Configuration.GetConnectionString("TrackNTrash");
if (!string.IsNullOrWhiteSpace(cs))
{
    builder.Services.AddSingleton<IAssetRepository>(new SqlAssetRepository(cs));
    builder.Services.AddHostedService<NightlyRecomputeService>();   // nightly stored-proc run
}
else
{
    builder.Services.AddSingleton<IAssetRepository, DemoAssetRepository>();   // runs without a DB
}

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "TrackNTrash.AssetApi" }))
   .WithTags("System");

app.MapGet("/assets/{trayQr}/history", async (string trayQr, IAssetRepository repo, CancellationToken ct) =>
    Results.Ok(await repo.GetHistoryAsync(trayQr, ct)))
   .WithTags("Assets").WithName("GetTrayHistory");

app.MapGet("/assets/summary", async (IAssetRepository repo, CancellationToken ct) =>
    Results.Ok(await repo.GetSummaryAsync(ct)))
   .WithTags("Assets").WithName("GetAssetSummary");

app.MapGet("/assets/exceptions", async (IAssetRepository repo, CancellationToken ct) =>
    Results.Ok(await repo.GetExceptionsAsync(ct)))
   .WithTags("Assets").WithName("GetAssetExceptions");

app.MapPost("/assets/recompute", async (IAssetRepository repo, CancellationToken ct) =>
{
    await repo.RecomputeAsync(ct);
    return Results.Ok(new { recomputed = true });
}).WithTags("Assets").WithName("RecomputeAssetMetrics");

app.Run();

public partial class Program { }
