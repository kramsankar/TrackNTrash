# TrackNTrash — Implementation Guide

How to stand this system up from nothing. Written in the order you actually do it, with the
things that cost time called out where you'll hit them rather than in an appendix.

---

## 1. What you are deploying

TrackNTrash tracks cartons and trays from a warehouse pick face to a retail store shelf, and
tells you the moment something diverges from what was planned.

Every scan is appended to an immutable event log; the current state of each order line is a
projection derived from that log. Nothing overwrites history, so "where was this carton at
14:20 yesterday" is always answerable.

| Component | What it is | Runs on |
|---|---|---|
| Tracking API | .NET 8 minimal API — ingestion, state machine, exceptions, masters, RBAC | Azure App Service |
| Azure SQL | event log + projections + master data | Azure SQL (France Central) |
| Admin & Exception Console | React SPA — live exception board, masters, roles, dashboards | Storage static website |
| Pick app | .NET MAUI — build a tray from an order, count units | Android handheld / Windows |
| Driver app | .NET MAUI — load trays to a trip, depart, return empties | Android handheld |
| Receiving app | .NET MAUI — reconcile against the ASN, capture POD | Android handheld |
| Dock vision module | Python + YOLOv8 ONNX — counts cartons on a tray from a camera | IoT Edge gateway |
| Functions | scheduled SLA sweep, dock verification pipeline | Azure Functions |

### The four checkpoints

Everything hangs off these. They are seeded in `database/schema/02_seed_reference.sql`.

| # | Checkpoint | Who | What it proves |
|---|---|---|---|
| 1 | `PickTrayBuild` | Picker | the right cartons went into the right tray |
| 2 | `DispatchDock` | Camera | the tray leaving the dock holds what the manifest says |
| 3 | `VehicleLoad` | Driver | the tray went onto the correct trip |
| 4 | `StoreReceive` | Store colleague | what arrived matches the ASN |

### The state machine

An order line walks `Ordered → Picked → Staged → Loaded → InTransit → Received`. Four
terminal exception states branch off it: `ShortShipped`, `Damaged`, `WrongStore`, `Lost`.

The transition table is an explicit dictionary in `ShipmentStateMachine.cs`, not scattered
conditionals, so every legal edge is enumerable and unit-tested. **An illegal transition never
blocks the event write** — the scan is recorded regardless and an `IllegalTransition`
exception is raised. A device that is out of order must never be able to lose data.

---

## 2. Minimal prerequisites

### 2.1 The genuinely minimal pilot

You can run a real pilot with **no bespoke hardware at all**:

- One Android phone (Android 8.0+), or even just a Windows laptop
- A browser for the console
- The Azure footprint

The three MAUI apps build for `net9.0-windows` as well as Android, and the camera checkpoint
is optional — the dock verification event can be posted by hand or skipped, in which case
lines move `Picked → Staged` on a manual dock pass. Start here. Add hardware once the flow is
proven with real stock.

### 2.2 Handheld devices (per person scanning)

| | Minimum | Comfortable |
|---|---|---|
| OS | Android 8.0 (API 26) | Android 12+ |
| RAM | 2 GB | 4 GB |
| Camera | 8 MP autofocus, rear | 12 MP with a good macro range |
| Screen | 5" | 5.5"–6" |
| Network | intermittent Wi-Fi | Wi-Fi + 4G fallback |
| Storage free | 200 MB | 500 MB |

Camera scanning is `ZXing.Net.MAUI`, so **autofocus matters far more than megapixels**. A
fixed-focus budget handset will fail to decode a carton QR at arm's length and frustrate
everyone. Rugged handhelds (Zebra TC2x, Honeywell CT30) work and their hardware scan trigger
behaves as a keyboard — the apps accept keyboard input into the scan field, so a hardware
scanner needs no code change.

APKs are release-signed with one certificate, so a rebuild installs as an upgrade rather than
a conflicting second app.

### 2.3 Dock camera (checkpoint 2) — optional for pilot

Only needed to automate the dispatch-dock count. From `PROVISIONING.md`:

