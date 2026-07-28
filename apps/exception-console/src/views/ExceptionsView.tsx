import { useState } from "react";
import type { ConsoleException, Filters } from "../types";
import { FiltersBar } from "../components/Filters";
import { ExceptionGrid } from "../components/ExceptionGrid";
import { ExceptionDetailPane } from "../components/ExceptionDetail";
import type { CurrentUser } from "../auth";

/**
 * Presentational exceptions monitor. The live feed + connection live at the app shell
 * (App.tsx), so this view just filters the shared list and shows detail. Filtering is
 * client-side over the already-live list.
 */
export function ExceptionsView({ items, user, refresh }: { items: ConsoleException[]; user: CurrentUser; refresh: () => void }) {
  const [filters, setFilters] = useState<Filters>({});
  const [selected, setSelected] = useState<number | undefined>();

  const filtered = items.filter((e) => matches(e, filters));

  return (
    <div className="ex-layout">
      <FiltersBar value={filters} onChange={setFilters} />
      <div className="ex-body">
        <div className="ex-left"><ExceptionGrid items={filtered} selectedId={selected} onSelect={setSelected} /></div>
        <div className="ex-right">
          {selected ? (
            <ExceptionDetailPane id={selected} user={user} onChanged={refresh} />
          ) : (
            <div className="detail-pane muted">Select an exception to see detail, evidence and the event timeline.</div>
          )}
        </div>
      </div>
    </div>
  );
}

function matches(e: ConsoleException, f: Filters): boolean {
  if (f.checkpoint && e.checkpoint !== f.checkpoint) return false;
  if (f.severity && e.severity !== f.severity) return false;
  if (f.status && e.status !== f.status) return false;
  if (f.route && e.route !== f.route) return false;
  return true;
}
