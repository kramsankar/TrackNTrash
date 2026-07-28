# TrackNTrash — Dispatch Track-and-Trace System

Warehouse-to-retail-store **track-and-trace** with QR + vision verification and reusable-asset (tray) tracking.

> Items are ordered → picked in the warehouse → placed in reusable trays → staged at the dispatch dock → loaded into vehicles → delivered to retail stores.

## Goals

1. **Reconciliation** — verify the correct items reach the correct store (order vs. physical) at every checkpoint.
2. **Asset tracking** — reusable trays/crates: custody chain, dwell time, loss.

## Checkpoints

| # | Checkpoint | Capture method |
|---|-----------|----------------|
| 1 | Pick & tray build | Handheld QR scan (Power Apps) |
| 2 | Dispatch dock | Fixed overhead camera (IoT Edge vision) |
| 3 | Vehicle loading | Driver handheld scan |
| 4 | Store receiving | Store staff handheld scan |

## Identity model

- **Cartons** — GS1 serialized QR: `(01)GTIN (21)serial`, or SSCC-18.
- **Trays** — permanent internal QR: `TRAY-{siteCode}-{seq}`.
- **Vehicles** — trip-manifest QR per trip.

## Core pattern

Every scan is an **immutable event**. A per-shipment-line state machine drives status:

```
Ordered → Picked → Staged → Loaded → InTransit → Received
```

Terminal exceptions: `ShortShipped`, `Damaged`, `WrongStore`, `Lost`.
Skipped transitions **do not block** the event write — they create an `Exception` record.

## Tech stack

- **Edge vision:** Azure IoT Edge · YOLOv8n · zxing-cpp / Dynamsoft multi-QR decode
- **Ingest:** Azure IoT Hub → Event Hub
- **Backend:** Azure Functions + .NET 8 Web API
- **Data:** Azure SQL (operational) + star-schema mart
- **Handheld:** Dataverse + Power Apps · Power Automate
- **ERP:** D365 F&O (business events) — BC notes as appendix
- **Analytics:** Power BI (TMDL semantic model)
- **Ops console:** React + SignalR
- **IaC:** Bicep + GitHub Actions

## Monorepo layout

```
TrackNTrash/
├── docs/                    Architecture, build plan, runbook
├── database/               [M1]  Azure SQL DDL, migrations, mart
├── services/
│   ├── label-api/          [M2]  GS1 QR / SSCC label generation (.NET 8)
│   └── tracking-api/       [M6]  Event ingest + state machine + exceptions (.NET 8)
├── apps/
│   ├── pick-app/           [M3]  Pick & tray build (Power Apps)
│   ├── driver-app/         [M7]  Vehicle loading & trips
│   ├── receiving-app/      [M8]  Store receiving + POD
│   └── exception-console/  [M12] Exception ops (React + SignalR)
├── edge/
│   ├── vision-module/      [M4]  Dock camera IoT Edge module (Python)
│   └── yolo-training/      [M5]  Carton detection training pipeline
├── integration/d365/       [M9]  D365 F&O integration (+ BC appendix)
├── analytics/
│   ├── asset-registry/     [M10] Tray custody + loss analytics
│   └── power-bi/           [M11] Data mart + dashboards
└── infra/                  [M13] Bicep, workflows, runbook
```

## Build order

| Phase | Modules | Outcome |
|-------|---------|---------|
| **1** | 1, 2, 3, 6 | Scan-based reconciliation end-to-end — **pilotable** |
| **2** | 7, 8, 9 | Full logistics loop + ERP posting |
| **3** | 4, 5 | Vision verification at the dock |
| **4** | 10, 11, 12 | Asset analytics, dashboards, exception ops |
| **5** | 13 | Hardened deployment |

Phase 1 alone delivers order-vs-received reconciliation and is a demo-ready pilot. Vision is an accuracy upgrade, not a dependency.

## Status

🎉 **All 13 modules built.**

✅ **Phase 1 (pilotable)** — M1 schema, M2 label API, M3 pick app, M6 tracking API.
✅ **Phase 2 (logistics loop + ERP)** — M7 trips/loading, M8 store receiving, M9 D365 integration.
✅ **Phase 3 (dock vision)** — M4 edge vision module, M5 YOLO training pipeline.
✅ **Phase 4 (analytics & ops)** — M10 asset analytics, M11 Power BI mart, M12 exception console (React + SignalR, browser-verified live).
✅ **Phase 5 (hardening)** — M13 Bicep IaC, GitHub Actions, runbook.

**136 .NET + 10 Python tests passing; React console builds & verified end-to-end; Bicep validates.** Local only — nothing pushed to GitHub yet. Per-module status in [docs/build-plan.md](docs/build-plan.md).