| Item | Target |
|---|---|
| Mount | Overhead, centred on the staging zone, lens parallel to the tray top |
| Height | 2.2–2.8 m (tune so a full tray fills ~70% of frame) |
| Field of view | Whole tray footprint + 20% margin, no neighbouring trays in shot |
| Lighting | ≥ 500 lux, even and diffuse, no specular glare on tape or labels |
| Resolution | ≥ 1080p, fixed focus **locked after commissioning** |
| Shutter | ≥ 1/250 s, fast enough to freeze a tray sliding in |
| Stream | RTSP |

**Carton QR labels must be ≥ 35 mm.** At 1080p, 2.5 m up, over a ~1.2 m field of view you get
roughly 0.55 mm/px. A 25 mm v3 QR lands near 1.5 px per module, below the ~2 px a decoder
needs — it will read intermittently, which is worse than not reading at all. 35 mm gives you
~2.2 px/module. Tray labels are larger with higher error correction and decode comfortably.

### 2.4 Edge gateway — only if you use the camera

| | Minimum |
|---|---|
| CPU | x64 or ARM64, 4 cores |
| RAM | 4 GB (8 GB if you add more camera streams) |
| Disk | **20 GB free** |
| OS | Ubuntu 22.04 LTS or Raspberry Pi OS 64-bit |
| Runtime | Azure IoT Edge 1.5 LTS |
| Network | outbound 443 and 8883 to Azure |
| GPIO | optional relay on `/dev/gpiomem` for a stack light or gate |

The disk figure is not padding. **The module image is 3.15 GB** because the detector loads its
model through `ultralytics`, which pulls in torch. Budget for that pull over a warehouse
network, and see §7 if you want it smaller.

### 2.5 Build machine

| | |
|---|---|
| .NET SDK | 9.0 (the API targets net8.0, the apps net9.0) |
| MAUI workloads | `dotnet workload install maui-windows maui-android` |
| Node | 20+ (console) |
| Python | 3.10+ (integration suite, edge module tests) |
| Azure CLI | current, logged in |
| sqlcmd | ODBC 17 tools |
| Android SDK | needed for APKs — pass its path explicitly, see §7 |
| Java JDK | 17 or 18, for `keytool` |

### 2.6 Azure

One subscription and rights to create resource groups. The dev footprint is App Service B1,
SQL, IoT Hub S1, Service Bus, Key Vault, two storage accounts, ACR Basic.

**IoT Hub S1 and SQL are the cost floor** — neither has a free tier that will carry a pilot.
Check current pricing before committing; this is not a free-tier system.

---

## 3. Standing it up

### Step 1 — Infrastructure

```bash
az group create --name rg-tracktrash-dev --location uksouth
az deployment group create --resource-group rg-tracktrash-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/params.dev.json
```

The template takes a `sqlLocation` separate from `location` on purpose. Azure SQL capacity is
region-gated and refuses new servers in busy regions with an unhelpful error — UK South, North
Europe, UK West and West Europe were all refused during this build. France Central worked. If
you get a capacity error, probe regions rather than assuming the template is wrong.

Note the outputs: `trackingApiUrl`, `sqlServerFqdn`, `iotHubName`, `storageAccount`,
`acrName`, `acrLoginServer`.

### Step 2 — Database

Base schema first, then every migration in order. They are idempotent, so re-running is safe.

```bash
sqlcmd -S <sqlServerFqdn> -d TrackNTrash -U tntadmin -P "$SQLPW" -N -C -I -b \
  -i database/schema/01_schema.sql
sqlcmd -S <sqlServerFqdn> -d TrackNTrash -U tntadmin -P "$SQLPW" -N -C -I -b \
  -i database/schema/02_seed_reference.sql
for f in database/migrations/0*.sql; do
  sqlcmd -S <sqlServerFqdn> -d TrackNTrash -U tntadmin -P "$SQLPW" -N -C -I -b -i "$f"
done
```

| Migration | Adds |
|---|---|
| 001 | tray manifest cache |
| 002 | application users (PBKDF2-HMAC-SHA256, 100k iterations) |
| 003 | items and cameras |
| 004 | widened carton serial charset for GS1 hyphenated serials |
| 005 | masters, roles, form mappings |
| 006 | order lines carried on a trip load |
| 007 | ASNs and the exception audit trail |
| 008 | receiving sessions |
| 009 | `CameraDevice` role |

`Checkpoint` is a reserved word in T-SQL. It is bracketed as `ref.[Checkpoint]` throughout —
if you write new SQL against that table, do the same. Filtered indexes and views also need
`SET QUOTED_IDENTIFIER ON`, which every migration sets.

