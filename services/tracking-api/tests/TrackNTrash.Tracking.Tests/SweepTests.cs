using Microsoft.Extensions.Logging.Abstractions;
using TrackNTrash.Tracking.Core;
using TrackNTrash.Tracking.Core.Notifications;
using TrackNTrash.Tracking.Core.Rules;
using TrackNTrash.Tracking.Core.Services;
using TrackNTrash.Tracking.Core.Stores;
using Xunit;

namespace TrackNTrash.Tracking.Tests;

public class SweepTests
{
    [Fact]
    public async Task Line_past_sla_raises_no_receive_sla()
    {
        var states = new InMemoryShipmentStateStore();
        var exc = new InMemoryExceptionStore();
        var notifier = new LoggingNotificationPublisher(NullLogger<LoggingNotificationPublisher>.Instance);

        // Put a line into InTransit 30h ago; SLA is 24h.
        var machine = new ShipmentStateMachine();
        await states.ApplyTransitionAsync(500,
            machine.Evaluate(ShipmentState.Loaded, TransitionTrigger.TelemetryDepart), 1, true);
        var rec = await states.GetOrCreateAsync(500);
        rec.StateEnteredUtc = DateTimeOffset.UtcNow.AddHours(-30);

        var sweep = new SweepService(states, exc, notifier, new ExceptionSeverityMatrix(),
            new ISweepExceptionRule[] { new NoReceiveWithinSlaRule() },
            new SweepOptions(TimeSpan.FromHours(24), TimeSpan.FromDays(3)),
            NullLogger<SweepService>.Instance);

        var raised = await sweep.RunAsync(DateTimeOffset.UtcNow);

        var ex = Assert.Single(raised);
        Assert.Equal(ExceptionType.NoReceiveSla, ex.Type);
        Assert.Equal(500, ex.OrderLineId);
    }

    [Fact]
    public async Task Line_past_double_sla_is_suspected_lost()
    {
        var states = new InMemoryShipmentStateStore();
        var exc = new InMemoryExceptionStore();
        var notifier = new LoggingNotificationPublisher(NullLogger<LoggingNotificationPublisher>.Instance);
        var machine = new ShipmentStateMachine();

        await states.ApplyTransitionAsync(600,
            machine.Evaluate(ShipmentState.Loaded, TransitionTrigger.TelemetryDepart), 1, true);
        (await states.GetOrCreateAsync(600)).StateEnteredUtc = DateTimeOffset.UtcNow.AddHours(-60); // > 2x 24h

        var sweep = new SweepService(states, exc, notifier, new ExceptionSeverityMatrix(),
            new ISweepExceptionRule[] { new NoReceiveWithinSlaRule() },
            new SweepOptions(TimeSpan.FromHours(24), TimeSpan.FromDays(3)),
            NullLogger<SweepService>.Instance);

        var raised = await sweep.RunAsync(DateTimeOffset.UtcNow);
        Assert.Equal(ExceptionType.SuspectedLost, Assert.Single(raised).Type);
    }

    [Fact]
    public async Task Line_within_sla_raises_nothing()
    {
        var states = new InMemoryShipmentStateStore();
        var exc = new InMemoryExceptionStore();
        var notifier = new LoggingNotificationPublisher(NullLogger<LoggingNotificationPublisher>.Instance);
        var machine = new ShipmentStateMachine();

        await states.ApplyTransitionAsync(700,
            machine.Evaluate(ShipmentState.Loaded, TransitionTrigger.TelemetryDepart), 1, true);
        // entered just now -> within SLA

        var sweep = new SweepService(states, exc, notifier, new ExceptionSeverityMatrix(),
            new ISweepExceptionRule[] { new NoReceiveWithinSlaRule() },
            SweepOptions.Default, NullLogger<SweepService>.Instance);

        Assert.Empty(await sweep.RunAsync(DateTimeOffset.UtcNow));
    }
}
