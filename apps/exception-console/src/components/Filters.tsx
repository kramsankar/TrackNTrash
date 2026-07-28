import type { Filters } from "../types";

const CHECKPOINTS = ["", "PickTrayBuild", "DispatchDock", "VehicleLoad", "StoreReceive"];
const SEVERITIES = ["", "Low", "Medium", "High", "Critical"];
const STATUSES = ["", "Open", "Acknowledged", "Escalated", "Resolved"];

export function FiltersBar({ value, onChange }: { value: Filters; onChange: (f: Filters) => void }) {
  return (
    <div className="filters">
      <Select label="Checkpoint" options={CHECKPOINTS} v={value.checkpoint} on={(x) => onChange({ ...value, checkpoint: x })} />
      <Select label="Severity" options={SEVERITIES} v={value.severity} on={(x) => onChange({ ...value, severity: x })} />
      <Select label="Status" options={STATUSES} v={value.status} on={(x) => onChange({ ...value, status: x })} />
      <input
        placeholder="Route…"
        value={value.route ?? ""}
        onChange={(e) => onChange({ ...value, route: e.target.value || undefined })}
      />
    </div>
  );
}

function Select({ label, options, v, on }: { label: string; options: string[]; v?: string; on: (x?: string) => void }) {
  return (
    <label>
      {label}
      <select value={v ?? ""} onChange={(e) => on(e.target.value || undefined)}>
        {options.map((o) => (
          <option key={o} value={o}>{o === "" ? "All" : o}</option>
        ))}
      </select>
    </label>
  );
}
