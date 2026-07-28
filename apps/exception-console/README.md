# Exception Console — Module 12

Lightweight **React (Vite + TypeScript)** ops console over the Module 6 tracking API, with **live SignalR updates**, evidence view, event timeline, one-click actions, audit, and Entra ID auth.

## Features

- **Open-exceptions grid** — filter by checkpoint / severity / status / route; sorted by severity then age; colour-coded severity badges; age formatted (m/h/d).
- **Detail pane** — annotated dock-camera frame or receiving photo, the full **event timeline** for the affected order line, and the **audit** trail.
- **One-click actions** — Acknowledge, Resolve (reason code + note), Escalate (→ Teams post). Every action is audited with user + timestamp.
- **Live** — SignalR (`/hubs/exceptions`) pushes `exceptionRaised` / `exceptionUpdated`; the grid updates with no polling. A live/offline indicator shows connection state.
- **Auth** — Entra ID; roles Dispatcher / Warehouse Manager / Admin gate the action buttons (`auth.ts`).

## Backend additions (in the tracking API)

| File | Role |
|------|------|
| `Console/ExceptionsHub.cs` | SignalR hub (`/hubs/exceptions`) |
| `Console/ConsoleExceptionStore.cs` | In-memory exception records + audit |
| `Console/SignalRExceptionRelay.cs` | `INotificationPublisher` decorator → store + hub push |
| `Program.cs` | `/console/exceptions*` endpoints, hub map, CORS |

## Run

Start the tracking API (Module 6) first, then:

```bash
npm install
npm run dev        # http://localhost:5173  (CORS-allowed by the API)
```

Env (optional `.env`):
```
VITE_API_BASE=http://localhost:5090
VITE_ENTRA_CLIENT_ID=...
VITE_ENTRA_TENANT_ID=...
VITE_API_SCOPE=api://tracktrash-tracking/.default
```

## Build

```bash
npm run build      # tsc -b && vite build → dist/
```

## Structure

```
src/
├── App.tsx                 layout, live wiring, filter state
├── api.ts                  REST client
├── signalr.ts              live hub connection
├── auth.ts                 Entra config + role gates (dev stub user)
├── types.ts
└── components/
    ├── Filters.tsx
    ├── ExceptionGrid.tsx
    └── ExceptionDetail.tsx  evidence + timeline + actions + audit
```

## Production notes

- Wire `@azure/msal-browser` in `auth.ts`; pass the acquired token via the `tokenFactory` to `api`/`signalr`. Gate `Resolve`/`Escalate` on role claims (already stubbed in `auth.ts`).
- `Escalate` posts to a Teams channel via a Service Bus subscriber on the exceptions topic (Module 6 notifications) — the console action flags the exception; the actual Teams post is server-side.
- The `/blob?uri=` image route should mint a short-lived read SAS server-side (see Module 8 storage design); never expose account keys.
