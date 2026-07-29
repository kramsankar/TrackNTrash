import { useCallback, useEffect, useState } from "react";
import { api, type UserRow, type MasterRecord } from "../api";
import type { CurrentUser } from "../auth";
import { EnterpriseGrid, text, badge, category } from "../vendor/enterprise-grid";
import "../vendor/enterprise-grid/enterprise-grid.css";
import "../grid-dark.css";

const columns = [
  text<UserRow>("username", "Username", { pinned: "left" }),
  text<UserRow>("displayName", "Display name"),
  category<UserRow>("roleName", "Role"),
  text<UserRow>("email", "Email"),
  category<UserRow>("siteCode", "Site"),
  badge<UserRow>("isActive", "Active", { true: "green", false: "slate" }),
];

const EMPTY = { username: "", displayName: "", email: "", roleId: "" as number | "", siteCode: "", password: "", isActive: true };

export function Users({ user }: { user: CurrentUser }) {
  const [rows, setRows] = useState<UserRow[]>([]);
  const [roles, setRoles] = useState<MasterRecord[]>([]);
  const [editing, setEditing] = useState<UserRow | null>(null);
  const [draft, setDraft] = useState({ ...EMPTY });
  const [msg, setMsg] = useState("");

  const refresh = useCallback(() => {
    api.listUsers(user.getToken()).then(setRows).catch(() => setRows([]));
    api.listMaster("role", user.getToken()).then(setRoles).catch(() => setRoles([]));
  }, [user]);
  useEffect(() => { refresh(); }, [refresh]);

  function startEdit(r: UserRow) {
    setEditing(r);
    setDraft({ username: r.username, displayName: r.displayName, email: r.email ?? "",
      roleId: r.roleId ?? "", siteCode: r.siteCode ?? "", password: "", isActive: r.isActive });
    setMsg("");
  }
  function startNew() { setEditing(null); setDraft({ ...EMPTY }); setMsg(""); }

  async function save() {
    if (!draft.username.trim()) { setMsg("❌ Username required"); return; }
    if (!editing && !draft.password) { setMsg("❌ A password is required for a new user"); return; }
    try {
      await api.saveUser({
        userId: editing?.userId, username: draft.username.trim(),
        displayName: draft.displayName || draft.username, email: draft.email || undefined,
        roleId: draft.roleId === "" ? undefined : Number(draft.roleId),
        siteCode: draft.siteCode || undefined,
        password: draft.password || undefined, isActive: draft.isActive,
      }, user.getToken());
      setMsg(editing ? "✅ User updated" : "✅ User created");
      if (!editing) startNew();
      refresh();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
  }

  return (
    <div className="view">
      <div className="view-head">
        <div><h2>Users</h2><p className="muted">People who can sign in. A user holds one role; the role decides what they can do.</p></div>
        <button onClick={refresh}>↻ Refresh</button>
      </div>

      <div className="card">
        <h3>{editing ? `Edit ${editing.username}` : "New user"}</h3>
        <div className="form-grid">
          <label>Username<input value={draft.username} disabled={!!editing}
            onChange={(e) => setDraft({ ...draft, username: e.target.value })} /></label>
          <label>Display name<input value={draft.displayName}
            onChange={(e) => setDraft({ ...draft, displayName: e.target.value })} /></label>
          <label>Email<input value={draft.email}
            onChange={(e) => setDraft({ ...draft, email: e.target.value })} /></label>
          <label>Role
            <select value={draft.roleId} onChange={(e) => setDraft({ ...draft, roleId: e.target.value === "" ? "" : +e.target.value })}>
              <option value="">—</option>
              {roles.map((r) => <option key={r.roleId} value={r.roleId}>{r.roleName}</option>)}
            </select>
          </label>
          <label>Site / store code<input value={draft.siteCode} placeholder="e.g. LDN1 or S-LDN1"
            onChange={(e) => setDraft({ ...draft, siteCode: e.target.value })} /></label>
          <label>{editing ? "New password (blank = unchanged)" : "Password"}
            <input type="password" value={draft.password} autoComplete="new-password"
              onChange={(e) => setDraft({ ...draft, password: e.target.value })} /></label>
          <label>Active
            <input type="checkbox" checked={draft.isActive}
              onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })} /></label>
        </div>
        <div className="btn-row">
          <button className="primary" onClick={save}>{editing ? "Save changes" : "Create user"}</button>
          {editing && <button onClick={startNew}>Cancel</button>}
          {msg && <span className="pill">{msg}</span>}
        </div>
      </div>

      <div className="grid-wrap">
        {rows.length === 0
          ? <div className="empty">No users yet.</div>
          : <EnterpriseGrid<UserRow> columns={columns} rows={rows} getRowId="userId"
              height={320} stateKey="tnt-users" exportFileName="users" onRowClick={startEdit} />}
      </div>
      <p className="muted" style={{ fontSize: 12 }}>Click a row to edit. Passwords are stored hashed (PBKDF2) and never shown.</p>
    </div>
  );
}
