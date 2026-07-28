import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { API_BASE } from "./api";
import type { ConsoleException } from "./types";

/**
 * Live exception feed. Fires onRaised for new exceptions and onUpdated when a status changes,
 * so the grid stays current without polling. Auto-reconnects.
 */
export function connectExceptionsHub(
  onRaised: (e: ConsoleException) => void,
  onUpdated: (e: ConsoleException) => void,
  onConnectedChange?: (connected: boolean) => void,
  tokenFactory?: () => string | undefined
): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE}/hubs/exceptions`, {
      accessTokenFactory: tokenFactory ? () => tokenFactory() ?? "" : undefined,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on("exceptionRaised", onRaised);
  connection.on("exceptionUpdated", onUpdated);
  connection.onreconnected(() => onConnectedChange?.(true));
  connection.onreconnecting(() => onConnectedChange?.(false));
  connection.onclose(() => onConnectedChange?.(false));
  connection.start()
    .then(() => onConnectedChange?.(true))
    .catch((err) => { console.error("SignalR connect failed:", err); onConnectedChange?.(false); });
  return connection;
}
