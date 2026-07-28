namespace TrackNTrash.AssetApi;

/// <summary>
/// Runs the asset-metrics stored procedure nightly. Hosted-service form so this project is
/// self-contained; the equivalent Azure Function timer (TimerTrigger "0 0 2 * * *") simply
/// resolves IAssetRepository and calls RecomputeAsync — see README.
/// </summary>
public sealed class NightlyRecomputeService : BackgroundService
{
    private readonly IAssetRepository _repo;
    private readonly ILogger<NightlyRecomputeService> _log;
    private readonly TimeSpan _runAtUtc = new(2, 0, 0);   // 02:00 UTC

    public NightlyRecomputeService(IAssetRepository repo, ILogger<NightlyRecomputeService> log)
    { _repo = repo; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var next = now.Date.Add(_runAtUtc);
            if (next <= now) next = next.AddDays(1);
            var delay = next - now;
            _log.LogInformation("Next asset-metrics recompute at {Next} UTC", next);
            try { await Task.Delay(delay, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try
            {
                await _repo.RecomputeAsync(stoppingToken);
                _log.LogInformation("Asset metrics recomputed.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Asset-metrics recompute failed.");
            }
        }
    }
}
