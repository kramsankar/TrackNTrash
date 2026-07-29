/**
 * Authentication for the console. Two sign-in methods, both supported:
 *
 *  1. Local username/password — POST /auth/login returns a JWT the API validates.
 *     Suited to shared warehouse devices where interactive AAD sign-in is awkward.
 *  2. Microsoft Entra ID (Azure AD) — MSAL redirect flow; the API validates the AAD token.
 *     Enabled when the deployment reports `entra: true` from /auth/config.
 *
 * The API advertises which methods are on via GET /auth/config, so the login screen
 * only offers what the deployment actually supports.
 */

const BASE = import.meta.env.VITE_API_BASE ?? "http://localhost:5090";
const STORAGE_KEY = "tnt.session";

export type Role = "Dispatcher" | "WarehouseManager" | "Admin";

export interface CurrentUser {
  name: string;
  upn: string;
  roles: Role[];
  getToken: () => string | undefined;
}

export interface Session {
  token: string;
  name: string;
  username: string;
  roles: Role[];
  expiresUtc: string;
  method: "local" | "entra";
}

export interface AuthConfig {
  local: boolean;
  entra: boolean;
  entraTenantId?: string | null;
  entraClientId?: string | null;
}

export async function fetchAuthConfig(): Promise<AuthConfig> {
  try {
    const r = await fetch(`${BASE}/auth/config`);
    if (!r.ok) throw new Error();
    return (await r.json()) as AuthConfig;
  } catch {
    return { local: false, entra: false };   // API unreachable / auth disabled
  }
}

export async function loginLocal(username: string, password: string): Promise<Session> {
  const r = await fetch(`${BASE}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });
  if (!r.ok) {
    const body = await r.json().catch(() => ({}));
    throw new Error((body as any).error ?? "Sign-in failed. Check your username and password.");
  }
  const d = await r.json();
  const session: Session = {
    token: d.token, name: d.name, username: d.username,
    roles: d.roles as Role[], expiresUtc: d.expiresUtc, method: "local",
  };
  saveSession(session);
  return session;
}

export function saveSession(s: Session) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(s));
}

export function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const s = JSON.parse(raw) as Session;
    if (new Date(s.expiresUtc).getTime() < Date.now()) { clearSession(); return null; }
    return s;
  } catch { return null; }
}

export function clearSession() {
  localStorage.removeItem(STORAGE_KEY);
}

export function userFromSession(s: Session): CurrentUser {
  return { name: s.name, upn: s.username, roles: s.roles, getToken: () => s.token };
}

/** Used when the deployment has no auth configured (open dev API). */
export function anonymousUser(): CurrentUser {
  return { name: "Dev User", upn: "dev@a-squaretechnologies.com", roles: ["Admin"], getToken: () => undefined };
}

export function canResolve(user: CurrentUser): boolean {
  return user.roles.includes("Admin") || user.roles.includes("WarehouseManager");
}
export function canEscalate(user: CurrentUser): boolean {
  return user.roles.length > 0;
}
