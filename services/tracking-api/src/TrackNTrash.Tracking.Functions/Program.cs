using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;
using TrackNTrash.Tracking.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ShipmentStateMachine>();
        services.AddSingleton(_ => new ExceptionSeverityMatrix());

        // Same store selection as the API: SQL when a connection string is present, else in-memory.
        // This makes the DockVerification pipeline persist to the same Azure SQL database.
        var sqlCs = ctx.Configuration.GetConnectionString("TrackNTrash")
                    ?? ctx.Configuration["ConnectionStrings:TrackNTrash"];
        if (!string.IsNullOrWhiteSpace(sqlCs))
        {
            services.AddSingleton<IEventStore>(new SqlEventStore(sqlCs));
            services.AddSingleton<IShipmentStateStore>(new SqlShipmentStateStore(sqlCs));
            services.AddSingleton<IExceptionStore>(new SqlExceptionStore(sqlCs));
            services.AddSingleton<IManifestStore>(new SqlManifestStore(sqlCs));
        }
        else
        {
            services.AddSingleton<IEventStore, InMemoryEventStore>();
            services.AddSingleton<IShipmentStateStore, InMemoryShipmentStateStore>();
            services.AddSingleton<IExceptionStore, InMemoryExceptionStore>();
            services.AddSingleton<IManifestStore, InMemoryManifestStore>();
        }

        var sbCs = ctx.Configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(sbCs))
            services.AddSingleton<INotificationPublisher>(new ServiceBusNotificationPublisher(sbCs,
                ctx.Configuration["ServiceBus:Topic"] ?? "exceptions"));
        else
            services.AddSingleton<INotificationPublisher, LoggingNotificationPublisher>();

        services.AddSingleton<IIngestExceptionRule, CountMismatchAtDockRule>();
        services.AddSingleton<ISweepExceptionRule, NoReceiveWithinSlaRule>();

        services.AddSingleton<IngestionService>();
        services.AddSingleton(_ => SweepOptions.Default);
        services.AddSingleton<SweepService>();
    })
    .Build();

host.Run();
