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

## Deploying it to a camera

Credentials come from the environment, **never from the module twin** — twin desired
properties are readable in the Azure portal by anyone with reader access on the IoT Hub.

For IoT Edge, set them as module environment variables in the deployment manifest:

```json
{
  "modules": {
    "visionModule": {
      "env": {
        "TNT_API_USERNAME": { "value": "camera-agent" },
        "TNT_API_PASSWORD": { "value": "<from Key Vault>" }
      }
    }
  }
}
```

Non-secret settings still belong in the twin:

```json
{
  "properties.desired": {
    "apiBaseUrl": "https://app-tracking-tracktrash-dev-4ymqn2.azurewebsites.net",
    "cameraCode": "CAM-DOCK-1",
    "heartbeatSeconds": 60
  }
}
```

`manifestSyncUrl` is derived from `apiBaseUrl`, so the two cannot drift and point at
different environments. Set it explicitly only if the manifest feed genuinely lives
elsewhere.

## Token handling

Nobody is present to retype a password, so the module holds the credentials and fetches a
token on demand: on first use, when the current one is within five minutes of expiry, and
once more if the server still answers `401`. A second `401` raises `AuthError` rather than
retrying, because wrong credentials do not fix themselves.

That failure is loud on purpose. A silent auth failure would leave the dock verifying every
tray against an empty expected-count table — passing everything, catching nothing.

## Rotating the password

```bash
curl -X POST "$API/auth/users" \
  -H "Content-Type: application/json" \
  -H "x-setup-key: $SETUP_KEY" \
  -d '{"username":"camera-agent","displayName":"Dock camera service account","password":"<new>","roles":"CameraDevice"}'
```

Then update `TNT_API_PASSWORD` in the deployment manifest. Existing tokens stay valid until
they expire (12 hours), so cameras keep working through the rollout.

Use alphanumeric passwords. A `%` in a password has already broken one toolchain in this
project.

## One account or one per camera?

One shared account is what is set up here, and it is the right default: cameras are
identified by `cameraCode` in the heartbeat, so the console still tells them apart. Move to
one account per camera only if you need to revoke a single unit without touching the rest —
at which point give each the same `CameraDevice` role.
