namespace TrackNTrash.Tracking.Core;

/// <summary>
/// Maps inbound event types (+ verdicts) to state-machine triggers.
/// Not every event maps to a transition (e.g. TrayBind, CartonScan are sub-events of a build);
/// those return null and are logged without advancing the line.
/// </summary>
public static class EventTriggerMap
{
    public static TransitionTrigger? Resolve(ScanEventInput e)
    {
        return e.EventType switch
        {
            "TrayBuildComplete" => TransitionTrigger.TrayBuildComplete,

            // Dock verification only advances on PASS; non-PASS raises an exception (rule), no transition.
            "DockVerification" => string.Equals(e.Verdict, "PASS", StringComparison.OrdinalIgnoreCase)
                ? TransitionTrigger.DockVerificationPass
                : null,

            "TripLoadScan"      => e.Verdict == "WRONG_TRIP"
                                    ? TransitionTrigger.WrongTripScan
                                    : TransitionTrigger.TripLoadScan,
            "TelemetryDepart"   => TransitionTrigger.TelemetryDepart,
            "ReceivingComplete" => e.Verdict == "SHORT"
                                    ? TransitionTrigger.ReconcileShort
                                    : TransitionTrigger.ReceivingComplete,

            // Sub-events (no line transition on their own)
            "TrayBind" or "CartonScan" or "StoreReceiveScan"
                or "TrayCustodyTransfer" or "EmptyTrayReturn" => null,

            _ => null
        };
    }
}
