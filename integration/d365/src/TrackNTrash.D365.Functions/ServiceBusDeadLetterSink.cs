using Microsoft.Extensions.Logging;
using TrackNTrash.D365.Integration;

namespace TrackNTrash.D365.Functions;

/// <summary>
/// Dead-letters a failed F&O post for the Power Automate repair flow to pick up.
/// Writes a structured record; wire a Service Bus sender (or a Dataverse table) as the sink.
/// A Power Automate flow subscribes to the repair queue, notifies the integration owner, and
/// offers a one-click re-post that re-enqueues the original event id.
/// </summary>
public sealed class ServiceBusDeadLetterSink : IDeadLetterSink
{
    private readonly ILogger<ServiceBusDeadLetterSink> _log;
    public ServiceBusDeadLetterSink(ILogger<ServiceBusDeadLetterSink> log) => _log = log;

    public Task DeadLetterAsync(string channel, string eventId, string payloadJson, string error, CancellationToken ct = default)
    {
        // TODO(prod): send to the 'd365-repair' Service Bus queue / Dataverse repair table.
        _log.LogError("DEAD-LETTER [{Channel}] event {EventId}: {Error} | payload={Payload}",
            channel, eventId, error, payloadJson);
        return Task.CompletedTask;
    }
}
