# Database — Module 1

Azure SQL operational schema for TrackNTrash.

## Files (run in order)

| # | File | Purpose |
|---|------|---------|
| 1 | `schema/01_schema.sql` | Schemas, sequences, tables, append-only trigger, hot-path indexes |
| 2 | `schema/02_seed_reference.sql` | Reference data: checkpoints, states, event types (idempotent MERGE) |
| 3 | `schema/03_views.sql` | Convenience views for API / reporting |
| — | `schema/ER_diagram.md` | Mermaid ER diagram + modeling notes |

`migrations/` is reserved for forward-only change scripts once the schema is deployed.

## Deploy

**sqlcmd:**
```bash
sqlcmd -S <server>.database.windows.net -d TrackNTrash -G -i schema/01_schema.sql -i schema/02_seed_reference.sql -i schema/03_views.sql
```

**Azure Data Studio / SSMS:** open each file against the target DB and run in the order above.

## Schemas

- `ops` — operational tables (orders, cartons, trays, trips, events, state, exceptions)
- `ref` — reference/lookup data and sequences
- `mart` — analytics star schema (added in Module 11)

## Design highlights

- **`ops.ScanEvent` is append-only.** `ops.TR_ScanEvent_NoMutate` blocks UPDATE/DELETE. Treat it as the immutable source of truth.
- **`ops.ShipmentLineState`** is a derived projection (1:1 per order line) with `LastEventId` for audit; `ops.ShipmentLineStateHistory` records every transition (legal and illegal).
- **Custody chain** via `ops.TrayCustody` (append per movement); current custodian denormalized on `ops.Tray` and exposed by `ops.vTrayCurrentCustody`.
- **GS1**: GTIN-14 (digits only), Serial ≤20 alphanumeric, SSCC-18 (filtered-unique when present).
- **Idempotency** on `(DeviceId, ClientEventId)`.
- **Sequences** `ref.SsccSerialReference`, `ref.CartonSerialReference`, `ref.TraySequence` are consumed by the Label API (Module 2).

## Hot-path indexes

- Carton lookup by `QrPayload`, by `Sscc` (filtered), by `OrderLineId`.
- Events by `OrderLineId` (timeline), by `ScannedQr`, by `(CheckpointId, IngestedUtc)`.
- Open exceptions by `(CheckpointId, Severity, CreatedUtc)` filtered to non-resolved.
