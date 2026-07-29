// src/EnterpriseGrid.tsx
import React6, {
  useCallback as useCallback2,
  useEffect as useEffect3,
  useImperativeHandle,
  useLayoutEffect as useLayoutEffect2,
  useMemo as useMemo5,
  useRef as useRef3,
  useState as useState7
} from "react";

// src/icons.tsx
import { jsx, jsxs } from "react/jsx-runtime";
function Svg({ size = 16, className, children }) {
  return /* @__PURE__ */ jsx(
    "svg",
    {
      width: size,
      height: size,
      viewBox: "0 0 24 24",
      fill: "none",
      stroke: "currentColor",
      strokeWidth: 2,
      strokeLinecap: "round",
      strokeLinejoin: "round",
      className,
      "aria-hidden": "true",
      focusable: "false",
      children
    }
  );
}
var p = (d, key) => /* @__PURE__ */ jsx("path", { d }, key ?? d);
var ChevronDown = (i) => /* @__PURE__ */ jsx(Svg, { ...i, children: p("m6 9 6 6 6-6") });
var ChevronUp = (i) => /* @__PURE__ */ jsx(Svg, { ...i, children: p("m18 15-6-6-6 6") });
var ChevronRight = (i) => /* @__PURE__ */ jsx(Svg, { ...i, children: p("m9 18 6-6-6-6") });
var ChevronLeft = (i) => /* @__PURE__ */ jsx(Svg, { ...i, children: p("m15 18-6-6 6-6") });
var ChevronsLeft = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("m11 17-5-5 5-5"),
  p("m18 17-5-5 5-5")
] });
var ChevronsRight = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("m6 17 5-5-5-5"),
  p("m13 17 5-5-5-5")
] });
var X = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M18 6 6 18"),
  p("m6 6 12 12")
] });
var Search = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  /* @__PURE__ */ jsx("circle", { cx: "11", cy: "11", r: "8" }),
  p("m21 21-4.3-4.3")
] });
var Download = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"),
  p("m7 10 5 5 5-5"),
  p("M12 15V3")
] });
var FileSpreadsheet = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"),
  p("M14 2v5h5"),
  p("M8 13h8"),
  p("M8 17h8"),
  p("M12 13v8")
] });
var Copy = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  /* @__PURE__ */ jsx("rect", { x: "8", y: "8", width: "13", height: "13", rx: "2" }),
  p("M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1")
] });
var Filter = (i) => /* @__PURE__ */ jsx(Svg, { ...i, children: p("M22 3H2l8 9.46V19l4 2v-8.54L22 3z") });
var Loader2 = (i) => /* @__PURE__ */ jsx(Svg, { ...i, children: p("M21 12a9 9 0 1 1-6.22-8.56") });
var Maximize2 = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M15 3h6v6"),
  p("M9 21H3v-6"),
  p("m21 3-7 7"),
  p("m3 21 7-7")
] });
var Minimize2 = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M4 14h6v6"),
  p("M20 10h-6V4"),
  p("m14 10 7-7"),
  p("m3 21 7-7")
] });
var MoreVertical = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  /* @__PURE__ */ jsx("circle", { cx: "12", cy: "5", r: "1", fill: "currentColor" }),
  /* @__PURE__ */ jsx("circle", { cx: "12", cy: "12", r: "1", fill: "currentColor" }),
  /* @__PURE__ */ jsx("circle", { cx: "12", cy: "19", r: "1", fill: "currentColor" })
] });
var RotateCcw = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"),
  p("M3 3v5h5")
] });
var Pin = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M12 17v5"),
  p("M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z")
] });
var PinOff = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M12 17v5"),
  p("M15 9.34V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8.82"),
  p("M9 6.76V10a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 14.48V16a1 1 0 0 0 1 1h11"),
  p("m2 2 20 20")
] });
var EyeOff = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M9.88 9.88a3 3 0 1 0 4.24 4.24"),
  p("M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68"),
  p("M6.61 6.61A13.53 13.53 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61"),
  p("m2 2 20 20")
] });
var Columns3 = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  /* @__PURE__ */ jsx("rect", { x: "3", y: "3", width: "18", height: "18", rx: "2" }),
  p("M9 3v18"),
  p("M15 3v18")
] });
var GripVertical = (i) => /* @__PURE__ */ jsx(Svg, { ...i, children: [5, 12, 19].flatMap(
  (cy) => [9, 15].map((cx) => /* @__PURE__ */ jsx("circle", { cx, cy, r: "1", fill: "currentColor" }, `${cx}-${cy}`))
) });
var AZ = [
  p("M20 8h-5", "az1"),
  p("M15 10V6.5a2.5 2.5 0 0 1 5 0V10", "az2"),
  p("M15 14h5l-5 6h5", "az3")
];
var ArrowUpAZ = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("m3 8 4-4 4 4"),
  p("M7 4v16"),
  AZ
] });
var ArrowDownAZ = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("m3 16 4 4 4-4"),
  p("M7 20V4"),
  AZ
] });
var Ban = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  /* @__PURE__ */ jsx("circle", { cx: "12", cy: "12", r: "10" }),
  p("m4.9 4.9 14.2 14.2")
] });
var Group = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("M3 7V5a2 2 0 0 1 2-2h2"),
  p("M17 3h2a2 2 0 0 1 2 2v2"),
  p("M21 17v2a2 2 0 0 1-2 2h-2"),
  p("M7 21H5a2 2 0 0 1-2-2v-2"),
  /* @__PURE__ */ jsx("rect", { x: "7", y: "7", width: "10", height: "10", rx: "1" })
] });
var Ungroup = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  /* @__PURE__ */ jsx("rect", { x: "3", y: "5", width: "8", height: "6", rx: "1" }),
  /* @__PURE__ */ jsx("rect", { x: "13", y: "13", width: "8", height: "6", rx: "1" })
] });
var MoveHorizontal = (i) => /* @__PURE__ */ jsxs(Svg, { ...i, children: [
  p("m18 8 4 4-4 4"),
  p("M2 12h20"),
  p("m6 8-4 4 4 4")
] });

// src/ColumnMenu.tsx
import { useMemo, useState as useState2 } from "react";

// src/Popover.tsx
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { jsx as jsx2, jsxs as jsxs2 } from "react/jsx-runtime";
function Popover({ anchor, open, onClose, children, align = "left", width, className = "" }) {
  const ref = useRef(null);
  const [pos, setPos] = useState(null);
  useLayoutEffect(() => {
    if (!open || !anchor) return;
    const place = () => {
      const a = anchor.getBoundingClientRect();
      const el = ref.current;
      const w = width ?? el?.offsetWidth ?? 240;
      const h = el?.offsetHeight ?? 200;
      let left = align === "right" ? a.right - w : a.left;
      left = Math.max(8, Math.min(left, window.innerWidth - w - 8));
      let top = a.bottom + 4;
      if (top + h > window.innerHeight - 8) top = Math.max(8, a.top - h - 4);
      setPos({ top, left });
    };
    place();
    window.addEventListener("resize", place);
    window.addEventListener("scroll", place, true);
    return () => {
      window.removeEventListener("resize", place);
      window.removeEventListener("scroll", place, true);
    };
  }, [open, anchor, align, width]);
  useEffect(() => {
    if (!open) return;
    const onDown = (e) => {
      const t = e.target;
      if (ref.current?.contains(t)) return;
      if (anchor?.contains(t)) return;
      onClose();
    };
    const onKey = (e) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        onClose();
      }
    };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey, true);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey, true);
    };
  }, [open, anchor, onClose]);
  if (!open) return null;
  return createPortal(
    /* @__PURE__ */ jsx2(
      "div",
      {
        ref,
        role: "dialog",
        style: {
          position: "fixed",
          top: pos?.top ?? -9999,
          left: pos?.left ?? -9999,
          width,
          zIndex: 9999,
          visibility: pos ? "visible" : "hidden"
        },
        className: `rounded-lg border border-slate-200 bg-white shadow-xl ${className}`,
        onMouseDown: (e) => e.stopPropagation(),
        children
      }
    ),
    document.body
  );
}
function MenuItem({
  icon,
  label,
  shortcut,
  disabled,
  danger,
  onClick
}) {
  return /* @__PURE__ */ jsxs2(
    "button",
    {
      type: "button",
      disabled,
      onClick,
      className: `flex w-full items-center gap-2.5 px-3 py-1.5 text-left text-[13px] transition-colors disabled:pointer-events-none disabled:opacity-40 ${danger ? "text-rose-600 hover:bg-rose-50" : "text-slate-700 hover:bg-slate-100"}`,
      children: [
        /* @__PURE__ */ jsx2("span", { className: "flex w-4 justify-center text-slate-400", children: icon }),
        /* @__PURE__ */ jsx2("span", { className: "flex-1 truncate", children: label }),
        shortcut && /* @__PURE__ */ jsx2("span", { className: "text-[11px] text-slate-400", children: shortcut })
      ]
    }
  );
}
function MenuDivider() {
  return /* @__PURE__ */ jsx2("div", { className: "my-1 border-t border-slate-100" });
}

