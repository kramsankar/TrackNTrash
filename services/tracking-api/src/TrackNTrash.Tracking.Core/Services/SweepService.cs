using Microsoft.Extensions.Logging;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Stores;

namespace TrackNTrash.Tracking.Core.Services;

public sealed record SweepOptions(TimeSpan ReceiveSla, TimeSpan TrayDwellLimit)
{
    public static SweepOptions Default => new(TimeSpan.FromHours(24), TimeSpan.FromDays(3));
}

/// <summary>Runs time-based exception rules on a schedule (invoked by the Functions timer trigger).</summary>
public sealed class SweepService
{
    private readonly IShipmentStateStore _states;
    private readonly IExceptionStore _exceptions;
    private readonly INotificationPublisher _notifier;
    private readonly ExceptionSeverityMatrix _severity;
    private readonly IReadOnlyList<ISweepExceptionRule> _rules;
    private readonly SweepOptions _options;
    private readonly ILogger<SweepService> _log;

    public SweepService(
        IShipmentStateStore states,
        IExceptionStore exceptions,
        INotificationPublisher notifier,
        ExceptionSeverityMatrix severity,
        IEnumerable<ISweepExceptionRule> rules,
        SweepOptions options,
        ILogger<SweepService> log)
    {
        _states = states;
        _exceptions = exceptions;
        _notifier = notifier;
        _severity = severity;
        _rules = rules.ToList();
        _options = options;
        _log = log;
    }

    public async Task<IReadOnlyList<TrackException>> RunAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var ctx = new SweepRuleContext(now, _states, _severity, _options.ReceiveSla, _options.TrayDwellLimit);
        var raised = new List<TrackException>();

        foreach (var rule in _rules)
        {
            try
            {
                raised.AddRange(await rule.EvaluateAsync(ctx, ct));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Sweep rule {Rule} failed", rule.Name);
            }
        }

        foreach (var ex in raised)
        {
            await _exceptions.AddAsync(ex, ct);
            await _notifier.PublishAsync(ex, ct);
        }

        _log.LogInformation("Sweep at {Now} raised {Count} exception(s)", now, raised.Count);
        return raised;
    }
}
