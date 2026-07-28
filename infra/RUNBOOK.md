# TrackNTrash Operations Runbook

## 0. Prerequisites

- Azure subscription with 3 resource groups: `rg-tracktrash-{dev,uat,prod}` + `rg-tracktrash-shared` (shared Key Vault holding SQL admin passwords).
- Entra app registration for GitHub OIDC (federated credentials per environment); `AZURE_CLIENT_ID/TENANT_ID/SUBSCRIPTION_ID` as repo secrets.
- Entra app registration for the API + console (app roles: Dispatcher / WarehouseManager / Admin).
- ACR (shared) for the edge image.

## 1. Provision infrastructure

```bash
az deployment group create -g rg-tracktrash-dev -f infra/bicep/main.bicep -p @infra/bicep/params.dev.json
```
Or run the **Deploy Infra** workflow (what-if first, then deploy; uat/prod gated by environment approvals).

Then store secrets referenced by the app settings into the environment Key Vault:
`sql-connection`, `servicebus-connection`, `iot-eventhub`.

## 2. Database

The **Deploy Services** workflow's `db-migrate` job applies (idempotent):
`01_schema.sql → 02_seed_reference.sql → 03_views.sql → metrics.sql → mart_schema.sql → etl.sql`.
For a manual run use `sqlcmd -G` with the same order.

## 3. Deploy services

Run **Deploy Services** for the environment → publishes the Tracking API (App Service) and Functions, then migrates the DB. Verify `GET https://app-tracking-tracktrash-<env>.azurewebsites.net/health`.

## 4. Edge device provisioning (dock camera)

See `edge/vision-module/PROVISIONING.md`. Summary:
1. Install IoT Edge 1.5 on the gateway; register the device in `iot-tracktrash-<env>`.
2. Run **Deploy Edge Module** (builds the image in ACR, updates the device deployment).
3. Confirm a test `DockVerification` message reaches IoT Hub.

### Camera commissioning checklist
| Item | Target |
|------|--------|
| Mount | Overhead, centered, lens parallel to tray top |
| Height | 2.2–2.8 m (full tray ≈ 70% of frame) |
| FOV | Whole tray + 20% margin; exclude neighbouring trays |
| Lighting | ≥ 500 lux even, no specular glare on labels |
| Resolution | ≥ 1080p, fixed focus locked |
| QR size | Carton QR ≥ 35 mm at 2.5 m / 1080p (≥ ~2 px/module) |

## 5. Label printer setup

- Zebra 203 dpi, 4×6 label stock.
- Label API `POST /labels/carton?includeZpl=true` returns ZPL; send raw to the printer (port 9100) or via a print connector.
- Validate one carton + one SSCC + one tray label; confirm scanners decode the GS1-QR (symbology `]Q3`).

## 6. Go-live cutover

1. Freeze order intake briefly; ensure D365 business events point at `fno-business-events` (prod).
2. Deploy infra + services + edge to prod; run smoke tests (below).
3. Print tray labels for the reusable fleet; seed `ops.Tray` via the Label API tray batch.
4. Enable the D365 integration Functions; verify one order flows F&O → tracking `POST /orders`.
5. Run one full loop on a pilot route: pick → dock → load → deliver → receive → empty-tray return.
6. Turn on Power BI scheduled refresh and RLS; share dashboards with store managers.

### Smoke tests (per environment)
- `GET /health` on API + Functions.
- `POST /events/scan` legal transition → state advances; illegal → exception + SignalR push to console.
- `POST /trips` + wrong-trip load → rejection with correct trip.
- `POST /receiving/start` → scan → complete → line Received.
- Dock module test trigger → `DockVerification` in IoT Hub; non-PASS → blob frame + relay.

## 7. Rollback

- **Services**: redeploy the previous App Service / Functions package (keep last-known-good artifact; slots recommended for prod — deploy to staging slot, swap, swap back to roll back).
- **Edge**: re-run **Deploy Edge Module** with the previous `imageTag`; the device pulls the prior image.
- **Database**: schema scripts are additive/idempotent. For a bad migration, restore from the automatic PITR backup (`az sql db restore`) to a new DB and repoint the connection string; never hard-delete `ops.ScanEvent`.
- **Model**: revert the module twin `modelPath` to the previous `carton_yolov8n_vN.onnx`.

## 8. Monitoring & alerts

- Application Insights: availability tests on `/health`; alert on 5xx rate, dependency failures, SQL DTU.
- Service Bus: alert on `d365-repair` queue depth > 0 (dead-lettered F&O posts → run the Power Automate repair flow).
- Dock drift: alert on PASS-rate / verification-time > 2σ (see `edge/yolo-training/drift-monitoring.md`).
- Exceptions: `Critical`/`High` exceptions raise Teams posts via the Service Bus `exceptions` topic subscriber.

## 9. Routine jobs

| Job | Cadence | Where |
|-----|---------|-------|
| Fact ETL (`usp_LoadFactsIncremental`) | hourly | Function/ADF/SQL Agent |
| Dimension + DimDate load | nightly | same |
| Asset metrics (`usp_ComputeNightlyMetrics`) | nightly 02:00 UTC | AssetApi hosted service / Function |
| Exception time-sweep | 15 min | Tracking Functions timer |
