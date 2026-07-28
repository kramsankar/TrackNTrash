using Microsoft.Extensions.Logging.Abstractions;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;
using Xunit;

namespace TrackNTrash.Tracking.Tests;

public class IngestionTests
{
    private static (IngestionService svc, InMemoryExceptionStore exc, InMemoryShipmentStateStore states, InMemoryManifestStore manifests) NewService(
        IEnumerable<IIngestExceptionRule>? rules = null)
    {
        var events = new InMemoryEventStore();
        var states = new InMemoryShipmentStateStore();
        var exc = new InMemoryExceptionStore();
        var manifests = new InMemoryManifestStore();
        var notifier = new LoggingNotificationPublisher(NullLogger<LoggingNotificationPublisher>.Instance);
        var svc = new IngestionService(events, states, exc, manifests, notifier,
            new ShipmentStateMachine(), new ExceptionSeverityMatrix(),
            rules ?? new IIngestExceptionRule[] { new CountMismatchAtDockRule() },
            NullLogger<IngestionService>.Instance);
        return (svc, exc, states, manifests);
    }

    [Fact]
    public async Task Duplicate_event_is_idempotent()
    {
        var (svc, _, _, _) = NewService();
        var e = new ScanEventInput
        {
            ClientEventId = "c1", DeviceId = "dev1", EventType = "TrayBuildComplete", OrderLineId = 100
        };

        var r1 = await svc.IngestAsync(e);
        var r2 = await svc.IngestAsync(e);

        Assert.False(r1.Duplicate);
        Assert.True(r2.Duplicate);
        Assert.Equal(r1.ScanEventId, r2.ScanEventId);
    }

    [Fact]
    public async Task Legal_event_advances_state()
    {
        var (svc, _, states, _) = NewService();
        var r = await svc.IngestAsync(new ScanEventInput
        {
            ClientEventId = "c1", DeviceId = "dev1", EventType = "TrayBuildComplete", OrderLineId = 100
        });

        Assert.True(r.TransitionLegal);
        Assert.Equal(ShipmentState.Picked, r.NewState);
        var state = await states.GetOrCreateAsync(100);
        Assert.Equal(ShipmentState.Picked, state.CurrentState);
    }

    [Fact]
    public async Task Illegal_transition_writes_event_but_raises_exception_and_keeps_state()
    {
        var (svc, exc, states, _) = NewService();
        // ReceivingComplete while still Ordered -> illegal jump
        var r = await svc.IngestAsync(new ScanEventInput
        {
            ClientEventId = "c1", DeviceId = "dev1", EventType = "ReceivingComplete", OrderLineId = 200
        });

        Assert.True(r.Accepted);                 // event still written
        Assert.False(r.TransitionLegal);
        Assert.Contains(r.Exceptions, x => x.Type == ExceptionType.IllegalTransition);

        var state = await states.GetOrCreateAsync(200);
        Assert.Equal(ShipmentState.Ordered, state.CurrentState);   // unchanged
        Assert.NotEmpty(await exc.GetOpenAsync());
    }

    [Fact]
    public async Task Dock_non_pass_verdict_raises_count_mismatch()
    {
        var (svc, exc, _, manifests) = NewService();
        await manifests.UpsertAsync(new TrayManifest { TrayQr = "TRAY-LDN1-000001", ExpectedCartonCount = 5 });

        var r = await svc.IngestAsync(new ScanEventInput
        {
            ClientEventId = "c1", DeviceId = "edge-dock", EventType = "DockVerification",
            TrayQr = "TRAY-LDN1-000001", Verdict = "COUNT_MISMATCH",
            MetaJson = "{\"frameRef\":\"exceptions/frame-1.jpg\"}"
        });

        var ex = Assert.Single(r.Exceptions);
        Assert.Equal(ExceptionType.CountMismatch, ex.Type);
        Assert.Equal("exceptions/frame-1.jpg", ex.FrameBlobUri);
        Assert.Contains("expected 5", ex.Detail);
    }

    [Fact]
    public async Task Dock_pass_verdict_raises_no_exception()
    {
        var (svc, _, _, _) = NewService();
        var r = await svc.IngestAsync(new ScanEventInput
        {
            ClientEventId = "c1", DeviceId = "edge-dock", EventType = "DockVerification",
            TrayQr = "TRAY-LDN1-000001", Verdict = "PASS"   // no order line -> isolate the rule
        });
        Assert.Empty(r.Exceptions);
    }
}
