namespace TrackNTrash.Tracking.Core;

/// <summary>Result of evaluating a trigger against a current state.</summary>
public sealed record TransitionResult(
    bool IsLegal,
    ShipmentState FromState,
    ShipmentState ToState,
    TransitionTrigger Trigger)
{
    /// <summary>True when the trigger caused a move into a terminal exception state.</summary>
    public bool IsExceptionState => ToState is ShipmentState.ShortShipped
        or ShipmentState.Damaged or ShipmentState.WrongStore or ShipmentState.Lost;
}

/// <summary>
/// Explicit, auditable transition table for the shipment-line state machine.
/// Deliberately a dictionary (not scattered if/else) so every legal edge is enumerable
/// and unit-testable. Illegal (missing) edges are surfaced, not thrown — the caller writes
/// the event regardless and raises an IllegalTransition exception.
/// </summary>
public sealed class ShipmentStateMachine
{
    // (currentState, trigger) -> nextState. Only legal edges are present.
    private static readonly IReadOnlyDictionary<(ShipmentState, TransitionTrigger), ShipmentState> Table
        = new Dictionary<(ShipmentState, TransitionTrigger), ShipmentState>
        {
            // ---- happy path ----
            { (ShipmentState.Ordered,   TransitionTrigger.TrayBuildComplete),    ShipmentState.Picked    },
            { (ShipmentState.Picked,    TransitionTrigger.DockVerificationPass), ShipmentState.Staged    },
            { (ShipmentState.Staged,    TransitionTrigger.TripLoadScan),         ShipmentState.Loaded    },
            { (ShipmentState.Loaded,    TransitionTrigger.TelemetryDepart),      ShipmentState.InTransit },
            { (ShipmentState.InTransit, TransitionTrigger.ReceivingComplete),    ShipmentState.Received  },

            // ---- terminal exception edges ----
            { (ShipmentState.Picked,    TransitionTrigger.ReconcileShort),  ShipmentState.ShortShipped },
            { (ShipmentState.Staged,    TransitionTrigger.DamageFlag),      ShipmentState.Damaged      },
            { (ShipmentState.Loaded,    TransitionTrigger.WrongTripScan),   ShipmentState.WrongStore   },
            { (ShipmentState.InTransit, TransitionTrigger.ReceiveSlaBreach),ShipmentState.Lost         },
        };

    /// <summary>Canonical target a trigger normally leads to — used to describe illegal jumps.</summary>
    private static readonly IReadOnlyDictionary<TransitionTrigger, ShipmentState> CanonicalTarget
        = new Dictionary<TransitionTrigger, ShipmentState>
        {
            { TransitionTrigger.TrayBuildComplete,    ShipmentState.Picked      },
            { TransitionTrigger.DockVerificationPass, ShipmentState.Staged      },
            { TransitionTrigger.TripLoadScan,         ShipmentState.Loaded      },
            { TransitionTrigger.TelemetryDepart,      ShipmentState.InTransit   },
            { TransitionTrigger.ReceivingComplete,    ShipmentState.Received    },
            { TransitionTrigger.ReconcileShort,       ShipmentState.ShortShipped},
            { TransitionTrigger.DamageFlag,           ShipmentState.Damaged     },
            { TransitionTrigger.WrongTripScan,        ShipmentState.WrongStore  },
            { TransitionTrigger.ReceiveSlaBreach,     ShipmentState.Lost        },
        };

    /// <summary>All legal edges — exposed for tests and documentation.</summary>
    public static IReadOnlyCollection<(ShipmentState From, TransitionTrigger Trigger, ShipmentState To)> LegalEdges
        => Table.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value)).ToList();

    public static bool IsTerminal(ShipmentState state) => state is
        ShipmentState.Received or ShipmentState.ShortShipped or
        ShipmentState.Damaged or ShipmentState.WrongStore or ShipmentState.Lost;

    /// <summary>
    /// Evaluate a trigger against the current state. Never throws: an unknown edge returns
    /// <c>IsLegal = false</c> with the canonical target of the trigger as the intended <c>ToState</c>.
    /// </summary>
    public TransitionResult Evaluate(ShipmentState current, TransitionTrigger trigger)
    {
        if (Table.TryGetValue((current, trigger), out var next))
            return new TransitionResult(true, current, next, trigger);

        var intended = CanonicalTarget.TryGetValue(trigger, out var t) ? t : current;
        return new TransitionResult(false, current, intended, trigger);
    }
}
