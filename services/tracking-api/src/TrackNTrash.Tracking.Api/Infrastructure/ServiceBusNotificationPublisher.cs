using System.Text.Json;
using Azure.Messaging.ServiceBus;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;

namespace TrackNTrash.Tracking.Api.Infrastructure;

/// <summary>
/// Publishes exceptions to an Azure Service Bus topic. Subscribers (Teams webhook, email,
/// Power Automate) attach their own subscriptions with filters on Severity/Type.
/// </summary>
public sealed class ServiceBusNotificationPublisher : INotificationPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusNotificationPublisher(string connectionString, string topicName)
    {
        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(topicName);
    }

    public async Task PublishAsync(TrackException exception, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(exception);
        var msg = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            Subject = exception.Type.ToString(),
            ApplicationProperties =
            {
                ["severity"] = exception.Severity.ToString(),
                ["type"] = exception.Type.ToString(),
                ["checkpoint"] = exception.Checkpoint ?? ""
            }
        };
        await _sender.SendMessageAsync(msg, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
