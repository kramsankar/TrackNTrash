using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrackNTrash.D365.Functions;
using TrackNTrash.D365.Integration;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        var trackingBaseUrl = ctx.Configuration["TrackingApiBaseUrl"] ?? "http://localhost:5090";
        var d365BaseUrl = ctx.Configuration["D365BaseUrl"] ?? "https://example.operations.dynamics.com";

        services.AddHttpClient<ITrackingIntakeClient, HttpTrackingIntakeClient>(c =>
            c.BaseAddress = new Uri(trackingBaseUrl));
        services.AddHttpClient<ID365Client, ODataD365Client>(c =>
            c.BaseAddress = new Uri(d365BaseUrl));   // add OAuth bearer handler in prod

        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();  // SQL-backed in prod
        services.AddSingleton<IDeadLetterSink, ServiceBusDeadLetterSink>();
        services.AddSingleton(_ => new PostingOptions { ShortageHandling = ShortageHandling.CreateCase });
        services.AddSingleton<D365PostingService>();
    })
    .Build();

host.Run();
