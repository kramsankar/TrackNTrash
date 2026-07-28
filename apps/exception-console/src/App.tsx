import { useCallback, useEffect, useState } from "react";
import { api } from "./api";
import { connectExceptionsHub } from "./signalr";
import type { ConsoleException } from "./types";
import { useDevUser } from "./auth";
import { Dashboard } from "./views/Dashboard";
import { Orders } from "./views/Orders";
import { Trips } from "./views/Trips";
import { Manifests } from "./views/Manifests";
import { LineLookup } from "./views/LineLookup";
import { ExceptionsView } from "./views/ExceptionsView";

type View = "dashboard" | "orders" | "trips" | "manifests" | "lookup" | "exceptions";

const MENU: { id: View; label: string; icon: string; group: string }[] = [
  { id: "dashboard", label: "Dashboard", icon: "▦", group: "Overview" },
  { id: "orders", label: "Orders", icon: "🧾", group: "Operations" },
  { id: "trips", label: "Trips & Loading", icon: "🚚", group: "Operations" },
  { id: "manifests", label: "Manifests (ASN)", icon: "📦", group: "Operations" },
  { id: "lookup", label: "Line Lookup", icon: "🔎", group: "Operations" },
  { id: "exceptions", label: "Exceptions", icon: "⚠️", group: "Monitoring" },
];

export default function App() {
  const user = useDevUser();
  const [view, setView] = useState<View>("dashboard");
  const [live, setLive] = useState(false);
  const [items, setItems] = useState<ConsoleException[]>([]);

  // App-level exception feed: one SignalR connection for the whole app, so the live
  // indicator and the shared exception list are accurate on every screen.
  const refresh = useCallback(() => {
    api.openExceptions(user.getToken()).then(setItems).catch(() => { /* ignore */ });
  }, [user]);

  useEffect(() => {
    refresh();
    const conn = connectExceptionsHub(
      (e) => setItems((prev) => [e, ...prev.filter((x) => x.id !== e.id)]),
      (e) => setItems((prev) => prev.map((x) => (x.id === e.id ? e : x))),
      setLive
    );
    return () => { conn.stop(); };
  }, []);

  const groups = [...new Set(MENU.map((m) => m.group))];

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">TrackNTrash</div>
        <div className="brand-sub">Dispatch Track &amp; Trace</div>
        {groups.map((g) => (
          <div key={g} className="nav-group">
            <div className="nav-group-title">{g}</div>
            {MENU.filter((m) => m.group === g).map((m) => (
              <button key={m.id} className={`nav-item ${view === m.id ? "active" : ""}`} onClick={() => setView(m.id)}>
                <span className="nav-icon">{m.icon}</span>{m.label}
                {m.id === "exceptions" && items.length > 0 && <span className="nav-badge">{items.length}</span>}
              </button>
            ))}
          </div>
        ))}
        <div className="sidebar-foot">
          <span className={`live ${live ? "on" : "off"}`}>{live ? "● live" : "○ offline"}</span>
          <span className="user">{user.name}<br /><small>{user.roles.join(", ")}</small></span>
        </div>
      </aside>

      <main className="content">
        {view === "dashboard" && <Dashboard items={items} onNavigate={(v) => setView(v as View)} />}
        {view === "orders" && <Orders user={user} />}
        {view === "trips" && <Trips user={user} />}
        {view === "manifests" && <Manifests user={user} />}
        {view === "lookup" && <LineLookup user={user} />}
        {view === "exceptions" && <ExceptionsView items={items} user={user} refresh={refresh} />}
      </main>
    </div>
  );
}