// src/utils.ts
function getNested(obj, path) {
  if (obj == null) return void 0;
  if (path.indexOf(".") === -1) return obj[path];
  return path.split(".").reduce((o, k) => o == null ? void 0 : o[k], obj);
}
function rawValue(col, row) {
  return col.valueGetter ? col.valueGetter(row) : getNested(row, col.field);
}
function displayValue(col, row) {
  const v = rawValue(col, row);
  if (col.valueFormatter) return col.valueFormatter(v, row);
  return v == null || v === "" ? "" : String(v);
}
function isBlank(v) {
  return v == null || v === "" || typeof v === "number" && Number.isNaN(v);
}
function toNumber(v) {
  if (v == null || v === "") return null;
  if (typeof v === "number") return Number.isNaN(v) ? null : v;
  const cleaned = String(v).replace(/[^0-9.eE+-]/g, "");
  if (cleaned === "" || cleaned === "-" || cleaned === ".") return null;
  const n = Number(cleaned);
  return Number.isNaN(n) ? null : n;
}
function toDayStamp(v) {
  if (v == null || v === "") return null;
  const d = v instanceof Date ? v : new Date(v);
  const t = d.getTime();
  if (Number.isNaN(t)) return null;
  return Date.UTC(d.getFullYear(), d.getMonth(), d.getDate());
}
function inferType(col, sample) {
  if (col.type) return col.type;
  for (const row of sample) {
    const v = rawValue(col, row);
    if (isBlank(v)) continue;
    if (typeof v === "number") return "number";
    if (typeof v === "boolean") return "boolean";
    if (v instanceof Date) return "date";
    if (typeof v === "string" && /^\d{4}-\d{2}-\d{2}([T ]|$)/.test(v)) return "date";
    return "text";
  }
  return "text";
}
function defaultAlign(type) {
  if (type === "number") return "right";
  if (type === "boolean") return "center";
  return "left";
}
function compareBy(a, b, type, dir) {
  const factor = dir === "asc" ? 1 : -1;
  const aBlank = isBlank(a);
  const bBlank = isBlank(b);
  if (aBlank && bBlank) return 0;
  if (aBlank) return 1;
  if (bBlank) return -1;
  if (type === "number") {
    const na = toNumber(a) ?? 0;
    const nb = toNumber(b) ?? 0;
    return (na - nb) * factor;
  }
  if (type === "date") {
    const da = toDayStamp(a) ?? 0;
    const db = toDayStamp(b) ?? 0;
    return (da - db) * factor;
  }
  if (type === "boolean") {
    return ((a ? 1 : 0) - (b ? 1 : 0)) * factor;
  }
  return String(a).localeCompare(String(b), void 0, { numeric: true, sensitivity: "base" }) * factor;
}
var INR = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 2 });
function formatNumber(n) {
  return INR.format(n);
}
function classNames(...parts) {
  return parts.filter(Boolean).join(" ");
}
function densityRowHeight(density) {
  return density === "compact" ? 30 : density === "comfortable" ? 48 : 38;
}
function estimateTextWidth(text2, charWidth = 7.2) {
  return text2.length * charWidth;
}
function downloadBlob(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
function lowerBound(arr, target) {
  let lo = 0;
  let hi = arr.length - 1;
  while (lo < hi) {
    const mid = lo + hi + 1 >> 1;
    if (arr[mid] <= target) lo = mid;
    else hi = mid - 1;
  }
  return lo;
}

// src/filters.ts
var TEXT_OPS = [
  { value: "contains", label: "Contains" },
  { value: "notContains", label: "Does not contain" },
  { value: "equals", label: "Equals" },
  { value: "notEqual", label: "Not equal" },
  { value: "startsWith", label: "Starts with" },
  { value: "endsWith", label: "Ends with" },
  { value: "blank", label: "Is blank" },
  { value: "notBlank", label: "Is not blank" }
];
var NUMBER_OPS = [
  { value: "equals", label: "=" },
  { value: "notEqual", label: "\u2260" },
  { value: "gt", label: ">" },
  { value: "gte", label: "\u2265" },
  { value: "lt", label: "<" },
  { value: "lte", label: "\u2264" },
  { value: "inRange", label: "Between" },
  { value: "blank", label: "Is blank" },
  { value: "notBlank", label: "Is not blank" }
];
var DATE_OPS = [
  { value: "equals", label: "On" },
  { value: "before", label: "Before" },
  { value: "after", label: "After" },
  { value: "inRange", label: "Between" },
  { value: "blank", label: "Is blank" },
  { value: "notBlank", label: "Is not blank" }
];
function emptyModel(kind) {
  switch (kind) {
    case "number":
      return { kind: "number", op: "equals", value: null, valueTo: null };
    case "date":
      return { kind: "date", op: "equals", value: null, valueTo: null };
    case "set":
      return { kind: "set", selected: null };
    default:
      return { kind: "text", op: "contains", value: "" };
  }
}
function isActive(model) {
  if (!model) return false;
  switch (model.kind) {
    case "text":
      if (model.op === "blank" || model.op === "notBlank") return true;
      return model.value.trim() !== "";
    case "number":
      if (model.op === "blank" || model.op === "notBlank") return true;
      if (model.op === "inRange") return model.value != null || model.valueTo != null;
      return model.value != null;
    case "date":
      if (model.op === "blank" || model.op === "notBlank") return true;
      if (model.op === "inRange") return !!(model.value || model.valueTo);
      return !!model.value;
    case "set":
      return model.selected != null;
    default:
      return false;
  }
}
function matchText(model, text2) {
  const hay = text2.toLowerCase();
  const needle = model.value.trim().toLowerCase();
  switch (model.op) {
    case "contains":
      return hay.includes(needle);
    case "notContains":
      return !hay.includes(needle);
    case "equals":
      return hay === needle;
    case "notEqual":
      return hay !== needle;
    case "startsWith":
      return hay.startsWith(needle);
    case "endsWith":
      return hay.endsWith(needle);
    case "blank":
      return hay === "";
    case "notBlank":
      return hay !== "";
    default:
      return true;
  }
}
function matchNumber(model, raw) {
  if (model.op === "blank") return isBlank(raw);
  if (model.op === "notBlank") return !isBlank(raw);
  const n = toNumber(raw);
  if (n == null) return false;
  const a = model.value;
  const b = model.valueTo;
  switch (model.op) {
    case "equals":
      return a != null && n === a;
    case "notEqual":
      return a != null && n !== a;
    case "gt":
      return a != null && n > a;
    case "gte":
      return a != null && n >= a;
    case "lt":
      return a != null && n < a;
    case "lte":
      return a != null && n <= a;
    case "inRange":
      if (a != null && n < a) return false;
      if (b != null && n > b) return false;
      return true;
    default:
      return true;
  }
}
function matchDate(model, raw) {
  if (model.op === "blank") return isBlank(raw);
  if (model.op === "notBlank") return !isBlank(raw);
  const t = toDayStamp(raw);
  if (t == null) return false;
  const a = model.value ? toDayStamp(model.value) : null;
  const b = model.valueTo ? toDayStamp(model.valueTo) : null;
  switch (model.op) {
    case "equals":
      return a != null && t === a;
    case "before":
      return a != null && t < a;
    case "after":
      return a != null && t > a;
    case "inRange":
      if (a != null && t < a) return false;
      if (b != null && t > b) return false;
      return true;
    default:
      return true;
  }
}
function matchSet(model, text2) {
  if (model.selected == null) return true;
  return model.selected.includes(text2);
}
function matchesFilter(model, col, row) {
  switch (model.kind) {
    case "text":
      return matchText(model, displayValue(col, row));
    case "number":
      return matchNumber(model, rawValue(col, row));
    case "date":
      return matchDate(model, rawValue(col, row));
    case "set":
      return matchSet(model, displayValue(col, row));
    default:
      return true;
  }
}
function applyFilters(rows, cols, filters, quickFilter) {
  const byField = new Map(cols.map((c) => [c.field, c]));
  const active = Object.entries(filters).filter(([field, m]) => isActive(m) && byField.has(field));
  const q = quickFilter.trim().toLowerCase();
  const quickTerms = q ? q.split(/\s+/) : [];
  const passesQuick = (row) => {
    if (!quickTerms.length) return true;
    const haystack = cols.map((c) => displayValue(c, row)).join(" \0 ").toLowerCase();
    return quickTerms.every((t) => haystack.includes(t));
  };
  const passesAllBut = (row, skipField) => {
    for (const [field, model] of active) {
      if (field === skipField) continue;
      if (!matchesFilter(model, byField.get(field), row)) return false;
    }
    return true;
  };
  const result = rows.filter((r) => passesQuick(r) && passesAllBut(r, null));
  const cache = /* @__PURE__ */ new Map();
  const rowsExcluding = (field) => {
    if (!active.some(([f]) => f === field)) return result;
    let cached = cache.get(field);
    if (!cached) {
      cached = rows.filter((r) => passesQuick(r) && passesAllBut(r, field));
      cache.set(field, cached);
    }
    return cached;
  };
  return { rows: result, rowsExcluding };
}
function distinctValues(col, rows) {
  const counts = /* @__PURE__ */ new Map();
  for (const row of rows) {
    const v = displayValue(col, row);
    counts.set(v, (counts.get(v) ?? 0) + 1);
  }
  return [...counts.entries()].map(([value, count]) => ({ value, count })).sort((a, b) => {
    if (a.value === "") return 1;
    if (b.value === "") return -1;
    return a.value.localeCompare(b.value, void 0, { numeric: true, sensitivity: "base" });
  });
}
function filterKindFor(col, type) {
  if (col.filter === false) return null;
  if (col.filter) return col.filter;
  if (type === "number") return "number";
  if (type === "date") return "date";
  if (type === "boolean") return "set";
  return "text";
}
function countActive(filters) {
  return Object.values(filters).filter(isActive).length;
}

// src/ColumnMenu.tsx
import { Fragment, jsx as jsx3, jsxs as jsxs3 } from "react/jsx-runtime";
var INPUT = "w-full rounded border border-slate-200 px-2 py-1.5 text-[13px] text-slate-800 outline-none focus:border-[var(--eg-accent)] focus:ring-1 focus:ring-[var(--eg-accent)]/30";
function TextFilterEditor({ model, onChange }) {
  const needsValue = model.op !== "blank" && model.op !== "notBlank";
  return /* @__PURE__ */ jsxs3("div", { className: "flex flex-col gap-1.5", children: [
    /* @__PURE__ */ jsx3("select", { className: INPUT, value: model.op, onChange: (e) => onChange({ ...model, op: e.target.value }), children: TEXT_OPS.map((o) => /* @__PURE__ */ jsx3("option", { value: o.value, children: o.label }, o.value)) }),
    needsValue && /* @__PURE__ */ jsx3(
      "input",
      {
        autoFocus: true,
        className: INPUT,
        placeholder: "Filter value\u2026",
        value: model.value,
        onChange: (e) => onChange({ ...model, value: e.target.value })
      }
    )
  ] });
}
function NumberFilterEditor({ model, onChange }) {
  const needsValue = model.op !== "blank" && model.op !== "notBlank";
  const num = (s) => s === "" ? null : Number(s);
  return /* @__PURE__ */ jsxs3("div", { className: "flex flex-col gap-1.5", children: [
    /* @__PURE__ */ jsx3("select", { className: INPUT, value: model.op, onChange: (e) => onChange({ ...model, op: e.target.value }), children: NUMBER_OPS.map((o) => /* @__PURE__ */ jsx3("option", { value: o.value, children: o.label }, o.value)) }),
    needsValue && /* @__PURE__ */ jsx3(
      "input",
      {
        autoFocus: true,
        type: "number",
        className: INPUT,
        placeholder: model.op === "inRange" ? "From" : "Value",
        value: model.value ?? "",
        onChange: (e) => onChange({ ...model, value: num(e.target.value) })
      }
    ),
    model.op === "inRange" && /* @__PURE__ */ jsx3(
      "input",
      {
        type: "number",
        className: INPUT,
        placeholder: "To",
        value: model.valueTo ?? "",
        onChange: (e) => onChange({ ...model, valueTo: num(e.target.value) })
      }
    )
  ] });
}
function DateFilterEditor({ model, onChange }) {
  const needsValue = model.op !== "blank" && model.op !== "notBlank";
  return /* @__PURE__ */ jsxs3("div", { className: "flex flex-col gap-1.5", children: [
    /* @__PURE__ */ jsx3("select", { className: INPUT, value: model.op, onChange: (e) => onChange({ ...model, op: e.target.value }), children: DATE_OPS.map((o) => /* @__PURE__ */ jsx3("option", { value: o.value, children: o.label }, o.value)) }),
    needsValue && /* @__PURE__ */ jsx3(
      "input",
      {
        type: "date",
        className: INPUT,
        value: model.value ?? "",
        onChange: (e) => onChange({ ...model, value: e.target.value || null })
      }
    ),
    model.op === "inRange" && /* @__PURE__ */ jsx3(
      "input",
      {
        type: "date",
        className: INPUT,
        value: model.valueTo ?? "",
        onChange: (e) => onChange({ ...model, valueTo: e.target.value || null })
      }
    )
  ] });
}
function SetFilterEditor({
  model,
  onChange,
  col,
  rows
}) {
  const [search, setSearch] = useState2("");
  const options = useMemo(() => distinctValues(col, rows), [col, rows]);
  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? options.filter((o) => o.value.toLowerCase().includes(q)) : options;
  }, [options, search]);
  const selected = model.selected ?? options.map((o) => o.value);
  const selectedSet = new Set(selected);
  const allVisibleChecked = visible.length > 0 && visible.every((o) => selectedSet.has(o.value));
  const setSelection = (next) => {
    onChange({ kind: "set", selected: next.length === options.length ? null : next });
  };
  const toggle = (value) => {
    const next = new Set(selectedSet);
    if (next.has(value)) next.delete(value);
    else next.add(value);
    setSelection(Array.from(next));
  };
  const toggleVisible = () => {
    const next = new Set(selectedSet);
    for (const o of visible) {
      if (allVisibleChecked) next.delete(o.value);
      else next.add(o.value);
    }
    setSelection(Array.from(next));
  };
  return /* @__PURE__ */ jsxs3("div", { className: "flex flex-col gap-1.5", children: [
    /* @__PURE__ */ jsxs3("div", { className: "relative", children: [
      /* @__PURE__ */ jsx3(Search, { size: 13, className: "absolute left-2 top-1/2 -translate-y-1/2 text-slate-400" }),
      /* @__PURE__ */ jsx3(
        "input",
        {
          autoFocus: true,
          className: `${INPUT} pl-7`,
          placeholder: "Search values\u2026",
          value: search,
          onChange: (e) => setSearch(e.target.value)
        }
      )
    ] }),
    /* @__PURE__ */ jsxs3("label", { className: "flex cursor-pointer items-center gap-2 border-b border-slate-100 px-1 pb-1.5 text-[13px] font-medium text-slate-700", children: [
      /* @__PURE__ */ jsx3("input", { type: "checkbox", checked: allVisibleChecked, onChange: toggleVisible, className: "accent-[var(--eg-accent)]" }),
      search ? `Select all (${visible.length} shown)` : "Select all"
    ] }),
    /* @__PURE__ */ jsxs3("div", { className: "max-h-56 overflow-y-auto pr-1", children: [
      visible.length === 0 && /* @__PURE__ */ jsx3("div", { className: "px-1 py-3 text-center text-[12px] text-slate-400", children: "No matches" }),
      visible.map((o) => /* @__PURE__ */ jsxs3("label", { className: "flex cursor-pointer items-center gap-2 rounded px-1 py-1 text-[13px] text-slate-700 hover:bg-slate-50", children: [
        /* @__PURE__ */ jsx3(
          "input",
          {
            type: "checkbox",
            checked: selectedSet.has(o.value),
            onChange: () => toggle(o.value),
            className: "accent-[var(--eg-accent)]"
          }
        ),
        /* @__PURE__ */ jsx3("span", { className: "flex-1 truncate", children: o.value === "" ? /* @__PURE__ */ jsx3("em", { className: "text-slate-400", children: "(Blank)" }) : o.value }),
        /* @__PURE__ */ jsx3("span", { className: "text-[11px] text-slate-400", children: o.count })
      ] }, o.value))
    ] })
  ] });
}
function FilterEditor({
  kind,
  model,
  onChange,
  col,
  rows
}) {
  if (model.kind !== kind) return null;
  switch (model.kind) {
    case "number":
      return /* @__PURE__ */ jsx3(NumberFilterEditor, { model, onChange });
    case "date":
      return /* @__PURE__ */ jsx3(DateFilterEditor, { model, onChange });
    case "set":
      return /* @__PURE__ */ jsx3(SetFilterEditor, { model, onChange, col, rows });
    default:
      return /* @__PURE__ */ jsx3(TextFilterEditor, { model, onChange });
  }
}
function ColumnMenu(props) {
  const { col, filterKind, model, onModelChange, open, onClose } = props;
  const [tab, setTab] = useState2("menu");
  const current = model ?? (filterKind ? emptyModel(filterKind) : void 0);
  const active = isActive(model);
  return /* @__PURE__ */ jsxs3(Popover, { anchor: props.anchor, open, onClose, align: "right", width: 260, children: [
    filterKind && /* @__PURE__ */ jsx3("div", { className: "flex border-b border-slate-100 text-[12px] font-medium", children: ["menu", "filter"].map((t) => /* @__PURE__ */ jsx3(
      "button",
      {
        type: "button",
        onClick: () => setTab(t),
        className: `flex-1 px-3 py-2 transition-colors ${tab === t ? "border-b-2 border-[var(--eg-accent)] text-[var(--eg-primary)]" : "text-slate-500 hover:text-slate-700"}`,
        children: t === "menu" ? "Column" : `Filter${active ? " \u2022" : ""}`
      },
      t
    )) }),
    tab === "menu" || !filterKind ? /* @__PURE__ */ jsxs3("div", { className: "py-1", children: [
      col.sortable !== false && /* @__PURE__ */ jsxs3(Fragment, { children: [
        /* @__PURE__ */ jsx3(
          MenuItem,
          {
            icon: /* @__PURE__ */ jsx3(ArrowUpAZ, { size: 14 }),
            label: "Sort ascending",
            onClick: () => {
              props.onSort("asc");
              onClose();
            }
          }
        ),
        /* @__PURE__ */ jsx3(
          MenuItem,
          {
            icon: /* @__PURE__ */ jsx3(ArrowDownAZ, { size: 14 }),
            label: "Sort descending",
            onClick: () => {
              props.onSort("desc");
              onClose();
            }
          }
        ),
        /* @__PURE__ */ jsx3(
          MenuItem,
          {
            icon: /* @__PURE__ */ jsx3(Ban, { size: 14 }),
            label: "Clear sort",
            disabled: !props.sortDir,
            onClick: () => {
              props.onSort(null);
              onClose();
            }
          }
        ),
        /* @__PURE__ */ jsx3(MenuDivider, {})
      ] }),
      !col.lockPinned && /* @__PURE__ */ jsxs3(Fragment, { children: [
        /* @__PURE__ */ jsx3(
          MenuItem,
          {
            icon: /* @__PURE__ */ jsx3(Pin, { size: 14 }),
            label: props.pinned === "left" ? "Unpin" : "Pin left",
            onClick: () => {
              props.onPin(props.pinned === "left" ? null : "left");
              onClose();
            }
          }
        ),
        /* @__PURE__ */ jsx3(
          MenuItem,
          {
            icon: props.pinned === "right" ? /* @__PURE__ */ jsx3(PinOff, { size: 14 }) : /* @__PURE__ */ jsx3(Pin, { size: 14 }),
            label: props.pinned === "right" ? "Unpin" : "Pin right",
            onClick: () => {
              props.onPin(props.pinned === "right" ? null : "right");
              onClose();
            }
          }
        ),
        /* @__PURE__ */ jsx3(MenuDivider, {})
      ] }),
      props.canGroup && /* @__PURE__ */ jsx3(
        MenuItem,
        {
          icon: props.grouped ? /* @__PURE__ */ jsx3(Ungroup, { size: 14 }) : /* @__PURE__ */ jsx3(Group, { size: 14 }),
          label: props.grouped ? "Remove from groups" : "Group by this column",
          onClick: () => {
            props.onToggleGroup();
            onClose();
          }
        }
      ),
      /* @__PURE__ */ jsx3(MenuItem, { icon: /* @__PURE__ */ jsx3(MoveHorizontal, { size: 14 }), label: "Auto-size this column", onClick: () => {
        props.onAutoSize();
        onClose();
      } }),
      !col.lockVisible && /* @__PURE__ */ jsx3(MenuItem, { icon: /* @__PURE__ */ jsx3(EyeOff, { size: 14 }), label: "Hide column", onClick: () => {
        props.onHide();
        onClose();
      } })
    ] }) : /* @__PURE__ */ jsxs3("div", { className: "p-2.5", children: [
      current && /* @__PURE__ */ jsx3(
        FilterEditor,
        {
          kind: filterKind,
          model: current,
          onChange: (m) => onModelChange(m),
          col,
          rows: props.filterRows
        }
      ),
      /* @__PURE__ */ jsxs3("div", { className: "mt-2 flex items-center justify-between border-t border-slate-100 pt-2", children: [
        /* @__PURE__ */ jsxs3(
          "button",
          {
            type: "button",
            onClick: () => onModelChange(void 0),
            className: "flex items-center gap-1 text-[12px] text-slate-500 hover:text-rose-600",
            children: [
              /* @__PURE__ */ jsx3(X, { size: 12 }),
              " Clear"
            ]
          }
        ),
        /* @__PURE__ */ jsx3(
          "button",
          {
            type: "button",
            onClick: onClose,
            className: "rounded bg-[var(--eg-primary)] px-2.5 py-1 text-[12px] font-medium text-white hover:opacity-90",
            children: "Done"
          }
        )
      ] })
    ] })
  ] });
}

// src/GroupPanel.tsx
import React3, { useState as useState3 } from "react";
import { jsx as jsx4, jsxs as jsxs4 } from "react/jsx-runtime";
function GroupPanel(props) {
  const [over, setOver] = useState3(false);
  const label = (field) => props.columns.find((c) => c.field === field)?.headerName ?? field;
  const canDrop = props.draggingField != null && !props.groupBy.includes(props.draggingField) && props.columns.find((c) => c.field === props.draggingField)?.enableRowGroup !== false;
  return /* @__PURE__ */ jsxs4(
    "div",
    {
      onDragOver: (e) => {
        if (canDrop) {
          e.preventDefault();
          setOver(true);
        }
      },
      onDragLeave: () => setOver(false),
      onDrop: (e) => {
        e.preventDefault();
        setOver(false);
        if (canDrop && props.draggingField) props.onDropField(props.draggingField);
      },
      className: `flex min-h-[38px] flex-wrap items-center gap-1.5 border-b border-slate-200 px-3 py-1.5 text-[12px] transition-colors ${over ? "bg-[var(--eg-accent)]/10" : "bg-slate-50"}`,
      children: [
        /* @__PURE__ */ jsx4(Group, { size: 13, className: "text-slate-400" }),
        props.groupBy.length === 0 ? /* @__PURE__ */ jsx4("span", { className: "text-slate-400", children: "Drag a column header here to group by it" }) : props.groupBy.map((field, i) => /* @__PURE__ */ jsxs4(React3.Fragment, { children: [
          i > 0 && /* @__PURE__ */ jsx4(ChevronRight, { size: 12, className: "text-slate-300" }),
          /* @__PURE__ */ jsxs4(
            "span",
            {
              draggable: true,
              onDragStart: (e) => {
                e.dataTransfer.setData("text/plain", field);
              },
              onDragOver: (e) => e.preventDefault(),
              onDrop: (e) => {
                e.preventDefault();
                e.stopPropagation();
                const src = e.dataTransfer.getData("text/plain") || props.draggingField;
                if (src && src !== field) {
                  if (props.groupBy.includes(src)) props.onReorder(src, field);
                  else props.onDropField(src);
                }
              },
              className: "inline-flex cursor-grab items-center gap-1 rounded-full border border-[var(--eg-accent)]/40 bg-white px-2 py-0.5 font-medium text-[var(--eg-primary)]",
              children: [
                label(field),
                /* @__PURE__ */ jsx4("button", { type: "button", onClick: () => props.onRemove(field), className: "text-slate-400 hover:text-rose-600", children: /* @__PURE__ */ jsx4(X, { size: 11 }) })
              ]
            }
          )
        ] }, field))
      ]
    }
  );
}

// src/StatusBar.tsx
import { Fragment as Fragment2, jsx as jsx5, jsxs as jsxs5 } from "react/jsx-runtime";
function Stat({ label, value }) {
  return /* @__PURE__ */ jsxs5("span", { className: "whitespace-nowrap", children: [
    /* @__PURE__ */ jsx5("span", { className: "text-slate-400", children: label }),
    " ",
    /* @__PURE__ */ jsx5("span", { className: "font-medium text-slate-700", children: value })
  ] });
}
function StatusBar(props) {
  const filtered = props.filteredRows !== props.totalRows;
  const s = props.rangeSummary;
  return /* @__PURE__ */ jsxs5("div", { className: "flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-slate-200 bg-slate-50 px-3 py-1.5 text-[11.5px]", children: [
    /* @__PURE__ */ jsx5(Stat, { label: "Rows", value: filtered ? `${formatNumber(props.filteredRows)} of ${formatNumber(props.totalRows)}` : formatNumber(props.totalRows) }),
    props.groupCount > 0 && /* @__PURE__ */ jsx5(Stat, { label: "Groups", value: formatNumber(props.groupCount) }),
    props.selectedRows > 0 && /* @__PURE__ */ jsx5(Stat, { label: "Selected", value: formatNumber(props.selectedRows) }),
    s && /* @__PURE__ */ jsxs5(Fragment2, { children: [
      /* @__PURE__ */ jsx5("span", { className: "ml-auto" }),
      /* @__PURE__ */ jsx5(Stat, { label: "Count", value: formatNumber(s.numericCount) }),
      /* @__PURE__ */ jsx5(Stat, { label: "Sum", value: formatNumber(s.sum) }),
      /* @__PURE__ */ jsx5(Stat, { label: "Avg", value: formatNumber(Math.round(s.avg * 100) / 100) }),
      /* @__PURE__ */ jsx5(Stat, { label: "Min", value: formatNumber(s.min) }),
      /* @__PURE__ */ jsx5(Stat, { label: "Max", value: formatNumber(s.max) })
    ] })
  ] });
}

// src/ToolPanel.tsx
import { useMemo as useMemo3, useState as useState5 } from "react";

