# Tracking API — Module 6

The core track-and-trace backend: **event ingestion + shipment-line state machine + exception engine + manifest sync + notifications**. .NET 8 Web API and Azure Functions sharing one Core library.

## Solution layout

```
tracking-api/
├── src/
│   ├── TrackNTrash.Tracking.Core/        ← domain (no infra deps) — fully unit-tested
│   │   ├── ShipmentStateMachine.cs        explicit transition table
│   │   ├── EventTriggerMap.cs             eventType (+verdict) → trigger
│   │   ├── ExceptionSeverityMatrix.cs     configurable type → severity
│   │   ├── Rules/ExceptionRules.cs        IIngest… / ISweep… + 2 example rules
│   │   ├── Services/IngestionService.cs   ingest pipeline
│   │   ├── Services/SweepService.cs        time-based sweep
│   │   ├── Stores/ (interfaces + in-memory)
│   │   └── Notifications/ (interface + logging publisher)
│   ├── TrackNTrash.Tracking.Api/          ← REST API, SQL event store, Service Bus publisher
│   └── TrackNTrash.Tracking.Functions/    ← Event Hub trigger + timer sweep (isolated worker)
└── tests/TrackNTrash.Tracking.Tests/      ← 115 tests
```

## State machine

Deliberately a **dictionary transition table** (`ShipmentStateMachine`), not scattered `if/else`, so every legal edge is enumerable and testable.

```
Ordered ─TrayBuildComplete→ Picked ─DockVerificationPass→ Staged
        ─TripLoadScan→ Loaded ─TelemetryDepart→ InTransit ─ReceivingComplete→ Received
Terminal exception edges: Picked→ShortShipped, Staged→Damaged, Loaded→WrongStore, InTransit→Lost
```

**Golden rule:** an illegal transition (e.g. `Loaded` without `Staged`) **never blocks the event write**. The event is appended, the projection stays put, and an `IllegalTransition` exception is raised. `Evaluate` never throws — an unknown edge returns `IsLegal = false` with the trigger's canonical intended target.

## Ingestion pipeline (`IngestionService`)

1. Append append-only, **idempotent** on `(deviceId, clientEventId)` → duplicates are no-ops that still return 200.
2. Resolve order line + map event → trigger (`EventTriggerMap`).
3. Evaluate state machine → advance projection, or raise `IllegalTransition`.
4. Run ingest-time rules.
5. Persist + publish every exception.

## Exception engine

Two interfaces, two example rules:

| Rule | Interface | Fires |
|------|-----------|-------|
| `CountMismatchAtDockRule` | `IIngestExceptionRule` | On a `DockVerification` event whose verdict ≠ PASS; pulls the tray manifest to report expected vs actual, extracts `frameRef`. |
| `NoReceiveWithinSlaRule` | `ISweepExceptionRule` | Timer sweep: lines in Loaded/InTransit past the receive SLA → `NoReceiveSla`; past 2× SLA → `SuspectedLost`. |

Add a rule by implementing the interface and registering it in DI — no pipeline changes.

Severity per type is a config matrix (`ExceptionSeverityMatrix`), overridable.

## Notifications

`INotificationPublisher` → `ServiceBusNotificationPublisher` (topic `exceptions`, with `severity`/`type`/`checkpoint` message properties for subscription filters). Falls back to `LoggingNotificationPublisher` when no Service Bus connection is configured. Subscribers: Teams webhook, email, Power Automate.

## Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/events/scan` | Ingest one event |
| POST | `/events/scan/batch` | Ingest a batch (pick app / offline flush) |
| GET | `/manifests?since=` | Edge delta-sync of expected tray manifests |
| PUT | `/manifests` | Upsert a manifest |
| GET | `/shipment-lines/{id}/state` | Current derived state |
| GET | `/exceptions/open` | Open exceptions |
| POST | `/admin/sweep` | Manually run the time-based sweep |

Full contract in [`openapi.yaml`](openapi.yaml); live Swagger at `/swagger`.

## Azure Functions host

- `DockVerificationFunction` — `EventHubTrigger` on the IoT Hub built-in endpoint; batches DockVerification messages through `IngestionService`.
- `SweepTimerFunction` — `TimerTrigger` (`0 */15 * * * *`) runs `SweepService`.

## Configuration

| Setting | Effect |
|---------|--------|
| `ConnectionStrings:TrackNTrash` | Present → SQL event store (Module 1 schema); absent → in-memory |
| `ServiceBus:ConnectionString` / `:Topic` | Present → Service Bus publisher; absent → logging |

## Run & test

```bash
dotnet run --project src/TrackNTrash.Tracking.Api   # http://localhost:5090/swagger
dotnet test                                          # 115 tests
```

Tests cover: **every legal edge**, the **exhaustive legal-iff-in-table** matrix over all (state × trigger) pairs, terminal-state lockout, the full happy path, ingestion idempotency, illegal-transition-still-writes-event, both example rules, and API integration (health, validation, ingest→state, manifest delta).
