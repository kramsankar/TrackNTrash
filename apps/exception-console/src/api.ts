import type { ConsoleException, ExceptionDetail, Filters } from "./types";

const BASE = import.meta.env.VITE_API_BASE ?? "http://localhost:5090";

function qs(f: Filters): string {
  const p = new URLSearchParams();
  if (f.checkpoint) p.set("checkpoint", f.checkpoint);
  if (f.severity) p.set("severity", f.severity);
  if (f.status) p.set("status", f.status);
  if (f.route) p.set("route", f.route);
  const s = p.toString();
  return s ? `?${s}` : "";
}

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(`${res.status} ${await res.text().catch(() => res.statusText)}`);
  return res.json() as Promise<T>;
}

function auth(token?: string): Record<string, string> {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function post<T>(path: string, body: unknown, token?: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...auth(token) },
    body: JSON.stringify(body),
  });
  return json<T>(res);
}
async function put<T>(path: string, body: unknown, token?: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...auth(token) },
    body: JSON.stringify(body),
  });
  return json<T>(res);
}
async function get<T>(path: string, token?: string): Promise<T> {
  return json<T>(await fetch(`${BASE}${path}`, { headers: auth(token) }));
}

export const api = {
  // ---- exceptions / console ----
  async list(filters: Filters, token?: string): Promise<ConsoleException[]> {
    return get(`/console/exceptions${qs(filters)}`, token);
  },
  async get(id: number, token?: string): Promise<ExceptionDetail> {
    return get(`/console/exceptions/${id}`, token);
  },
  acknowledge: (id: number, user: string, token?: string) => post<ConsoleException>(`/console/exceptions/${id}/acknowledge`, { user }, token),
  resolve: (id: number, user: string, reasonCode: string, note: string, token?: string) => post<ConsoleException>(`/console/exceptions/${id}/resolve`, { user, reasonCode, note }, token),
  escalate: (id: number, user: string, token?: string) => post<ConsoleException>(`/console/exceptions/${id}/escalate`, { user }, token),
  openExceptions: (token?: string) => get<ConsoleException[]>(`/console/exceptions`, token),

  // ---- orders ----
  createOrder: (body: OrderReq, token?: string) => post<OrderResp>(`/orders`, body, token),

  // ---- events ----
  scan: (body: ScanReq, token?: string) => post<ScanResp>(`/events/scan`, body, token),
  lineState: (id: number, token?: string) => get<LineState>(`/shipment-lines/${id}/state`, token),

  // ---- trips ----
  createTrip: (body: TripReq, token?: string) => post<TripResp>(`/trips`, body, token),
  getTrip: (n: string, token?: string) => get<any>(`/trips/${n}`, token),
  loadTray: (n: string, trayQr: string, deviceId: string, token?: string) => post<any>(`/trips/${n}/load`, { trayQr, deviceId }, token),
  depart: (tripNumber: string, token?: string) => post<any>(`/events/telemetry`, { tripNumber, event: "depart", deviceId: "admin-console" }, token),

  // ---- manifests ----
  upsertManifest: (body: ManifestReq, token?: string) => put<any>(`/manifests`, body, token),
  manifests: (sinceIso: string, token?: string) => get<{ count: number; manifests: ManifestRow[] }>(`/manifests?since=${encodeURIComponent(sinceIso)}`, token),

  // ---- admin ----
  runSweep: (token?: string) => post<any>(`/admin/sweep`, {}, token),
  health: () => get<{ status: string; service: string }>(`/health`),
};

export const API_BASE = BASE;

// ---- request/response shapes ----
export interface OrderReq { orderNumber: string; storeCode: string; erpReference?: string; lines: { lineNumber: number; gtin: string; orderedQty: number; uom?: string; expectedCartonCount: number; erpLineReference?: string }[]; }
export interface OrderResp { orderNumber: string; storeCode: string; orderLineIds: number[]; }
export interface ScanReq { clientEventId: string; deviceId: string; eventType: string; checkpoint?: string; orderLineId?: number; trayQr?: string; verdict?: string; }
export interface ScanResp { accepted: boolean; duplicate: boolean; scanEventId?: number; newState?: string; transitionLegal: boolean; exceptions: { type: string }[]; }
export interface LineState { orderLineId: number; currentState: string; previousState?: string; lastEventId?: number; pickedCartons: number; receivedCartons: number; stateEnteredUtc: string; }
export interface TripReq { vehicleReg: string; driverName?: string; routeCode?: string; stops: { sequence: number; storeCode: string }[]; plannedTrays: { trayQr: string; stopSequence: number; orderLineIds: number[] }[]; }
export interface TripResp { tripNumber: string; manifestQr: string; status: string; stops: number; trays: number; }
export interface ManifestReq { trayQr: string; tripId?: number; expectedCartonCount: number; expectedCartonPayloads: string[]; }
export interface ManifestRow { trayQr: string; tripId?: number; expectedCartonCount: number; expectedCartonPayloads: string[]; updatedUtc: string; }
