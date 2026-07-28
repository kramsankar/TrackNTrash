using TrackNTrash.Tracking.Core;
using Xunit;

namespace TrackNTrash.Tracking.Tests;

public class StateMachineTests
{
    private readonly ShipmentStateMachine _sm = new();

    // ---------------- Legal transitions (every edge in the table) ----------------

    public static IEnumerable<object[]> LegalTransitions() => new List<object[]>
    {
        new object[] { ShipmentState.Ordered,   TransitionTrigger.TrayBuildComplete,    ShipmentState.Picked      },
        new object[] { ShipmentState.Picked,    TransitionTrigger.DockVerificationPass, ShipmentState.Staged      },
        new object[] { ShipmentState.Staged,    TransitionTrigger.TripLoadScan,         ShipmentState.Loaded      },
        new object[] { ShipmentState.Loaded,    TransitionTrigger.TelemetryDepart,      ShipmentState.InTransit   },
        new object[] { ShipmentState.InTransit, TransitionTrigger.ReceivingComplete,    ShipmentState.Received    },
        new object[] { ShipmentState.Picked,    TransitionTrigger.ReconcileShort,       ShipmentState.ShortShipped},
        new object[] { ShipmentState.Staged,    TransitionTrigger.DamageFlag,           ShipmentState.Damaged     },
        new object[] { ShipmentState.Loaded,    TransitionTrigger.WrongTripScan,        ShipmentState.WrongStore  },
        new object[] { ShipmentState.InTransit, TransitionTrigger.ReceiveSlaBreach,     ShipmentState.Lost        },
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public void Legal_transition_advances_to_expected_state(ShipmentState from, TransitionTrigger trigger, ShipmentState to)
    {
        var r = _sm.Evaluate(from, trigger);
        Assert.True(r.IsLegal);
        Assert.Equal(to, r.ToState);
    }

    // ---------------- Illegal transitions (exhaustive) ----------------
    // Every (state, trigger) pair NOT in the legal set must be reported illegal.

    public static IEnumerable<object[]> AllStateTriggerPairs()
    {
        foreach (ShipmentState s in Enum.GetValues<ShipmentState>())
            foreach (TransitionTrigger t in Enum.GetValues<TransitionTrigger>())
                yield return new object[] { s, t };
    }

    private static readonly HashSet<(ShipmentState, TransitionTrigger)> Legal = new()
    {
        (ShipmentState.Ordered,   TransitionTrigger.TrayBuildComplete),
        (ShipmentState.Picked,    TransitionTrigger.DockVerificationPass),
        (ShipmentState.Staged,    TransitionTrigger.TripLoadScan),
        (ShipmentState.Loaded,    TransitionTrigger.TelemetryDepart),
        (ShipmentState.InTransit, TransitionTrigger.ReceivingComplete),
        (ShipmentState.Picked,    TransitionTrigger.ReconcileShort),
        (ShipmentState.Staged,    TransitionTrigger.DamageFlag),
        (ShipmentState.Loaded,    TransitionTrigger.WrongTripScan),
        (ShipmentState.InTransit, TransitionTrigger.ReceiveSlaBreach),
    };

    [Theory]
    [MemberData(nameof(AllStateTriggerPairs))]
    public void Every_pair_is_legal_iff_in_the_table(ShipmentState from, TransitionTrigger trigger)
    {
        var r = _sm.Evaluate(from, trigger);
        bool expectLegal = Legal.Contains((from, trigger));
        Assert.Equal(expectLegal, r.IsLegal);
    }

    [Fact]
    public void Illegal_transition_never_throws_and_reports_intended_target()
    {
        // Loaded without Staged: jump from Ordered via TripLoadScan is illegal.
        var r = _sm.Evaluate(ShipmentState.Ordered, TransitionTrigger.TripLoadScan);
        Assert.False(r.IsLegal);
        Assert.Equal(ShipmentState.Ordered, r.FromState);
        Assert.Equal(ShipmentState.Loaded, r.ToState); // canonical intended target
    }

    [Fact]
    public void Terminal_states_accept_no_further_triggers()
    {
        foreach (var terminal in new[] { ShipmentState.Received, ShipmentState.ShortShipped,
                     ShipmentState.Damaged, ShipmentState.WrongStore, ShipmentState.Lost })
        {
            Assert.True(ShipmentStateMachine.IsTerminal(terminal));
            foreach (TransitionTrigger t in Enum.GetValues<TransitionTrigger>())
                Assert.False(_sm.Evaluate(terminal, t).IsLegal);
        }
    }

    [Fact]
    public void Full_happy_path_reaches_Received()
    {
        var s = ShipmentState.Ordered;
        foreach (var trigger in new[]
        {
            TransitionTrigger.TrayBuildComplete, TransitionTrigger.DockVerificationPass,
            TransitionTrigger.TripLoadScan, TransitionTrigger.TelemetryDepart,
            TransitionTrigger.ReceivingComplete
        })
        {
            var r = _sm.Evaluate(s, trigger);
            Assert.True(r.IsLegal);
            s = r.ToState;
        }
        Assert.Equal(ShipmentState.Received, s);
    }

    [Fact]
    public void Exception_edges_are_flagged_as_exception_states()
    {
        Assert.True(_sm.Evaluate(ShipmentState.Loaded, TransitionTrigger.WrongTripScan).IsExceptionState);
        Assert.False(_sm.Evaluate(ShipmentState.Ordered, TransitionTrigger.TrayBuildComplete).IsExceptionState);
    }
}
