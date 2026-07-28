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
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}

export const api = {
  async list(filters: Filters, token?: string): Promise<ConsoleException[]> {
    return json(await fetch(`${BASE}/console/exceptions${qs(filters)}`, { headers: auth(token) }));
  },
  async get(id: number, token?: string): Promise<ExceptionDetail> {
    return json(await fetch(`${BASE}/console/exceptions/${id}`, { headers: auth(token) }));
  },
  async acknowledge(id: number, user: string, token?: string) {
    return action(id, "acknowledge", { user }, token);
  },
  async resolve(id: number, user: string, reasonCode: string, note: string, token?: string) {
    return action(id, "resolve", { user, reasonCode, note }, token);
  },
  async escalate(id: number, user: string, token?: string) {
    return action(id, "escalate", { user }, token);
  },
};

async function action(id: number, verb: string, body: object, token?: string) {
  const res = await fetch(`${BASE}/console/exceptions/${id}/${verb}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...auth(token) },
    body: JSON.stringify(body),
  });
  return json<ConsoleException>(res);
}

function auth(token?: string): Record<string, string> {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export const API_BASE = BASE;
