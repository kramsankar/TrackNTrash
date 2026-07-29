import { useCallback, useEffect, useState } from "react";
import { api } from "./api";
import { connectExceptionsHub } from "./signalr";
import type { ConsoleException } from "./types";
import {
  anonymousUser, clearSession, fetchAuthConfig, loadSession, userFromSession,
  type AuthConfig, type CurrentUser, type Session,
} from "./auth";
import { Login } from "./views/Login";
import { Dashboard } from "./views/Dashboard";
import { Orders } from "./views/Orders";
import { Trips } from "./views/Trips";
import { Manifests } from "./views/Manifests";
import { LineLookup } from "./views/LineLookup";
import { Assets } from "./views/Assets";
import { ExceptionsView } from "./views/ExceptionsView";

type View = "dashboard" | "orders" | "trips" | "manifests" | "assets" | "lookup" | "exceptions";

const MENU: { id: View; label: string; icon: string; group: string }[] = [
  { id: "dashboard", label: "Dashboard", icon: "▦", group: "Overview" },
  { id: "orders", label: "Orders", icon: "🧾", group: "Operations" },
  { id: "trips", label: "Trips & Loading", icon: "🚚", group: "Operations" },
  { id: "manifests", label: "Manifests (ASN)", icon: "📦", group: "Operations" },
  { id: "assets", label: "Asset Master", icon: "🗄️", group: "Operations" },
  { id: "lookup", label: "Line Lookup", icon: "🔎", group: "Operations" },
  { id: "exceptions", label: "Exceptions", icon: "⚠️", group: "Monitoring" },
];

export default function App() {
  const [authConfig, setAuthConfig] = useState<AuthConfig | null>(null);
  const [session, setSession] = useState<Session | null>(loadSession());
  const [view, setView] = useState<View>("dashboard");
  const [live, setLive] = useState(false);
  const [items, setItems] = useState<ConsoleException[]>([]);
  const [navOpen, setNavOpen] = useState(false);

  useEffect(() => { fetchAuthConfig().then(setAuthConfig); }, []);

  const authRequired = !!authConfig && (authConfig.local || authConfig.entra);
  const user: CurrentUser | null = session ? userFromSession(session) : (authRequired ? null : anonymousUser());

  const refresh = useCallback(() => {
    if (!user) return;
    api.openExceptions(user.getToken()).then(setItems).catch(() => { /* ignore */ });
  }, [user]);

  useEffect(() => {
    if (!user) return;
    refresh();
    const conn = connectExceptionsHub(
      (e) => setItems((prev) => [e, ...prev.filter((x) => x.id !== e.id)]),
      (e) => setItems((prev) => prev.map((x) => (x.id === e.id ? e : x))),
      setLive,
      () => user.getToken(),
    );
    return () => { conn.stop(); };
  }, [session, authConfig]);

  function signOut() { clearSession(); setSession(null); setItems([]); setLive(false); }
  function go(v: View) { setView(v); setNavOpen(false); }

  if (!authConfig) return <div className="boot">Loading…</div>;
  if (!user) return <Login config={authConfig} onSignedIn={setSession} />;

  const groups = [...new Set(MENU.map((m) => m.group))];
  const current = MENU.find((m) => m.id === view);

  return (
    <div className={`shell ${navOpen ? "nav-open" : ""}`}>
      {/* Mobile top bar */}
      <header className="topbar">
        <button className="burger" onClick={() => setNavOpen((o) => !o)} aria-label="Menu">☰</button>
        <span className="topbar-title">{current?.label ?? "TrackNTrash"}</span>
        <span className={`live ${live ? "on" : "off"}`}>{live ? "●" : "○"}</span>
      </header>

      {navOpen && <div className="nav-scrim" onClick={() => setNavOpen(false)} />}

      <aside className="sidebar">
        <div className="brand">TrackNTrash</div>
        <div className="brand-sub">Dispatch Track &amp; Trace</div>
        {groups.map((g) => (
          <div key={g} className="nav-group">
            <div className="nav-group-title">{g}</div>
            {MENU.filter((m) => m.group === g).map((m) => (
              <button key={m.id} className={`nav-item ${view === m.id ? "active" : ""}`} onClick={() => go(m.id)}>
                <span className="nav-icon">{m.icon}</span>{m.label}
                {m.id === "exceptions" && items.length > 0 && <span className="nav-badge">{items.length}</span>}
              </button>
            ))}
          </div>
        ))}
        <div className="sidebar-foot">
          <div className="foot-row">
            <span className={`live ${live ? "on" : "off"}`}>{live ? "● live" : "○ offline"}</span>
            <span className="user">{user.name}<br /><small>{user.roles.join(", ") || "no role"}</small></span>
          </div>
          {session && <button className="signout" onClick={signOut}>Sign out</button>}
        </div>
      </aside>

      <main className="content">
        {view === "dashboard" && <Dashboard items={items} onNavigate={(v) => go(v as View)} user={user} />}
        {view === "orders" && <Orders user={user} />}
        {view === "trips" && <Trips user={user} />}
        {view === "manifests" && <Manifests user={user} />}
        {view === "assets" && <Assets user={user} />}
        {view === "lookup" && <LineLookup user={user} />}
        {view === "exceptions" && <ExceptionsView items={items} user={user} refresh={refresh} />}
      </main>
    </div>
  );
}
