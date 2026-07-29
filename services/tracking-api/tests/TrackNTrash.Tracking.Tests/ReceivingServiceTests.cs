using Microsoft.Extensions.Logging.Abstractions;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Receiving;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;
using Xunit;

namespace TrackNTrash.Tracking.Tests;

public class ReceivingServiceTests
{
    private static (ReceivingService svc, InMemoryAsnStore asns, InMemoryShipmentStateStore states) NewStack()
    {
        var events = new InMemoryEventStore();
        var states = new InMemoryShipmentStateStore();
        var exc = new InMemoryExceptionStore();
        var manifests = new InMemoryManifestStore();
        var notifier = new LoggingNotificationPublisher(NullLogger<LoggingNotificationPublisher>.Instance);
        var ingest = new IngestionService(events, states, exc, manifests, notifier,
            new ShipmentStateMachine(), new ExceptionSeverityMatrix(),
            Array.Empty<IIngestExceptionRule>(), new NoOpTrayProjection(),
NullLogger<IngestionService>.Instance);
        var asns = new InMemoryAsnStore();
        var svc = new ReceivingService(asns, ingest, new ExceptionSeverityMatrix(), NullLogger<ReceivingService>.Instance);
        return (svc, asns, states);
    }

    private static Asn Asn(string tray, string store, params (string payload, long line)[] cartons) => new()
    {
        TrayQr = tray, StoreCode = store,
        ExpectedCartons = cartons.Select(c => new ExpectedCarton { Payload = c.payload, OrderLineId = c.line }).ToList()
    };

    [Fact]
    public async Task Expected_carton_is_received_and_tally_updates()
    {
        var (svc, asns, _) = NewStack();
        await asns.UpsertAsync(Asn("TRAY-1", "S1", ("P1", 1), ("P2", 2)));
        var s = await svc.StartAsync("TRAY-1", "S1");

        var r = await svc.ScanAsync(s!, "P1");
        Assert.Equal(CartonReceiveOutcome.Received, r.Outcome);
        Assert.Equal(1, r.Received);
        Assert.Equal(2, r.Expected);
    }

    [Fact]
    public async Task Duplicate_scan_is_flagged()
    {
        var (svc, asns, _) = NewStack();
        await asns.UpsertAsync(Asn("TRAY-1", "S1", ("P1", 1)));
        var s = await svc.StartAsync("TRAY-1", "S1");
        await svc.ScanAsync(s!, "P1");
        var dup = await svc.ScanAsync(s!, "P1");
        Assert.Equal(CartonReceiveOutcome.Duplicate, dup.Outcome);
    }

    [Fact]
    public async Task Unexpected_carton_is_over_and_names_correct_store()
    {
        var (svc, asns, _) = NewStack();
        await asns.UpsertAsync(Asn("TRAY-1", "S1", ("P1", 1)));
        await asns.UpsertAsync(Asn("TRAY-9", "S2", ("PX", 9)));   // PX really belongs to S2
        var s = await svc.StartAsync("TRAY-1", "S1");

        var r = await svc.ScanAsync(s!, "PX");
        Assert.Equal(CartonReceiveOutcome.Over, r.Outcome);
        Assert.Equal("S2", r.CorrectStoreCode);
    }

    [Fact]
    public void Damaged_requires_a_photo()
    {
        var (svc, _, _) = NewStack();
        var s = new ReceivingSession { Asn = Asn("TRAY-1", "S1", ("P1", 1)) };
        Assert.Throws<ArgumentException>(() => svc.FlagDamaged(s, "P1", ""));
        var ok = svc.FlagDamaged(s, "P1", "blob://damage/1.jpg");
        Assert.Equal(CartonReceiveOutcome.Damaged, ok.Outcome);
    }

    [Fact]
    public async Task Complete_marks_received_lines_and_reports_shorts()
    {
        var (svc, asns, states) = NewStack();
        await asns.UpsertAsync(Asn("TRAY-1", "S1", ("P1", 100), ("P2", 101)));
        var s = await svc.StartAsync("TRAY-1", "S1");
        await svc.ScanAsync(s!, "P1");   // receive P1, leave P2 short

        // Put lines into InTransit so ReceivingComplete is a legal transition to Received.
        var machine = new ShipmentStateMachine();
        foreach (long line in new long[] { 100, 101 })
        {
            await states.ApplyTransitionAsync(line, machine.Evaluate(ShipmentState.Ordered, TransitionTrigger.TrayBuildComplete), 1, true);
            await states.ApplyTransitionAsync(line, machine.Evaluate(ShipmentState.Picked, TransitionTrigger.DockVerificationPass), 1, true);
            await states.ApplyTransitionAsync(line, machine.Evaluate(ShipmentState.Staged, TransitionTrigger.TripLoadScan), 1, true);
            await states.ApplyTransitionAsync(line, machine.Evaluate(ShipmentState.Loaded, TransitionTrigger.TelemetryDepart), 1, true);
        }

        var summary = await svc.CompleteAsync(s!, "store-dev",
            new ProofOfDelivery { ReceiverName = "Sam Store" });

        Assert.Equal(2, summary.ExpectedCount);
        Assert.Equal(1, summary.ReceivedCount);
        Assert.Contains("P2", summary.ShortPayloads);
        Assert.False(summary.Clean);
        Assert.Equal(ShipmentState.Received, (await states.GetOrCreateAsync(100)).CurrentState);
    }

    [Fact]
    public async Task Clean_delivery_reports_clean()
    {
        var (svc, asns, states) = NewStack();
        await asns.UpsertAsync(Asn("TRAY-1", "S1", ("P1", 200)));
        var s = await svc.StartAsync("TRAY-1", "S1");
        await svc.ScanAsync(s!, "P1");

        var summary = await svc.CompleteAsync(s!, "store-dev", new ProofOfDelivery { ReceiverName = "Sam" });
        Assert.True(summary.Clean);
    }
}
