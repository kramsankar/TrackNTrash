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
async function del<T>(path: string, token?: string): Promise<T> {
  return json<T>(await fetch(`${BASE}${path}`, { method: "DELETE", headers: auth(token) }));
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
  listOrders: (token?: string) => get<OrderRow[]>(`/orders`, token),

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

  // ---- assets ----
  listAssets: (token?: string) => get<AssetRow[]>(`/assets`, token),
  assetSummary: (token?: string) => get<AssetSummary>(`/assets/summary`, token),
  registerAssets: (siteCode: string, count: number, token?: string) => post<{ registered: number; trayQrs: string[] }>(`/assets/register`, { siteCode, count }, token),

  // ---- items ----
  listCartons: (token?: string) => get<CartonRow[]>(`/cartons`, token),
  createCarton: (body: CartonSetupReq, token?: string) => post<any>(`/cartons`, body, token),
  listItemCounts: (token?: string) => get<ItemCountRow[]>(`/items/counts`, token),
  recordItemCount: (body: ItemCountReq, token?: string) => post<CountResult>(`/items/count`, body, token),

  // ---- cameras ----
  listCameras: (token?: string) => get<CameraRow[]>(`/cameras`, token),
  upsertCamera: (body: CameraReq, token?: string) => post<{ cameraId: number }>(`/cameras`, body, token),
  placeCamera: (cameraId: number, body: PlacementReq, token?: string) => post<any>(`/cameras/${cameraId}/placement`, body, token),
  listSiteMaps: (token?: string) => get<SiteMapRow[]>(`/sitemaps`, token),
  upsertSiteMap: (body: SiteMapReq, token?: string) => post<{ siteMapId: number }>(`/sitemaps`, body, token),

  // ---- generic masters ----
  listMaster: (key: string, token?: string) => get<MasterRecord[]>(`/masters/${key}`, token),
  createMaster: (key: string, body: MasterRecord, token?: string) => post<{ id: number }>(`/masters/${key}`, body, token),
  updateMaster: (key: string, id: number, body: MasterRecord, token?: string) => put<any>(`/masters/${key}/${id}`, body, token),
  deleteMaster: (key: string, id: number, token?: string) => del<any>(`/masters/${key}/${id}`, token),

  // ---- RBAC ----
  listForms: (token?: string) => get<FormRow[]>(`/rbac/forms`, token),
  listMappings: (roleId?: number, token?: string) => get<MappingRow[]>(`/rbac/mappings${roleId ? `?roleId=${roleId}` : ""}`, token),
  saveMapping: (body: MappingRow, token?: string) => post<any>(`/rbac/mappings`, body, token),
  listUsers: (token?: string) => get<UserRow[]>(`/rbac/users`, token),
  saveUser: (body: SaveUserReq, token?: string) => post<{ userId: number }>(`/rbac/users`, body, token),
  myPermissions: (username: string, token?: string) => get<MappingRow[]>(`/rbac/permissions?username=${encodeURIComponent(username)}`, token),

  // ---- admin ----
  runSweep: (token?: string) => post<any>(`/admin/sweep`, {}, token),
  health: () => get<{ status: string; service: string }>(`/health`),
};

export const API_BASE = BASE;

// ---- request/response shapes ----
export interface OrderReq { orderNumber: string; storeCode: string; erpReference?: string; lines: { lineNumber: number; gtin: string; orderedQty: number; uom?: string; expectedCartonCount: number; erpLineReference?: string }[]; }
export interface OrderResp { orderNumber: string; storeCode: string; orderLineIds: number[]; }
export interface OrderRow { orderLineId: number; orderNumber: string; storeCode: string; erpReference?: string; lineNumber: number; gtin: string; orderedQty: number; expectedCartonCount: number; currentState: string; receivedCartons: number; createdUtc: string; }
export interface ScanReq { clientEventId: string; deviceId: string; eventType: string; checkpoint?: string; orderLineId?: number; trayQr?: string; verdict?: string; }
export interface ScanResp { accepted: boolean; duplicate: boolean; scanEventId?: number; newState?: string; transitionLegal: boolean; exceptions: { type: string }[]; }
export interface LineState { orderLineId: number; currentState: string; previousState?: string; lastEventId?: number; pickedCartons: number; receivedCartons: number; stateEnteredUtc: string; }
export interface TripReq { vehicleReg: string; driverName?: string; routeCode?: string; stops: { sequence: number; storeCode: string }[]; plannedTrays: { trayQr: string; stopSequence: number; orderLineIds: number[] }[]; }
export interface TripResp { tripNumber: string; manifestQr: string; status: string; stops: number; trays: number; }
export interface ManifestReq { trayQr: string; tripId?: number; expectedCartonCount: number; expectedCartonPayloads: string[]; }
export interface ManifestRow { trayQr: string; tripId?: number; expectedCartonCount: number; expectedCartonPayloads: string[]; updatedUtc: string; }
export interface AssetRow { trayId: number; trayQr: string; siteCode: string; trayStatus: string; currentCustodianType: string; currentCustodianRef?: string; lastSeenUtc?: string; createdUtc: string; }
export interface AssetSummary { total: number; available: number; inUse: number; inTransit: number; atStore: number; lost: number; }

// ---- items ----
export interface CartonRow { cartonId: number; serial: string; gtin: string; expectedItemCount: number; itemIdentification: string; registeredItems: number; status: string; }
export interface CartonSetupReq { orderLineId: number; gtin: string; serial: string; expectedItemCount: number; itemIdentification: string; items?: { barcode: string; gtin?: string; description?: string }[]; }
export interface ItemCountRow { itemCountId: number; cartonId: number; cartonSerial: string; checkpoint?: string; expectedCount: number; scannedCount: number; visionCount?: number; cameraCode?: string; verdict: string; confidence?: number; observedUtc: string; }
export interface ItemCountReq { cartonId: number; checkpoint?: string; scannedBarcodes: string[]; visionCount?: number | null; cameraId?: number | null; frameBlobUri?: string; confidence?: number; deviceId?: string; }
export interface CountResult { itemCountId: number; expected: number; scanned: number; vision?: number; verdict: string; detail: string; }

// ---- cameras ----
export interface CameraRow { cameraId: number; cameraCode: string; name: string; cameraKind: string; siteCode: string; zone?: string; station?: string; checkpoint?: string; rtspUrl?: string; purpose: string; status: string; lastSeenUtc?: string; x?: number; y?: number; headingDeg?: number; siteMapId?: number; }
export interface CameraReq { cameraCode: string; name: string; cameraKind: string; siteCode: string; zone?: string; station?: string; checkpoint?: string; rtspUrl?: string; purpose: string; status: string; }
export interface PlacementReq { siteMapId: number; x: number; y: number; headingDeg?: number; }
export interface SiteMapRow { siteMapId: number; siteCode: string; name: string; imageUri?: string; width: number; height: number; }
export interface SiteMapReq { siteCode: string; name: string; imageUri?: string; width?: number; height?: number; }

// ---- masters / RBAC ----
export type MasterRecord = Record<string, any>;
export interface FormRow { formId: string; formName: string; formGroup: string; sortOrder: number; }
export interface MappingRow { roleId: number; roleName?: string; formId: string; canView: boolean; canCreate: boolean; canEdit: boolean; canDelete: boolean; }
export interface UserRow { userId: number; username: string; displayName: string; email?: string; roleId?: number; roleName?: string; siteCode?: string; isActive: boolean; lastLoginUtc?: string; }
export interface SaveUserReq { userId?: number; username: string; displayName: string; email?: string; roleId?: number; siteCode?: string; password?: string; isActive: boolean; }