### Step 3 — Secrets

The API reads its connection strings through Key Vault references in app settings, resolved by
its managed identity. Secrets are created **control-plane, as ARM resources in the Bicep** —
not with `az keyvault secret set`. That is deliberate: on a subscription where you have
contributor but no Key Vault data-plane role, `secret set` returns `Forbidden` and
`az role assignment create` may fail with `MissingSubscription`. The ARM path works regardless.

To read a secret value back you need **Key Vault Secrets User**. Contributor is not enough.

Set `Auth:SigningKey` and `Auth:SetupKey` as app settings. Then seed the first admin:

```bash
curl -X POST "$API/auth/users" -H "Content-Type: application/json" \
  -H "x-setup-key: $SETUP_KEY" \
  -d '{"username":"admin","displayName":"Admin","password":"<strong>","roles":"Admin"}'
```

`/auth/users` is gated by that header, not by a token — it is the bootstrap path and the only
way in before any user exists.

### Step 4 — API

```bash
dotnet publish services/tracking-api/src/TrackNTrash.Tracking.Api -c Release -o ./publish
# zip ./publish, then:
az webapp deploy --resource-group rg-tracktrash-dev --name <app> --src-path api.zip --type zip
```

### Step 5 — Console

```bash
cd apps/exception-console
npm ci && npm run build
az storage blob upload-batch --account-name <storage> --destination '$web' --source dist --overwrite
```

Set the API base URL for the build, and add the console's origin to `Cors:Origins` on the API
(comma-separated, so the Vite dev server can sit alongside it).

### Step 6 — Roles and users

Sign in to the console as admin, then **Setup → Roles / Users / Mapping**. Roles seeded by
migration 005: `Admin`, `WarehouseManager`, `Dispatcher`, `Picker`, `StoreManager`, plus
`CameraDevice` from 009. Permissions are per role × form: view / create / edit / delete. Admin
short-circuits every check.

`CameraDevice` deliberately has no form mappings — it is not a console login.

### Step 7 — Handheld apps

```bash
dotnet publish apps/pick-app/maui -c Release -f net9.0-android \
  -p:AndroidSdkDirectory="C:/Android/sdk" \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=".secrets/tracktrash-release.keystore" \
  -p:AndroidSigningKeyAlias=tracktrash \
  -p:AndroidSigningKeyPass="$KSPW" -p:AndroidSigningStorePass="$KSPW"
```

Repeat for `driver-app` and `receiving-app`. Publish the APKs plus `apps/downloads/index.html`
to the storage static website; the page carries a QR code so a phone can reach it from a
desk screen.

Each app opens on a sign-in card and stays disabled until a token is in hand — the API does
not accept anonymous scans.

### Step 8 — Edge camera (optional)

Only if you are automating checkpoint 2.

```bash
# image (server-side; no local Docker needed)
az acr build --registry <acr> --image tracktrash/dockvision:1.0 --platform linux/amd64 \
  edge/vision-module

# register the gateway, then apply the deployment
./edge/vision-module/scripts/apply-edge-deployment.ps1 -DeviceId dock-cam-ldn1
```

The script pulls the camera service-account credentials from Key Vault, renders the manifest to
a temp file, applies it, and deletes the rendered copy — the password is never committed and is
on disk only for the duration of the call.

**`models/carton_yolov8n.onnx` is not in the repo.** Without it the module starts and then
raises on the first tray it tries to count, because the detector loads its model lazily. Bake
it into the image or mount it as a volume — the volume route also lets you update the model in
the field without a 3.15 GB rebuild.

---

## 4. Verifying it actually works

Three gates, in increasing confidence. Run all three; the first two can pass while the system
loses data, which is exactly what happened during this build.

```bash
# 1. domain logic — 127 tests, in-memory stores, seconds
dotnet test services/tracking-api -c Release

# 2. deployed API + Azure SQL — 89 checks
python tests/integration/api_persistence_test.py \
  --password "$ADMIN_PW" --sql-password "$SQL_PW" --camera-password "$CAMERA_PW"

# 3. edge module
cd edge/vision-module && python -m pytest -q
```

