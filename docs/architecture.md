# Architecture Overview

## 1. End-to-end flow

```mermaid
flowchart LR
    subgraph WH[Warehouse]
        A[Pick & Tray Build<br/>Power Apps scan] --> B[Dispatch Dock<br/>Fixed camera + IoT Edge]
        B --> C[Vehicle Loading<br/>Driver scan]
    end
    subgraph TR[Transit]
        C --> D[In Transit<br/>Telematics geofence]
    end
    subgraph ST[Retail Store]
        D --> E[Store Receiving<br/>Staff scan + POD]
    end

    A -. events .-> HUB
    B -. IoT Hub .-> HUB
    C -. events .-> HUB
    E -. events .-> HUB

    subgraph AZ[Azure]
        HUB[[IoT Hub / Event Hub]] --> ING[Ingestion Function]
        ING --> SM[State Machine + Exception Engine<br/>.NET 8]
        SM --> SQL[(Azure SQL<br/>append-only ScanEvent)]
        SM --> SB[[Service Bus<br/>exception topic]]
        SB --> NOTIF[Teams / Email / Power Automate]
        SQL --> MART[(Power BI Mart)]
    end

    SM <-. business events .-> D365[D365 F&O]
    SQL --> CONSOLE[Exception Console<br/>React + SignalR]
    MART --> PBI[Power BI Dashboards]
```

## 2. State machine

```mermaid
stateDiagram-v2
    [*] --> Ordered
    Ordered --> Picked: TrayBuildComplete
    Picked --> Staged: DockVerification PASS
    Staged --> Loaded: TripLoadScan
    Loaded --> InTransit: TelemetryDepart
    InTransit --> Received: ReceivingComplete

    Picked --> ShortShipped: reconcile short
    Staged --> Damaged: damage flag
    Loaded --> WrongStore: wrong-trip scan
    InTransit --> Lost: no receive within SLA

    ShortShipped --> [*]
    Damaged --> [*]
    WrongStore --> [*]
    Lost --> [*]
    Received --> [*]
```

**Rule:** an event that implies an illegal transition (e.g. `Loaded` while still `Ordered`) is **always written** to `ScanEvent`; the state machine then raises an `Exception` record instead of advancing state. Events are the source of truth; `ShipmentLineState` is a derived projection with a `LastEventId` FK for audit.

## 3. Event idempotency

Every write-producing scan carries `(deviceId, clientEventId)`. Ingestion dedupes on this pair so offline replay / retries never double-count. `ScanEvent` is append-only (no UPDATE/DELETE), with a `rowversion` column for optimistic concurrency on projections.

## 4. Component responsibilities

| Layer | Component | Module | Responsibility |
|-------|-----------|--------|----------------|
| Data | Azure SQL schema | M1 | Orders, cartons, trays, trips, events, state, exceptions |
| Service | Label API | M2 | GS1 QR + SSCC generation, ZPL/SVG output |
| App | Pick app | M3 | Tray build reconciliation, offline queue |
| Edge | Vision module | M4 | Dock multi-QR decode + carton count verdict |
| ML | YOLO pipeline | M5 | Carton detection model + export/benchmark |
| Service | Tracking API | M6 | Ingest, state machine, exception rules, notifications |
| App | Driver app | M7 | Trip load validation, multi-drop |
| App | Receiving app | M8 | ASN reconcile, POD, tray custody transfer |
| Integration | D365 F&O | M9 | SO/work in, picking/ASN/delivery out |
| Analytics | Asset registry | M10 | Custody chain, dwell, loss, fleet sizing |
| Analytics | Power BI | M11 | Star schema, DAX, dashboards, RLS |
| Web | Exception console | M12 | Ops triage, live updates, audit |
| Ops | IaC + runbook | M13 | Bicep, CI/CD, commissioning |

## 5. Trust & security posture

- Handheld auth via Entra ID; console roles: Dispatcher / Warehouse Manager / Admin.
- Store-manager RLS in Power BI (own store only).
- Blob lifecycle: exception frames/photos 1 year, PASS samples 30 days.
- Secrets in Key Vault; no credentials in code or config committed to the repo.