// src/PivotPanel.tsx
import { useMemo as useMemo2, useState as useState4 } from "react";
import { jsx as jsx6, jsxs as jsxs6 } from "react/jsx-runtime";
var AGGS = [
  { value: "sum", label: "sum" },
  { value: "avg", label: "avg" },
  { value: "min", label: "min" },
  { value: "max", label: "max" },
  { value: "count", label: "count" }
];
function Zone({
  zone,
  title,
  icon,
  fields,
  hint,
  label,
  zoneOf,
  dragging,
  setDragging,
  overZone,
  setOverZone,
  onAssign,
  onReorder,
  onSetAgg,
  aggOf
}) {
  return /* @__PURE__ */ jsxs6(
    "div",
    {
      onDragOver: (e) => {
        if (dragging) {
          e.preventDefault();
          setOverZone(zone);
        }
      },
      onDragLeave: () => setOverZone((z) => z === zone ? null : z),
      onDrop: (e) => {
        e.preventDefault();
        setOverZone(null);
        if (dragging) onAssign(dragging, zone);
        setDragging(null);
      },
      className: `mb-2 rounded-md border p-2 transition-colors ${overZone === zone ? "border-[var(--eg-accent)] bg-[var(--eg-accent)]/10" : "border-slate-200 bg-slate-50/60"}`,
      children: [
        /* @__PURE__ */ jsxs6("div", { className: "mb-1 flex items-center gap-1.5 text-[11px] font-semibold text-slate-500", children: [
          icon,
          title
        ] }),
        fields.length === 0 ? /* @__PURE__ */ jsx6("div", { className: "px-1 py-1 text-[11px] italic text-slate-400", children: hint }) : /* @__PURE__ */ jsx6("div", { className: "flex flex-col gap-1", children: fields.map((f) => /* @__PURE__ */ jsxs6(
          "div",
          {
            draggable: true,
            onDragStart: () => setDragging(f),
            onDragEnd: () => {
              setDragging(null);
              setOverZone(null);
            },
            onDragOver: (e) => {
              if (dragging && dragging !== f) e.preventDefault();
            },
            onDrop: (e) => {
              e.preventDefault();
              e.stopPropagation();
              if (dragging && dragging !== f) {
                if (zoneOf(dragging) === zone) onReorder(zone, dragging, f);
                else {
                  onAssign(dragging, zone);
                  onReorder(zone, dragging, f);
                }
              }
              setDragging(null);
              setOverZone(null);
            },
            className: "flex cursor-grab items-center gap-1 rounded border border-slate-200 bg-white px-1.5 py-1 text-[12px]",
            children: [
              /* @__PURE__ */ jsx6(GripVertical, { size: 12, className: "shrink-0 text-slate-300" }),
              /* @__PURE__ */ jsx6("span", { className: "flex-1 truncate", children: label(f) }),
              zone === "values" && /* @__PURE__ */ jsx6(
                "select",
                {
                  value: typeof aggOf(f) === "string" ? String(aggOf(f)) : "sum",
                  onChange: (e) => onSetAgg(f, e.target.value),
                  onClick: (e) => e.stopPropagation(),
                  className: "rounded border border-slate-200 bg-white px-1 text-[10px] text-slate-600 outline-none",
                  children: AGGS.map((a) => /* @__PURE__ */ jsx6("option", { value: String(a.value), children: a.label }, String(a.value)))
                }
              ),
              /* @__PURE__ */ jsx6("button", { type: "button", onClick: () => onAssign(f, null), className: "text-slate-400 hover:text-rose-600", children: /* @__PURE__ */ jsx6(X, { size: 11 }) })
            ]
          },
          f
        )) })
      ]
    }
  );
}
function PivotPanel(props) {
  const {
    enabled,
    onToggleEnabled,
    columns,
    rowFields,
    columnFields,
    valueFields,
    onAssign,
    onReorder,
    onSetAgg,
    aggOf
  } = props;
  const [search, setSearch] = useState4("");
  const [dragging, setDragging] = useState4(null);
  const [overZone, setOverZone] = useState4(null);
  const label = (f) => columns.find((c) => c.field === f)?.headerName ?? f;
  const listed = useMemo2(() => {
    const q = search.trim().toLowerCase();
    return columns.filter((c) => !q || (c.headerName ?? c.field).toLowerCase().includes(q));
  }, [columns, search]);
  const zoneOf = (field) => rowFields.includes(field) ? "rows" : columnFields.includes(field) ? "columns" : valueFields.includes(field) ? "values" : null;
  const zoneCommon = {
    label,
    zoneOf,
    dragging,
    setDragging,
    overZone,
    setOverZone,
    onAssign,
    onReorder,
    onSetAgg,
    aggOf
  };
  return /* @__PURE__ */ jsxs6("div", { className: "flex flex-col", children: [
    /* @__PURE__ */ jsxs6("div", { className: "sticky top-0 z-10 border-b border-slate-100 bg-white p-2.5", children: [
      /* @__PURE__ */ jsxs6("label", { className: "mb-2 flex cursor-pointer items-center gap-2 text-[13px] font-medium text-slate-700", children: [
        /* @__PURE__ */ jsx6(
          "span",
          {
            role: "switch",
            "aria-checked": enabled,
            tabIndex: 0,
            onClick: () => onToggleEnabled(!enabled),
            onKeyDown: (e) => {
              if (e.key === " " || e.key === "Enter") {
                e.preventDefault();
                onToggleEnabled(!enabled);
              }
            },
            className: `relative inline-flex h-5 w-9 shrink-0 rounded-full transition-colors ${enabled ? "bg-[var(--eg-accent)]" : "bg-slate-300"}`,
            children: /* @__PURE__ */ jsx6("span", { className: `absolute top-0.5 h-4 w-4 rounded-full bg-white transition-all ${enabled ? "left-[18px]" : "left-0.5"}` })
          }
        ),
        "Pivot Mode"
      ] }),
      /* @__PURE__ */ jsxs6("div", { className: "relative", children: [
        /* @__PURE__ */ jsx6(Search, { size: 13, className: "absolute left-2 top-1/2 -translate-y-1/2 text-slate-400" }),
        /* @__PURE__ */ jsx6(
          "input",
          {
            className: "w-full rounded border border-slate-200 py-1.5 pl-7 pr-2 text-[13px] outline-none focus:border-[var(--eg-accent)]",
            placeholder: "Search\u2026",
            value: search,
            onChange: (e) => setSearch(e.target.value)
          }
        )
      ] })
    ] }),
    /* @__PURE__ */ jsxs6("div", { className: "p-2", children: [
      /* @__PURE__ */ jsxs6("div", { className: "mb-3 max-h-40 overflow-y-auto", children: [
        listed.map((c) => {
          const z = zoneOf(c.field);
          return /* @__PURE__ */ jsxs6(
            "div",
            {
              draggable: true,
              onDragStart: () => setDragging(c.field),
              onDragEnd: () => {
                setDragging(null);
                setOverZone(null);
              },
              className: "flex cursor-grab items-center gap-1.5 rounded px-1 py-1 text-[13px] hover:bg-slate-50",
              children: [
                /* @__PURE__ */ jsx6(
                  "input",
                  {
                    type: "checkbox",
                    checked: z != null,
                    onChange: () => onAssign(c.field, z ? null : c.type === "number" ? "values" : "rows"),
                    className: "accent-[var(--eg-accent)]"
                  }
                ),
                /* @__PURE__ */ jsx6(GripVertical, { size: 12, className: "shrink-0 text-slate-300" }),
                /* @__PURE__ */ jsx6("span", { className: `flex-1 truncate ${z ? "text-slate-800" : "text-slate-500"}`, children: c.headerName ?? c.field }),
                z && /* @__PURE__ */ jsx6("span", { className: "text-[9px] uppercase text-slate-400", children: z })
              ]
            },
            c.field
          );
        }),
        !listed.length && /* @__PURE__ */ jsx6("div", { className: "px-1 py-3 text-center text-[12px] text-slate-400", children: "No matches" })
      ] }),
      /* @__PURE__ */ jsx6(
        Zone,
        {
          ...zoneCommon,
          zone: "rows",
          title: "Row Groups",
          icon: /* @__PURE__ */ jsx6(Columns3, { size: 12 }),
          fields: rowFields,
          hint: "Drag a field here"
        }
      ),
      /* @__PURE__ */ jsx6(
        Zone,
        {
          ...zoneCommon,
          zone: "values",
          title: "Values",
          icon: /* @__PURE__ */ jsx6("span", { className: "text-[11px]", children: "\u03A3" }),
          fields: valueFields,
          hint: "Drag a numeric field here"
        }
      ),
      /* @__PURE__ */ jsx6(
        Zone,
        {
          ...zoneCommon,
          zone: "columns",
          title: "Column Labels",
          icon: /* @__PURE__ */ jsx6(Columns3, { size: 12 }),
          fields: columnFields,
          hint: "Drag a field here"
        }
      ),
      enabled && (!rowFields.length || !valueFields.length || !columnFields.length) && /* @__PURE__ */ jsx6("div", { className: "rounded border border-amber-200 bg-amber-50 px-2 py-1.5 text-[11px] text-amber-800", children: "A pivot needs at least one field in each of the three zones. Until then the grid shows its normal rows." })
    ] })
  ] });
}

// src/ToolPanel.tsx
import { jsx as jsx7, jsxs as jsxs7 } from "react/jsx-runtime";
var TAB_BTN = "flex w-full flex-col items-center gap-1 px-1.5 py-2.5 text-[10px] font-medium transition-colors";
function ToolPanel(props) {
  const { tab, onTabChange } = props;
  const activeFilters = countActive(props.filters);
  return /* @__PURE__ */ jsxs7("div", { className: "flex shrink-0 border-l border-slate-200 bg-white", children: [
    tab && /* @__PURE__ */ jsx7("div", { className: "w-[260px] overflow-y-auto border-r border-slate-200", children: tab === "columns" ? /* @__PURE__ */ jsx7(ColumnsPanel, { ...props }) : tab === "pivot" && props.pivot ? /* @__PURE__ */ jsx7(PivotPanel, { ...props.pivot }) : /* @__PURE__ */ jsx7(FiltersPanel, { ...props }) }),
    /* @__PURE__ */ jsxs7("div", { className: "flex w-[52px] flex-col bg-slate-50", children: [
      /* @__PURE__ */ jsxs7(
        "button",
        {
          type: "button",
          onClick: () => onTabChange(tab === "columns" ? null : "columns"),
          className: `${TAB_BTN} ${tab === "columns" ? "bg-white text-[var(--eg-primary)]" : "text-slate-500 hover:bg-white/60"}`,
          children: [
            /* @__PURE__ */ jsx7(Columns3, { size: 16 }),
            "Columns"
          ]
        }
      ),
      /* @__PURE__ */ jsxs7(
        "button",
        {
          type: "button",
          onClick: () => onTabChange(tab === "filters" ? null : "filters"),
          className: `${TAB_BTN} relative ${tab === "filters" ? "bg-white text-[var(--eg-primary)]" : "text-slate-500 hover:bg-white/60"}`,
          children: [
            /* @__PURE__ */ jsx7(Filter, { size: 16 }),
            "Filters",
            activeFilters > 0 && /* @__PURE__ */ jsx7("span", { className: "absolute right-1.5 top-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-[var(--eg-accent)] px-1 text-[9px] font-bold text-white", children: activeFilters })
          ]
        }
      ),
      props.pivot && /* @__PURE__ */ jsxs7(
        "button",
        {
          type: "button",
          onClick: () => onTabChange(tab === "pivot" ? null : "pivot"),
          className: `${TAB_BTN} relative ${tab === "pivot" ? "bg-white text-[var(--eg-primary)]" : "text-slate-500 hover:bg-white/60"}`,
          children: [
            /* @__PURE__ */ jsx7(Group, { size: 16 }),
            "Pivot",
            props.pivot.enabled && /* @__PURE__ */ jsx7("span", { className: "absolute right-2 top-2 h-2 w-2 rounded-full bg-[var(--eg-accent)]" })
          ]
        }
      )
    ] })
  ] });
}
function ColumnsPanel(props) {
  const [search, setSearch] = useState5("");
  const [dragField, setDragField] = useState5(null);
  const visible = useMemo3(() => {
    const q = search.trim().toLowerCase();
    if (!q) return props.columns;
    return props.columns.filter((c) => (c.headerName ?? c.field).toLowerCase().includes(q));
  }, [props.columns, search]);
  const shownCount = props.columns.filter((c) => !props.hidden.has(c.field)).length;
  return /* @__PURE__ */ jsxs7("div", { className: "flex flex-col", children: [
    /* @__PURE__ */ jsxs7("div", { className: "sticky top-0 z-10 border-b border-slate-100 bg-white p-2.5", children: [
      /* @__PURE__ */ jsxs7("div", { className: "relative mb-2", children: [
        /* @__PURE__ */ jsx7(Search, { size: 13, className: "absolute left-2 top-1/2 -translate-y-1/2 text-slate-400" }),
        /* @__PURE__ */ jsx7(
          "input",
          {
            className: "w-full rounded border border-slate-200 py-1.5 pl-7 pr-2 text-[13px] outline-none focus:border-[var(--eg-accent)]",
            placeholder: "Search columns\u2026",
            value: search,
            onChange: (e) => setSearch(e.target.value)
          }
        )
      ] }),
      /* @__PURE__ */ jsxs7("div", { className: "flex items-center justify-between text-[11px] text-slate-500", children: [
        /* @__PURE__ */ jsxs7("span", { children: [
          shownCount,
          " of ",
          props.columns.length,
          " shown"
        ] }),
        /* @__PURE__ */ jsxs7("div", { className: "flex gap-2", children: [
          /* @__PURE__ */ jsx7("button", { type: "button", className: "hover:text-[var(--eg-primary)]", onClick: () => props.onSetAllVisible(true), children: "All" }),
          /* @__PURE__ */ jsx7("button", { type: "button", className: "hover:text-[var(--eg-primary)]", onClick: () => props.onSetAllVisible(false), children: "None" }),
          /* @__PURE__ */ jsxs7("button", { type: "button", className: "flex items-center gap-0.5 hover:text-[var(--eg-primary)]", onClick: props.onResetColumns, children: [
            /* @__PURE__ */ jsx7(RotateCcw, { size: 11 }),
            " Reset"
          ] })
        ] })
      ] })
    ] }),
    /* @__PURE__ */ jsx7("div", { className: "p-1.5", children: visible.map((col) => {
      const isHidden = props.hidden.has(col.field);
      const pin = props.pinned[col.field] ?? null;
      const grouped = props.groupBy.includes(col.field);
      return /* @__PURE__ */ jsxs7(
        "div",
        {
          draggable: !search,
          onDragStart: () => setDragField(col.field),
          onDragOver: (e) => {
            if (dragField && dragField !== col.field) e.preventDefault();
          },
          onDrop: (e) => {
            e.preventDefault();
            if (dragField && dragField !== col.field) props.onReorder(dragField, col.field);
            setDragField(null);
          },
          onDragEnd: () => setDragField(null),
          className: `group flex items-center gap-1.5 rounded px-1.5 py-1 text-[13px] hover:bg-slate-50 ${dragField === col.field ? "opacity-40" : ""}`,
          children: [
            /* @__PURE__ */ jsx7(GripVertical, { size: 13, className: search ? "text-transparent" : "cursor-grab text-slate-300" }),
            /* @__PURE__ */ jsx7(
              "input",
              {
                type: "checkbox",
                checked: !isHidden,
                disabled: col.lockVisible,
                onChange: () => props.onToggleVisible(col.field),
                className: "accent-[var(--eg-accent)] disabled:opacity-40"
              }
            ),
            /* @__PURE__ */ jsx7("span", { className: `flex-1 truncate ${isHidden ? "text-slate-400" : "text-slate-700"}`, children: col.headerName ?? col.field }),
            col.enableRowGroup !== false && /* @__PURE__ */ jsx7(
              "button",
              {
                type: "button",
                title: grouped ? "Remove from row groups" : "Group by this column",
                onClick: () => props.onToggleGroup(col.field),
                className: `rounded px-1 text-[10px] font-bold transition-colors ${grouped ? "bg-[var(--eg-accent)] text-white" : "text-slate-300 hover:text-slate-600 group-hover:text-slate-500"}`,
                children: "G"
              }
            ),
            !col.lockPinned && /* @__PURE__ */ jsx7(
              "button",
              {
                type: "button",
                title: pin ? `Pinned ${pin} \u2014 click to cycle` : "Pin column",
                onClick: () => props.onTogglePin(col.field),
                className: `rounded p-0.5 transition-colors ${pin ? "text-[var(--eg-accent)]" : "text-slate-300 hover:text-slate-600"}`,
                children: /* @__PURE__ */ jsx7(Pin, { size: 12 })
              }
            )
          ]
        },
        col.field
      );
    }) })
  ] });
}
function FiltersPanel(props) {
  const [open, setOpen] = useState5(null);
  const filterable = props.columns.filter((c) => props.filterKinds[c.field]);
  const active = countActive(props.filters);
  return /* @__PURE__ */ jsxs7("div", { className: "flex flex-col", children: [
    /* @__PURE__ */ jsxs7("div", { className: "sticky top-0 z-10 flex items-center justify-between border-b border-slate-100 bg-white p-2.5 text-[11px] text-slate-500", children: [
      /* @__PURE__ */ jsxs7("span", { children: [
        active,
        " active filter",
        active === 1 ? "" : "s"
      ] }),
      /* @__PURE__ */ jsxs7(
        "button",
        {
          type: "button",
          disabled: !active,
          onClick: props.onClearFilters,
          className: "flex items-center gap-1 hover:text-rose-600 disabled:opacity-40",
          children: [
            /* @__PURE__ */ jsx7(X, { size: 11 }),
            " Clear all"
          ]
        }
      )
    ] }),
    /* @__PURE__ */ jsxs7("div", { className: "p-1.5", children: [
      filterable.map((col) => {
        const kind = props.filterKinds[col.field];
        const model = props.filters[col.field] ?? emptyModel(kind);
        const on = isActive(props.filters[col.field]);
        const expanded = open === col.field;
        return /* @__PURE__ */ jsxs7("div", { className: "mb-1 rounded border border-slate-100", children: [
          /* @__PURE__ */ jsxs7(
            "button",
            {
              type: "button",
              onClick: () => setOpen(expanded ? null : col.field),
              className: "flex w-full items-center gap-1.5 px-2 py-1.5 text-left text-[13px] text-slate-700 hover:bg-slate-50",
              children: [
                /* @__PURE__ */ jsx7(Filter, { size: 11, className: on ? "text-[var(--eg-accent)]" : "text-slate-300" }),
                /* @__PURE__ */ jsx7("span", { className: "flex-1 truncate", children: col.headerName ?? col.field }),
                /* @__PURE__ */ jsx7("span", { className: "text-[10px] text-slate-400", children: expanded ? "\u2212" : "+" })
              ]
            }
          ),
          expanded && /* @__PURE__ */ jsxs7("div", { className: "border-t border-slate-100 p-2", children: [
            /* @__PURE__ */ jsx7(
              FilterEditor,
              {
                kind,
                model,
                onChange: (m) => props.onFilterChange(col.field, m),
                col,
                rows: props.filterRowsFor(col.field)
              }
            ),
            on && /* @__PURE__ */ jsx7(
              "button",
              {
                type: "button",
                onClick: () => props.onFilterChange(col.field, void 0),
                className: "mt-1.5 text-[11px] text-slate-500 hover:text-rose-600",
                children: "Clear this filter"
              }
            )
          ] })
        ] }, col.field);
      }),
      filterable.length === 0 && /* @__PURE__ */ jsx7("div", { className: "px-2 py-6 text-center text-[12px] text-slate-400", children: "No filterable columns." })
    ] })
  ] });
}

