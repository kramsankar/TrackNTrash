using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ShipmentStateMachine>();
        services.AddSingleton(_ => new ExceptionSeverityMatrix());

        // In-memory here for illustration; wire SQL/Service Bus stores from configuration in prod.
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddSingleton<IShipmentStateStore, InMemoryShipmentStateStore>();
        services.AddSingleton<IExceptionStore, InMemoryExceptionStore>();
        services.AddSingleton<IManifestStore, InMemoryManifestStore>();
        services.AddSingleton<INotificationPublisher, LoggingNotificationPublisher>();

        services.AddSingleton<IIngestExceptionRule, CountMismatchAtDockRule>();
        services.AddSingleton<ISweepExceptionRule, NoReceiveWithinSlaRule>();

        services.AddSingleton<IngestionService>();
        services.AddSingleton(_ => SweepOptions.Default);
        services.AddSingleton<SweepService>();
    })
    .Build();

host.Run();
