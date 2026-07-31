# Edge Device Provisioning & Camera Commissioning

## 0. What is already provisioned (dev)

| | |
|---|---|
| IoT Hub | `iot-tracktrash-dev-z3yo3x` |
| Edge device | `dock-cam-ldn1` (`iotEdge=true`, enabled, `Disconnected` until a gateway attaches) |
| Deployment | applied — `dockvision` at image `2.1`, model bind-mounted read-only |
| Registry | `crtracktrashdevz3yo3x.azurecr.io` (Basic, admin user enabled) |
| Image | `tracktrash/dockvision:2.1` (`1.0` was the 3.15 GB torch build) |
| Device connection string | Key Vault secret `edge-device-cs-dock-cam-ldn1` |

The registry is declared in `infra/bicep/main.bicep`, so a full deployment adopts it rather
than creating a second one. The **device is not** and cannot be: IoT Hub device identities
live on the hub's data plane, not in ARM, so they are not expressible in Bicep. Registering a
device is a deliberate step — see below.

## 1. Edge device

1. Install Azure IoT Edge runtime (1.5 LTS) on the gateway device (x64 or ARM64).
2. Provision it with the device connection string:
   ```bash
   az keyvault secret show --vault-name kv-tracktrashdev-z3yo3x --name edge-device-cs-dock-cam-ldn1 --query value -o tsv
   ```
   then `sudo iotedge config mp --connection-string '<that>'` and `sudo iotedge config apply`.
3. Grant Docker access to `/dev/gpiomem` (relay) — the deployment sets `Privileged` + device mapping.
4. Apply the deployment — use the script, not raw `set-modules`, so the camera credentials are
   pulled from Key Vault rather than pasted into the manifest:
   ```bash
   ./scripts/apply-edge-deployment.ps1 -DeviceId dock-cam-ldn1
   ```

### Registering a further device

The `azure-iot` CLI extension is required for `az iot hub device-identity create`. If it
fails to install (it does on the bundled 32-bit Python in the Azure CLI on Windows), the
IoT Hub service REST API does the same job — `PUT https://<hub>.azure-devices.net/devices/<id>?api-version=2021-04-12`
with `{"deviceId": "...", "status": "enabled", "capabilities": {"iotEdge": true}}` and a
SAS token built from the `iothubowner` key. Omit the symmetric keys and the hub issues them.

Store the resulting connection string as a Key Vault secret named
`edge-device-cs-<deviceId>` so commissioning a replacement does not mean re-issuing keys.

## 2. Model

**The model is mounted from the gateway, not baked into the image.**

| | |
|---|---|
| On the gateway | `/var/lib/tracktrash/models/carton_yolov8n.onnx` |
| In the container | `/app/models/carton_yolov8n.onnx`, **read-only** |
| Twin setting | `modelPath` (absolute) |

A retrained model is then a file copy and a module restart — not a rebuild, a registry push,
and a pull to every dock. The mount is read-only because the module only ever reads it, and a
writable mount is a way for a running container to corrupt the file the whole dock depends on.

### Putting the model on a gateway

```bash
sudo mkdir -p /var/lib/tracktrash/models
sudo cp carton_yolov8n.onnx /var/lib/tracktrash/models/
sudo chmod 0444 /var/lib/tracktrash/models/carton_yolov8n.onnx
sudo iotedge restart dockvision
```

**Not yet in place for `dock-cam-ldn1`** — the exported `carton_yolov8n.onnx` comes from
Module 5 and is not in the repo.

### When it is missing

The module starts, signs in, and keeps sending heartbeats, so the console still shows the
camera as alive. At boot it prints:

```
[model] MISSING at /app/models/carton_yolov8n.onnx — verification will fail.
```

and any verification raises `ModelMissing`, whose message names both the gateway directory and
the container mount. That is deliberate: with the model mounted rather than baked in, a
forgotten `Binds` entry and a file in the wrong directory look identical, and onnxruntime's own
error names only the path. A zero-byte file counts as missing too — a truncated copy would
otherwise load and then misbehave, which is harder to diagnose than an outright absence.

### Rebuilding the image

`az acr build` builds server-side, so no local Docker is needed:

```bash
az acr build --registry crtracktrashdevz3yo3x --image tracktrash/dockvision:2.1 --platform linux/amd64 .
```

Add `--no-logs` on a Windows console. The CLI can otherwise crash while *streaming* the build
log (`cp1252` cannot encode the output). The build itself is unaffected either way — check it
with `az acr task list-runs --registry crtracktrashdevz3yo3x --top 3 -o table`.

### Why the image is small now

`1.0` carried `ultralytics`, and therefore torch, because the detector loaded its model through
`YOLO(...)`. That is a dependency only the model **export** needs — inference needs an ONNX
session and some array arithmetic.

From `2.0`, `app/detector.py` runs `onnxruntime` directly and does its own decoding: letterbox,
threshold, and non-maximum suppression. That arithmetic is the part most likely to be subtly
wrong, so it sits in module-level functions covered by unit tests against synthetic tensors —
including the two export layouts, adjacent cartons that must not be merged, and duplicate boxes
on one carton that must be.

`ultralytics` now lives in `requirements-dev.txt`, commented, on the workstation that does the
export. `pytest` moved there too rather than shipping to every gateway.

To re-export a model for this detector:

```bash
yolo export model=carton_yolov8n.pt format=onnx imgsz=640 opset=12
```

Either layout works — plain, or `nms=True` for an end-to-end export. `decode()` inspects the
output shape rather than assuming, because guessing wrong yields a plausible but meaningless
count. For a GPU gateway, swap `onnxruntime` for `onnxruntime-gpu` and use a CUDA base image;
the detector already prefers `CUDAExecutionProvider` when the runtime offers it.

## 3. Camera commissioning checklist

| Item | Target |
|------|--------|
| Mount | Overhead, centered on the staging zone, lens parallel to the tray top |
| Height | 2.2–2.8 m above the floor (tune so a full tray fills ~70% of frame) |
| Field of view | Entire tray footprint + 20% margin; avoid neighbouring trays in frame |
| Lighting | ≥ 500 lux even, diffuse; no direct specular glare on carton tape/labels |
| Resolution | ≥ 1080p; fixed focus locked after commissioning |
| Shutter | Fast enough to freeze a tray sliding into the zone (≥ 1/250s) |

### QR size vs distance (rule of thumb)
A QR module must be ≥ 2 px to decode reliably. For a 1080p camera at 2.5 m over a ~1.2 m FOV (≈ 0.55 mm/px), a 25 mm QR (v3, ~29 modules → ~0.86 mm/module ≈ 1.5 px) is marginal — **use ≥ 35 mm carton QR** (≥ 2.2 px/module) or increase resolution. Trays use larger high-ECC codes, so they decode comfortably.

## 4. Verify

- Trigger a test verification (direct method `trigger`) and confirm a `DockVerification` message reaches IoT Hub.
- Place a known-good tray → expect `PASS`; remove a carton → expect `MISSING_CARTON` + an annotated frame in the `exceptions` blob container + relay pulse.

## 5. Drift monitoring

- 1% of `PASS` frames sampled to Blob for monthly re-validation (see Module 5 drift plan).
- Alert if PASS-rate or mean verification time deviates > 2σ from the rolling baseline.
