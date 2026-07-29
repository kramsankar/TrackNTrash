import { useCallback, useEffect, useMemo, useState } from "react";
import { api, type FormRow, type MappingRow, type MasterRecord } from "../api";
import type { CurrentUser } from "../auth";

type Perm = "canView" | "canCreate" | "canEdit" | "canDelete";
const PERMS: { key: Perm; label: string }[] = [
  { key: "canView", label: "View" },
  { key: "canCreate", label: "Create" },
  { key: "canEdit", label: "Edit" },
  { key: "canDelete", label: "Delete" },
];

/**
 * Role × form permission matrix, following the BMS model: pick a role, tick what it may
 * do on each screen. Admin roles bypass the matrix entirely and always have full access.
 */
export function RoleMapping({ user }: { user: CurrentUser }) {
  const [roles, setRoles] = useState<MasterRecord[]>([]);
  const [forms, setForms] = useState<FormRow[]>([]);
  const [roleId, setRoleId] = useState<number | "">("");
  const [matrix, setMatrix] = useState<Record<string, MappingRow>>({});
  const [dirty, setDirty] = useState<Set<string>>(new Set());
  const [msg, setMsg] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.listMaster("role", user.getToken()).then((r) => {
      setRoles(r);
      setRoleId((cur) => (cur === "" && r.length ? r.find((x) => !x.isAdmin)?.roleId ?? r[0].roleId : cur));
    }).catch(() => setRoles([]));
    api.listForms(user.getToken()).then(setForms).catch(() => setForms([]));
  }, [user]);

  const loadMatrix = useCallback(() => {
    if (roleId === "") return;
    api.listMappings(Number(roleId), user.getToken()).then((rows) => {
      const m: Record<string, MappingRow> = {};
      for (const r of rows) m[r.formId] = r;
      setMatrix(m); setDirty(new Set()); setMsg("");
    }).catch(() => setMatrix({}));
  }, [roleId, user]);
  useEffect(() => { loadMatrix(); }, [loadMatrix]);

  const selectedRole = roles.find((r) => r.roleId === roleId);
  const isAdminRole = !!selectedRole?.isAdmin;

  const groups = useMemo(() => {
    const g: Record<string, FormRow[]> = {};
    for (const f of forms) (g[f.formGroup] ??= []).push(f);
    return g;
  }, [forms]);

  function value(formId: string, perm: Perm): boolean {
    if (isAdminRole) return true;
    return !!matrix[formId]?.[perm];
  }

  function toggle(formId: string, perm: Perm) {
    if (isAdminRole) return;
    setMatrix((prev) => {
      const cur = prev[formId] ?? { roleId: Number(roleId), formId, canView: false, canCreate: false, canEdit: false, canDelete: false };
      const next = { ...cur, [perm]: !cur[perm] };
      // Granting any action implies being able to see the screen.
      if (perm !== "canView" && next[perm]) next.canView = true;
      // Removing view removes everything — you cannot act on a screen you cannot open.
      if (perm === "canView" && !next.canView) { next.canCreate = false; next.canEdit = false; next.canDelete = false; }
      return { ...prev, [formId]: next };
    });
    setDirty((d) => new Set(d).add(formId));
  }

  function toggleRow(formId: string, on: boolean) {
    if (isAdminRole) return;
    setMatrix((prev) => ({ ...prev, [formId]: { roleId: Number(roleId), formId,
      canView: on, canCreate: on, canEdit: on, canDelete: on } }));
    setDirty((d) => new Set(d).add(formId));
  }

  async function save() {
    if (roleId === "" || dirty.size === 0) return;
    setBusy(true);
    try {
      for (const formId of dirty) {
        const m = matrix[formId];
        await api.saveMapping({ roleId: Number(roleId), formId,
          canView: m.canView, canCreate: m.canCreate, canEdit: m.canEdit, canDelete: m.canDelete }, user.getToken());
      }
      setMsg(`✅ Saved ${dirty.size} screen${dirty.size === 1 ? "" : "s"}`);
      setDirty(new Set());
    } catch (e) { setMsg("❌ " + (e as Error).message); }
    finally { setBusy(false); }
  }

  return (
    <div className="view">
      <div className="view-head">
        <div>
          <h2>Role Mapping</h2>
          <p className="muted">What each role may do on each screen. Granting an action also grants View; removing View removes the rest.</p>
        </div>
        <button onClick={loadMatrix}>↻ Reload</button>
      </div>

      <div className="card">
        <div className="btn-row">
          <label className="inline-label">
            Role
            <select value={roleId} onChange={(e) => setRoleId(e.target.value === "" ? "" : +e.target.value)}>
              {roles.map((r) => <option key={r.roleId} value={r.roleId}>{r.roleName}{r.isAdmin ? " (full admin)" : ""}</option>)}
            </select>
          </label>
          <button className="primary" onClick={save} disabled={busy || isAdminRole || dirty.size === 0}>
            {dirty.size ? `Save ${dirty.size} change${dirty.size === 1 ? "" : "s"}` : "Save"}
          </button>
          {msg && <span className="pill">{msg}</span>}
        </div>
        {isAdminRole && (
          <p className="muted" style={{ fontSize: 13, marginTop: 8 }}>
            <b>{selectedRole?.roleName}</b> is a full-admin role — it always has every permission, so the matrix is read-only.
          </p>
        )}
      </div>

      {Object.entries(groups).map(([group, list]) => (
        <div key={group} className="card">
          <h3>{group}</h3>
          <div className="tbl-scroll">
            <table className="mini-grid perm-matrix">
              <thead>
                <tr>
                  <th style={{ minWidth: 180 }}>Screen</th>
                  {PERMS.map((p) => <th key={p.key} style={{ textAlign: "center" }}>{p.label}</th>)}
                  <th style={{ textAlign: "center" }}>All</th>
                </tr>
              </thead>
              <tbody>
                {list.map((f) => {
                  const all = PERMS.every((p) => value(f.formId, p.key));
                  return (
                    <tr key={f.formId} className={dirty.has(f.formId) ? "row-dirty" : undefined}>
                      <td>{f.formName}</td>
                      {PERMS.map((p) => (
                        <td key={p.key} style={{ textAlign: "center" }}>
                          <input type="checkbox" disabled={isAdminRole}
                            checked={value(f.formId, p.key)}
                            onChange={() => toggle(f.formId, p.key)}
                            aria-label={`${p.label} ${f.formName}`} />
                        </td>
                      ))}
                      <td style={{ textAlign: "center" }}>
                        <input type="checkbox" disabled={isAdminRole} checked={all}
                          onChange={(e) => toggleRow(f.formId, e.target.checked)}
                          aria-label={`All permissions for ${f.formName}`} />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      ))}
    </div>
  );
}
