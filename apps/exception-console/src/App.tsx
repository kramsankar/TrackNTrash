import { useCallback, useEffect, useState } from "react";
import { api, type MappingRow } from "./api";
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
import { Items } from "./views/Items";
import { Cameras } from "./views/Cameras";
import { ExceptionsView } from "./views/ExceptionsView";
import { MasterScreen } from "./views/MasterScreen";
import { MASTER_CONFIGS } from "./views/masterConfigs";
import { Users } from "./views/Users";
import { RoleMapping } from "./views/RoleMapping";

/** Menu entries use the same formId the permission matrix is keyed on. */
const MENU: { id: string; label: string; icon: string; group: string }[] = [
  { id: "dashboard", label: "Dashboard", icon: "▦", group: "Overview" },
  { id: "orders", label: "Orders", icon: "🧾", group: "Operations" },
  { id: "trips", label: "Trips & Loading", icon: "🚚", group: "Operations" },
  { id: "manifests", label: "Manifests (ASN)", icon: "📦", group: "Operations" },
  { id: "assets", label: "Asset Master", icon: "🗄️", group: "Operations" },
  { id: "lookup", label: "Line Lookup", icon: "🔎", group: "Operations" },
  { id: "items", label: "Item Counting", icon: "🔢", group: "Inspection" },
  { id: "cameras", label: "Cameras & Map", icon: "📷", group: "Inspection" },
  { id: "exceptions", label: "Exceptions", icon: "⚠️", group: "Monitoring" },
  { id: "m_product", label: "Products", icon: "🏷️", group: "Masters" },
  { id: "m_store", label: "Stores", icon: "🏬", group: "Masters" },
  { id: "m_zone", label: "Zones", icon: "🗺️", group: "Masters" },
  { id: "m_rack", label: "Racks", icon: "🧱", group: "Masters" },
  { id: "m_vehicle", label: "Vehicles", icon: "🚛", group: "Masters" },
  { id: "m_device", label: "Devices", icon: "📟", group: "Masters" },
  { id: "m_role", label: "Roles", icon: "🎭", group: "Administration" },
  { id: "m_user", label: "Users", icon: "👤", group: "Administration" },
  { id: "m_mapping", label: "Role Mapping", icon: "🔐", group: "Administration" },
];

export default function App() {
  const [authConfig, setAuthConfig] = useState<AuthConfig | null>(null);
  const [session, setSession] = useState<Session | null>(loadSession());
  const [view, setView] = useState<string>("dashboard");
  const [live, setLive] = useState(false);
  const [items, setItems] = useState<ConsoleException[]>([]);
  const [navOpen, setNavOpen] = useState(false);
  const [perms, setPerms] = useState<MappingRow[] | null>(null);

  useEffect(() => { fetchAuthConfig().then(setAuthConfig); }, []);

  const authRequired = !!authConfig && (authConfig.local || authConfig.entra);
  const user: CurrentUser | null = session ? userFromSession(session) : (authRequired ? null : anonymousUser());

  const refresh = useCallback(() => {
    if (!user) return;
    api.openExceptions(user.getToken()).then(setItems).catch(() => { /* ignore */ });
  }, [user]);

  // Pull this user's permission matrix so the menu only offers what they may open.
  useEffect(() => {
    if (!user) { setPerms(null); return; }
    api.myPermissions(user.upn, user.getToken())
      .then(setPerms)
      .catch(() => setPerms([]));   // no RBAC configured → fall back to showing everything
  }, [session, authConfig]);

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

  function signOut() { clearSession(); setSession(null); setItems([]); setLive(false); setPerms(null); }
  function go(v: string) { setView(v); setNavOpen(false); }

  if (!authConfig) return <div className="boot">Loading…</div>;
  if (!user) return <Login config={authConfig} onSignedIn={setSession} />;

  // An empty matrix means RBAC isn't set up for this user — show everything rather than
  // locking them out of their own console.
  const canView = (formId: string) =>
    !perms || perms.length === 0 || perms.some((p) => p.formId === formId && p.canView);
  const visible = MENU.filter((m) => canView(m.id));
  const groups = [...new Set(visible.map((m) => m.group))];
  const current = MENU.find((m) => m.id === view);

  return (
    <div className={`shell ${navOpen ? "nav-open" : ""}`}>
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
            {visible.filter((m) => m.group === g).map((m) => (
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
          <button className="signout" onClick={signOut}>Sign out</button>
        </div>
      </aside>

      <main className="content">
        {!canView(view) ? (
          <div className="view"><h2>No access</h2>
            <p className="muted">Your role doesn’t have permission to open this screen.</p></div>
        ) : (
          <>
            {view === "dashboard" && <Dashboard items={items} onNavigate={go} user={user} />}
            {view === "orders" && <Orders user={user} />}
            {view === "trips" && <Trips user={user} />}
            {view === "manifests" && <Manifests user={user} />}
            {view === "assets" && <Assets user={user} />}
            {view === "items" && <Items user={user} />}
            {view === "cameras" && <Cameras user={user} />}
            {view === "lookup" && <LineLookup user={user} />}
            {view === "exceptions" && <ExceptionsView items={items} user={user} refresh={refresh} />}
            {view === "m_user" && <Users user={user} />}
            {view === "m_mapping" && <RoleMapping user={user} />}
            {MASTER_CONFIGS[view] && <MasterScreen config={MASTER_CONFIGS[view]} user={user} />}
          </>
        )}
      </main>
    </div>
  );
}
