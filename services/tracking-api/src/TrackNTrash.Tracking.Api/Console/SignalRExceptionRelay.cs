using Microsoft.AspNetCore.SignalR;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;

namespace TrackNTrash.Tracking.Api.Console;

/// <summary>
/// INotificationPublisher decorator: forwards to the inner publisher (Service Bus / logging),
/// records the exception in the console store, and pushes it live to connected consoles via SignalR.
/// </summary>
public sealed class SignalRExceptionRelay : INotificationPublisher
{
    private readonly INotificationPublisher _inner;
    private readonly ConsoleExceptionStore _store;
    private readonly IHubContext<ExceptionsHub> _hub;

    public SignalRExceptionRelay(INotificationPublisher inner, ConsoleExceptionStore store, IHubContext<ExceptionsHub> hub)
    {
        _inner = inner;
        _store = store;
        _hub = hub;
    }

    public async Task PublishAsync(TrackException exception, CancellationToken ct = default)
    {
        await _inner.PublishAsync(exception, ct);
        var record = _store.Add(exception);
        await _hub.Clients.All.SendAsync("exceptionRaised", record, ct);
    }
}
