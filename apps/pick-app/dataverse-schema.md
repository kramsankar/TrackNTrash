# Dataverse Schema — Pick App

The app reads assigned pick work and master data from Dataverse and writes a local scan buffer that is flushed to Power Automate. Dataverse mirrors the operational subset needed offline; Azure SQL remains the system of record (synced via Module 6 / Module 9).

> Naming: publisher prefix `tnt` (TrackNTrash). Tables use logical names `tnt_*`.

## Tables

### `tnt_salesorder` (Sales Order)
| Column | Type | Notes |
|--------|------|-------|
| `tnt_ordernumber` | Text (primary) | e.g. `SO1001` |
| `tnt_storecode` | Text | Destination store |
| `tnt_status` | Choice | Open / Picking / Picked / Closed |
| `tnt_erpreference` | Text | D365 SO id |
| `tnt_assignedto` | Lookup (User) | Picker assignment |
| `tnt_qrvalue` | Text | Sales-order QR payload for scan match |

### `tnt_orderline` (Order Line)
| Column | Type | Notes |
|--------|------|-------|
| `tnt_name` | Text (primary) | `SO1001-1` |
| `tnt_salesorder` | Lookup → tnt_salesorder | Parent |
| `tnt_linenumber` | Whole number | |
| `tnt_gtin` | Text | GTIN-14 |
| `tnt_productdescription` | Text | |
| `tnt_expectedcartoncount` | Whole number | Target for reconciliation |
| `tnt_pickedcount` | Whole number | Running tally (rolled up on complete) |
| `tnt_state` | Choice | Ordered / Picked / … (mirror of ShipmentLineState) |

### `tnt_carton` (Expected Carton)
| Column | Type | Notes |
|--------|------|-------|
| `tnt_serial` | Text (primary) | GS1 (21) serial |
| `tnt_orderline` | Lookup → tnt_orderline | |
| `tnt_gtin` | Text | |
| `tnt_qrpayload` | Text | Full carton QR payload for exact match |
| `tnt_status` | Choice | Expected / Picked / … |

### `tnt_tray` (Tray Asset)
| Column | Type | Notes |
|--------|------|-------|
| `tnt_trayqr` | Text (primary) | `TRAY-LDN1-000001` |
| `tnt_status` | Choice | Available / InUse / InTransit / AtStore / Maintenance / Lost |
| `tnt_custodiantype` | Choice | Warehouse / Vehicle / Store |
| `tnt_custodianref` | Text | |

### `tnt_scanbuffer` (local-first scan buffer — offline queue)
Written by the app for every carton/tray scan; drained by the completion flow.
| Column | Type | Notes |
|--------|------|-------|
| `tnt_clienteventid` | Text (primary) | GUID per scan — **idempotency key** |
| `tnt_eventtype` | Choice | TrayBind / CartonScan / TrayBuildComplete |
| `tnt_orderline` | Lookup → tnt_orderline | nullable |
| `tnt_trayqr` | Text | |
| `tnt_scannedqr` | Text | |
| `tnt_deviceid` | Text | Device identifier |
| `tnt_userid` | Text | Picker UPN |
| `tnt_eventutc` | DateTime | Client timestamp |
| `tnt_syncstatus` | Choice | Pending / Synced / Conflict |

> `tnt_scanbuffer` is optional if you keep the queue purely in an app `Collection`; persisting to Dataverse survives app restarts and hands conflict resolution to the server. See `offline-and-conflicts.md`.

## Relationships

- `tnt_salesorder` 1:N `tnt_orderline` 1:N `tnt_carton`
- `tnt_orderline` 1:N `tnt_scanbuffer` (optional)

## Choice: shipment state (mirror)
`Ordered, Picked, Staged, Loaded, InTransit, Received, ShortShipped, Damaged, WrongStore, Lost` — mirrors `ref.ShipmentState` so the handheld shows the same status vocabulary as the backend.
