import { useCallback, useEffect, useState } from "react";
import { api, type MasterRecord } from "../api";
import type { CurrentUser } from "../auth";
import { EnterpriseGrid, text, badge, category } from "../vendor/enterprise-grid";
import "../vendor/enterprise-grid/enterprise-grid.css";
import "../grid-dark.css";

/** One field on a master form. */
export interface Field {
  name: string;                     // camelCase, matches the API payload
  label: string;
  type?: "text" | "number" | "select" | "checkbox";
  options?: string[];               // for select
  lookup?: string;                  // master key to load options from (e.g. "zone")
  lookupValue?: string;             // option value field
  lookupLabel?: string;             // option display field
  required?: boolean;
  placeholder?: string;
  width?: "wide";
  inGrid?: boolean;                 // show as a grid column (default true)
}

export interface MasterConfig {
  key: string;                      // API route segment
  title: string;
  blurb: string;
  idField: string;                  // e.g. "productId"
  fields: Field[];
}

/**
 * Generic master-data screen: an EnterpriseGrid of the records plus one form that
 * both creates and edits. Every master is a config, not a bespoke screen.
 */
export function MasterScreen({ config, user }: { config: MasterConfig; user: CurrentUser }) {
  const [rows, setRows] = useState<MasterRecord[]>([]);
  const [lookups, setLookups] = useState<Record<string, MasterRecord[]>>({});
  const [editing, setEditing] = useState<MasterRecord | null>(null);
  const [draft, setDraft] = useState<MasterRecord>({});
  const [msg, setMsg] = useState("");
  const [busy, setBusy] = useState(false);

  const refresh = useCallback(() => {
    api.listMaster(config.key, user.getToken()).then(setRows).catch(() => setRows([]));
    // Load any referenced masters so selects show names rather than raw ids.
    const needed = [...new Set(config.fields.filter((f) => f.lookup).map((f) => f.lookup!))];
    needed.forEach((k) => {
      api.listMaster(k, user.getToken())
        .then((r) => setLookups((prev) => ({ ...prev, [k]: r })))
        .catch(() => { /* lookup unavailable — the field just shows ids */ });
    });
  }, [config, user]);

  useEffect(() => { refresh(); setEditing(null); setDraft(blank(config)); setMsg(""); }, [config.key]);

  const columns = config.fields
    .filter((f) => f.inGrid !== false)
    .map((f) => {
      if (f.type === "checkbox") return badge<MasterRecord>(f.name, f.label, { true: "green", false: "slate" });
      if (f.type === "select" || f.lookup) return category<MasterRecord>(f.name, f.label);
      return text<MasterRecord>(f.name, f.label);
    });

  function startEdit(row: MasterRecord) {
    setEditing(row);
    setDraft({ ...row });
    setMsg("");
  }
  function startNew() { setEditing(null); setDraft(blank(config)); setMsg(""); }

  async function save() {
    const missing = config.fields.filter((f) => f.required && !String(draft[f.name] ?? "").trim());
    if (missing.length) { setMsg(`❌ ${missing.map((m) => m.label).join(", ")} required`); return; }
    setBusy(true);
    try {
      const id = editing?.[config.idField];
      if (id) { await api.updateMaster(config.key, Number(id), draft, user.getToken()); setMsg(`✅ Updated`); }
      else { await api.createMaster(config.key, draft, user.getToken()); setMsg(`✅ Created`); startNew(); }
      refresh();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
    finally { setBusy(false); }
  }

  async function remove(row: MasterRecord) {
    if (!confirm(`Remove "${row[config.fields[0].name]}"? It will be deactivated, not erased.`)) return;
    try {
      await api.deleteMaster(config.key, Number(row[config.idField]), user.getToken());
      setMsg("✅ Deactivated"); if (editing?.[config.idField] === row[config.idField]) startNew();
      refresh();
    } catch (e) { setMsg("❌ " + (e as Error).message); }
  }

  return (
    <div className="view">
      <div className="view-head">
        <div><h2>{config.title}</h2><p className="muted">{config.blurb}</p></div>
        <button onClick={refresh}>↻ Refresh</button>
      </div>

      <div className="card">
        <h3>{editing ? `Edit ${config.title.replace(/s$/, "")}` : `New ${config.title.replace(/s$/, "")}`}</h3>
        <div className="form-grid">
          {config.fields.map((f) => (
            <label key={f.name} className={f.width === "wide" ? "wide" : undefined}>
              {f.label}{f.required && <span style={{ color: "var(--bad)" }}> *</span>}
              {f.type === "checkbox" ? (
                <input type="checkbox" checked={!!draft[f.name]}
                  onChange={(e) => setDraft({ ...draft, [f.name]: e.target.checked })} />
              ) : f.lookup ? (
                <select value={draft[f.name] ?? ""} onChange={(e) => setDraft({ ...draft, [f.name]: e.target.value === "" ? null : +e.target.value })}>
                  <option value="">—</option>
                  {(lookups[f.lookup] ?? []).map((o) => (
                    <option key={o[f.lookupValue ?? "id"]} value={o[f.lookupValue ?? "id"]}>
                      {o[f.lookupLabel ?? "name"]}
                    </option>
                  ))}
                </select>
              ) : f.type === "select" ? (
                <select value={draft[f.name] ?? ""} onChange={(e) => setDraft({ ...draft, [f.name]: e.target.value })}>
                  <option value="">—</option>
                  {(f.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              ) : (
                <input type={f.type === "number" ? "number" : "text"} value={draft[f.name] ?? ""}
                  placeholder={f.placeholder}
                  onChange={(e) => setDraft({ ...draft, [f.name]: f.type === "number" ? (e.target.value === "" ? null : +e.target.value) : e.target.value })} />
              )}
            </label>
          ))}
        </div>
        <div className="btn-row">
          <button className="primary" onClick={save} disabled={busy}>{editing ? "Save changes" : "Create"}</button>
          {editing && <button onClick={startNew}>Cancel</button>}
          {editing && <button className="danger" onClick={() => remove(editing)}>Deactivate</button>}
          {msg && <span className="pill">{msg}</span>}
        </div>
      </div>

      <div className="grid-wrap">
        {rows.length === 0
          ? <div className="empty">No records yet — add the first one above.</div>
          : <EnterpriseGrid<MasterRecord> columns={columns} rows={rows} getRowId={config.idField}
              height={340} stateKey={`tnt-master-${config.key}`} exportFileName={config.key}
              onRowClick={startEdit} />}
      </div>
      <p className="muted" style={{ fontSize: 12 }}>Click any row to edit it.</p>
    </div>
  );
}

function blank(config: MasterConfig): MasterRecord {
  const d: MasterRecord = {};
  for (const f of config.fields) d[f.name] = f.type === "checkbox" ? true : f.type === "number" ? null : "";
  return d;
}