// src/aggregations.ts
function runAgg(fn, values, rows) {
  if (typeof fn === "function") return fn(values, rows);
  switch (fn) {
    case "count":
      return values.length;
    case "first":
      return values.length ? values[0] : null;
    case "last":
      return values.length ? values[values.length - 1] : null;
    default:
      break;
  }
  const nums = values.map(toNumber).filter((n) => n != null);
  if (!nums.length) return null;
  switch (fn) {
    case "sum":
      return nums.reduce((a, b) => a + b, 0);
    case "avg":
      return nums.reduce((a, b) => a + b, 0) / nums.length;
    case "min":
      return Math.min(...nums);
    case "max":
      return Math.max(...nums);
    default:
      return null;
  }
}
function aggregateRows(cols, rows) {
  const out = {};
  for (const col of cols) {
    if (!col.aggFunc) continue;
    const values = [];
    for (const row of rows) {
      const v = rawValue(col, row);
      if (v != null && v !== "") values.push(v);
    }
    out[col.field] = runAgg(col.aggFunc, values, rows);
  }
  return out;
}
function summarize(values) {
  const nums = values.map(toNumber).filter((n) => n != null);
  if (!nums.length) return null;
  const sum = nums.reduce((a, b) => a + b, 0);
  return {
    count: values.length,
    numericCount: nums.length,
    sum,
    avg: sum / nums.length,
    min: Math.min(...nums),
    max: Math.max(...nums)
  };
}

// src/exporters.ts
function exportCell(col, row) {
  const raw = rawValue(col, row);
  if (col.exportValue) return col.exportValue(raw, row);
  if (col.valueFormatter) return col.valueFormatter(raw, row);
  return raw == null ? "" : String(raw);
}
function groupLabel(node, columns) {
  const col = columns.find((c) => c.field === node.field);
  const header = col?.headerName ?? node.field;
  return `${"    ".repeat(node.depth)}${header}: ${node.label} (${node.count})`;
}
function toMatrix(opts) {
  const cols = opts.columns.filter((c) => !c.suppressExport);
  const header = cols.map((c) => c.headerName ?? c.field);
  const rows = [header];
  for (const node of opts.nodes) {
    if (node.kind === "detail") continue;
    if (node.kind === "group") {
      const line = new Array(cols.length).fill("");
      line[0] = groupLabel(node, cols);
      cols.forEach((c, i) => {
        if (i === 0) return;
        const agg = node.aggregates[c.field];
        if (agg != null) line[i] = c.type === "number" ? toNumber(agg) : agg;
      });
      rows.push(line);
      continue;
    }
    rows.push(
      cols.map((c) => {
        if (c.type === "number" && !c.exportValue) return toNumber(rawValue(c, node.data));
        return exportCell(c, node.data);
      })
    );
  }
  if (opts.totals) {
    const line = new Array(cols.length).fill("");
    line[0] = "Total";
    cols.forEach((c, i) => {
      if (i === 0) return;
      const agg = opts.totals[c.field];
      if (agg != null) line[i] = c.type === "number" ? toNumber(agg) : agg;
    });
    rows.push(line);
  }
  return rows;
}
function csvEscape(v) {
  const s = v == null ? "" : String(v);
  return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}
function exportToCsv(opts) {
  const matrix = toMatrix(opts);
  const csv = matrix.map((r) => r.map(csvEscape).join(",")).join("\r\n");
  downloadBlob(new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8;" }), `${opts.fileName}.csv`);
}
async function exportToExcel(opts) {
  let XLSX;
  try {
    XLSX = await import("xlsx");
  } catch {
    throw new Error(
      'Excel export needs the optional "xlsx" package. Run `npm i xlsx`, or use exportToCsv instead.'
    );
  }
  const matrix = toMatrix(opts);
  const sheet = XLSX.utils.aoa_to_sheet(matrix);
  const cols = opts.columns.filter((c) => !c.suppressExport);
  sheet["!cols"] = cols.map((c, i) => {
    const headerLen = String(c.headerName ?? c.field).length;
    const widest = matrix.slice(1, 200).reduce((max, r) => Math.max(max, String(r[i] ?? "").length), headerLen);
    return { wch: Math.min(50, Math.max(10, widest + 2)) };
  });
  sheet["!autofilter"] = {
    ref: XLSX.utils.encode_range({
      s: { r: 0, c: 0 },
      e: { r: Math.max(0, matrix.length - 1), c: Math.max(0, cols.length - 1) }
    })
  };
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, "Data");
  XLSX.writeFile(book, `${opts.fileName}.xlsx`);
}
function rangeToTsv(columns, nodes, rowStart, rowEnd, colStart, colEnd, includeHeaders) {
  const cols = columns.slice(colStart, colEnd + 1);
  const lines = [];
  if (includeHeaders) lines.push(cols.map((c) => c.headerName ?? c.field).join("	"));
  for (let r = rowStart; r <= rowEnd && r < nodes.length; r++) {
    const node = nodes[r];
    if (node.kind === "detail") continue;
    if (node.kind === "group") {
      lines.push(cols.map((c, i) => i === 0 ? `${node.label} (${node.count})` : node.aggregates[c.field] ?? "").join("	"));
      continue;
    }
    lines.push(cols.map((c) => displayValue(c, node.data)).join("	"));
  }
  return lines.join("\n");
}
async function copyToClipboard(text2) {
  try {
    await navigator.clipboard.writeText(text2);
    return true;
  } catch {
    const ta = document.createElement("textarea");
    ta.value = text2;
    ta.style.position = "fixed";
    ta.style.opacity = "0";
    document.body.appendChild(ta);
    ta.select();
    let ok = false;
    try {
      ok = document.execCommand("copy");
    } catch {
      ok = false;
    }
    ta.remove();
    return ok;
  }
}

// src/rowModel.ts
function sortRows(rows, sort, cols, types) {
  if (!sort.length) return rows;
  const byField = new Map(cols.map((c) => [c.field, c]));
  const specs = sort.filter((s) => byField.has(s.field));
  if (!specs.length) return rows;
  return [...rows].sort((a, b) => {
    for (const spec of specs) {
      const col = byField.get(spec.field);
      const cmp = compareBy(rawValue(col, a), rawValue(col, b), types[spec.field] ?? "text", spec.dir);
      if (cmp !== 0) return cmp;
    }
    return 0;
  });
}
function buildGroups(rows, groupBy, cols, depth = 0, idPrefix = "g") {
  const field = groupBy[depth];
  const col = cols.find((c) => c.field === field);
  const buckets = /* @__PURE__ */ new Map();
  for (const row of rows) {
    const key = col ? displayValue(col, row) : String(row[field] ?? "");
    const bucket = buckets.get(key);
    if (bucket) bucket.push(row);
    else buckets.set(key, [row]);
  }
  const out = [];
  let i = 0;
  for (const [key, bucket] of buckets) {
    const id = `${idPrefix}:${depth}:${i++}:${key}`;
    const node = {
      kind: "group",
      id,
      field,
      key,
      label: key === "" ? "(Blank)" : key,
      depth,
      count: bucket.length,
      aggregates: aggregateRows(cols, bucket),
      leaves: bucket,
      children: [],
      index: -1
    };
    node.children = depth + 1 < groupBy.length ? buildGroups(bucket, groupBy, cols, depth + 1, id) : bucket.map((data, j) => ({
      kind: "leaf",
      id: `${id}:l${j}`,
      data,
      depth: depth + 1,
      index: -1
    }));
    out.push(node);
  }
  return out;
}
function flatten(nodes, opts) {
  const out = [];
  const pushLeaf = (leaf) => {
    const raw = opts.getId(leaf.data);
    const id = raw === "__pending__" ? `__pending__${out.length}` : raw;
    out.push({ ...leaf, id, index: out.length });
    if (opts.hasDetail && opts.expandedDetails.has(id)) {
      const detail = {
        kind: "detail",
        id: `${id}::detail`,
        parentId: id,
        data: leaf.data,
        depth: leaf.depth,
        height: opts.detailRowHeight,
        index: out.length
      };
      out.push(detail);
    }
  };
  const walk = (list) => {
    for (const node of list) {
      if (node.kind === "leaf") {
        pushLeaf(node);
        continue;
      }
      out.push({ ...node, index: out.length });
      if (!opts.collapsed.has(node.id)) walk(node.children);
    }
  };
  walk(nodes);
  return out;
}
function collectGroupIds(nodes, into = []) {
  for (const node of nodes) {
    if (node.kind === "group") {
      into.push(node.id);
      collectGroupIds(node.children, into);
    }
  }
  return into;
}
function buildOffsets(nodes, rowHeight) {
  const tops = new Array(nodes.length);
  let y = 0;
  for (let i = 0; i < nodes.length; i++) {
    tops[i] = y;
    y += nodes[i].kind === "detail" ? nodes[i].height : rowHeight;
  }
  return { tops, total: y };
}

// src/treeModel.ts
function buildTree(rows, cols, opts) {
  const { getDataPath, parentField, getRowId } = opts;
  const roots = /* @__PURE__ */ new Map();
  const ensure = (path) => {
    let level = roots;
    let node;
    for (let i = 0; i < path.length; i++) {
      const key = path[i];
      node = level.get(key);
      if (!node) {
        node = { key, path: path.slice(0, i + 1), children: /* @__PURE__ */ new Map() };
        level.set(key, node);
      }
      level = node.children;
    }
    return node;
  };
  if (getDataPath) {
    for (const row of rows) {
      const path = getDataPath(row);
      if (!path?.length) continue;
      ensure(path).data = row;
    }
  } else if (parentField) {
    const byId = /* @__PURE__ */ new Map();
    for (const row of rows) byId.set(getRowId(row), row);
    const pathCache = /* @__PURE__ */ new Map();
    const pathOf = (row) => {
      const id = getRowId(row);
      const cached = pathCache.get(id);
      if (cached) return cached;
      const path = [];
      const seen = /* @__PURE__ */ new Set();
      let cur = row;
      while (cur) {
        const cid = getRowId(cur);
        if (seen.has(cid)) break;
        seen.add(cid);
        path.unshift(cid);
        const pid = cur[parentField];
        if (pid == null || pid === "" || pid === cid) break;
        cur = byId.get(String(pid));
      }
      pathCache.set(id, path);
      return path;
    };
    for (const row of rows) {
      const path = pathOf(row);
      if (path.length) ensure(path).data = row;
    }
  } else {
    return rows.map((data, i) => ({ kind: "leaf", id: String(i), data, depth: 0, index: -1 }));
  }
  const toNode = (b, depth, idPrefix) => {
    const id = `${idPrefix}/${b.key}`;
    if (!b.children.size && b.data) {
      return { kind: "leaf", id, data: b.data, depth, index: -1 };
    }
    const children = [...b.children.values()].map((c) => toNode(c, depth + 1, id));
    const leaves = collectLeaves(children);
    return {
      kind: "group",
      id,
      field: "__tree__",
      key: b.key,
      label: b.data ? labelFor(b, cols) : opts.missingLabel?.(b.key) ?? b.key,
      depth,
      count: leaves.length,
      // Parents aggregate their descendants, like a grouping bucket. Where a
      // node also has its own row, the renderer prefers its real value for
      // columns that declare no aggFunc.
      aggregates: aggregateRows(cols, leaves),
      leaves,
      children,
      index: -1,
      data: b.data
    };
  };
  return [...roots.values()].map((b) => toNode(b, 0, "t"));
}
function collectLeaves(nodes) {
  const out = [];
  for (const n of nodes) {
    if (n.kind === "leaf") out.push(n.data);
    else {
      out.push(...n.leaves);
      if (n.data) out.push(n.data);
    }
  }
  return out;
}
function labelFor(b, cols) {
  if (!b.data) return b.key;
  const first = cols.find((c) => !c.hide && c.type !== "number");
  if (!first) return b.key;
  const v = first.valueGetter ? first.valueGetter(b.data) : b.data[first.field];
  return v == null || v === "" ? b.key : String(v);
}
function collectTreeIds(nodes, into = []) {
  for (const n of nodes) {
    if (n.kind === "group") {
      into.push(n.id);
      collectTreeIds(n.children, into);
    }
  }
  return into;
}

// src/pivot.ts
var OTHER = "Other";
function defaultCellFormat(v) {
  if (v == null || v === "") return "";
  const n = Number(v);
  if (!Number.isFinite(n)) return String(v);
  return Number.isInteger(n) ? String(n) : n.toFixed(2);
}
function pivotFieldName(pivotValue, valueField) {
  return `pv::${pivotValue}::${valueField}`;
}
function parsePivotField(field) {
  if (!field.startsWith("pv::")) return null;
  const rest = field.slice(4);
  const i = rest.lastIndexOf("::");
  if (i < 0) return null;
  return { pivotValue: rest.slice(0, i), valueField: rest.slice(i + 2) };
}
function buildPivot(rows, columns, opts) {
  const { rowFields, valueFields, maxColumns = 40, totalColumn = true } = opts;
  const pivotFields = opts.pivotFields?.length ? opts.pivotFields : opts.pivotField ? [opts.pivotField] : [];
  const byField = new Map(columns.map((c) => [c.field, c]));
  const pivotKeyOf = (row) => pivotFields.map((f) => {
    const c = byField.get(f);
    return c ? displayValue(c, row) : String(row[f] ?? "");
  }).join(" \u203A ");
  const weight = /* @__PURE__ */ new Map();
  for (const row of rows) {
    const key = pivotKeyOf(row);
    let w = 0;
    for (const vf of valueFields) w += Math.abs(Number(row[vf]) || 0);
    weight.set(key, (weight.get(key) ?? 0) + (w || 1));
  }
  const ordered = [...weight.entries()].sort((a, b) => b[1] - a[1]).map(([k]) => k);
  const kept = ordered.slice(0, maxColumns);
  const truncated = ordered.length > kept.length;
  const keptSet = new Set(kept);
  const pivotValues = truncated ? [...kept, OTHER] : kept;
  const groups = /* @__PURE__ */ new Map();
  for (const row of rows) {
    const keyParts = rowFields.map((f) => {
      const c = byField.get(f);
      return c ? displayValue(c, row) : String(row[f] ?? "");
    });
    const gk = keyParts.join("\0");
    let g = groups.get(gk);
    if (!g) {
      g = { keyParts, byPivot: /* @__PURE__ */ new Map(), all: [] };
      groups.set(gk, g);
    }
    const raw = pivotKeyOf(row);
    const pv = keptSet.has(raw) ? raw : OTHER;
    g.byPivot.set(pv, [...g.byPivot.get(pv) ?? [], row]);
    g.all.push(row);
  }
  const outRows = [];
  let i = 0;
  for (const [, g] of groups) {
    const rec = { __pivotId: String(i++) };
    rowFields.forEach((f, k) => {
      rec[f] = g.keyParts[k];
    });
    for (const pv of pivotValues) {
      const bucket = g.byPivot.get(pv) ?? [];
      for (const vf of valueFields) {
        const col = byField.get(vf);
        const values = bucket.map((r) => rawValue(col ?? { field: vf }, r)).filter((v) => v != null && v !== "");
        rec[pivotFieldName(pv, vf)] = values.length ? runAgg(col?.aggFunc ?? "sum", values, bucket) : null;
      }
    }
    if (totalColumn) {
      for (const vf of valueFields) {
        const col = byField.get(vf);
        const values = g.all.map((r) => rawValue(col ?? { field: vf }, r)).filter((v) => v != null && v !== "");
        rec[`total::${vf}`] = values.length ? runAgg(col?.aggFunc ?? "sum", values, g.all) : null;
      }
    }
    outRows.push(rec);
  }
  const outCols = rowFields.map((f, idx) => {
    const src = byField.get(f);
    return {
      field: f,
      headerName: src?.headerName ?? f,
      width: src?.width ?? 170,
      pinned: idx === 0 ? "left" : void 0,
      filter: "set"
    };
  });
  for (const pv of pivotValues) {
    for (const vf of valueFields) {
      const src = byField.get(vf);
      outCols.push({
        field: pivotFieldName(pv, vf),
        // With one value field the pivot value alone is the clearest header;
        // with several it has to carry both.
        headerName: valueFields.length > 1 ? src?.headerName ?? vf : pv,
        headerGroup: valueFields.length > 1 ? pv : void 0,
        headerTooltip: `${pv} \u2014 ${src?.headerName ?? vf}`,
        type: "number",
        align: "right",
        width: 130,
        aggFunc: src?.aggFunc ?? "sum",
        valueFormatter: src?.valueFormatter ?? defaultCellFormat,
        enableRowGroup: false
      });
    }
  }
  if (totalColumn) {
    for (const vf of valueFields) {
      const src = byField.get(vf);
      outCols.push({
        field: `total::${vf}`,
        headerName: valueFields.length > 1 ? `Total ${src?.headerName ?? vf}` : "Total",
        type: "number",
        align: "right",
        width: 140,
        pinned: "right",
        aggFunc: src?.aggFunc ?? "sum",
        valueFormatter: src?.valueFormatter ?? defaultCellFormat,
        enableRowGroup: false
      });
    }
  }
  return { rows: outRows, columns: outCols, pivotValues, truncated };
}

