# Asset Registry & Custody Analytics — Module 10

Reusable-tray (asset) tracking: the custody chain, nightly-computed utilization/dwell/loss metrics, suspected-lost detection, and a write-off flow. Answers "where is every tray, how hard is it working, and which routes/stores lose them".

## Custody chain

Every tray movement appends a `ops.TrayCustody` row (from/to custodian + trip ref) — see Module 1. Current custodian = latest row (`ops.vTrayCurrentCustody`), also denormalized on `ops.Tray`. The full history is `GET /assets/{trayQr}/history`.

## Nightly metrics (Azure Function timer → SQL)

| Metric | Definition |
|--------|-----------|
| **Trips/tray/month** | Distinct trips a tray was loaded on, per calendar month (utilization) |
| **Dwell by location** | Time between custody-in and custody-out at each custodian; flag > threshold |
| **Suspected lost** | Not seen (no scan/custody) in N days → raises a `SuspectedLost` exception |
| **Loss rate by route** | Trays that went out on a route and never returned ÷ trays sent |
| **Loss rate by store** | Same, attributed to the delivery store (finds problem stores) |
| **Fleet sizing** | `ceil(daily demand × cycle time / target utilization)` — circulating stock vs cycle time |

## Files

| File | Role |
|------|------|
| `sql/metrics.sql` | Metric tables + the nightly computation stored procedures |
| `src/AssetMetricsFunction/` | Timer-trigger Function that runs the procs + suspected-lost sweep |
| `src/AssetApi/` | `GET /assets/{trayQr}/history`, `/assets/summary`, `/assets/exceptions` |
| `write-off.md` | Suspected-lost → Lost → F&O fixed-asset/expense hook |

## API

| Route | Returns |
|-------|---------|
| `GET /assets/{trayQr}/history` | Ordered custody chain for a tray |
| `GET /assets/summary` | Fleet KPIs: utilization, dwell, loss %, fleet-size recommendation |
| `GET /assets/exceptions` | Open asset exceptions (suspected lost, dwell exceeded) |

## Write-off flow

`SuspectedLost` (auto, after N days unseen) → **confirmed `Lost`** after a further M days (or manual confirm) → optional F&O fixed-asset retirement / expense posting hook. See `write-off.md`.
