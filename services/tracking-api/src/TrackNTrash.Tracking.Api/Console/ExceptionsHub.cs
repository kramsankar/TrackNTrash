using Microsoft.AspNetCore.SignalR;

namespace TrackNTrash.Tracking.Api.Console;

/// <summary>
/// SignalR hub the exception console connects to. The server pushes:
///   * "exceptionRaised"  — a new exception arrived
///   * "exceptionUpdated" — status changed (acknowledge / resolve / escalate)
/// Clients don't invoke server methods; this is a one-way push surface.
/// In production, protect with [Authorize] (Entra ID).
/// </summary>
public sealed class ExceptionsHub : Hub
{
}