// src/serverSide.ts
import { useCallback, useEffect as useEffect2, useMemo as useMemo4, useRef as useRef2, useState as useState6 } from "react";
function useServerSideRows(datasource, params, opts = {}) {
  const { blockSize = 100, maxBlocks = 20, debounceMs = 200 } = opts;
  const [blocks, setBlocks] = useState6(() => /* @__PURE__ */ new Map());
  const [rowCount, setRowCount] = useState6(0);
  const [inFlight, setInFlight] = useState6(0);
  const [error, setError] = useState6(null);
  const loaded = useRef2(/* @__PURE__ */ new Set());
  const pending = useRef2(/* @__PURE__ */ new Set());
  const wanted = useRef2(/* @__PURE__ */ new Set());
  const timer = useRef2(null);
  const generation = useRef2(0);
  const countKnown = useRef2(false);
  const queryKey = useMemo4(
    () => JSON.stringify({
      sort: params.sort,
      filters: params.filters,
      quick: params.quickFilter,
      groupBy: params.groupBy,
      groupKeys: params.groupKeys ?? []
    }),
    [params.sort, params.filters, params.quickFilter, params.groupBy, params.groupKeys]
  );
  const paramsRef = useRef2(params);
  paramsRef.current = params;
  const dsRef = useRef2(datasource);
  dsRef.current = datasource;
  useEffect2(() => {
    generation.current++;
    countKnown.current = false;
    pending.current.clear();
    wanted.current.clear();
    loaded.current.clear();
    setBlocks(/* @__PURE__ */ new Map());
    setRowCount(0);
    setError(null);
    wanted.current.add(0);
    scheduleFetch();
  }, [queryKey, datasource]);
  const fetchBlock = useCallback(async (block, gen) => {
    const ds = dsRef.current;
    if (!ds) return;
    pending.current.add(block);
    setInFlight((n) => n + 1);
    try {
      const p2 = paramsRef.current;
      const result = await ds.getRows({
        startRow: block * blockSize,
        endRow: (block + 1) * blockSize,
        sortModel: p2.sort,
        filterModel: p2.filters,
        quickFilter: p2.quickFilter,
        groupKeys: p2.groupKeys ?? [],
        groupBy: p2.groupBy
      });
      if (gen !== generation.current) return;
      setBlocks((prev) => {
        const next = new Map(prev);
        next.set(block, result.rows);
        if (next.size > maxBlocks) {
          const sorted = [...next.keys()].sort((a, b) => Math.abs(b - block) - Math.abs(a - block));
          for (const k of sorted.slice(0, next.size - maxBlocks)) next.delete(k);
        }
        loaded.current = new Set(next.keys());
        return next;
      });
      if (result.rowCount != null) {
        countKnown.current = true;
        setRowCount(result.rowCount);
      } else if (result.rows.length < blockSize) {
        countKnown.current = true;
        setRowCount(block * blockSize + result.rows.length);
      } else if (!countKnown.current) {
        setRowCount((c) => Math.max(c, (block + 2) * blockSize));
      }
      setError(null);
    } catch (e) {
      if (gen === generation.current) setError(e instanceof Error ? e : new Error(String(e)));
    } finally {
      pending.current.delete(block);
      setInFlight((n) => Math.max(0, n - 1));
    }
  }, [blockSize, maxBlocks]);
  const scheduleFetch = useCallback(() => {
    if (timer.current != null) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      timer.current = null;
      const gen = generation.current;
      for (const b of wanted.current) {
        if (pending.current.has(b)) continue;
        void fetchBlock(b, gen);
      }
      wanted.current.clear();
    }, debounceMs);
  }, [debounceMs, fetchBlock]);
  const ensureRange = useCallback((first, last) => {
    const from = Math.max(0, Math.floor(first / blockSize));
    const to = Math.max(0, Math.floor(last / blockSize));
    let added = false;
    for (let b = from; b <= to; b++) {
      if (!loaded.current.has(b) && !pending.current.has(b) && !wanted.current.has(b)) {
        wanted.current.add(b);
        added = true;
      }
    }
    if (added) scheduleFetch();
  }, [blockSize, scheduleFetch]);
  const refresh = useCallback(() => {
    generation.current++;
    countKnown.current = false;
    pending.current.clear();
    wanted.current.clear();
    loaded.current.clear();
    setBlocks(/* @__PURE__ */ new Map());
    wanted.current.add(0);
    scheduleFetch();
  }, [scheduleFetch]);
  useEffect2(() => () => {
    if (timer.current != null) clearTimeout(timer.current);
  }, []);
  const rows = useMemo4(() => {
    const out = new Array(rowCount).fill(null);
    for (const [block, list] of blocks) {
      const base = block * blockSize;
      for (let i = 0; i < list.length; i++) {
        const idx = base + i;
        if (idx < rowCount) out[idx] = list[i];
      }
    }
    return out;
  }, [blocks, rowCount, blockSize]);
  return { rows, rowCount, loading: inFlight > 0, error, ensureRange, refresh };
}

