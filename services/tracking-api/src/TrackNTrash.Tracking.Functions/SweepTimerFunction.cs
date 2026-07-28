using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TrackNTrash.Tracking.Core.Services;

namespace TrackNTrash.Tracking.Functions;

/// <summary>Periodic sweep for time-based exception rules (NoReceiveWithinSla, TrayDwell…).</summary>
public sealed class SweepTimerFunction
{
    private readonly SweepService _sweep;
    private readonly ILogger<SweepTimerFunction> _log;

    public SweepTimerFunction(SweepService sweep, ILogger<SweepTimerFunction> log)
    {
        _sweep = sweep;
        _log = log;
    }

    // Every 15 minutes. Adjust via the CRON expression / app setting.
    [Function("ExceptionSweep")]
    public async Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer, FunctionContext context)
    {
        var raised = await _sweep.RunAsync(DateTimeOffset.UtcNow, context.CancellationToken);
        _log.LogInformation("Sweep completed: {Count} exception(s) raised", raised.Count);
    }
}
