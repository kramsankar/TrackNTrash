# Offline Mode & Conflict Handling — Pick App

Warehouses have dead zones. The app must let a picker complete a tray with no connectivity and reconcile cleanly on reconnect.

## Local queue model

Every scan appends to the in-app collection `colScanQueue` (and, optionally, a persisted `tnt_scanbuffer` Dataverse row so the queue survives an app kill). Each entry carries a **`clientEventId` (GUID)** — the idempotency key the backend dedupes on.

```
scan → Collect(colScanQueue, { clientEventId: GUID(), … })
complete → TrayBuildComplete.Run(…, JSON(colScanQueue))
```

On `status:"ok"` the queue is cleared. On failure it is **retained** and retried.

## Connectivity handling

- `Set(gOnline, Connection.Connected)` on start and on a timer.
- If offline at completion, the app still navigates to Complete and shows `⏳ N queued`.
- A background **sync**: a timer (or the app's `OnVisible`) re-invokes the flow when `Connection.Connected` becomes true and `CountRows(colScanQueue) > 0`.

```powerapps
// timerSync.OnTimerEnd
If(Connection.Connected && CountRows(colScanQueue) > 0,
   Set(gFlowResult, TrayBuildComplete.Run(gDeviceId, gUser, gOrder.tnt_ordernumber,
                                          gTray.tnt_trayqr, JSON(colScanQueue)));
   If(gFlowResult.status = "ok", Clear(colScanQueue))
)
```

## Idempotency (server side, Module 6)

`ScanEvent` has `UNIQUE (DeviceId, ClientEventId)`. The Function performs an idempotent upsert-or-ignore:
- New `(deviceId, clientEventId)` → insert.
- Seen before → **no-op**, still return 2xx so the device clears its queue.

This makes "resend the whole queue" always safe, however many times it fires.

## Cross-device conflict: same carton scanned twice

Two pickers can scan the same physical carton onto different trays.

**Detection** — the carton's active tray binding is guarded by the Module 1 filtered unique index `UX_TrayContent_ActiveCarton` (a carton is bound to ≤1 tray at a time). The Function, when processing a `CartonScan`/`TrayBuildComplete`, attempts to create the active binding:

| Outcome | Server behavior | Device feedback |
|---------|-----------------|-----------------|
| Binding free | Bind carton → tray, advance line | `✅ synced` |
| Already bound to **this** tray/device (replay) | No-op (idempotent) | `✅ synced` |
| Already bound to a **different** tray/device | Reject that carton, write an `Exception` (`UnknownCarton`/`WrongTray` — duplicate custody) referencing both trays | Flow returns a per-event `rejected[]`; app shows red "Carton already on TRAY-… — remove and rescan" |

**Resolution** — the app reads `gFlowResult.rejected` (array of `{clientEventId, reason, conflictTrayQr}`), removes those serials from `colScannedSerials`, decrements the local tally, and re-enables the affected line so the picker physically resolves it.

```powerapps
ForAll(gFlowResult.rejected,
    Remove(colScannedSerials, LookUp(colScannedSerials, serial = ThisRecord.serial));
    Notify("Conflict: " & ThisRecord.reason & " (" & ThisRecord.conflictTrayQr & ")",
           NotificationType.Error)
)
```

## What the picker sees

- **Fully offline tray build** — works end to end; Complete shows `⏳ queued`.
- **Reconnect** — auto-sync flips to `✅ Synced`; any conflicts surface as red line items to re-scan.
- **App killed mid-build** — if `tnt_scanbuffer` persistence is enabled, reopening restores the queue; otherwise the picker restarts the tray (cartons not yet server-bound are still free).

## Recommendation

Enable the persisted `tnt_scanbuffer` for production. The pure-collection queue is simpler but loses in-progress scans on a crash; the Dataverse-backed queue is durable and lets the server own conflict truth.