**Why gate 2 exists.** The unit suite ran green while trips, tray custody, ASNs, receiving
sessions and the console's exception list were all silently not persisting — every one of those
tests uses in-memory stores, so none of them could see it. Gate 2 drives the real HTTP API and
then asserts the rows reached Azure SQL. "The endpoint returned 200" is not "the data was
saved." Its exit code is non-zero on failure so it can gate a deploy.

It also sweeps every readable endpoint for a `401` rather than checking a hand-maintained list,
because a hand-maintained list already missed ten endpoints once.

### Manual smoke test

Sign in on all three apps, then push one line end to end: create an order in the pick app,
scan cartons, complete the tray, take the order-line id to the driver app, create a trip, load
the tray, depart, then in the receiving app set up the ASN, scan, and complete. Watch the line
reach `Received` in the console. Deliberately scan a carton onto the wrong trip to see the
`WrongTrip` rejection and a `Critical` exception land on the board live.

---

## 5. Security posture

- Every endpoint requires a token except `/health`, `/auth/config`, `/auth/login`, and
  `/auth/users` (setup-key gated).
- Passwords are PBKDF2-HMAC-SHA256, 100k iterations, stored as `base64(salt):base64(hash)`.
- Tokens last 12 hours. Two schemes are accepted: local JWT and Entra ID.
- Device accounts are **confined, not just authenticated**. `CameraDevice` is refused by the
  default policy and admitted only on manifest sync and its own heartbeat. A camera sits
  unattended where a contractor can reach it, so a leaked camera credential must buy nothing —
  and it gets `403` on everything else. That is asserted by the integration suite.
- Handheld tokens persist to device preferences so a warehouse handset asks for a password
  once, not every shift.

---

## 6. Ongoing operations

| Job | Cadence | How |
|---|---|---|
| SLA sweep | scheduled Function | flags `NoReceiveSla` past 24 h, `TrayDwellExceeded` past 3 days |
| Camera drift | monthly | 1% of PASS frames sampled to Blob for re-validation |
| Credential rotation | per policy | API first, then Key Vault, then push to devices |
| Model refresh | as accuracy drifts | replace the mounted ONNX, no image rebuild |

Alert if PASS-rate or mean verification time moves more than 2σ from the rolling baseline.

---

## 7. Things that will cost you an afternoon

Every one of these was hit during this build.

| Symptom | Cause and fix |
|---|---|
| SQL server creation refused with a capacity error | Region gating. Probe regions; use the `sqlLocation` parameter. France Central worked when four others did not. |
| `Incorrect syntax near 'Checkpoint'` | Reserved T-SQL word. Bracket it: `ref.[Checkpoint]`. |
| Filtered index or view fails to create | Needs `SET QUOTED_IDENTIFIER ON`. |
| `az keyvault secret set` returns `Forbidden` | No data-plane role. Create secrets as ARM resources in Bicep instead. |
| MSBuild signing fails obscurely | A `%` in the keystore password — MSBuild reads it as an escape. Use alphanumeric only. |
| `error XA5300` on an Android build | Android SDK not on the probed path. Pass `-p:AndroidSdkDirectory="C:/Android/sdk"`. |
| `az extension add --name azure-iot` fails with a pip error | The Azure CLI's bundled 32-bit Python. Use the IoT Hub REST API — `PUT /devices/{id}` with a SAS token from the `iothubowner` key. |
| `az acr build` crashes mid-log with a `charmap` error | `cp1252` console cannot encode the build output. The build is unaffected — check `az acr task list-runs`. |
| Carton QR rejected by a check constraint | GS1 serials may contain hyphens; migration 004 widened the charset. |
| Module stuck in `backoff` on the device | The image tag is not in the registry. The apply script warns about this before deploying. |

---

## 8. Rough order of effort

Assuming Azure access and the repo in hand.

| Stage | Time |
|---|---|
| Infrastructure + database + API deployed | half a day |
| Console up, admin seeded, roles configured | half a day |
| Masters loaded (sites, zones, racks, stores, products, vehicles) | 1–2 days, mostly data gathering |
| APKs built, signed, distributed | half a day |
| One line walked end to end and verified | half a day |
| Dock camera commissioned (mount, light, focus, model) | 2–3 days per dock |

The camera is the long pole and the only part needing physical work. Everything before it is
software you can prove in a day or two — which is the argument for doing the pilot without it.
