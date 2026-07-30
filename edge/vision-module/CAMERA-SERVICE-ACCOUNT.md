# Camera service account

The dock camera reaches the tracking API for exactly two things:

| Endpoint | Why |
|---|---|
| `GET /manifests?since=…` | how many cartons a tray should hold, for the dock count |
| `POST /cameras/{code}/heartbeat` | so a working camera is not indistinguishable from a dead one |

Both used to be anonymous. They are guarded now, so the module signs in as its own account.

## Why it is not just another user

A camera runs unattended, on a device mounted where a contractor can reach it. Its
credentials are the likeliest in the system to leak. The `CameraDevice` role is therefore
**refused by the API's default policy** and admitted only on the two endpoints above.

A stolen camera credential gets `403` on everything else — the order book, trips, scan
events, masters, RBAC, the exception console. That is asserted by the integration suite
(`A leaked camera credential reaches nothing else`), not just intended.

The role deliberately has no `RoleFormMapping` rows: it is not a console login and has no
business opening a screen.

## Where the credentials live

| Where | What |
|---|---|
| Key Vault `kv-tracktrashdev-4ymqn2` | `camera-agent-username`, `camera-agent-password` — the source of truth |
| `infra/bicep/main.bicep` | declares both secrets (`cameraAgentPassword` is a `@secure()` param) |
| `deployment.json` | carries `TNT_API_USERNAME` / `TNT_API_PASSWORD` as **placeholders** |
| the module's environment | where the real values land, at apply time |

Credentials go into module **environment variables, never the module twin** — twin desired
properties are readable in the Azure portal by anyone with reader access on the IoT Hub.
Non-secret settings do belong in the twin: `apiBaseUrl`, `cameraCode`, `heartbeatSeconds`.

`manifestSyncUrl` is derived from `apiBaseUrl`, so the two cannot drift and point at
different environments. Set it explicitly only if the manifest feed genuinely lives
elsewhere.

## Applying it to a device

The committed manifest holds no secrets. `scripts/apply-edge-deployment.ps1` reads them from
Key Vault, renders the manifest to a temp file, applies it, and deletes the rendered copy in
a `finally` block — so the password is on disk only for the duration of the call.

```bash
./scripts/apply-edge-deployment.ps1 -DeviceId dock-cam-ldn1 -AcrName <acr>
```

Add `-WhatIf` to render and validate without applying. The script refuses to deploy a
manifest that still contains a placeholder, so a missing `-AcrName` fails loudly rather than
shipping `<ACR>.azurecr.io` to a device.

It also checks the device is registered first — `az iot edge set-modules` against a device
that does not exist fails obscurely.

### What is in place

| | |
|---|---|
| IoT Hub | `iot-tracktrash-dev-4ymqn2` |
| Edge device | `dock-cam-ldn1` — registered, `iotEdge=true`, enabled |
| Registry | `crtracktrashdev4ymqn2.azurecr.io` (Basic, admin user on) |
| Image | `crtracktrashdev4ymqn2.azurecr.io/tracktrash/dockvision:1.0` |
| Vault secrets | `camera-agent-username`, `camera-agent-password`, `edge-device-cs-dock-cam-ldn1` |

### Still needed before a camera actually verifies a tray

1. **The detection model.** `models/` holds only a placeholder README —
   `carton_yolov8n.onnx` comes from Module 5 and is not in the repo. The module starts
   without it but the first detection raises. Either bake it into the image or mount it as a
   volume, which also lets it be updated in the field without a rebuild.
2. **A physical gateway running the IoT Edge runtime**, provisioned with the device
   connection string from `edge-device-cs-dock-cam-ldn1`. The device shows `Disconnected`
   until then. See [PROVISIONING.md](PROVISIONING.md).
3. **The `azure-iot` CLI extension** on whatever machine applies the deployment
   (`az extension add --name azure-iot`).
4. **Key Vault read access** — the caller needs a data-plane role (Key Vault Secrets User) on
   the vault. Control-plane contributor is *not* enough to read a secret value.

## Token handling

Nobody is present to retype a password, so the module holds the credentials and fetches a
token on demand: on first use, when the current one is within five minutes of expiry, and
once more if the server still answers `401`. A second `401` raises `AuthError` rather than
retrying, because wrong credentials do not fix themselves.

That failure is loud on purpose. A silent auth failure would leave the dock verifying every
tray against an empty expected-count table — passing everything, catching nothing.

## Rotating the password

Three steps, in this order — the API first, so a camera never holds a credential the API has
already stopped accepting.

```bash
# 1. change it on the API
curl -X POST "$API/auth/users" \
  -H "Content-Type: application/json" \
  -H "x-setup-key: $SETUP_KEY" \
  -d '{"username":"camera-agent","displayName":"Dock camera service account","password":"NEW","roles":"CameraDevice"}'
```

```bash
# 2. update the source of truth
az keyvault secret set --vault-name kv-tracktrashdev-4ymqn2 --name camera-agent-password --value NEW
```

```bash
# 3. push it to each device
./scripts/apply-edge-deployment.ps1 -DeviceId dock-cam-ldn1 -AcrName <acr>
```

Existing tokens stay valid until they expire (12 hours), so cameras keep working through the
rollout — they only need the new password once their current token lapses.

Use alphanumeric passwords. A `%` in a password has already broken one toolchain in this
project.

## One account or one per camera?

One shared account is what is set up here, and it is the right default: cameras are
identified by `cameraCode` in the heartbeat, so the console still tells them apart. Move to
one account per camera only if you need to revoke a single unit without touching the rest —
at which point give each the same `CameraDevice` role.