// src/EnterpriseGrid.tsx
import { Fragment as Fragment3, jsx as jsx8, jsxs as jsxs8 } from "react/jsx-runtime";
var SELECT_COL = "__select__";
var GROUP_COL = "__group__";
var HEADER_HEIGHT = 40;
var DEFAULT_WIDTH = 150;
var MIN_WIDTH = 60;
var OVERSCAN = 8;
function loadState(key) {
  if (!key) return null;
  try {
    const raw = localStorage.getItem(`grid:${key}`);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}
function saveState(key, state) {
  if (!key) return;
  try {
    localStorage.setItem(`grid:${key}`, JSON.stringify(state));
  } catch {
  }
}
function EnterpriseGridInner(props, apiRef) {
  const [pivotOn, setPivotOn] = useState7(!!props.pivotMode);
  const [pivotRows, setPivotRows] = useState7(
    () => props.pivotRowFields ?? props.columns.filter((c) => c.rowGroup).map((c) => c.field)
  );
  const [pivotCols, setPivotCols] = useState7(
    () => props.pivotFields ?? (props.pivotField ? [props.pivotField] : [])
  );
  const [pivotVals, setPivotVals] = useState7(
    () => props.pivotValueFields ?? props.columns.filter((c) => c.aggFunc).map((c) => c.field)
  );
  const [pivotAggs, setPivotAggs] = useState7({});
  const pivoted = useMemo5(() => {
    if (!pivotOn || !pivotCols.length || !pivotRows.length || !pivotVals.length) return null;
    const cols = props.columns.map((c) => pivotAggs[c.field] ? { ...c, aggFunc: pivotAggs[c.field] } : c);
    return buildPivot(props.rows, cols, {
      rowFields: pivotRows,
      pivotFields: pivotCols,
      valueFields: pivotVals,
      maxColumns: props.pivotMaxColumns,
      totalColumn: props.pivotTotalColumn
    });
  }, [
    pivotOn,
    pivotRows,
    pivotCols,
    pivotVals,
    pivotAggs,
    props.pivotMaxColumns,
    props.pivotTotalColumn,
    props.rows,
    props.columns
  ]);
  const assignPivot = useCallback2((field, zone) => {
    const drop = (list) => list.filter((f) => f !== field);
    setPivotRows((r) => zone === "rows" ? [...drop(r), field] : drop(r));
    setPivotCols((c) => zone === "columns" ? [...drop(c), field] : drop(c));
    setPivotVals((v) => zone === "values" ? [...drop(v), field] : drop(v));
  }, []);
  const reorderPivot = useCallback2((zone, field, before) => {
    const move = (list) => {
      const next = list.filter((f) => f !== field);
      const at = before ? next.indexOf(before) : next.length;
      next.splice(at < 0 ? next.length : at, 0, field);
      return next;
    };
    if (zone === "rows") setPivotRows(move);
    else if (zone === "columns") setPivotCols(move);
    else setPivotVals(move);
  }, []);
  const {
    columns: rawColumns,
    rows: rawRows,
    getRowId: rawGetRowId,
    pivotPanel = false,
    datasource,
    serverSideOptions,
    treeData = false,
    getDataPath,
    parentField,
    loading = false,
    height = 560,
    density = "normal",
    rowHeight: rowHeightProp,
    pagination = false,
    pageSize: pageSizeProp = 100,
    pageSizeOptions = [25, 50, 100, 250, 500],
    selection = "none",
    onSelectionChanged,
    onViewChanged,
    onRowClick,
    onRowDoubleClick,
    onCellValueChanged,
    masterDetail,
    detailRowHeight = 220,
    expandedDetailIds,
    onDetailToggle,
    sideBar = true,
    statusBar = true,
    groupPanel = true,
    toolbar = true,
    contextMenu = true,
    totalsRow = false,
    quickFilter: quickFilterProp,
    onQuickFilterChange,
    exportFileName = "export",
    stateKey,
    emptyMessage = "No rows to show.",
    className = "",
    rowClass,
    toolbarExtras
  } = props;
  const columns = pivoted?.columns ?? rawColumns;
  const rows = pivoted?.rows ?? rawRows;
  const getRowId = pivoted ? "__pivotId" : rawGetRowId;
  const serverSide = !!datasource && !pivoted;
  const persisted = useMemo5(() => loadState(stateKey), [stateKey]);
  const fieldList = useMemo5(() => columns.map((c) => c.field), [columns]);
  const [colOrder, setColOrder] = useState7(() => {
    const saved = persisted?.columns?.map((c) => c.field).filter((f) => fieldList.includes(f)) ?? [];
    return [...saved, ...fieldList.filter((f) => !saved.includes(f))];
  });
  const [colWidths, setColWidths] = useState7(() => {
    const out = {};
    for (const c of persisted?.columns ?? []) if (c.width) out[c.field] = c.width;
    return out;
  });
  const [hidden, setHidden] = useState7(() => {
    const saved = persisted?.columns;
    if (saved?.length) return new Set(saved.filter((c) => c.hide).map((c) => c.field));
    return new Set(columns.filter((c) => c.hide).map((c) => c.field));
  });
  const [pinnedMap, setPinnedMap] = useState7(() => {
    const out = {};
    for (const c of columns) if (c.pinned) out[c.field] = c.pinned;
    for (const c of persisted?.columns ?? []) out[c.field] = c.pinned ?? null;
    return out;
  });
  useEffect3(() => {
    setColOrder((prev) => {
      const kept = prev.filter((f) => fieldList.includes(f));
      const added = fieldList.filter((f) => !kept.includes(f));
      return added.length || kept.length !== prev.length ? [...kept, ...added] : prev;
    });
  }, [fieldList]);
  const [sort, setSort] = useState7(persisted?.sort ?? []);
  const [filters, setFilters] = useState7(persisted?.filters ?? {});
  const [groupBy, setGroupBy] = useState7(
    persisted?.groupBy ?? columns.filter((c) => c.rowGroup).map((c) => c.field)
  );
  const [innerQuick, setInnerQuick] = useState7(persisted?.quickFilter ?? "");
  const quick = quickFilterProp !== void 0 ? quickFilterProp : innerQuick;
  const setQuick = useCallback2(
    (v) => {
      if (quickFilterProp === void 0) setInnerQuick(v);
      onQuickFilterChange?.(v);
    },
    [quickFilterProp, onQuickFilterChange]
  );
  const [collapsed, setCollapsed] = useState7(/* @__PURE__ */ new Set());
  const [innerDetails, setInnerDetails] = useState7(/* @__PURE__ */ new Set());
  const expandedDetails = useMemo5(
    () => expandedDetailIds ? new Set(expandedDetailIds) : innerDetails,
    [expandedDetailIds, innerDetails]
  );
  const toggleDetail = useCallback2(
    (rowId) => {
      const willExpand = !expandedDetails.has(rowId);
      onDetailToggle?.(rowId, willExpand);
      if (expandedDetailIds) return;
      setInnerDetails((prev) => {
        const next = new Set(prev);
        if (willExpand) next.add(rowId);
        else next.delete(rowId);
        return next;
      });
    },
    [expandedDetails, expandedDetailIds, onDetailToggle]
  );
  const [selectedIds, setSelectedIds] = useState7(/* @__PURE__ */ new Set());
  const [page, setPage] = useState7(0);
  const [pageSize, setPageSize] = useState7(pageSizeProp);
  const [toolTab, setToolTab] = useState7(null);
  const [menuField, setMenuField] = useState7(null);
  const [menuAnchor, setMenuAnchor] = useState7(null);
  const [gridMenuAnchor, setGridMenuAnchor] = useState7(null);
  const [ctxMenu, setCtxMenu] = useState7(null);
  const [maximized, setMaximized] = useState7(false);
  const [focusCell, setFocusCell] = useState7(null);
  const [rangeEnd, setRangeEnd] = useState7(null);
  const [editing, setEditing] = useState7(null);
  const [dragField, setDragField] = useState7(null);
  const [dragOverField, setDragOverField] = useState7(null);
  const [scrollTop, setScrollTop] = useState7(0);
  const [viewport, setViewport] = useState7({ width: 0, height: 0 });
  const viewportRef = useRef3(null);
  const rootRef = useRef3(null);
  const isDraggingRange = useRef3(false);
  const rowHeight = rowHeightProp ?? densityRowHeight(density);
  const getId = useCallback2(
    (row) => {
      if (row == null) return "__pending__";
      return typeof getRowId === "function" ? getRowId(row) : String(row?.[getRowId] ?? "");
    },
    [getRowId]
  );
  const types = useMemo5(() => {
    const sample = rows.slice(0, 100);
    const out = {};
    for (const c of columns) out[c.field] = inferType(c, sample);
    return out;
  }, [columns, rows]);
  const filterKinds = useMemo5(() => {
    const out = {};
    for (const c of columns) out[c.field] = filterKindFor(c, types[c.field] ?? "text");
    return out;
  }, [columns, types]);
  const colByField = useMemo5(() => new Map(columns.map((c) => [c.field, c])), [columns]);
  const orderedDefs = useMemo5(
    () => colOrder.map((f) => colByField.get(f)).filter((c) => !!c),
    [colOrder, colByField]
  );
  const server = useServerSideRows(
    serverSide ? datasource : null,
    { sort, filters, quickFilter: quick, groupBy },
    serverSideOptions
  );
  const filterPass = useMemo5(
    () => serverSide ? { rows: [], rowsExcluding: () => [] } : applyFilters(rows, columns, filters, quick),
    [serverSide, rows, columns, filters, quick]
  );
  const sorted = useMemo5(
    () => serverSide ? server.rows : sortRows(filterPass.rows, sort, columns, types),
    [serverSide, server.rows, filterPass.rows, sort, columns, types]
  );
  const treeRoots = useMemo5(
    () => treeData && (getDataPath || parentField) ? buildTree(sorted, columns, { getDataPath, parentField, getRowId: getId }) : null,
    [treeData, getDataPath, parentField, sorted, columns, getId]
  );
  const groupTree = useMemo5(
    () => !treeRoots && !serverSide && groupBy.length ? buildGroups(sorted, groupBy, columns) : null,
    [treeRoots, serverSide, sorted, groupBy, columns]
  );
  const allNodes = useMemo5(() => {
    const base = treeRoots ? treeRoots : groupTree ? groupTree : sorted.map((data, i) => ({ kind: "leaf", id: String(i), data, depth: 0, index: i }));
    return flatten(base, {
      getId,
      collapsed,
      expandedDetails,
      detailRowHeight,
      hasDetail: !!masterDetail
    });
  }, [groupTree, sorted, collapsed, expandedDetails, detailRowHeight, masterDetail, getId]);
  const totalPages = pagination ? Math.max(1, Math.ceil(allNodes.length / pageSize)) : 1;
  const safePage = Math.min(page, totalPages - 1);
  const nodes = useMemo5(
    () => pagination ? allNodes.slice(safePage * pageSize, (safePage + 1) * pageSize) : allNodes,
    [allNodes, pagination, safePage, pageSize]
  );
  const { tops, total: bodyHeight } = useMemo5(() => buildOffsets(nodes, rowHeight), [nodes, rowHeight]);
  const grandTotals = useMemo5(
    () => totalsRow ? aggregateRows(columns, sorted) : null,
    [totalsRow, columns, sorted]
  );
  useEffect3(() => {
    setPage(0);
  }, [filters, quick, groupBy, pageSize, sort]);
  const viewCbRef = useRef3(onViewChanged);
  viewCbRef.current = onViewChanged;
  useEffect3(() => {
    viewCbRef.current?.(sorted);
  }, [sorted]);
  const renderCols = useMemo5(() => {
    const visible = orderedDefs.filter((c) => !hidden.has(c.field) && !groupBy.includes(c.field));
    const base = visible.map((def) => ({
      id: def.field,
      def,
      header: def.headerName ?? def.field,
      width: colWidths[def.field] ?? def.width ?? DEFAULT_WIDTH,
      pinned: pinnedMap[def.field] ?? null,
      align: def.align ?? defaultAlign(types[def.field] ?? "text")
    }));
    const lead = [];
    if (selection !== "none") {
      lead.push({ id: SELECT_COL, def: null, header: "", width: 42, pinned: "left", special: "select", align: "center" });
    }
    if (groupBy.length || treeRoots) {
      lead.push({
        id: GROUP_COL,
        def: null,
        header: treeRoots ? "Hierarchy" : "Group",
        width: colWidths[GROUP_COL] ?? (treeRoots ? 300 : 240 + (groupBy.length - 1) * 16),
        pinned: "left",
        special: "group",
        align: "left"
      });
    }
    const all = [...lead, ...base];
    const flexCols = all.filter((c) => c.def?.flex);
    if (flexCols.length && viewport.width > 0) {
      const fixed = all.filter((c) => !c.def?.flex).reduce((s, c) => s + c.width, 0);
      const leftover = viewport.width - fixed - 2;
      if (leftover > 0) {
        const totalFlex = flexCols.reduce((s, c) => s + (c.def.flex ?? 1), 0);
        for (const c of flexCols) {
          if (colWidths[c.id] != null) continue;
          const share = leftover * (c.def.flex ?? 1) / totalFlex;
          c.width = Math.max(c.def.minWidth ?? MIN_WIDTH, Math.round(share));
        }
      }
    }
    const rank = (p2) => p2 === "left" ? 0 : p2 === "right" ? 2 : 1;
    return all.sort((a, b) => rank(a.pinned) - rank(b.pinned));
  }, [orderedDefs, hidden, groupBy, colWidths, pinnedMap, selection, types, viewport.width]);
  const stickyOffsets = useMemo5(() => {
    const left = {};
    const right = {};
    let acc = 0;
    for (const c of renderCols) {
      if (c.pinned !== "left") continue;
      left[c.id] = acc;
      acc += c.width;
    }
    acc = 0;
    for (let i = renderCols.length - 1; i >= 0; i--) {
      const c = renderCols[i];
      if (c.pinned !== "right") continue;
      right[c.id] = acc;
      acc += c.width;
    }
    return { left, right };
  }, [renderCols]);
  const totalWidth = useMemo5(() => renderCols.reduce((s, c) => s + c.width, 0), [renderCols]);
  const layoutWidth = Math.max(totalWidth, viewport.width || 0);
  const selectedRows = useMemo5(
    () => sorted.filter((r) => selectedIds.has(getId(r))),
    [sorted, selectedIds, getId]
  );
  const selectionSignature = useMemo5(() => [...selectedIds].sort().join("|"), [selectedIds]);
  const selectionCbRef = useRef3(onSelectionChanged);
  selectionCbRef.current = onSelectionChanged;
  const selectedRowsRef = useRef3(selectedRows);
  selectedRowsRef.current = selectedRows;
  useEffect3(() => {
    selectionCbRef.current?.(selectedRowsRef.current);
  }, [selectionSignature]);
  const toggleRowSelection = useCallback2(
    (id, additive) => {
      setSelectedIds((prev) => {
        if (selection === "single") return prev.has(id) ? /* @__PURE__ */ new Set() : /* @__PURE__ */ new Set([id]);
        const next = new Set(additive ? prev : prev);
        if (next.has(id)) next.delete(id);
        else next.add(id);
        return next;
      });
    },
    [selection]
  );
  const setGroupSelection = useCallback2(
    (node, select) => {
      setSelectedIds((prev) => {
        const next = new Set(prev);
        for (const leaf of node.leaves) {
          const id = getId(leaf);
          if (select) next.add(id);
          else next.delete(id);
        }
        return next;
      });
    },
    [getId]
  );
  const allFilteredSelected = sorted.length > 0 && sorted.every((r) => selectedIds.has(getId(r)));
  const toggleSelectAll = useCallback2(() => {
    setSelectedIds((prev) => {
      if (sorted.length && sorted.every((r) => prev.has(getId(r)))) {
        const next2 = new Set(prev);
        for (const r of sorted) next2.delete(getId(r));
        return next2;
      }
      const next = new Set(prev);
      for (const r of sorted) next.add(getId(r));
      return next;
    });
  }, [sorted, getId]);
  const sortDirOf = useCallback2((field) => sort.find((s) => s.field === field)?.dir ?? null, [sort]);
  const applySort = useCallback2((field, dir, additive) => {
    setSort((prev) => {
      const without = prev.filter((s) => s.field !== field);
      if (dir == null) return additive ? without : [];
      return additive ? [...without, { field, dir }] : [{ field, dir }];
    });
  }, []);
  const cycleSort = useCallback2(
    (field, additive) => {
      const current = sortDirOf(field);
      const next = current === "asc" ? "desc" : current === "desc" ? null : "asc";
      applySort(field, next, additive);
    },
    [sortDirOf, applySort]
  );
  const toggleVisible = useCallback2((field) => {
    setHidden((prev) => {
      const next = new Set(prev);
      if (next.has(field)) next.delete(field);
      else next.add(field);
      return next;
    });
  }, []);
  const setAllVisible = useCallback2(
    (visible) => {
      setHidden(visible ? /* @__PURE__ */ new Set() : new Set(columns.filter((c) => !c.lockVisible).map((c) => c.field)));
    },
    [columns]
  );
  const cyclePin = useCallback2((field) => {
    setPinnedMap((prev) => {
      const cur = prev[field] ?? null;
      const next = cur === null ? "left" : cur === "left" ? "right" : null;
      return { ...prev, [field]: next };
    });
  }, []);
  const setPin = useCallback2((field, pin) => {
    setPinnedMap((prev) => ({ ...prev, [field]: pin }));
  }, []);
  const reorderColumn = useCallback2((field, beforeField) => {
    setColOrder((prev) => {
      const next = prev.filter((f) => f !== field);
      const at = beforeField ? next.indexOf(beforeField) : next.length;
      next.splice(at < 0 ? next.length : at, 0, field);
      return next;
    });
  }, []);
  const autoSizeColumn = useCallback2(
    (field) => {
      const col = colByField.get(field);
      if (!col) return;
      const sample = sorted.slice(0, 300);
      let widest = estimateTextWidth(col.headerName ?? field) + 46;
      for (const row of sample) {
        widest = Math.max(widest, estimateTextWidth(displayValue(col, row)) + 26);
      }
      setColWidths((prev) => ({ ...prev, [field]: Math.min(460, Math.max(MIN_WIDTH, Math.round(widest))) }));
    },
    [colByField, sorted]
  );
  const autoSizeAll = useCallback2(() => {
    const sample = sorted.slice(0, 300);
    const next = {};
    for (const col of orderedDefs) {
      if (hidden.has(col.field)) continue;
      let widest = estimateTextWidth(col.headerName ?? col.field) + 46;
      for (const row of sample) widest = Math.max(widest, estimateTextWidth(displayValue(col, row)) + 26);
      next[col.field] = Math.min(460, Math.max(MIN_WIDTH, Math.round(widest)));
    }
    setColWidths((prev) => ({ ...prev, ...next }));
  }, [orderedDefs, hidden, sorted]);
  const resetColumns = useCallback2(() => {
    setColOrder(fieldList);
    setColWidths({});
    setHidden(new Set(columns.filter((c) => c.hide).map((c) => c.field)));
    const pins = {};
    for (const c of columns) pins[c.field] = c.pinned ?? null;
    setPinnedMap(pins);
    setGroupBy(columns.filter((c) => c.rowGroup).map((c) => c.field));
    setSort([]);
  }, [columns, fieldList]);
  const startResize = useCallback2(
    (id, startX, startWidth) => {
      const onMove = (e) => {
        setColWidths((prev) => ({ ...prev, [id]: Math.max(MIN_WIDTH, startWidth + (e.clientX - startX)) }));
      };
      const onUp = () => {
        document.removeEventListener("mousemove", onMove);
        document.removeEventListener("mouseup", onUp);
        document.body.style.cursor = "";
        document.body.style.userSelect = "";
      };
      document.body.style.cursor = "col-resize";
      document.body.style.userSelect = "none";
      document.addEventListener("mousemove", onMove);
      document.addEventListener("mouseup", onUp);
    },
    []
  );
  const toggleGroupField = useCallback2((field) => {
    setGroupBy((prev) => prev.includes(field) ? prev.filter((f) => f !== field) : [...prev, field]);
  }, []);
  const reorderGroup = useCallback2((field, beforeField) => {
    setGroupBy((prev) => {
      const next = prev.filter((f) => f !== field);
      const at = beforeField ? next.indexOf(beforeField) : next.length;
      next.splice(at < 0 ? next.length : at, 0, field);
      return next;
    });
  }, []);
  const toggleCollapse = useCallback2((id) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);
  const expandAll = useCallback2(() => setCollapsed(/* @__PURE__ */ new Set()), []);
  const collapseAll = useCallback2(() => {
    setCollapsed(new Set(
      treeRoots ? collectTreeIds(treeRoots) : groupTree ? collectGroupIds(groupTree) : []
    ));
  }, [treeRoots, groupTree]);
  const setFilterFor = useCallback2((field, model) => {
    setFilters((prev) => {
      const next = { ...prev };
      if (model === void 0) delete next[field];
      else next[field] = model;
      return next;
    });
  }, []);
  const clearFilters = useCallback2(() => {
    setFilters({});
    setQuick("");
  }, [setQuick]);
  const isEditable = useCallback2((col, row) => {
    const def = col.def;
    if (!def?.editable) return false;
    return typeof def.editable === "function" ? def.editable(row) : true;
  }, []);
  const beginEdit = useCallback2(
    (rowIdx, colIdx) => {
      const node = nodes[rowIdx];
      const col = renderCols[colIdx];
      if (!node || node.kind !== "leaf" || !col?.def || !isEditable(col, node.data)) return;
      setEditing({ row: rowIdx, col: colIdx, value: rawValue(col.def, node.data), error: null });
    },
    [nodes, renderCols, isEditable]
  );
  const commitEdit = useCallback2(() => {
    setEditing((current) => {
      if (!current) return null;
      const node = nodes[current.row];
      const col = renderCols[current.col];
      if (!node || node.kind !== "leaf" || !col?.def) return null;
      const def = col.def;
      const oldValue = rawValue(def, node.data);
      let newValue = current.value;
      if (def.type === "number" || def.cellEditor === "number") {
        newValue = current.value === "" || current.value == null ? null : Number(current.value);
        if (newValue != null && Number.isNaN(newValue)) return { ...current, error: "Not a number" };
      }
      const error = def.validate?.(newValue, node.data) ?? null;
      if (error) return { ...current, error };
      if (newValue !== oldValue) {
        onCellValueChanged?.({ row: node.data, field: def.field, oldValue, newValue });
      }
      return null;
    });
  }, [nodes, renderCols, onCellValueChanged]);
  const range = useMemo5(() => {
    if (!focusCell) return null;
    const end = rangeEnd ?? focusCell;
    return {
      r0: Math.min(focusCell.row, end.row),
      r1: Math.max(focusCell.row, end.row),
      c0: Math.min(focusCell.col, end.col),
      c1: Math.max(focusCell.col, end.col)
    };
  }, [focusCell, rangeEnd]);
  const rangeSummary = useMemo5(() => {
    if (!range) return null;
    if (range.r0 === range.r1 && range.c0 === range.c1) return null;
    const values = [];
    for (let r = range.r0; r <= range.r1 && r < nodes.length; r++) {
      const node = nodes[r];
      if (node.kind !== "leaf") continue;
      for (let c = range.c0; c <= range.c1 && c < renderCols.length; c++) {
        const def = renderCols[c].def;
        if (def) values.push(rawValue(def, node.data));
      }
    }
    return summarize(values);
  }, [range, nodes, renderCols]);
  const copyRange = useCallback2(
    async (withHeaders) => {
      if (!range) return;
      const defs = renderCols.map((c) => c.def).filter((d) => !!d);
      const offset = renderCols.findIndex((c) => c.def);
      const c0 = Math.max(0, range.c0 - offset);
      const c1 = Math.max(0, range.c1 - offset);
      const tsv = rangeToTsv(defs, nodes, range.r0, range.r1, c0, c1, withHeaders);
      await copyToClipboard(tsv);
    },
    [range, renderCols, nodes]
  );
  const ensureIndexVisible = useCallback2(
    (index) => {
      const vp = viewportRef.current;
      if (!vp || index < 0 || index >= tops.length) return;
      const top = tops[index];
      const bottom = top + rowHeight;
      const viewTop = vp.scrollTop;
      const viewBottom = viewTop + vp.clientHeight - HEADER_HEIGHT;
      if (top < viewTop) vp.scrollTop = top;
      else if (bottom > viewBottom) vp.scrollTop = bottom - vp.clientHeight + HEADER_HEIGHT;
    },
    [tops, rowHeight]
  );
  const onKeyDown = useCallback2(
    (e) => {
      if (editing) {
        if (e.key === "Escape") {
          e.preventDefault();
          setEditing(null);
        }
        if (e.key === "Enter") {
          e.preventDefault();
          commitEdit();
        }
        return;
      }
      const target = e.target;
      if (target && target !== e.currentTarget) {
        const tag = target.tagName;
        if (tag === "INPUT" || tag === "SELECT" || tag === "TEXTAREA" || target.isContentEditable) return;
      }
      const mod = e.ctrlKey || e.metaKey;
      if (mod && e.key.toLowerCase() === "c") {
        e.preventDefault();
        void copyRange(e.shiftKey);
        return;
      }
      if (mod && e.key.toLowerCase() === "a" && selection === "multiple") {
        e.preventDefault();
        toggleSelectAll();
        return;
      }
      if (!focusCell) return;
      const move = (dr, dc) => {
        e.preventDefault();
        const row = Math.max(0, Math.min(nodes.length - 1, focusCell.row + dr));
        const col = Math.max(0, Math.min(renderCols.length - 1, focusCell.col + dc));
        if (e.shiftKey) setRangeEnd({ row, col });
        else {
          setFocusCell({ row, col });
          setRangeEnd(null);
        }
        ensureIndexVisible(row);
      };
      switch (e.key) {
        case "ArrowDown":
          move(1, 0);
          break;
        case "ArrowUp":
          move(-1, 0);
          break;
        case "ArrowRight":
          move(0, 1);
          break;
        case "ArrowLeft":
          move(0, -1);
          break;
        case "PageDown":
          move(Math.floor(viewport.height / rowHeight) || 10, 0);
          break;
        case "PageUp":
          move(-(Math.floor(viewport.height / rowHeight) || 10), 0);
          break;
        case "Home":
          e.preventDefault();
          setFocusCell({ row: 0, col: focusCell.col });
          ensureIndexVisible(0);
          break;
        case "End":
          e.preventDefault();
          setFocusCell({ row: nodes.length - 1, col: focusCell.col });
          ensureIndexVisible(nodes.length - 1);
          break;
        case "Enter":
        case "F2":
          e.preventDefault();
          beginEdit(focusCell.row, focusCell.col);
          break;
        case " ": {
          const node = nodes[focusCell.row];
          if (node?.kind === "group") {
            e.preventDefault();
            toggleCollapse(node.id);
          } else if (node?.kind === "leaf" && selection !== "none") {
            e.preventDefault();
            toggleRowSelection(node.id, true);
          }
          break;
        }
        default:
          break;
      }
    },
    [
      editing,
      commitEdit,
      copyRange,
      focusCell,
      nodes,
      renderCols.length,
      viewport.height,
      rowHeight,
      beginEdit,
      ensureIndexVisible,
      toggleCollapse,
      toggleRowSelection,
      selection,
      toggleSelectAll
    ]
  );
  const rafRef = useRef3(0);
  const onScroll = useCallback2((e) => {
    const top = e.currentTarget.scrollTop;
    cancelAnimationFrame(rafRef.current);
    rafRef.current = requestAnimationFrame(() => setScrollTop(top));
  }, []);
  useEffect3(() => () => cancelAnimationFrame(rafRef.current), []);
  useLayoutEffect2(() => {
    const el = viewportRef.current;
    if (!el) return;
    const measure = () => setViewport({ width: el.clientWidth, height: el.clientHeight });
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  const [first, last] = useMemo5(() => {
    if (!nodes.length) return [0, -1];
    const viewHeight = viewport.height || 400;
    const startY = Math.max(0, scrollTop - HEADER_HEIGHT);
    const start = Math.max(0, lowerBound(tops, startY) - OVERSCAN);
    let end = start;
    while (end < nodes.length && tops[end] < startY + viewHeight + OVERSCAN * rowHeight) end++;
    return [start, Math.min(nodes.length - 1, end)];
  }, [nodes.length, tops, scrollTop, viewport.height, rowHeight]);
  useEffect3(() => {
    if (serverSide && last >= first) server.ensureRange(first, last);
  }, [serverSide, first, last, server.ensureRange]);
  const gridState = useMemo5(
    () => ({
      columns: colOrder.map((field, order) => ({
        field,
        order,
        width: colWidths[field],
        hide: hidden.has(field),
        pinned: pinnedMap[field] ?? null
      })),
      sort,
      filters,
      groupBy,
      quickFilter: quick
    }),
    [colOrder, colWidths, hidden, pinnedMap, sort, filters, groupBy, quick]
  );
  useEffect3(() => {
    if (!stateKey) return;
    const t = setTimeout(() => saveState(stateKey, gridState), 400);
    return () => clearTimeout(t);
  }, [stateKey, gridState]);
  const runExcelExport = useCallback2((opts) => {
    exportToExcel(opts).catch((err) => {
      console.error("[EnterpriseGrid] Excel export failed:", err);
    });
  }, []);
  const exportOpts = useCallback2(
    (fileName) => ({
      columns: renderCols.map((c) => c.def).filter((d) => !!d),
      nodes: allNodes,
      groupBy,
      totals: grandTotals,
      fileName: fileName ?? exportFileName
    }),
    [renderCols, allNodes, groupBy, grandTotals, exportFileName]
  );
  useImperativeHandle(
    apiRef,
    () => ({
      exportCsv: (f) => exportToCsv(exportOpts(f)),
      exportExcel: (f) => runExcelExport(exportOpts(f)),
      getDisplayedRows: () => sorted,
      getSelectedRows: () => selectedRows,
      clearSelection: () => setSelectedIds(/* @__PURE__ */ new Set()),
      selectAll: () => setSelectedIds(new Set(sorted.map(getId))),
      setQuickFilter: setQuick,
      setFilterModel: setFilters,
      getFilterModel: () => filters,
      clearFilters,
      setGroupBy,
      expandAll,
      collapseAll,
      expandAllDetails: () => setInnerDetails(new Set(sorted.map(getId))),
      collapseAllDetails: () => setInnerDetails(/* @__PURE__ */ new Set()),
      autoSizeColumns: autoSizeAll,
      resetColumns,
      getState: () => gridState,
      setState: (s) => {
        if (s.sort) setSort(s.sort);
        if (s.filters) setFilters(s.filters);
        if (s.groupBy) setGroupBy(s.groupBy);
        if (s.quickFilter !== void 0) setQuick(s.quickFilter);
        if (s.columns) {
          setColOrder(s.columns.map((c) => c.field));
          setColWidths(Object.fromEntries(s.columns.filter((c) => c.width).map((c) => [c.field, c.width])));
          setHidden(new Set(s.columns.filter((c) => c.hide).map((c) => c.field)));
          setPinnedMap(Object.fromEntries(s.columns.map((c) => [c.field, c.pinned ?? null])));
        }
      },
      ensureIndexVisible
    }),
    [
      exportOpts,
      sorted,
      selectedRows,
      getId,
      setQuick,
      filters,
      clearFilters,
      expandAll,
      collapseAll,
      autoSizeAll,
      resetColumns,
      gridState,
      ensureIndexVisible
    ]
  );
  const cellStyle = (col) => {
    const style = {
      width: col.width,
      minWidth: col.width,
      maxWidth: col.width,
      textAlign: col.align
    };
    if (col.pinned === "left") {
      style.position = "sticky";
      style.left = stickyOffsets.left[col.id];
      style.zIndex = 2;
    } else if (col.pinned === "right") {
      style.position = "sticky";
      style.right = stickyOffsets.right[col.id];
      style.zIndex = 2;
    }
    return style;
  };
  const renderAgg = useCallback2((def, agg, sample) => {
    if (agg == null) return "";
    const row = sample ?? {};
    const value = typeof agg === "number" ? Math.round(agg * 100) / 100 : agg;
    if (def.valueFormatter) {
      try {
        const out = def.valueFormatter(value, row);
        if (out) return out;
      } catch {
      }
    } else if (def.cellRenderer) {
      try {
        const out = def.cellRenderer(value, row);
        if (out != null) return out;
      } catch {
      }
    }
    return typeof value === "number" ? formatNumber(value) : String(value);
  }, []);
  const renderGroupCell = (node) => /* @__PURE__ */ jsxs8(
    "button",
    {
      type: "button",
      onClick: (e) => {
        e.stopPropagation();
        toggleCollapse(node.id);
      },
      style: { paddingLeft: 6 + node.depth * 16 },
      className: "flex w-full items-center gap-1.5 text-left",
      children: [
        collapsed.has(node.id) ? /* @__PURE__ */ jsx8(ChevronRight, { size: 14 }) : /* @__PURE__ */ jsx8(ChevronDown, { size: 14 }),
        /* @__PURE__ */ jsx8("span", { className: "truncate font-semibold", children: node.label }),
        /* @__PURE__ */ jsx8("span", { className: "shrink-0 rounded-full bg-slate-200 px-1.5 text-[10px] font-medium text-slate-600", children: node.count })
      ]
    }
  );
  const renderCellContent = (col, node, rowIdx, colIdx) => {
    if (col.special === "select") {
      if (node.kind === "group") {
        const groupNode = node;
        const all = groupNode.leaves.length > 0 && groupNode.leaves.every((l) => selectedIds.has(getId(l)));
        const some = !all && groupNode.leaves.some((l) => selectedIds.has(getId(l)));
        return /* @__PURE__ */ jsx8(
          "input",
          {
            type: "checkbox",
            checked: all,
            ref: (el) => {
              if (el) el.indeterminate = some;
            },
            onClick: (e) => e.stopPropagation(),
            onChange: () => setGroupSelection(groupNode, !all),
            className: "accent-[var(--eg-accent)]"
          }
        );
      }
      if (node.kind !== "leaf") return null;
      return /* @__PURE__ */ jsx8(
        "input",
        {
          type: "checkbox",
          checked: selectedIds.has(node.id),
          onClick: (e) => e.stopPropagation(),
          onChange: () => toggleRowSelection(node.id, true),
          className: "accent-[var(--eg-accent)]"
        }
      );
    }
    if (col.special === "group") {
      if (node.kind === "group") return renderGroupCell(node);
      return /* @__PURE__ */ jsx8("span", { style: { paddingLeft: 6 + node.depth * 16 }, className: "block" });
    }
    const def = col.def;
    if (node.kind === "group") {
      const groupNode = node;
      if (groupNode.data && !def.aggFunc) {
        const raw2 = rawValue(def, groupNode.data);
        if (raw2 != null && raw2 !== "") {
          return def.cellRenderer ? /* @__PURE__ */ jsx8(Fragment3, { children: def.cellRenderer(raw2, groupNode.data) }) : /* @__PURE__ */ jsx8("span", { className: "truncate", children: def.valueFormatter ? def.valueFormatter(raw2, groupNode.data) : String(raw2) });
        }
      }
      const content = renderAgg(def, groupNode.aggregates[def.field], groupNode.leaves[0]);
      if (!content) return null;
      return /* @__PURE__ */ jsx8("span", { className: "truncate font-semibold text-slate-700", children: content });
    }
    if (node.kind !== "leaf") return null;
    const row = node.data;
    if (row == null) {
      return /* @__PURE__ */ jsx8("span", { className: "block h-3 w-3/4 animate-pulse rounded bg-slate-200" });
    }
    const isEditingCell = editing?.row === rowIdx && editing?.col === colIdx;
    if (isEditingCell) {
      const opts = def.cellEditorParams?.options;
      const common = {
        autoFocus: true,
        className: `w-full rounded border px-1 py-0.5 text-[13px] outline-none ${editing?.error ? "border-rose-400 bg-rose-50" : "border-[var(--eg-accent)]"}`,
        onBlur: () => commitEdit(),
        onClick: (e) => e.stopPropagation()
      };
      if (def.cellEditor === "select" && opts) {
        return /* @__PURE__ */ jsx8(
          "select",
          {
            ...common,
            value: editing.value ?? "",
            onChange: (e) => setEditing((s) => s ? { ...s, value: e.target.value } : s),
            children: opts.map((o) => /* @__PURE__ */ jsx8("option", { value: o.value, children: o.label }, String(o.value)))
          }
        );
      }
      return /* @__PURE__ */ jsx8(
        "input",
        {
          ...common,
          type: def.cellEditor === "number" || def.type === "number" ? "number" : def.cellEditor === "date" || def.type === "date" ? "date" : "text",
          value: editing.value ?? "",
          title: editing?.error ?? void 0,
          onChange: (e) => setEditing((s) => s ? { ...s, value: e.target.value, error: null } : s)
        }
      );
    }
    const raw = rawValue(def, row);
    if (def.cellRenderer) return def.cellRenderer(raw, row);
    const text2 = def.valueFormatter ? def.valueFormatter(raw, row) : raw == null || raw === "" ? "\u2014" : String(raw);
    return /* @__PURE__ */ jsx8("span", { className: "truncate", children: text2 });
  };
  const activeFilterCount = countActive(filters);
  const header = /* @__PURE__ */ jsx8(
    "div",
    {
      className: "sticky top-0 z-30 flex bg-[var(--eg-primary)] text-white",
      style: { height: HEADER_HEIGHT, width: layoutWidth },
      children: renderCols.map((col, colIdx) => {
        const dir = col.def ? sortDirOf(col.def.field) : null;
        const sortIdx = col.def ? sort.findIndex((s) => s.field === col.def.field) : -1;
        const filtered = col.def ? isActive(filters[col.def.field]) : false;
        const style = cellStyle(col);
        if (col.special === "select") {
          return /* @__PURE__ */ jsx8("div", { style: { ...style, zIndex: 4 }, className: "flex items-center justify-center bg-[var(--eg-primary)]", children: selection === "multiple" && /* @__PURE__ */ jsx8(
            "input",
            {
              type: "checkbox",
              checked: allFilteredSelected,
              ref: (el) => {
                if (el) el.indeterminate = !allFilteredSelected && selectedIds.size > 0;
              },
              onChange: toggleSelectAll,
              className: "accent-[var(--eg-accent)]",
              title: "Select all filtered rows"
            }
          ) }, col.id);
        }
        return /* @__PURE__ */ jsxs8(
          "div",
          {
            style: { ...style, zIndex: col.pinned ? 4 : 1 },
            className: classNames(
              "group relative flex items-center gap-1 border-r border-white/10 bg-[var(--eg-primary)] px-2 text-[12.5px] font-semibold",
              dragOverField === col.id && "bg-[var(--eg-accent)]/40"
            ),
            title: col.def?.headerTooltip ?? col.header,
            draggable: !!col.def,
            onDragStart: (e) => {
              if (!col.def) return;
              setDragField(col.def.field);
              e.dataTransfer.setData("text/plain", col.def.field);
              e.dataTransfer.effectAllowed = "move";
            },
            onDragEnd: () => {
              setDragField(null);
              setDragOverField(null);
            },
            onDragOver: (e) => {
              if (dragField && col.def && dragField !== col.def.field) {
                e.preventDefault();
                setDragOverField(col.id);
              }
            },
            onDragLeave: () => setDragOverField((f) => f === col.id ? null : f),
            onDrop: (e) => {
              e.preventDefault();
              setDragOverField(null);
              if (dragField && col.def && dragField !== col.def.field) reorderColumn(dragField, col.def.field);
              setDragField(null);
            },
            children: [
              /* @__PURE__ */ jsx8(
                "span",
                {
                  className: classNames("flex-1 truncate", col.def?.sortable !== false && "cursor-pointer"),
                  onClick: (e) => col.def && col.def.sortable !== false && cycleSort(col.def.field, e.shiftKey),
                  children: col.header
                }
              ),
              dir && /* @__PURE__ */ jsxs8("span", { className: "flex shrink-0 items-center text-[var(--eg-accent)]", children: [
                dir === "asc" ? /* @__PURE__ */ jsx8(ChevronUp, { size: 13 }) : /* @__PURE__ */ jsx8(ChevronDown, { size: 13 }),
                sort.length > 1 && /* @__PURE__ */ jsx8("span", { className: "text-[9px]", children: sortIdx + 1 })
              ] }),
              filtered && /* @__PURE__ */ jsx8(Filter, { size: 11, className: "shrink-0 text-[var(--eg-accent)]" }),
              col.def && /* @__PURE__ */ jsx8(
                "button",
                {
                  type: "button",
                  onClick: (e) => {
                    e.stopPropagation();
                    setMenuField(col.def.field);
                    setMenuAnchor(e.currentTarget);
                  },
                  className: "shrink-0 rounded p-0.5 text-white/50 opacity-0 transition-opacity hover:bg-white/20 hover:text-white group-hover:opacity-100",
                  children: /* @__PURE__ */ jsx8(MoreVertical, { size: 13 })
                }
              ),
              col.def?.resizable !== false && /* @__PURE__ */ jsx8(
                "div",
                {
                  onMouseDown: (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    startResize(col.id, e.clientX, col.width);
                  },
                  onDoubleClick: () => col.def && autoSizeColumn(col.def.field),
                  className: "absolute right-0 top-0 h-full w-1.5 cursor-col-resize hover:bg-[var(--eg-accent)]"
                }
              )
            ]
          },
          col.id
        );
      })
    }
  );
  const bodyRows = [];
  for (let i = first; i <= last; i++) {
    const node = nodes[i];
    if (!node) continue;
    const top = tops[i];
    if (node.kind === "detail") {
      bodyRows.push(
        /* @__PURE__ */ jsx8(
          "div",
          {
            style: { position: "absolute", top, height: node.height, width: layoutWidth },
            className: "border-b border-slate-200 bg-slate-50",
            children: /* @__PURE__ */ jsx8("div", { className: "sticky left-0 h-full overflow-auto p-3", style: { width: viewport.width || "100%" }, children: masterDetail?.(node.data) })
          },
          node.id
        )
      );
      continue;
    }
    const isGroup = node.kind === "group";
    const isSelected = node.kind === "leaf" && selectedIds.has(node.id);
    const extra = node.kind === "leaf" && rowClass ? rowClass(node.data) : "";
    bodyRows.push(
      /* @__PURE__ */ jsx8(
        "div",
        {
          style: { position: "absolute", top, height: rowHeight, width: layoutWidth },
          className: classNames(
            "flex border-b border-slate-100 text-[13px]",
            isGroup ? "bg-slate-100 font-medium text-slate-700" : i % 2 ? "bg-slate-50/60" : "bg-white",
            isSelected && "bg-[var(--eg-accent)]/12",
            !isGroup && onRowClick && "cursor-pointer",
            "hover:bg-[var(--eg-accent)]/8",
            extra
          ),
          onClick: () => {
            if (node.kind === "leaf") onRowClick?.(node.data);
          },
          onDoubleClick: () => {
            if (node.kind === "leaf") onRowDoubleClick?.(node.data);
          },
          onContextMenu: (e) => {
            if (!contextMenu) return;
            e.preventDefault();
            setCtxMenu({ x: e.clientX, y: e.clientY });
          },
          children: renderCols.map((col, colIdx) => {
            const inRange = range && i >= range.r0 && i <= range.r1 && colIdx >= range.c0 && colIdx <= range.c1;
            const isFocused = focusCell?.row === i && focusCell?.col === colIdx;
            const custom = col.def?.cellClass && node.kind === "leaf" ? typeof col.def.cellClass === "function" ? col.def.cellClass(rawValue(col.def, node.data), node.data) : col.def.cellClass : "";
            return /* @__PURE__ */ jsxs8(
              "div",
              {
                style: cellStyle(col),
                className: classNames(
                  "flex items-center overflow-hidden whitespace-nowrap px-2",
                  col.align === "right" && "justify-end",
                  col.align === "center" && "justify-center",
                  col.pinned && (isGroup ? "bg-slate-100" : isSelected ? "bg-[#eef7f2]" : i % 2 ? "bg-slate-50" : "bg-white"),
                  col.pinned === "left" && "border-r border-slate-200",
                  col.pinned === "right" && "border-l border-slate-200",
                  inRange && !isFocused && "bg-[var(--eg-accent)]/15",
                  isFocused && "outline outline-2 -outline-offset-2 outline-[var(--eg-accent)]",
                  col.special === "group" && "font-semibold",
                  custom
                ),
                onMouseDown: (e) => {
                  if (e.button !== 0 || col.special === "select") return;
                  if (e.shiftKey && focusCell) setRangeEnd({ row: i, col: colIdx });
                  else {
                    setFocusCell({ row: i, col: colIdx });
                    setRangeEnd(null);
                  }
                  isDraggingRange.current = true;
                },
                onMouseEnter: () => {
                  if (isDraggingRange.current) setRangeEnd({ row: i, col: colIdx });
                },
                onMouseUp: () => {
                  isDraggingRange.current = false;
                },
                onDoubleClick: (e) => {
                  if (col.def && node.kind === "leaf" && isEditable(col, node.data)) {
                    e.stopPropagation();
                    beginEdit(i, colIdx);
                  }
                },
                children: [
                  masterDetail && col.special !== "select" && colIdx === (selection !== "none" ? 1 : 0) && node.kind === "leaf" && /* @__PURE__ */ jsx8(
                    "button",
                    {
                      type: "button",
                      onClick: (e) => {
                        e.stopPropagation();
                        toggleDetail(node.id);
                      },
                      className: "mr-1 shrink-0 text-slate-400 hover:text-[var(--eg-primary)]",
                      children: expandedDetails.has(node.id) ? /* @__PURE__ */ jsx8(ChevronDown, { size: 13 }) : /* @__PURE__ */ jsx8(ChevronRight, { size: 13 })
                    }
                  ),
                  renderCellContent(col, node, i, colIdx)
                ]
              },
              col.id
            );
          })
        },
        node.id
      )
    );
  }
  const menuCol = menuField ? colByField.get(menuField) : null;
  const totalsLabelIdx = Math.max(0, renderCols.findIndex((c) => c.special !== "select"));
  const rootStyle = {
    ["--eg-primary"]: "var(--grid-primary, #0B3D2E)",
    ["--eg-accent"]: "var(--grid-accent, #2E9E6B)"
  };
  const shell = /* @__PURE__ */ jsxs8(
    "div",
    {
      ref: rootRef,
      style: rootStyle,
      className: classNames(
        "flex flex-col overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm",
        maximized && "fixed inset-3 z-[9998]",
        className
      ),
      children: [
        toolbar && /* @__PURE__ */ jsxs8("div", { className: "flex flex-wrap items-center gap-2 border-b border-slate-200 bg-white px-3 py-2", children: [
          /* @__PURE__ */ jsxs8("div", { className: "relative", children: [
            /* @__PURE__ */ jsx8(Search, { size: 14, className: "absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400" }),
            /* @__PURE__ */ jsx8(
              "input",
              {
                value: quick,
                onChange: (e) => setQuick(e.target.value),
                placeholder: "Search all columns\u2026",
                className: "h-8 w-64 rounded-md border border-slate-200 pl-8 pr-7 text-[13px] outline-none focus:border-[var(--eg-accent)] focus:ring-1 focus:ring-[var(--eg-accent)]/25"
              }
            ),
            quick && /* @__PURE__ */ jsx8("button", { type: "button", onClick: () => setQuick(""), className: "absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600", children: /* @__PURE__ */ jsx8(X, { size: 13 }) })
          ] }),
          (activeFilterCount > 0 || sort.length > 0 || groupBy.length > 0) && /* @__PURE__ */ jsxs8(
            "button",
            {
              type: "button",
              onClick: () => {
                clearFilters();
                setSort([]);
                setGroupBy([]);
              },
              className: "flex h-8 items-center gap-1.5 rounded-md border border-slate-200 px-2.5 text-[12.5px] text-slate-600 hover:bg-slate-50",
              children: [
                /* @__PURE__ */ jsx8(RotateCcw, { size: 13 }),
                " Reset view"
              ]
            }
          ),
          /* @__PURE__ */ jsxs8("div", { className: "ml-auto flex items-center gap-1.5", children: [
            toolbarExtras,
            /* @__PURE__ */ jsxs8(
              "button",
              {
                type: "button",
                onClick: () => exportToCsv(exportOpts()),
                className: "flex h-8 items-center gap-1.5 rounded-md border border-slate-200 px-2.5 text-[12.5px] font-medium text-slate-700 hover:bg-slate-50",
                children: [
                  /* @__PURE__ */ jsx8(Download, { size: 13 }),
                  " CSV"
                ]
              }
            ),
            /* @__PURE__ */ jsxs8(
              "button",
              {
                type: "button",
                onClick: () => runExcelExport(exportOpts()),
                className: "flex h-8 items-center gap-1.5 rounded-md border border-slate-200 px-2.5 text-[12.5px] font-medium text-slate-700 hover:bg-slate-50",
                children: [
                  /* @__PURE__ */ jsx8(FileSpreadsheet, { size: 13 }),
                  " Excel"
                ]
              }
            ),
            /* @__PURE__ */ jsx8(
              "button",
              {
                type: "button",
                onClick: () => setMaximized((m) => !m),
                title: maximized ? "Restore" : "Maximise",
                className: "flex h-8 w-8 items-center justify-center rounded-md border border-slate-200 text-slate-600 hover:bg-slate-50",
                children: maximized ? /* @__PURE__ */ jsx8(Minimize2, { size: 13 }) : /* @__PURE__ */ jsx8(Maximize2, { size: 13 })
              }
            ),
            /* @__PURE__ */ jsx8(
              "button",
              {
                type: "button",
                onClick: (e) => setGridMenuAnchor(e.currentTarget),
                className: "flex h-8 w-8 items-center justify-center rounded-md border border-slate-200 text-slate-600 hover:bg-slate-50",
                children: /* @__PURE__ */ jsx8(MoreVertical, { size: 14 })
              }
            )
          ] })
        ] }),
        groupPanel && /* @__PURE__ */ jsx8(
          GroupPanel,
          {
            groupBy,
            columns,
            draggingField: dragField,
            onRemove: (f) => setGroupBy((prev) => prev.filter((x) => x !== f)),
            onReorder: reorderGroup,
            onDropField: (f) => setGroupBy((prev) => prev.includes(f) ? prev : [...prev, f])
          }
        ),
        /* @__PURE__ */ jsxs8("div", { className: "flex min-h-0 flex-1", children: [
          /* @__PURE__ */ jsxs8(
            "div",
            {
              ref: viewportRef,
              tabIndex: 0,
              onScroll,
              onKeyDown,
              onMouseUp: () => {
                isDraggingRange.current = false;
              },
              className: "relative min-w-0 flex-1 overflow-auto outline-none",
              style: { height: maximized ? void 0 : typeof height === "number" ? height : height },
              children: [
                /* @__PURE__ */ jsxs8("div", { style: { width: totalWidth, minWidth: "100%", position: "relative" }, children: [
                  header,
                  /* @__PURE__ */ jsxs8("div", { style: { position: "relative", height: bodyHeight }, children: [
                    bodyRows,
                    !loading && nodes.length === 0 && /* @__PURE__ */ jsx8(
                      "div",
                      {
                        className: "sticky left-0 flex items-center justify-center py-16 text-[13px] text-slate-400",
                        style: { width: viewport.width || "100%" },
                        children: emptyMessage
                      }
                    )
                  ] }),
                  grandTotals && nodes.length > 0 && /* @__PURE__ */ jsx8(
                    "div",
                    {
                      className: "sticky bottom-0 z-20 flex border-t-2 border-[var(--eg-primary)] bg-slate-100 text-[13px] font-semibold text-slate-800",
                      style: { height: rowHeight, width: layoutWidth },
                      children: renderCols.map((col, idx) => {
                        const content = idx === totalsLabelIdx ? `Total \xB7 ${formatNumber(sorted.length)} rows` : col.def ? renderAgg(col.def, grandTotals[col.def.field], sorted[0]) : "";
                        return /* @__PURE__ */ jsx8(
                          "div",
                          {
                            style: { ...cellStyle(col), zIndex: col.pinned ? 3 : 1 },
                            className: classNames(
                              "flex items-center overflow-hidden whitespace-nowrap bg-slate-100 px-2",
                              col.align === "right" && "justify-end",
                              col.align === "center" && "justify-center"
                            ),
                            children: content
                          },
                          col.id
                        );
                      })
                    }
                  )
                ] }),
                loading && /* @__PURE__ */ jsx8("div", { className: "sticky left-0 top-0 z-40 flex h-full w-full items-center justify-center bg-white/70", style: { width: viewport.width }, children: /* @__PURE__ */ jsx8(Loader2, { size: 22, className: "animate-spin text-[var(--eg-primary)]" }) })
              ]
            }
          ),
          sideBar && /* @__PURE__ */ jsx8(
            ToolPanel,
            {
              tab: toolTab,
              onTabChange: setToolTab,
              columns: orderedDefs,
              hidden,
              pinned: pinnedMap,
              groupBy,
              onToggleVisible: toggleVisible,
              onSetAllVisible: setAllVisible,
              onTogglePin: cyclePin,
              onToggleGroup: toggleGroupField,
              onReorder: reorderColumn,
              onResetColumns: resetColumns,
              filterKinds,
              filters,
              onFilterChange: setFilterFor,
              onClearFilters: clearFilters,
              filterRowsFor: filterPass.rowsExcluding,
              pivot: pivotPanel ? {
                enabled: pivotOn,
                onToggleEnabled: setPivotOn,
                columns: props.columns,
                rowFields: pivotRows,
                columnFields: pivotCols,
                valueFields: pivotVals,
                onAssign: assignPivot,
                onReorder: reorderPivot,
                onSetAgg: (f, a) => setPivotAggs((prev) => ({ ...prev, [f]: a })),
                aggOf: (f) => pivotAggs[f] ?? props.columns.find((c) => c.field === f)?.aggFunc ?? "sum"
              } : void 0
            }
          )
        ] }),
        pagination && /* @__PURE__ */ jsxs8("div", { className: "flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 bg-white px-3 py-1.5 text-[12.5px] text-slate-600", children: [
          /* @__PURE__ */ jsxs8("div", { className: "flex items-center gap-2", children: [
            /* @__PURE__ */ jsx8("span", { children: "Rows per page" }),
            /* @__PURE__ */ jsx8(
              "select",
              {
                value: pageSize,
                onChange: (e) => setPageSize(Number(e.target.value)),
                className: "rounded border border-slate-200 px-1.5 py-0.5 outline-none focus:border-[var(--eg-accent)]",
                children: pageSizeOptions.map((n) => /* @__PURE__ */ jsx8("option", { value: n, children: n }, n))
              }
            ),
            /* @__PURE__ */ jsx8("span", { className: "text-slate-400", children: allNodes.length === 0 ? "0 rows" : `${formatNumber(safePage * pageSize + 1)}\u2013${formatNumber(Math.min((safePage + 1) * pageSize, allNodes.length))} of ${formatNumber(allNodes.length)}` })
          ] }),
          /* @__PURE__ */ jsxs8("div", { className: "flex items-center gap-1", children: [
            /* @__PURE__ */ jsx8(PagBtn, { disabled: safePage === 0, onClick: () => setPage(0), children: /* @__PURE__ */ jsx8(ChevronsLeft, { size: 15 }) }),
            /* @__PURE__ */ jsx8(PagBtn, { disabled: safePage === 0, onClick: () => setPage((p2) => p2 - 1), children: /* @__PURE__ */ jsx8(ChevronLeft, { size: 15 }) }),
            /* @__PURE__ */ jsxs8("span", { className: "px-1.5", children: [
              "Page ",
              safePage + 1,
              " of ",
              totalPages
            ] }),
            /* @__PURE__ */ jsx8(PagBtn, { disabled: safePage >= totalPages - 1, onClick: () => setPage((p2) => p2 + 1), children: /* @__PURE__ */ jsx8(ChevronRight, { size: 15 }) }),
            /* @__PURE__ */ jsx8(PagBtn, { disabled: safePage >= totalPages - 1, onClick: () => setPage(totalPages - 1), children: /* @__PURE__ */ jsx8(ChevronsRight, { size: 15 }) })
          ] })
        ] }),
        statusBar && /* @__PURE__ */ jsx8(
          StatusBar,
          {
            totalRows: serverSide ? server.rowCount : rows.length,
            filteredRows: serverSide ? server.rowCount : sorted.length,
            selectedRows: selectedIds.size,
            groupCount: groupTree ? groupTree.length : 0,
            rangeSummary
          }
        ),
        menuCol && /* @__PURE__ */ jsx8(
          ColumnMenu,
          {
            anchor: menuAnchor,
            open: !!menuField,
            onClose: () => {
              setMenuField(null);
              setMenuAnchor(null);
            },
            col: menuCol,
            filterKind: filterKinds[menuCol.field] ?? null,
            filterRows: filterPass.rowsExcluding(menuCol.field),
            model: filters[menuCol.field],
            onModelChange: (m) => setFilterFor(menuCol.field, m),
            sortDir: sortDirOf(menuCol.field),
            onSort: (d) => applySort(menuCol.field, d, false),
            pinned: pinnedMap[menuCol.field] ?? null,
            onPin: (p2) => setPin(menuCol.field, p2),
            onHide: () => toggleVisible(menuCol.field),
            onAutoSize: () => autoSizeColumn(menuCol.field),
            grouped: groupBy.includes(menuCol.field),
            canGroup: menuCol.enableRowGroup !== false,
            onToggleGroup: () => toggleGroupField(menuCol.field)
          }
        ),
        /* @__PURE__ */ jsx8(Popover, { anchor: gridMenuAnchor, open: !!gridMenuAnchor, onClose: () => setGridMenuAnchor(null), align: "right", width: 220, children: /* @__PURE__ */ jsxs8("div", { className: "py-1", children: [
          /* @__PURE__ */ jsx8(MenuItem, { icon: /* @__PURE__ */ jsx8(Maximize2, { size: 13 }), label: "Auto-size all columns", onClick: () => {
            autoSizeAll();
            setGridMenuAnchor(null);
          } }),
          /* @__PURE__ */ jsx8(MenuItem, { icon: /* @__PURE__ */ jsx8(ChevronDown, { size: 13 }), label: "Expand all groups", disabled: !groupBy.length, onClick: () => {
            expandAll();
            setGridMenuAnchor(null);
          } }),
          /* @__PURE__ */ jsx8(MenuItem, { icon: /* @__PURE__ */ jsx8(ChevronRight, { size: 13 }), label: "Collapse all groups", disabled: !groupBy.length, onClick: () => {
            collapseAll();
            setGridMenuAnchor(null);
          } }),
          /* @__PURE__ */ jsx8(MenuDivider, {}),
          /* @__PURE__ */ jsx8(MenuItem, { icon: /* @__PURE__ */ jsx8(X, { size: 13 }), label: "Clear all filters", disabled: !activeFilterCount && !quick, onClick: () => {
            clearFilters();
            setGridMenuAnchor(null);
          } }),
          /* @__PURE__ */ jsx8(MenuItem, { icon: /* @__PURE__ */ jsx8(RotateCcw, { size: 13 }), label: "Reset columns", onClick: () => {
            resetColumns();
            setGridMenuAnchor(null);
          } })
        ] }) }),
        ctxMenu && /* @__PURE__ */ jsx8(
          ContextMenuPortal,
          {
            x: ctxMenu.x,
            y: ctxMenu.y,
            onClose: () => setCtxMenu(null),
            items: [
              { label: "Copy", icon: /* @__PURE__ */ jsx8(Copy, { size: 13 }), shortcut: "Ctrl+C", onClick: () => void copyRange(false), disabled: !range },
              { label: "Copy with headers", icon: /* @__PURE__ */ jsx8(Copy, { size: 13 }), onClick: () => void copyRange(true), disabled: !range },
              { divider: true },
              { label: "Export CSV", icon: /* @__PURE__ */ jsx8(Download, { size: 13 }), onClick: () => exportToCsv(exportOpts()) },
              { label: "Export Excel", icon: /* @__PURE__ */ jsx8(FileSpreadsheet, { size: 13 }), onClick: () => runExcelExport(exportOpts()) },
              { divider: true },
              { label: "Auto-size all columns", icon: /* @__PURE__ */ jsx8(Maximize2, { size: 13 }), onClick: autoSizeAll },
              { label: "Reset columns", icon: /* @__PURE__ */ jsx8(RotateCcw, { size: 13 }), onClick: resetColumns }
            ]
          }
        )
      ]
    }
  );
  return shell;
}
function PagBtn({ children, disabled, onClick }) {
  return /* @__PURE__ */ jsx8(
    "button",
    {
      type: "button",
      disabled,
      onClick,
      className: "flex h-7 w-7 items-center justify-center rounded text-slate-500 transition-colors hover:bg-slate-100 disabled:pointer-events-none disabled:opacity-30",
      children
    }
  );
}
function ContextMenuPortal({ x, y, items, onClose }) {
  const ref = useRef3(null);
  const [pos, setPos] = useState7({ top: y, left: x });
  useLayoutEffect2(() => {
    const el = ref.current;
    if (!el) return;
    const w = el.offsetWidth;
    const h = el.offsetHeight;
    setPos({
      top: Math.min(y, window.innerHeight - h - 8),
      left: Math.min(x, window.innerWidth - w - 8)
    });
  }, [x, y]);
  useEffect3(() => {
    const close = () => onClose();
    document.addEventListener("mousedown", close);
    document.addEventListener("scroll", close, true);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("scroll", close, true);
    };
  }, [onClose]);
  return /* @__PURE__ */ jsx8(
    "div",
    {
      ref,
      style: { position: "fixed", top: pos.top, left: pos.left, zIndex: 9999, minWidth: 210 },
      className: "rounded-lg border border-slate-200 bg-white py-1 shadow-xl",
      onMouseDown: (e) => e.stopPropagation(),
      children: items.map(
        (it, i) => it.divider ? /* @__PURE__ */ jsx8(MenuDivider, {}, `d${i}`) : /* @__PURE__ */ jsx8(
          MenuItem,
          {
            icon: it.icon,
            label: it.label,
            shortcut: it.shortcut,
            disabled: it.disabled,
            onClick: () => {
              it.onClick?.();
              onClose();
            }
          },
          it.label
        )
      )
    }
  );
}
var EnterpriseGrid = React6.forwardRef(EnterpriseGridInner);

// src/presets.ts
import React7 from "react";
var inr0 = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 0 });
var inr2 = new Intl.NumberFormat("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
function formatMoney(v, decimals = 0) {
  const n = toNumber(v);
  if (n == null) return "";
  return `\u20B9${(decimals === 0 ? inr0 : inr2).format(n)}`;
}
function formatDate(v) {
  if (v == null || v === "") return "";
  const d = v instanceof Date ? v : new Date(v);
  if (Number.isNaN(d.getTime())) return String(v);
  return d.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
}
function money(field, headerName, extra = {}) {
  return {
    field,
    headerName,
    type: "number",
    align: "right",
    width: 140,
    aggFunc: "sum",
    valueFormatter: (v) => formatMoney(v),
    ...extra
  };
}
function percent(field, headerName, extra = {}) {
  return {
    field,
    headerName,
    type: "number",
    align: "right",
    width: 100,
    aggFunc: "avg",
    valueFormatter: (v) => {
      const n = toNumber(v);
      return n == null ? "" : `${n.toFixed(2)}%`;
    },
    ...extra
  };
}
function date(field, headerName, extra = {}) {
  return {
    field,
    headerName,
    type: "date",
    width: 120,
    valueFormatter: (v) => formatDate(v),
    ...extra
  };
}
function text(field, headerName, extra = {}) {
  return { field, headerName, type: "text", width: 150, ...extra };
}
function category(field, headerName, extra = {}) {
  return { field, headerName, type: "text", filter: "set", width: 140, enableRowGroup: true, ...extra };
}
var TONE_CLASS = {
  green: "bg-emerald-50 text-emerald-700 ring-emerald-600/20",
  amber: "bg-amber-50 text-amber-700 ring-amber-600/20",
  red: "bg-rose-50 text-rose-700 ring-rose-600/20",
  blue: "bg-sky-50 text-sky-700 ring-sky-600/20",
  slate: "bg-slate-100 text-slate-600 ring-slate-500/20"
};
function badge(field, headerName, tones, extra = {}) {
  return {
    field,
    headerName,
    filter: "set",
    width: 130,
    align: "center",
    enableRowGroup: true,
    cellRenderer: (v) => {
      if (v == null || v === "") return null;
      const tone = TONE_CLASS[tones[String(v)] ?? "slate"];
      return React7.createElement(
        "span",
        { className: `inline-flex rounded-full px-2 py-0.5 text-[11px] font-medium ring-1 ring-inset ${tone}` },
        String(v)
      );
    },
    ...extra
  };
}
export {
  EnterpriseGrid,
  GroupPanel,
  MenuDivider,
  MenuItem,
  PivotPanel,
  Popover,
  StatusBar,
  ToolPanel,
  aggregateRows,
  applyFilters,
  badge,
  buildPivot,
  buildTree,
  category,
  collectTreeIds,
  date,
  distinctValues,
  emptyModel,
  exportToCsv,
  exportToExcel,
  formatDate,
  formatMoney,
  isActive as isFilterActive,
  money,
  parsePivotField,
  percent,
  pivotFieldName,
  runAgg,
  summarize,
  text,
  useServerSideRows
};
//# sourceMappingURL=index.mjs.map
