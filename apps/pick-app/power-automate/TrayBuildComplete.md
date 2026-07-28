# Power Automate Flow — `TrayBuildComplete`

Instant (button-triggered from Power Apps) cloud flow. Receives the queued scan events from the pick app and relays them to the Module 6 Azure Function, which writes the append-only `ScanEvent` rows and runs the state machine.

## Signature (Power Apps → flow inputs)

| Input | Type | Notes |
|-------|------|-------|
| `deviceId` | string | Device identifier |
| `userId` | string | Picker UPN |
| `orderNumber` | string | Sales order |
| `trayQr` | string | Bound tray |
| `eventsJson` | string | JSON array of queued scan events (TrayBind, CartonScan, TrayBuildComplete) |

Returns `{ status: "ok" | "error", accepted: <int>, message: <string> }`.

## Steps

1. **Trigger** — *PowerApps (V2)* with the five inputs above.
2. **Parse JSON** — schema = the scan-event array (see `flow-definition.json`).
3. **Select** — project each queued event into the Function's `/events/scan` contract:
   ```
   {
     "clientEventId": item()?['clientEventId'],
     "eventType":     item()?['eventType'],
     "checkpoint":    "PickTrayBuild",
     "deviceId":      triggerBody()?['deviceId'],
     "userId":        triggerBody()?['userId'],
     "scannedQr":     item()?['scannedQr'],
     "orderNumber":   triggerBody()?['orderNumber'],
     "orderLineRef":  item()?['orderLine'],
     "trayQr":        triggerBody()?['trayQr'],
     "eventUtc":      item()?['eventUtc'],
     "meta":          item()?['cartons']
   }
   ```
4. **HTTP → Azure Function** — `POST {FunctionBaseUrl}/api/events/scan/batch`
   - Header `x-functions-key` from environment variable / Key Vault.
   - Body = the projected array.
   - The Function dedupes on `(deviceId, clientEventId)`, so re-runs are safe.
5. **Condition** — on 2xx → `Respond {status:"ok", accepted: length(...)}`; else → `Respond {status:"error"}` and post to the dead-letter (see below).
6. **Terminate** — `Succeeded`.

## Resilience

- **Retry policy** on the HTTP action: exponential, 4 attempts.
- **Failure path** — write the raw payload to a Dataverse `tnt_scanbuffer` row with `tnt_syncstatus = Conflict` (or a Service Bus dead-letter) and notify the integration owner. The app keeps its local queue until it sees `status:"ok"`.
- **Idempotency** — every event carries `clientEventId`; safe to resend.

## Environment variables

| Name | Purpose |
|------|---------|
| `tnt_FunctionBaseUrl` | Module 6 Function App base URL |
| `tnt_FunctionKey` | Function key (Key Vault reference) |

See `flow-definition.json` for an importable skeleton (Solution / `pac`). Replace connection references before import.
