using Microsoft.Extensions.Logging;

namespace TrackNTrash.Tracking.Core.Notifications;

/// <summary>Publishes exceptions to downstream subscribers (Service Bus topic in prod).</summary>
public interface INotificationPublisher
{
    Task PublishAsync(TrackException exception, CancellationToken ct = default);
}

/// <summary>Default publisher — logs only. Swapped for Service Bus in the Api/Functions host.</summary>
public sealed class LoggingNotificationPublisher : INotificationPublisher
{
    private readonly ILogger<LoggingNotificationPublisher> _log;
    public LoggingNotificationPublisher(ILogger<LoggingNotificationPublisher> log) => _log = log;

    public Task PublishAsync(TrackException exception, CancellationToken ct = default)
    {
        _log.LogWarning("Exception raised: {Type} ({Severity}) — {Detail} [orderLine={OrderLine} tray={Tray} trip={Trip}]",
            exception.Type, exception.Severity, exception.Detail,
            exception.OrderLineId, exception.TrayId, exception.TripId);
        return Task.CompletedTask;
    }
}
