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
import { LANGS, getLang, setLang, t, tRef, loadReference, type Lang } from "./i18n";

/** Menu entries use the same formId the permission matrix is keyed on. */
const MENU: { id: string; label: string; icon: string; group: string }[] = [
  { id: "dashboard", label: "nav.dashboard", icon: "▦", group: "Overview" },
  { id: "orders", label: "nav.orders", icon: "🧾", group: "Operations" },
  { id: "trips", label: "nav.trips", icon: "🚚", group: "Operations" },
  { id: "manifests", label: "nav.manifests", icon: "📦", group: "Operations" },
  { id: "assets", label: "nav.assets", icon: "🗄️", group: "Operations" },
  { id: "lookup", label: "nav.lookup", icon: "🔎", group: "Operations" },
  { id: "items", label: "nav.items", icon: "🔢", group: "Inspection" },
  { id: "cameras", label: "nav.cameras", icon: "📷", group: "Inspection" },
  { id: "exceptions", label: "nav.exceptions", icon: "⚠️", group: "Monitoring" },
  { id: "m_product", label: "nav.products", icon: "🏷️", group: "Masters" },
  { id: "m_store", label: "nav.stores", icon: "🏬", group: "Masters" },
  { id: "m_zone", label: "nav.zones", icon: "🗺️", group: "Masters" },
  { id: "m_rack", label: "nav.racks", icon: "🧱", group: "Masters" },
  { id: "m_vehicle", label: "nav.vehicles", icon: "🚛", group: "Masters" },
  { id: "m_device", label: "nav.devices", icon: "📟", group: "Masters" },
  { id: "m_role", label: "nav.roles", icon: "🎭", group: "Administration" },
  { id: "m_user", label: "nav.users", icon: "👤", group: "Administration" },
  { id: "m_mapping", label: "nav.mapping", icon: "🔐", group: "Administration" },
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
  // Language lives here so one change re-renders the whole shell. The reference bundle
  // (states, exception types) is fetched per language and cached.
  const [lang, setLangState] = useState<Lang>(getLang());
  const [, setRefLoaded] = useState(0);
  useEffect(() => {
    // The bundle is per language and cached; bumping a counter re-renders once it lands so
    // translated states appear without the user touching anything again.
    loadReference(lang, user?.getToken()).then(() => setRefLoaded((n) => n + 1));
  }, [lang, session]);
  function changeLang(next: Lang) {
    setLang(next);
    setLangState(next);
    loadReference(next, user?.getToken()).then(() => setRefLoaded((n) => n + 1));
  }

  const visible = MENU.filter((m) => canView(m.id));
  const groups = [...new Set(visible.map((m) => m.group))];
  const current = MENU.find((m) => m.id === view);

  return (
    <div className={`shell ${navOpen ? "nav-open" : ""}`}>
      <header className="topbar">
        <button className="burger" onClick={() => setNavOpen((o) => !o)} aria-label={t("nav.menu", lang)}>☰</button>
        <span className="topbar-title">{current ? t(current.label, lang) : t("app.name", lang)}</span>
        <select className="lang-select" value={lang} aria-label={t("label.language", lang)}
                onChange={(e) => changeLang(e.target.value as Lang)}>
          {LANGS.map((l) => <option key={l.code} value={l.code}>{l.native}</option>)}
        </select>
        <span className={`live ${live ? "on" : "off"}`}>{live ? "●" : "○"}</span>
      </header>

      {navOpen && <div className="nav-scrim" onClick={() => setNavOpen(false)} />}

      <aside className="sidebar">
        <div className="brand">{t("app.name", lang)}</div>
        <div className="brand-sub">{t("app.tagline", lang)}</div>
        {groups.map((g) => (
          <div key={g} className="nav-group">
            <div className="nav-group-title">{t(`group.${g}`, lang)}</div>
            {visible.filter((m) => m.group === g).map((m) => (
              <button key={m.id} className={`nav-item ${view === m.id ? "active" : ""}`} onClick={() => go(m.id)}>
                <span className="nav-icon">{m.icon}</span>{t(m.label, lang)}
                {m.id === "exceptions" && items.length > 0 && <span className="nav-badge">{items.length}</span>}
              </button>
            ))}
          </div>
        ))}
        <div className="sidebar-foot">
          <div className="foot-row">
            <span className={`live ${live ? "on" : "off"}`}>{live ? `● ${t("state.live", lang)}` : `○ ${t("state.offline", lang)}`}</span>
            <span className="user">{user.name}<br /><small>{user.roles.map((r) => tRef("role", r, lang)).join(", ") || t("label.noRole", lang)}</small></span>
          </div>
          <button className="signout" onClick={signOut}>{t("action.signOut", lang)}</button>
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
