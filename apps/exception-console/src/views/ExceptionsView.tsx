import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "../api";
import { connectExceptionsHub } from "../signalr";
import type { ConsoleException, Filters } from "../types";
import { FiltersBar } from "../components/Filters";
import { ExceptionGrid } from "../components/ExceptionGrid";
import { ExceptionDetailPane } from "../components/ExceptionDetail";
import type { CurrentUser } from "../auth";

export function ExceptionsView({ user, onLive }: { user: CurrentUser; onLive: (b: boolean) => void }) {
  const [filters, setFilters] = useState<Filters>({});
  const [items, setItems] = useState<ConsoleException[]>([]);
  const [selected, setSelected] = useState<number | undefined>();
  const filtersRef = useRef(filters);
  filtersRef.current = filters;

  const refresh = useCallback(() => {
    api.list(filtersRef.current, user.getToken()).then(setItems).catch(console.error);
  }, [user]);

  useEffect(() => { refresh(); }, [filters, refresh]);

  useEffect(() => {
    const conn = connectExceptionsHub(
      (e) => setItems((prev) => (matches(e, filtersRef.current) ? [e, ...prev.filter((x) => x.id !== e.id)] : prev)),
      (e) => setItems((prev) => prev.map((x) => (x.id === e.id ? e : x))),
      onLive
    );
    return () => { conn.stop(); };
  }, []);

  return (
    <div className="ex-layout">
      <FiltersBar value={filters} onChange={setFilters} />
      <div className="ex-body">
        <div className="ex-left"><ExceptionGrid items={items} selectedId={selected} onSelect={setSelected} /></div>
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
