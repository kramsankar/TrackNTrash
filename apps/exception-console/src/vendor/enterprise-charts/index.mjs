// src/EnterpriseChart.tsx
import React8, { useEffect as useEffect2, useImperativeHandle as useImperativeHandle6, useMemo as useMemo7, useRef as useRef8, useState as useState8 } from "react";

// src/CartesianChart.tsx
import React2, { useImperativeHandle, useMemo, useRef as useRef2, useState as useState2 } from "react";

// src/scales.ts
var clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
function niceStep(raw) {
  if (raw <= 0 || !Number.isFinite(raw)) return 1;
  const mag = Math.pow(10, Math.floor(Math.log10(raw)));
  const norm = raw / mag;
  const step = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10;
  return step * mag;
}
function niceDomain(min, max, count = 6) {
  if (!Number.isFinite(min) || !Number.isFinite(max)) return [0, 1, 0.2];
  if (min === max) {
    const pad = Math.abs(min) > 0 ? Math.abs(min) * 0.1 : 1;
    min -= pad;
    max += pad;
  }
  const step = niceStep((max - min) / Math.max(1, count));
  return [Math.floor(min / step) * step, Math.ceil(max / step) * step, step];
}
function linearScale(domain, range, opts = {}) {
  let [d0, d1] = domain;
  if (d0 === d1) {
    d0 -= 1;
    d1 += 1;
  }
  const [r0, r1] = opts.reverse ? [range[1], range[0]] : range;
  const span = d1 - d0;
  const fn = ((v) => {
    const n = typeof v === "number" ? v : Number(v);
    if (!Number.isFinite(n)) return r0;
    return r0 + (n - d0) / span * (r1 - r0);
  });
  fn.kind = "number";
  fn.domain = [d0, d1];
  fn.range = range;
  fn.bandwidth = 0;
  fn.invert = (px) => d0 + (px - r0) / (r1 - r0) * span;
  fn.ticks = (count = 6) => {
    const step = niceStep(span / Math.max(1, count));
    const start = Math.ceil(d0 / step) * step;
    const out = [];
    for (let v = start, i = 0; v <= d1 + step * 1e-9 && i < 1e3; v += step, i++) {
      out.push(Math.abs(v) < step * 1e-9 ? 0 : Number(v.toFixed(10)));
    }
    return out;
  };
  return fn;
}
function logScale(domain, range) {
  const d0 = Math.max(domain[0], 1e-9);
  const d1 = Math.max(domain[1], d0 * 10);
  const l0 = Math.log10(d0);
  const l1 = Math.log10(d1);
  const [r0, r1] = range;
  const fn = ((v) => {
    const n = Math.max(Number(v), 1e-9);
    return r0 + (Math.log10(n) - l0) / (l1 - l0) * (r1 - r0);
  });
  fn.kind = "log";
  fn.domain = [d0, d1];
  fn.range = range;
  fn.bandwidth = 0;
  fn.invert = (px) => Math.pow(10, l0 + (px - r0) / (r1 - r0) * (l1 - l0));
  fn.ticks = () => {
    const out = [];
    for (let e = Math.floor(l0); e <= Math.ceil(l1); e++) {
      const base = Math.pow(10, e);
      if (base >= d0 && base <= d1) out.push(base);
    }
    return out;
  };
  return fn;
}
function bandScale(values, range, opts = {}) {
  const padding = opts.padding ?? 0.2;
  const [r0, r1] = opts.reverse ? [range[1], range[0]] : range;
  const n = Math.max(1, values.length);
  const total = r1 - r0;
  const step = total / n;
  const band = step * (1 - padding);
  const offset = (step - band) / 2;
  const index = /* @__PURE__ */ new Map();
  values.forEach((v, i) => index.set(String(v), i));
  const fn = ((v) => {
    const i = index.get(String(v));
    if (i === void 0) return r0;
    return r0 + i * step + offset;
  });
  fn.kind = "category";
  fn.domain = [values[0], values[values.length - 1]];
  fn.range = range;
  fn.bandwidth = Math.abs(band);
  fn.values = values;
  fn.invert = (px) => {
    const i = clamp(Math.floor((px - r0) / step), 0, n - 1);
    return values[i];
  };
  fn.ticks = (count) => {
    if (!count || values.length <= count) return values;
    const every = Math.ceil(values.length / count);
    return values.filter((_, i) => i % every === 0);
  };
  return fn;
}
var MS = { minute: 6e4, hour: 36e5, day: 864e5, week: 6048e5, month: 26298e5, year: 315576e5 };
function timeScale(domain, range) {
  const base = linearScale(domain, range);
  const fn = ((v) => base(v instanceof Date ? v.getTime() : Number(v)));
  fn.kind = "time";
  fn.domain = base.domain;
  fn.range = range;
  fn.bandwidth = 0;
  fn.invert = (px) => new Date(base.invert(px));
  fn.ticks = (count = 6) => {
    const [d0, d1] = base.domain;
    const span = d1 - d0;
    const target = span / Math.max(1, count);
    const unit = [MS.minute, MS.hour, MS.day, MS.week, MS.month, MS.year].reduce((best, u) => Math.abs(u - target) < Math.abs(best - target) ? u : best, MS.day);
    const step = Math.max(unit, niceStep(target / unit) * unit);
    const out = [];
    for (let t = Math.ceil(d0 / step) * step, i = 0; t <= d1 && i < 500; t += step, i++) out.push(new Date(t));
    return out;
  };
  return fn;
}
function extent(values) {
  let lo = Infinity;
  let hi = -Infinity;
  for (const v of values) {
    if (v == null) continue;
    const n = Number(v);
    if (!Number.isFinite(n)) continue;
    if (n < lo) lo = n;
    if (n > hi) hi = n;
  }
  return Number.isFinite(lo) ? [lo, hi] : [0, 1];
}

// src/theme.ts
var PALETTE = [
  "#2E5BBA",
  // blue
  "#E8523A",
  // red
  "#1E8449",
  // green
  "#F7943A",
  // amber
  "#7B4DBF",
  // purple
  "#0E9594",
  // teal
  "#C0392B",
  // brick
  "#64748B",
  // slate
  "#D946A0",
  // magenta
  "#8B5E1F"
  // bronze
];
var SEMANTIC = {
  positive: "#1E8449",
  negative: "#C0392B",
  neutral: "#64748B",
  total: "#2E5BBA"
};
var RAMP = ["#EFF4FB", "#2E5BBA"];
function colorAt(palette, i) {
  const p = palette && palette.length ? palette : PALETTE;
  return p[i % p.length];
}
function mix(a, b, t) {
  const pa = hexToRgb(a);
  const pb = hexToRgb(b);
  if (!pa || !pb) return a;
  const k = Math.max(0, Math.min(1, t));
  const c = [0, 1, 2].map((i) => Math.round(pa[i] + (pb[i] - pa[i]) * k));
  return `#${c.map((v) => v.toString(16).padStart(2, "0")).join("")}`;
}
function hexToRgb(hex) {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return null;
  const n = parseInt(m[1], 16);
  return [n >> 16 & 255, n >> 8 & 255, n & 255];
}
function contrastText(bg) {
  const rgb = hexToRgb(bg);
  if (!rgb) return "#111827";
  const l = (0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]) / 255;
  return l > 0.6 ? "#111827" : "#ffffff";
}
var INR = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 2 });
function compact(n) {
  const a = Math.abs(n);
  const sign = n < 0 ? "-" : "";
  if (a >= 1e7) return `${sign}${(a / 1e7).toFixed(a / 1e7 >= 100 ? 0 : 2)} Cr`;
  if (a >= 1e5) return `${sign}${(a / 1e5).toFixed(a / 1e5 >= 100 ? 0 : 2)} L`;
  if (a >= 1e3) return `${sign}${(a / 1e3).toFixed(a / 1e3 >= 100 ? 0 : 1)} K`;
  return INR.format(n);
}
function formatValue(v) {
  if (v == null || v === "") return "\u2014";
  if (v instanceof Date) return v.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
  if (typeof v === "number") return INR.format(v);
  return String(v);
}
function formatDateTick(d, spanMs) {
  if (spanMs > 315576e5 * 2) return String(d.getFullYear());
  if (spanMs > 26298e5 * 2) return d.toLocaleDateString("en-IN", { month: "short", year: "2-digit" });
  if (spanMs > 864e5 * 2) return d.toLocaleDateString("en-IN", { day: "2-digit", month: "short" });
  return d.toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" });
}
function linePath(pts) {
  if (!pts.length) return "";
  return pts.map((p, i) => `${i ? "L" : "M"}${p.x.toFixed(2)},${p.y.toFixed(2)}`).join(" ");
}
function smoothPath(pts) {
  if (pts.length < 3) return linePath(pts);
  let d = `M${pts[0].x.toFixed(2)},${pts[0].y.toFixed(2)}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[Math.max(0, i - 1)];
    const p1 = pts[i];
    const p2 = pts[i + 1];
    const p3 = pts[Math.min(pts.length - 1, i + 2)];
    const c1x = p1.x + (p2.x - p0.x) / 6;
    const c1y = p1.y + (p2.y - p0.y) / 6;
    const c2x = p2.x - (p3.x - p1.x) / 6;
    const c2y = p2.y - (p3.y - p1.y) / 6;
    d += ` C${c1x.toFixed(2)},${c1y.toFixed(2)} ${c2x.toFixed(2)},${c2y.toFixed(2)} ${p2.x.toFixed(2)},${p2.y.toFixed(2)}`;
  }
  return d;
}
function stepPath(pts) {
  if (!pts.length) return "";
  let d = `M${pts[0].x.toFixed(2)},${pts[0].y.toFixed(2)}`;
  for (let i = 1; i < pts.length; i++) {
    d += ` L${pts[i].x.toFixed(2)},${pts[i - 1].y.toFixed(2)} L${pts[i].x.toFixed(2)},${pts[i].y.toFixed(2)}`;
  }
  return d;
}
function pathFor(pts, curve = "linear") {
  return curve === "smooth" ? smoothPath(pts) : curve === "step" ? stepPath(pts) : linePath(pts);
}
function arcPath(cx, cy, rOuter, rInner, a0, a1) {
  const large = a1 - a0 > Math.PI ? 1 : 0;
  const p = (r, a) => `${(cx + r * Math.sin(a)).toFixed(2)},${(cy - r * Math.cos(a)).toFixed(2)}`;
  if (rInner <= 0) {
    return `M${cx},${cy} L${p(rOuter, a0)} A${rOuter},${rOuter} 0 ${large} 1 ${p(rOuter, a1)} Z`;
  }
  return `M${p(rOuter, a0)} A${rOuter},${rOuter} 0 ${large} 1 ${p(rOuter, a1)} L${p(rInner, a1)} A${rInner},${rInner} 0 ${large} 0 ${p(rInner, a0)} Z`;
}
function roundedTopRect(x, y, w, h, r) {
  const rr = Math.max(0, Math.min(r, w / 2, Math.abs(h)));
  if (h >= 0) {
    return `M${x},${y + h} L${x},${y + rr} Q${x},${y} ${x + rr},${y} L${x + w - rr},${y} Q${x + w},${y} ${x + w},${y + rr} L${x + w},${y + h} Z`;
  }
  const yb = y + h;
  return `M${x},${yb} L${x},${y - rr} Q${x},${y} ${x + rr},${y} L${x + w - rr},${y} Q${x + w},${y} ${x + w},${y - rr} L${x + w},${yb} Z`;
}

// src/chrome.tsx
import { useCallback, useEffect, useRef, useState } from "react";
import { Fragment, jsx, jsxs } from "react/jsx-runtime";
function useWidth() {
  const [w, setW] = useState(0);
  const observer = useRef(null);
  const setRef = useCallback((node) => {
    observer.current?.disconnect();
    observer.current = null;
    if (!node) return;
    setW(node.clientWidth);
    const ro = new ResizeObserver(() => setW(node.clientWidth));
    ro.observe(node);
    observer.current = ro;
  }, []);
  useEffect(() => () => observer.current?.disconnect(), []);
  return [setRef, w];
}
function useAnimation(key, enabled = true, ms = 450) {
  const [t, setT] = useState(enabled ? 0 : 1);
  const raf = useRef(0);
  useEffect(() => {
    if (!enabled) {
      setT(1);
      return;
    }
    setT(0);
    const start = performance.now();
    const tick = (now) => {
      const p = Math.min(1, (now - start) / ms);
      setT(1 - Math.pow(1 - p, 3));
      if (p < 1) raf.current = requestAnimationFrame(tick);
    };
    raf.current = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf.current);
  }, [key, enabled, ms]);
  return t;
}
function Axis({ scale, orient, def = {}, plot, tickCount }) {
  if (def.hidden) return null;
  const ticks = scale.ticks(def.tickCount ?? tickCount ?? (orient === "bottom" ? 8 : 6));
  const isX = orient === "bottom";
  const rotation = def.labelRotation ?? 0;
  const spanMs = scale.kind === "time" ? Number(scale.domain[1]) - Number(scale.domain[0]) : 0;
  const label = (v) => {
    if (def.format) return def.format(v);
    if (scale.kind === "time") return formatDateTick(v instanceof Date ? v : new Date(v), spanMs);
    if (typeof v === "number") return compact(v);
    return String(v);
  };
  return /* @__PURE__ */ jsxs("g", { "aria-hidden": "true", children: [
    def.gridLines !== false && ticks.map((t, i) => {
      const p = scale(t) + (isX ? scale.bandwidth / 2 : 0);
      return isX ? /* @__PURE__ */ jsx(
        "line",
        {
          x1: p,
          x2: p,
          y1: plot.y,
          y2: plot.y + plot.h,
          stroke: "var(--ec-grid, #e5e7eb)",
          strokeWidth: 1
        },
        `g${i}`
      ) : /* @__PURE__ */ jsx(
        "line",
        {
          y1: p,
          y2: p,
          x1: plot.x,
          x2: plot.x + plot.w,
          stroke: "var(--ec-grid, #e5e7eb)",
          strokeWidth: 1
        },
        `g${i}`
      );
    }),
    isX ? /* @__PURE__ */ jsx("line", { x1: plot.x, x2: plot.x + plot.w, y1: plot.y + plot.h, y2: plot.y + plot.h, stroke: "var(--ec-axis, #9ca3af)" }) : /* @__PURE__ */ jsx(
      "line",
      {
        y1: plot.y,
        y2: plot.y + plot.h,
        x1: orient === "left" ? plot.x : plot.x + plot.w,
        x2: orient === "left" ? plot.x : plot.x + plot.w,
        stroke: "var(--ec-axis, #9ca3af)"
      }
    ),
    ticks.map((t, i) => {
      const p = scale(t) + (isX ? scale.bandwidth / 2 : 0);
      if (isX) {
        const y = plot.y + plot.h + 16;
        return /* @__PURE__ */ jsx(
          "text",
          {
            x: p,
            y,
            textAnchor: rotation ? "end" : "middle",
            transform: rotation ? `rotate(${-Math.abs(rotation)} ${p} ${y})` : void 0,
            fontSize: 11,
            fill: "var(--ec-text-muted, #6b7280)",
            children: label(t)
          },
          `t${i}`
        );
      }
      const x = orient === "left" ? plot.x - 8 : plot.x + plot.w + 8;
      return /* @__PURE__ */ jsx(
        "text",
        {
          x,
          y: p + 4,
          textAnchor: orient === "left" ? "end" : "start",
          fontSize: 11,
          fill: "var(--ec-text-muted, #6b7280)",
          children: label(t)
        },
        `t${i}`
      );
    }),
    def.title && (isX ? /* @__PURE__ */ jsx(
      "text",
      {
        x: plot.x + plot.w / 2,
        y: plot.y + plot.h + 40,
        textAnchor: "middle",
        fontSize: 11,
        fontWeight: 600,
        fill: "var(--ec-text, #374151)",
        children: def.title
      }
    ) : /* @__PURE__ */ jsx(
      "text",
      {
        transform: `rotate(-90 ${orient === "left" ? 14 : plot.x + plot.w + 46} ${plot.y + plot.h / 2})`,
        x: orient === "left" ? 14 : plot.x + plot.w + 46,
        y: plot.y + plot.h / 2,
        textAnchor: "middle",
        fontSize: 11,
        fontWeight: 600,
        fill: "var(--ec-text, #374151)",
        children: def.title
      }
    ))
  ] });
}
function Legend({
  items,
  position = "bottom",
  toggleable = true,
  onToggle
}) {
  if (position === "none" || !items.length) return null;
  const vertical = position === "left" || position === "right";
  return /* @__PURE__ */ jsx(
    "div",
    {
      className: `flex flex-wrap items-center gap-x-4 gap-y-1 text-[12px] ${vertical ? "flex-col items-start" : "justify-center"}`,
      style: { padding: vertical ? "4px 8px" : "6px 4px" },
      children: items.map((it) => /* @__PURE__ */ jsxs(
        "button",
        {
          type: "button",
          disabled: !toggleable,
          onClick: () => onToggle?.(it.name),
          "aria-pressed": !it.hidden,
          className: `flex items-center gap-1.5 rounded px-1 transition-opacity ${toggleable ? "cursor-pointer hover:bg-black/5" : "cursor-default"} ${it.hidden ? "opacity-40" : ""}`,
          children: [
            /* @__PURE__ */ jsx(
              "span",
              {
                "aria-hidden": "true",
                style: { background: it.color },
                className: `inline-block h-2.5 w-2.5 shrink-0 rounded-sm ${it.hidden ? "grayscale" : ""}`
              }
            ),
            /* @__PURE__ */ jsx("span", { style: { color: "var(--ec-text, #374151)", textDecoration: it.hidden ? "line-through" : void 0 }, children: it.name })
          ]
        },
        it.name
      ))
    }
  );
}
function ChartTooltip({ state, width, height }) {
  if (!state) return null;
  const W = 190;
  const left = state.x + W + 16 > width ? Math.max(4, state.x - W - 12) : state.x + 12;
  const top = Math.min(Math.max(4, state.y - 10), Math.max(4, height - 90));
  return /* @__PURE__ */ jsx(
    "div",
    {
      role: "tooltip",
      className: "pointer-events-none absolute z-20 rounded-lg border px-2.5 py-2 text-[12px] shadow-lg",
      style: {
        left,
        top,
        minWidth: W,
        background: "var(--ec-tooltip-bg, #ffffff)",
        borderColor: "var(--ec-border, #e5e7eb)",
        color: "var(--ec-text, #374151)"
      },
      children: state.custom ?? /* @__PURE__ */ jsxs(Fragment, { children: [
        /* @__PURE__ */ jsx("div", { className: "mb-1 font-semibold", style: { color: "var(--ec-text, #111827)" }, children: state.title }),
        state.rows.map((r, i) => /* @__PURE__ */ jsxs("div", { className: "flex items-center gap-1.5 leading-5", children: [
          /* @__PURE__ */ jsx("span", { "aria-hidden": "true", style: { background: r.color }, className: "inline-block h-2 w-2 shrink-0 rounded-sm" }),
          /* @__PURE__ */ jsx("span", { className: "flex-1 truncate", children: r.name }),
          /* @__PURE__ */ jsx("span", { className: "font-medium tabular-nums", children: r.formatted })
        ] }, i))
      ] })
    }
  );
}
function ChartFrame({
  title,
  subtitle,
  children,
  className = "",
  empty,
  emptyMessage,
  height
}) {
  return /* @__PURE__ */ jsxs("div", { className: `relative w-full ${className}`, children: [
    (title || subtitle) && /* @__PURE__ */ jsxs("div", { className: "mb-1 px-1", children: [
      title && /* @__PURE__ */ jsx("div", { className: "text-[13px] font-semibold", style: { color: "var(--ec-text, #111827)" }, children: title }),
      subtitle && /* @__PURE__ */ jsx("div", { className: "text-[11px]", style: { color: "var(--ec-text-muted, #6b7280)" }, children: subtitle })
    ] }),
    empty ? /* @__PURE__ */ jsx(
      "div",
      {
        className: "flex items-center justify-center rounded-lg border border-dashed text-[12px]",
        style: { height, borderColor: "var(--ec-border, #e5e7eb)", color: "var(--ec-text-muted, #6b7280)" },
        children: emptyMessage ?? "No data to chart."
      }
    ) : children
  ] });
}
function useHidden(initial = []) {
  const [hidden, setHidden] = useState(new Set(initial));
  const toggle = useCallback((name) => {
    setHidden((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }, []);
  return { hidden, setHidden, toggle };
}

// src/exportImage.ts
var CSS_VARS = [
  "--ec-grid",
  "--ec-axis",
  "--ec-text",
  "--ec-text-muted",
  "--ec-border",
  "--ec-surface",
  "--ec-crosshair",
  "--ec-accent",
  "--ec-annotation"
];
function svgToString(svg) {
  if (!svg) return null;
  const clone = svg.cloneNode(true);
  clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
  const computed = getComputedStyle(svg);
  const decls = CSS_VARS.map((v) => `${v}: ${computed.getPropertyValue(v).trim() || fallbackFor(v)};`).join(" ");
  const style = document.createElementNS("http://www.w3.org/2000/svg", "style");
  style.textContent = `svg { ${decls} font-family: ${computed.fontFamily || "system-ui, sans-serif"}; }`;
  clone.insertBefore(style, clone.firstChild);
  const bg = document.createElementNS("http://www.w3.org/2000/svg", "rect");
  bg.setAttribute("width", "100%");
  bg.setAttribute("height", "100%");
  bg.setAttribute("fill", "var(--ec-surface, #ffffff)");
  clone.insertBefore(bg, style.nextSibling);
  return new XMLSerializer().serializeToString(clone);
}
function fallbackFor(v) {
  switch (v) {
    case "--ec-grid":
      return "#e5e7eb";
    case "--ec-axis":
      return "#9ca3af";
    case "--ec-text":
      return "#374151";
    case "--ec-text-muted":
      return "#6b7280";
    case "--ec-border":
      return "#e5e7eb";
    case "--ec-surface":
      return "#ffffff";
    case "--ec-crosshair":
      return "#9ca3af";
    case "--ec-accent":
      return "#2E5BBA";
    case "--ec-annotation":
      return "#E8523A";
    default:
      return "currentColor";
  }
}
async function svgToPNG(svg, scale = 2) {
  const source = svgToString(svg);
  if (!source || !svg) return null;
  const w = svg.width.baseVal.value || svg.clientWidth;
  const h = svg.height.baseVal.value || svg.clientHeight;
  const blob = new Blob([source], { type: "image/svg+xml;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  try {
    const img = await loadImage(url);
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(w * scale);
    canvas.height = Math.round(h * scale);
    const ctx = canvas.getContext("2d");
    if (!ctx) return null;
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL("image/png");
  } finally {
    URL.revokeObjectURL(url);
  }
}
function loadImage(src) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error("Could not rasterise the chart SVG."));
    img.src = src;
  });
}
async function downloadChart(svg, fileName = "chart", format = "png") {
  let href = null;
  if (format === "svg") {
    const s = svgToString(svg);
    if (!s) return;
    href = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(s)}`;
  } else {
    href = await svgToPNG(svg);
  }
  if (!href) return;
  const a = document.createElement("a");
  a.href = href;
  a.download = `${fileName}.${format}`;
  document.body.appendChild(a);
  a.click();
  a.remove();
}

// src/CartesianChart.tsx
import { jsx as jsx2, jsxs as jsxs2 } from "react/jsx-runtime";
var DEFAULT_PAD = { top: 12, right: 16, bottom: 40, left: 56 };
function CartesianChartInner(props, apiRef) {
  const {
    data,
    series,
    height = 320,
    xAxis = {},
    yAxis = {},
    yAxisRight,
    palette,
    legend,
    tooltip,
    padding,
    annotations = [],
    zoom = false,
    crosshair = true,
    animate = true,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef2(null);
  const { hidden, setHidden, toggle } = useHidden(series.filter((s) => s.hidden).map((s) => s.name ?? s.yKey));
  const [tip, setTip] = useState2(null);
  const [zoomRange, setZoomRange] = useState2(null);
  const [drag, setDrag] = useState2(null);
  const nameOf = (s) => s.name ?? s.yKey;
  const visible = series.filter((s) => !hidden.has(nameOf(s)));
  const pad = { ...DEFAULT_PAD, ...padding };
  if (yAxisRight) pad.right = Math.max(pad.right, 56);
  if (xAxis.title) pad.bottom += 18;
  const plot = {
    x: pad.left,
    y: pad.top,
    w: Math.max(0, width - pad.left - pad.right),
    h: Math.max(0, height - pad.top - pad.bottom)
  };
  const xType = xAxis.type ?? inferXType(data, series);
  const hasBars = visible.some((s) => s.type === "column" || s.type === "bar");
  const xValues = useMemo(() => {
    const seen = [];
    const set = /* @__PURE__ */ new Set();
    for (const row of data) {
      for (const s of series) {
        const v = row[s.xKey];
        const k = String(v);
        if (!set.has(k)) {
          set.add(k);
          seen.push(v);
        }
      }
    }
    return seen;
  }, [data, series]);
  const xScale = useMemo(() => {
    const range = [plot.x, plot.x + plot.w];
    if (xType === "category") {
      const vals = zoomRange ? xValues.slice(Math.floor(zoomRange[0]), Math.ceil(zoomRange[1]) + 1) : xValues;
      return bandScale(vals, range, { padding: hasBars ? 0.25 : 0.05, reverse: xAxis.reverse });
    }
    const nums = xValues.map((v) => v instanceof Date ? v.getTime() : Number(v));
    let [lo, hi] = extent(nums);
    if (zoomRange) {
      lo = zoomRange[0];
      hi = zoomRange[1];
    }
    if (xAxis.min != null) lo = xAxis.min;
    if (xAxis.max != null) hi = xAxis.max;
    return xType === "time" ? timeScale([lo, hi], range) : linearScale([lo, hi], range, { reverse: xAxis.reverse });
  }, [xType, xValues, plot.x, plot.w, hasBars, zoomRange, xAxis.min, xAxis.max, xAxis.reverse]);
  const stacks = useMemo(() => buildStacks(data, visible), [data, visible]);
  const makeY = (side, def = yAxis) => {
    const mine = visible.filter((s) => (s.yAxis ?? "left") === side);
    const range = [plot.y + plot.h, plot.y];
    if (!mine.length) return linearScale([0, 1], range);
    const vals = [];
    for (const s of mine) {
      if (s.stack) continue;
      for (const row of data) vals.push(Number(row[s.yKey]));
    }
    for (const [, byX] of stacks) {
      for (const [, tot] of byX) {
        vals.push(tot.pos);
        vals.push(tot.neg);
      }
    }
    let [lo, hi] = extent(vals);
    const anyNormalized = mine.some((s) => s.normalized);
    if (anyNormalized) {
      lo = 0;
      hi = 100;
    }
    if (def.includeZero !== false && (hasBars || lo > 0)) lo = Math.min(0, lo);
    if (def.min != null) lo = def.min;
    if (def.max != null) hi = def.max;
    if (def.type === "log") return logScale([Math.max(lo, 1e-9), hi], range);
    const [nlo, nhi] = niceDomain(lo, hi, def.tickCount ?? 6);
    return linearScale([nlo, nhi], range, { reverse: def.reverse });
  };
  const yLeft = useMemo(() => makeY("left", yAxis), [visible, data, plot.y, plot.h, stacks, yAxis, hasBars]);
  const yRight = useMemo(() => yAxisRight ? makeY("right", yAxisRight) : null, [visible, data, plot.y, plot.h, stacks, yAxisRight]);
  const anim = useAnimation(`${data.length}:${visible.map(nameOf).join()}`, animate);
  const barSeries = visible.filter((s) => s.type === "column" || s.type === "bar");
  const barGroups = useMemo(() => {
    const slots = [];
    for (const s of barSeries) {
      const key = s.stack ?? `__${nameOf(s)}`;
      if (!slots.includes(key)) slots.push(key);
    }
    return slots;
  }, [barSeries]);
  const slotWidth = barGroups.length ? (xScale.bandwidth || plot.w / Math.max(1, xValues.length)) / barGroups.length : 0;
  const pointerX = (e) => {
    const rect = svgRef.current?.getBoundingClientRect();
    return rect ? e.clientX - rect.left : 0;
  };
  const nearestX = (px) => {
    if (xScale.kind === "category") return xScale.invert(px);
    const target = xScale.invert(px);
    const t = target instanceof Date ? target.getTime() : Number(target);
    let best = null;
    let bestD = Infinity;
    for (const v of xValues) {
      const n = v instanceof Date ? v.getTime() : Number(v);
      const d = Math.abs(n - t);
      if (d < bestD) {
        bestD = d;
        best = v;
      }
    }
    return best;
  };
  const [hoverX, setHoverX] = useState2(null);
  const onMove = (e) => {
    if (!plot.w || !data.length) return;
    const px = pointerX(e);
    if (drag) {
      setDrag({ ...drag, to: px });
      return;
    }
    if (px < plot.x || px > plot.x + plot.w) {
      setTip(null);
      setHoverX(null);
      return;
    }
    const xv = nearestX(px);
    setHoverX(xv);
    if (tooltip?.enabled === false) return;
    const rows = visible.map((s) => {
      const datum = data.find((d) => String(d[s.xKey]) === String(xv));
      const raw = datum ? datum[s.yKey] : null;
      return {
        name: nameOf(s),
        color: s.color ?? colorAt(palette, series.indexOf(s)),
        value: raw,
        formatted: typeof raw === "number" ? formatValue(raw) : String(raw ?? "\u2014"),
        datum: datum ?? {}
      };
    }).filter((r) => r.value != null);
    if (!rows.length) {
      setTip(null);
      return;
    }
    const xFormatted = xAxis.format ? xAxis.format(xv) : formatValue(xv);
    setTip({
      x: xScale(xv) + xScale.bandwidth / 2,
      y: e.clientY - (svgRef.current?.getBoundingClientRect().top ?? 0),
      title: xFormatted,
      rows: rows.map(({ name, color, formatted }) => ({ name, color, formatted })),
      custom: tooltip?.render?.({ items: rows, x: xv, xFormatted })
    });
  };
  const applyZoom = () => {
    if (!drag || Math.abs(drag.to - drag.from) < 12) {
      setDrag(null);
      return;
    }
    const [a, b] = [drag.from, drag.to].sort((p, q) => p - q);
    if (xScale.kind === "category") {
      const all = xValues;
      const i0 = all.indexOf(xScale.invert(a));
      const i1 = all.indexOf(xScale.invert(b));
      if (i0 >= 0 && i1 > i0) setZoomRange([i0, i1]);
    } else {
      const v0 = xScale.invert(a);
      const v1 = xScale.invert(b);
      setZoomRange([Number(v0 instanceof Date ? v0.getTime() : v0), Number(v1 instanceof Date ? v1.getTime() : v1)]);
    }
    setDrag(null);
  };
  useImperativeHandle(apiRef, () => ({
    toSVG: () => svgToString(svgRef.current),
    toPNG: (scale = 2) => svgToPNG(svgRef.current, scale),
    download: (fileName = "chart", format = "png") => downloadChart(svgRef.current, fileName, format),
    resetZoom: () => setZoomRange(null),
    getHiddenSeries: () => [...hidden],
    setHiddenSeries: (names) => setHidden(new Set(names))
  }), [hidden, setHidden]);
  const legendItems = series.map((s, i) => ({
    name: nameOf(s),
    color: s.color ?? colorAt(palette, i),
    hidden: hidden.has(nameOf(s))
  }));
  const legendPos = legend?.position ?? "bottom";
  const empty = !data.length || !series.length;
  return /* @__PURE__ */ jsxs2(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty,
      emptyMessage,
      children: [
        legendPos === "top" && /* @__PURE__ */ jsx2(Legend, { items: legendItems, position: "top", toggleable: legend?.toggleable !== false, onToggle: toggle }),
        /* @__PURE__ */ jsxs2("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
          width > 0 && /* @__PURE__ */ jsxs2(
            "svg",
            {
              ref: svgRef,
              width,
              height,
              role: "img",
              "aria-label": ariaLabel ?? describeChart(title, series.map(nameOf), data.length),
              style: { touchAction: zoom ? "none" : void 0, cursor: drag ? "ew-resize" : void 0 },
              onPointerMove: onMove,
              onPointerLeave: () => {
                setTip(null);
                setHoverX(null);
                setDrag(null);
              },
              onPointerDown: (e) => {
                if (zoom) {
                  const px = pointerX(e);
                  setDrag({ from: px, to: px });
                }
              },
              onPointerUp: applyZoom,
              onDoubleClick: () => setZoomRange(null),
              children: [
                /* @__PURE__ */ jsx2(Axis, { scale: xScale, orient: "bottom", def: xAxis, plot }),
                /* @__PURE__ */ jsx2(Axis, { scale: yLeft, orient: "left", def: yAxis, plot }),
                yRight && /* @__PURE__ */ jsx2(Axis, { scale: yRight, orient: "right", def: { ...yAxisRight, gridLines: false }, plot }),
                annotations.map((a, i) => /* @__PURE__ */ jsx2(AnnotationMark, { a, xScale, yScale: yLeft, plot }, i)),
                /* @__PURE__ */ jsx2("clipPath", { id: "ec-plot", children: /* @__PURE__ */ jsx2("rect", { x: plot.x, y: plot.y, width: plot.w, height: plot.h }) }),
                /* @__PURE__ */ jsxs2("g", { clipPath: "url(#ec-plot)", children: [
                  barSeries.map((s) => /* @__PURE__ */ jsx2(
                    Bars,
                    {
                      s,
                      data,
                      xScale,
                      yScale: s.yAxis === "right" && yRight ? yRight : yLeft,
                      color: s.color ?? colorAt(palette, series.indexOf(s)),
                      slot: barGroups.indexOf(s.stack ?? `__${nameOf(s)}`),
                      slotWidth,
                      stacks,
                      anim,
                      plot,
                      hoverX,
                      onClick: (datum, index) => onSeriesClick?.({ series: nameOf(s), datum, index })
                    },
                    `b-${nameOf(s)}`
                  )),
                  visible.filter((s) => s.type === "line" || s.type === "area").map((s) => /* @__PURE__ */ jsx2(
                    LineArea,
                    {
                      s,
                      data,
                      xScale,
                      yScale: s.yAxis === "right" && yRight ? yRight : yLeft,
                      color: s.color ?? colorAt(palette, series.indexOf(s)),
                      stacks,
                      anim,
                      plot,
                      hoverX,
                      onClick: (datum, index) => onSeriesClick?.({ series: nameOf(s), datum, index })
                    },
                    `l-${nameOf(s)}`
                  )),
                  visible.filter((s) => s.type === "scatter" || s.type === "bubble").map((s) => /* @__PURE__ */ jsx2(
                    Points,
                    {
                      s,
                      data,
                      xScale,
                      yScale: s.yAxis === "right" && yRight ? yRight : yLeft,
                      color: s.color ?? colorAt(palette, series.indexOf(s)),
                      anim,
                      onClick: (datum, index) => onSeriesClick?.({ series: nameOf(s), datum, index })
                    },
                    `p-${nameOf(s)}`
                  ))
                ] }),
                crosshair && hoverX != null && !drag && /* @__PURE__ */ jsx2(
                  "line",
                  {
                    x1: xScale(hoverX) + xScale.bandwidth / 2,
                    x2: xScale(hoverX) + xScale.bandwidth / 2,
                    y1: plot.y,
                    y2: plot.y + plot.h,
                    stroke: "var(--ec-crosshair, #9ca3af)",
                    strokeDasharray: "3 3",
                    pointerEvents: "none"
                  }
                ),
                drag && /* @__PURE__ */ jsx2(
                  "rect",
                  {
                    x: Math.min(drag.from, drag.to),
                    y: plot.y,
                    width: Math.abs(drag.to - drag.from),
                    height: plot.h,
                    fill: "var(--ec-accent, #2E5BBA)",
                    opacity: 0.12,
                    pointerEvents: "none"
                  }
                )
              ]
            }
          ),
          /* @__PURE__ */ jsx2(ChartTooltip, { state: tip, width, height }),
          zoomRange && /* @__PURE__ */ jsx2(
            "button",
            {
              type: "button",
              onClick: () => setZoomRange(null),
              className: "absolute right-2 top-2 rounded border bg-white/90 px-2 py-0.5 text-[11px] shadow-sm",
              style: { borderColor: "var(--ec-border, #e5e7eb)" },
              children: "Reset zoom"
            }
          )
        ] }),
        legendPos !== "top" && legendPos !== "none" && /* @__PURE__ */ jsx2(Legend, { items: legendItems, position: legendPos, toggleable: legend?.toggleable !== false, onToggle: toggle })
      ]
    }
  );
}
function buildStacks(data, series) {
  const out = /* @__PURE__ */ new Map();
  for (const s of series) {
    if (!s.stack) continue;
    let byX = out.get(s.stack);
    if (!byX) {
      byX = /* @__PURE__ */ new Map();
      out.set(s.stack, byX);
    }
    for (const row of data) {
      const k = String(row[s.xKey]);
      const cur = byX.get(k) ?? { pos: 0, neg: 0 };
      const v = Number(row[s.yKey]) || 0;
      if (v >= 0) cur.pos += v;
      else cur.neg += v;
      byX.set(k, cur);
    }
  }
  return out;
}
function stackBase(stacks, series, s, row) {
  if (!s.stack) return { base: 0, total: 0 };
  const key = String(row[s.xKey]);
  const totals = stacks.get(s.stack)?.get(key) ?? { pos: 0, neg: 0 };
  let base = 0;
  for (const other of series) {
    if (other.stack !== s.stack) continue;
    if (other === s) break;
    base += Number(row[other.yKey]) || 0;
  }
  return { base, total: totals.pos + Math.abs(totals.neg) };
}
function Bars({ s, data, xScale, yScale, color, anim, slot = 0, slotWidth = 0, stacks, hoverX, onClick }) {
  const zero = yScale(0);
  const fillOpacity = s.fillOpacity ?? 1;
  return /* @__PURE__ */ jsx2("g", { children: data.map((row, i) => {
    const xv = row[s.xKey];
    let v = Number(row[s.yKey]);
    if (!Number.isFinite(v)) return null;
    let base = 0;
    if (s.stack && stacks) {
      const st = stackBase(stacks, [s], s, row);
      base = st.base;
      if (s.normalized && st.total) v = v / st.total * 100;
    }
    const x0 = xScale(xv) + slot * slotWidth;
    const w = Math.max(1, slotWidth || xScale.bandwidth || 12);
    const yTop = yScale(base + v);
    const yBase = s.stack ? yScale(base) : zero;
    const h = (yTop - yBase) * anim;
    const label = s.labels ? compact(Number(row[s.yKey])) : null;
    return /* @__PURE__ */ jsxs2("g", { children: [
      /* @__PURE__ */ jsx2(
        "path",
        {
          d: roundedTopRect(x0, yBase + h, w, -h, 3),
          fill: color,
          fillOpacity: hoverX != null && String(hoverX) !== String(xv) ? fillOpacity * 0.45 : fillOpacity,
          onClick: () => onClick?.(row, i),
          style: { cursor: onClick ? "pointer" : void 0 }
        }
      ),
      label && Math.abs(h) > 14 && /* @__PURE__ */ jsx2(
        "text",
        {
          x: x0 + w / 2,
          y: yBase + h - 4,
          textAnchor: "middle",
          fontSize: 10,
          fill: "var(--ec-text-muted, #6b7280)",
          children: label
        }
      )
    ] }, i);
  }) });
}
function LineArea({ s, data, xScale, yScale, color, anim, plot, stacks, hoverX, onClick }) {
  const half = xScale.bandwidth / 2;
  const pts = data.map((row) => {
    let v = Number(row[s.yKey]);
    if (!Number.isFinite(v)) return null;
    let base = 0;
    if (s.stack && stacks) {
      const st = stackBase(stacks, [s], s, row);
      base = st.base;
      if (s.normalized && st.total) v = v / st.total * 100;
    }
    return { x: xScale(row[s.xKey]) + half, y: yScale(base + v), row, base };
  }).filter(Boolean);
  if (!pts.length) return null;
  const d = pathFor(pts, s.curve ?? "linear");
  const zeroY = yScale(0);
  return /* @__PURE__ */ jsxs2("g", { children: [
    s.type === "area" && /* @__PURE__ */ jsx2(
      "path",
      {
        d: `${d} L${pts[pts.length - 1].x},${s.stack ? yScale(pts[pts.length - 1].base) : zeroY} L${pts[0].x},${s.stack ? yScale(pts[0].base) : zeroY} Z`,
        fill: color,
        opacity: (s.fillOpacity ?? 0.18) * anim
      }
    ),
    /* @__PURE__ */ jsx2(
      "path",
      {
        d,
        fill: "none",
        stroke: color,
        strokeWidth: s.strokeWidth ?? 2,
        strokeLinecap: "round",
        strokeLinejoin: "round",
        pathLength: 1,
        style: s.dashed ? { strokeDasharray: "5 4", opacity: anim } : { strokeDasharray: 1, strokeDashoffset: 1 - anim }
      }
    ),
    s.marker !== false && pts.map((p, i) => {
      const active = hoverX != null && String(p.row[s.xKey]) === String(hoverX);
      if (!active && s.marker !== true && pts.length > 40) return null;
      return /* @__PURE__ */ jsx2(
        "circle",
        {
          cx: p.x,
          cy: p.y,
          r: active ? 4.5 : 3,
          fill: "var(--ec-surface, #ffffff)",
          stroke: color,
          strokeWidth: 2,
          opacity: anim,
          onClick: () => onClick?.(p.row, i),
          style: { cursor: onClick ? "pointer" : void 0 }
        },
        i
      );
    }),
    s.labels && pts.map((p, i) => /* @__PURE__ */ jsx2(
      "text",
      {
        x: p.x,
        y: p.y - 8,
        textAnchor: "middle",
        fontSize: 10,
        fill: "var(--ec-text-muted, #6b7280)",
        opacity: anim,
        children: compact(Number(p.row[s.yKey]))
      },
      `t${i}`
    ))
  ] });
}
function Points({ s, data, xScale, yScale, color, anim, onClick }) {
  const sizes = s.sizeKey ? extent(data.map((d) => Number(d[s.sizeKey]))) : null;
  const half = xScale.bandwidth / 2;
  return /* @__PURE__ */ jsx2("g", { children: data.map((row, i) => {
    const y = Number(row[s.yKey]);
    if (!Number.isFinite(y)) return null;
    let r = 4;
    if (sizes && s.sizeKey) {
      const v = Number(row[s.sizeKey]);
      const t = sizes[1] === sizes[0] ? 0.5 : (v - sizes[0]) / (sizes[1] - sizes[0]);
      r = 4 + t * 18;
    }
    return /* @__PURE__ */ jsx2(
      "circle",
      {
        cx: xScale(row[s.xKey]) + half,
        cy: yScale(y),
        r: r * anim,
        fill: color,
        fillOpacity: s.fillOpacity ?? 0.65,
        stroke: color,
        strokeWidth: 1,
        onClick: () => onClick?.(row, i),
        style: { cursor: onClick ? "pointer" : void 0 }
      },
      i
    );
  }) });
}
function AnnotationMark({ a, xScale, yScale, plot }) {
  const scale = a.axis === "x" ? xScale : yScale;
  const p0 = scale(a.value instanceof Date ? a.value.getTime() : a.value) + (a.axis === "x" ? scale.bandwidth / 2 : 0);
  const p1 = a.to != null ? scale(a.to instanceof Date ? a.to.getTime() : a.to) + (a.axis === "x" ? scale.bandwidth / 2 : 0) : null;
  const color = a.color ?? "var(--ec-annotation, #E8523A)";
  if (p1 != null) {
    const [lo, hi] = [p0, p1].sort((m, n) => m - n);
    return a.axis === "x" ? /* @__PURE__ */ jsx2("rect", { x: lo, y: plot.y, width: hi - lo, height: plot.h, fill: color, opacity: 0.08 }) : /* @__PURE__ */ jsx2("rect", { x: plot.x, y: lo, width: plot.w, height: hi - lo, fill: color, opacity: 0.08 });
  }
  return /* @__PURE__ */ jsxs2("g", { children: [
    a.axis === "x" ? /* @__PURE__ */ jsx2("line", { x1: p0, x2: p0, y1: plot.y, y2: plot.y + plot.h, stroke: color, strokeWidth: 1.5, strokeDasharray: a.dashed === false ? void 0 : "5 4" }) : /* @__PURE__ */ jsx2("line", { y1: p0, y2: p0, x1: plot.x, x2: plot.x + plot.w, stroke: color, strokeWidth: 1.5, strokeDasharray: a.dashed === false ? void 0 : "5 4" }),
    a.label && /* @__PURE__ */ jsx2(
      "text",
      {
        x: a.axis === "x" ? p0 + 4 : plot.x + plot.w - 4,
        y: a.axis === "x" ? plot.y + 12 : p0 - 4,
        textAnchor: a.axis === "x" ? "start" : "end",
        fontSize: 10,
        fontWeight: 600,
        fill: color,
        children: a.label
      }
    )
  ] });
}
function inferXType(data, series) {
  const key = series[0]?.xKey;
  if (!key) return "category";
  for (const row of data) {
    const v = row[key];
    if (v == null) continue;
    if (v instanceof Date) return "time";
    if (typeof v === "number") return "number";
    if (typeof v === "string" && /^\d{4}-\d{2}-\d{2}/.test(v)) return "time";
    return "category";
  }
  return "category";
}
function describeChart(title, names, n) {
  return `${title ? title + ". " : ""}Chart of ${names.join(", ")} across ${n} data points.`;
}
var CartesianChart = React2.forwardRef(CartesianChartInner);

// src/PieChart.tsx
import React3, { useImperativeHandle as useImperativeHandle2, useMemo as useMemo2, useRef as useRef3, useState as useState3 } from "react";
import { jsx as jsx3, jsxs as jsxs3 } from "react/jsx-runtime";
var TAU = Math.PI * 2;
function PieChartInner(props, apiRef) {
  const {
    data,
    labelKey,
    valueKey,
    height = 320,
    innerRadius = 0,
    roseType = false,
    centerLabel,
    centerValue,
    otherThreshold = 0,
    palette,
    legend,
    tooltip,
    animate = true,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef3(null);
  const { hidden, setHidden, toggle } = useHidden();
  const [tip, setTip] = useState3(null);
  const [active, setActive] = useState3(null);
  const slices = useMemo2(() => {
    const raw = data.map((d) => ({ label: String(d[labelKey]), value: Number(d[valueKey]) || 0, datum: d })).filter((s) => s.value > 0).sort((a, b) => b.value - a.value);
    const total2 = raw.reduce((s, r) => s + r.value, 0);
    if (!otherThreshold || !total2) return raw;
    const keep = raw.filter((r) => r.value / total2 >= otherThreshold);
    const rest = raw.filter((r) => r.value / total2 < otherThreshold);
    if (rest.length < 2) return raw;
    return [...keep, { label: "Other", value: rest.reduce((s, r) => s + r.value, 0), datum: { __other: rest } }];
  }, [data, labelKey, valueKey, otherThreshold]);
  const visible = slices.filter((s) => !hidden.has(s.label));
  const total = visible.reduce((s, r) => s + r.value, 0);
  const anim = useAnimation(`${slices.length}:${total}`, animate);
  const size = Math.min(width || 0, height);
  const cx = (width || 0) / 2;
  const cy = height / 2;
  const rOuter = Math.max(0, size / 2 - 12);
  const rInner = innerRadius > 0 ? rOuter * innerRadius : 0;
  const maxValue = Math.max(...visible.map((s) => s.value), 1);
  useImperativeHandle2(apiRef, () => ({
    toSVG: () => svgToString(svgRef.current),
    toPNG: (scale = 2) => svgToPNG(svgRef.current, scale),
    download: (f = "chart", fmt = "png") => downloadChart(svgRef.current, f, fmt),
    resetZoom: () => {
    },
    getHiddenSeries: () => [...hidden],
    setHiddenSeries: (n) => setHidden(new Set(n))
  }), [hidden, setHidden]);
  let angle = 0;
  const arcs = visible.map((s, i) => {
    const sweep = total ? s.value / total * TAU : 0;
    const a0 = angle;
    const a1 = angle + sweep * anim;
    angle += sweep;
    const r = roseType ? rInner + (rOuter - rInner) * (0.45 + 0.55 * (s.value / maxValue)) : rOuter;
    return { ...s, a0, a1, r, mid: (a0 + a1) / 2, index: i, share: total ? s.value / total : 0 };
  });
  const legendItems = slices.map((s, i) => ({
    name: s.label,
    color: colorAt(palette, i),
    hidden: hidden.has(s.label)
  }));
  const legendPos = legend?.position ?? "right";
  const empty = !slices.length;
  return /* @__PURE__ */ jsx3(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty,
      emptyMessage,
      children: /* @__PURE__ */ jsxs3("div", { className: `flex ${legendPos === "right" ? "flex-row items-center" : "flex-col"}`, children: [
        /* @__PURE__ */ jsxs3("div", { ref: wrapRef, className: "relative min-w-0 flex-1", style: { height }, children: [
          width > 0 && /* @__PURE__ */ jsxs3(
            "svg",
            {
              ref: svgRef,
              width,
              height,
              role: "img",
              "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Proportional chart of ${slices.length} categories.`,
              onPointerLeave: () => {
                setTip(null);
                setActive(null);
              },
              children: [
                arcs.map((a) => {
                  const color = colorAt(palette, slices.findIndex((s) => s.label === a.label));
                  const isActive = active === a.index;
                  return /* @__PURE__ */ jsx3(
                    "path",
                    {
                      d: arcPath(cx, cy, a.r * (isActive ? 1.03 : 1), rInner, a.a0, a.a1),
                      fill: color,
                      stroke: "var(--ec-surface, #ffffff)",
                      strokeWidth: 2,
                      style: { cursor: onSeriesClick ? "pointer" : "default", transition: "opacity .12s" },
                      opacity: active == null || isActive ? 1 : 0.55,
                      onPointerMove: (e) => {
                        const rect = svgRef.current?.getBoundingClientRect();
                        setActive(a.index);
                        setTip({
                          x: e.clientX - (rect?.left ?? 0),
                          y: e.clientY - (rect?.top ?? 0),
                          title: a.label,
                          rows: [{
                            name: valueKey,
                            color,
                            formatted: `${formatValue(a.value)}  (${(a.share * 100).toFixed(1)}%)`
                          }],
                          custom: tooltip?.render?.({
                            items: [{ name: a.label, color, value: a.value, formatted: formatValue(a.value), datum: a.datum }],
                            x: a.label,
                            xFormatted: a.label
                          })
                        });
                      },
                      onClick: () => onSeriesClick?.({ series: a.label, datum: a.datum, index: a.index })
                    },
                    a.label
                  );
                }),
                arcs.filter((a) => a.share > 0.06).map((a) => {
                  const rl = rInner > 0 ? (rInner + a.r) / 2 : a.r * 0.65;
                  const x = cx + rl * Math.sin(a.mid);
                  const y = cy - rl * Math.cos(a.mid);
                  const color = colorAt(palette, slices.findIndex((s) => s.label === a.label));
                  return /* @__PURE__ */ jsxs3(
                    "text",
                    {
                      x,
                      y: y + 4,
                      textAnchor: "middle",
                      fontSize: 11,
                      fontWeight: 600,
                      fill: contrastText(color),
                      pointerEvents: "none",
                      opacity: anim,
                      children: [
                        (a.share * 100).toFixed(0),
                        "%"
                      ]
                    },
                    `l${a.label}`
                  );
                }),
                rInner > 0 && (centerLabel || centerValue) && /* @__PURE__ */ jsxs3("g", { pointerEvents: "none", children: [
                  centerValue && /* @__PURE__ */ jsx3(
                    "text",
                    {
                      x: cx,
                      y: cy + (centerLabel ? 2 : 6),
                      textAnchor: "middle",
                      fontSize: 20,
                      fontWeight: 700,
                      fill: "var(--ec-text, #111827)",
                      children: centerValue
                    }
                  ),
                  centerLabel && /* @__PURE__ */ jsx3(
                    "text",
                    {
                      x: cx,
                      y: cy + (centerValue ? 20 : 6),
                      textAnchor: "middle",
                      fontSize: 11,
                      fill: "var(--ec-text-muted, #6b7280)",
                      children: centerLabel
                    }
                  )
                ] })
              ]
            }
          ),
          /* @__PURE__ */ jsx3(ChartTooltip, { state: tip, width, height })
        ] }),
        legendPos !== "none" && /* @__PURE__ */ jsx3(Legend, { items: legendItems, position: legendPos, toggleable: legend?.toggleable !== false, onToggle: toggle })
      ] })
    }
  );
}
var PieChart = React3.forwardRef(PieChartInner);

// src/Special.tsx
import React4, { useImperativeHandle as useImperativeHandle3, useMemo as useMemo3, useRef as useRef4, useState as useState4 } from "react";
import { jsx as jsx4, jsxs as jsxs4 } from "react/jsx-runtime";
function useChartApi(svgRef, apiRef) {
  useImperativeHandle3(apiRef, () => ({
    toSVG: () => svgToString(svgRef.current),
    toPNG: (scale = 2) => svgToPNG(svgRef.current, scale),
    download: (f = "chart", fmt = "png") => downloadChart(svgRef.current, f, fmt),
    resetZoom: () => {
    },
    getHiddenSeries: () => [],
    setHiddenSeries: () => {
    }
  }), []);
}
function WaterfallInner(props, apiRef) {
  const {
    data,
    labelKey,
    valueKey,
    totals = [],
    height = 320,
    yAxis = {},
    positiveColor = SEMANTIC.positive,
    negativeColor = SEMANTIC.negative,
    totalColor = SEMANTIC.total,
    animate = true,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    onSeriesClick,
    padding
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef4(null);
  const [tip, setTip] = useState4(null);
  useChartApi(svgRef, apiRef);
  const pad = { top: 16, right: 16, bottom: 44, left: 64, ...padding };
  const plot = { x: pad.left, y: pad.top, w: Math.max(0, width - pad.left - pad.right), h: Math.max(0, height - pad.top - pad.bottom) };
  const bars = useMemo3(() => {
    let running = 0;
    return data.map((d) => {
      const label = String(d[labelKey]);
      const value = Number(d[valueKey]) || 0;
      const isTotal = totals.includes(label);
      const start = isTotal ? 0 : running;
      const end = isTotal ? value : running + value;
      if (!isTotal) running = end;
      else running = value;
      return { label, value, start, end, isTotal, datum: d };
    });
  }, [data, labelKey, valueKey, totals]);
  const [lo, hi] = extent(bars.flatMap((b) => [b.start, b.end]));
  const [nlo, nhi] = niceDomain(Math.min(0, lo), hi, yAxis.tickCount ?? 6);
  const y = linearScale([nlo, nhi], [plot.y + plot.h, plot.y]);
  const x = bandScale(bars.map((b) => b.label), [plot.x, plot.x + plot.w], { padding: 0.3 });
  const anim = useAnimation(bars.length, animate);
  return /* @__PURE__ */ jsx4(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !bars.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs4("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsxs4(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Waterfall of ${bars.length} steps.`,
            onPointerLeave: () => setTip(null),
            children: [
              /* @__PURE__ */ jsx4(Axis, { scale: y, orient: "left", def: yAxis, plot }),
              /* @__PURE__ */ jsx4(Axis, { scale: x, orient: "bottom", def: { gridLines: false, labelRotation: bars.length > 8 ? 35 : 0 }, plot }),
              bars.map((b, i) => {
                const color = b.isTotal ? totalColor : b.value >= 0 ? positiveColor : negativeColor;
                const yTop = y(Math.max(b.start, b.end));
                const yBot = y(Math.min(b.start, b.end));
                const h = Math.max(1, (yBot - yTop) * anim);
                const bx = x(b.label);
                const bw = x.bandwidth;
                return /* @__PURE__ */ jsxs4("g", { children: [
                  i < bars.length - 1 && !bars[i + 1].isTotal && /* @__PURE__ */ jsx4(
                    "line",
                    {
                      x1: bx + bw,
                      x2: x(bars[i + 1].label),
                      y1: y(b.end),
                      y2: y(b.end),
                      stroke: "var(--ec-axis, #9ca3af)",
                      strokeDasharray: "3 3",
                      opacity: anim
                    }
                  ),
                  /* @__PURE__ */ jsx4(
                    "rect",
                    {
                      x: bx,
                      y: yTop,
                      width: bw,
                      height: h,
                      fill: color,
                      rx: 2,
                      style: { cursor: onSeriesClick ? "pointer" : void 0 },
                      onPointerMove: (e) => {
                        const r = svgRef.current?.getBoundingClientRect();
                        setTip({
                          x: e.clientX - (r?.left ?? 0),
                          y: e.clientY - (r?.top ?? 0),
                          title: b.label,
                          rows: [
                            { name: b.isTotal ? "Total" : "Change", color, formatted: formatValue(b.value) },
                            ...b.isTotal ? [] : [{ name: "Running", color: SEMANTIC.neutral, formatted: formatValue(b.end) }]
                          ]
                        });
                      },
                      onClick: () => onSeriesClick?.({ series: b.label, datum: b.datum, index: i })
                    }
                  ),
                  /* @__PURE__ */ jsx4(
                    "text",
                    {
                      x: bx + bw / 2,
                      y: yTop - 5,
                      textAnchor: "middle",
                      fontSize: 10,
                      opacity: anim,
                      fill: "var(--ec-text-muted, #6b7280)",
                      children: b.isTotal ? compact(b.value) : `${b.value >= 0 ? "+" : ""}${compact(b.value)}`
                    }
                  )
                ] }, b.label);
              })
            ]
          }
        ),
        /* @__PURE__ */ jsx4(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
function HeatmapInner(props, apiRef) {
  const {
    data,
    xKey,
    yKey,
    valueKey,
    height = 320,
    colorRange = RAMP,
    showValues = false,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    onSeriesClick,
    padding,
    animate = true
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef4(null);
  const [tip, setTip] = useState4(null);
  useChartApi(svgRef, apiRef);
  const xs = useMemo3(() => [...new Set(data.map((d) => String(d[xKey])))], [data, xKey]);
  const ys = useMemo3(() => [...new Set(data.map((d) => String(d[yKey])))], [data, yKey]);
  const [lo, hi] = extent(data.map((d) => Number(d[valueKey])));
  const pad = { top: 12, right: 16, bottom: 46, left: 110, ...padding };
  const plot = { x: pad.left, y: pad.top, w: Math.max(0, width - pad.left - pad.right), h: Math.max(0, height - pad.top - pad.bottom) };
  const cw = plot.w / Math.max(1, xs.length);
  const ch = plot.h / Math.max(1, ys.length);
  const anim = useAnimation(data.length, animate);
  return /* @__PURE__ */ jsx4(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !data.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs4("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsxs4(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Heatmap, ${xs.length} by ${ys.length} cells.`,
            onPointerLeave: () => setTip(null),
            children: [
              ys.map((yv, r) => /* @__PURE__ */ jsx4(
                "text",
                {
                  x: plot.x - 8,
                  y: plot.y + r * ch + ch / 2 + 4,
                  textAnchor: "end",
                  fontSize: 11,
                  fill: "var(--ec-text-muted, #6b7280)",
                  children: yv
                },
                yv
              )),
              xs.map((xv, c) => /* @__PURE__ */ jsx4(
                "text",
                {
                  x: plot.x + c * cw + cw / 2,
                  y: plot.y + plot.h + 16,
                  textAnchor: "middle",
                  fontSize: 11,
                  fill: "var(--ec-text-muted, #6b7280)",
                  children: xv
                },
                xv
              )),
              data.map((d, i) => {
                const c = xs.indexOf(String(d[xKey]));
                const r = ys.indexOf(String(d[yKey]));
                if (c < 0 || r < 0) return null;
                const v = Number(d[valueKey]);
                const t = hi === lo ? 0.5 : (v - lo) / (hi - lo);
                const fill = mix(colorRange[0], colorRange[1], t);
                return /* @__PURE__ */ jsxs4("g", { children: [
                  /* @__PURE__ */ jsx4(
                    "rect",
                    {
                      x: plot.x + c * cw + 1,
                      y: plot.y + r * ch + 1,
                      width: Math.max(0, cw - 2),
                      height: Math.max(0, ch - 2),
                      rx: 2,
                      fill,
                      opacity: anim,
                      style: { cursor: onSeriesClick ? "pointer" : void 0 },
                      onPointerMove: (e) => {
                        const rect = svgRef.current?.getBoundingClientRect();
                        setTip({
                          x: e.clientX - (rect?.left ?? 0),
                          y: e.clientY - (rect?.top ?? 0),
                          title: `${d[yKey]} \xB7 ${d[xKey]}`,
                          rows: [{ name: valueKey, color: fill, formatted: formatValue(v) }]
                        });
                      },
                      onClick: () => onSeriesClick?.({ series: String(d[yKey]), datum: d, index: i })
                    }
                  ),
                  showValues && cw > 34 && ch > 18 && /* @__PURE__ */ jsx4(
                    "text",
                    {
                      x: plot.x + c * cw + cw / 2,
                      y: plot.y + r * ch + ch / 2 + 4,
                      textAnchor: "middle",
                      fontSize: 10,
                      fill: contrastText(fill),
                      pointerEvents: "none",
                      children: compact(v)
                    }
                  )
                ] }, i);
              })
            ]
          }
        ),
        /* @__PURE__ */ jsx4(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
function squarify(items, x, y, w, h, depth = 0) {
  const out = [];
  const total = items.reduce((s, i) => s + i.value, 0);
  if (!total || w <= 0 || h <= 0) return out;
  let rest = items.slice().sort((a, b) => b.value - a.value);
  let cx = x, cy = y, cw = w, ch = h;
  let remaining = total;
  while (rest.length) {
    const horizontal = cw >= ch;
    const side = horizontal ? ch : cw;
    const row = [];
    let rowSum = 0;
    let bestRatio = Infinity;
    while (rest.length) {
      const next = rest[0];
      const trySum = rowSum + next.value;
      const thickness2 = trySum / remaining * (horizontal ? cw : ch);
      const worst = Math.max(
        ...[...row, next].map((it) => {
          const len = it.value / trySum * side;
          return Math.max(thickness2 / len, len / thickness2);
        })
      );
      if (worst > bestRatio) break;
      bestRatio = worst;
      rowSum = trySum;
      row.push(rest.shift());
    }
    const thickness = rowSum / remaining * (horizontal ? cw : ch);
    let offset = 0;
    for (const it of row) {
      const len = it.value / rowSum * side;
      out.push(horizontal ? { x: cx, y: cy + offset, w: thickness, h: len, label: it.label, value: it.value, datum: it.datum, depth } : { x: cx + offset, y: cy, w: len, h: thickness, label: it.label, value: it.value, datum: it.datum, depth });
      offset += len;
    }
    if (horizontal) {
      cx += thickness;
      cw -= thickness;
    } else {
      cy += thickness;
      ch -= thickness;
    }
    remaining -= rowSum;
  }
  return out;
}
function TreemapInner(props, apiRef) {
  const {
    data,
    labelKey,
    valueKey,
    groupKey,
    height = 320,
    palette,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    onSeriesClick,
    animate = true
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef4(null);
  const [tip, setTip] = useState4(null);
  useChartApi(svgRef, apiRef);
  const anim = useAnimation(data.length, animate);
  const tiles = useMemo3(() => {
    const items = data.map((d) => ({ label: String(d[labelKey]), value: Number(d[valueKey]) || 0, datum: d, group: groupKey ? String(d[groupKey]) : null })).filter((i) => i.value > 0);
    if (!width || !height) return [];
    if (!groupKey) return squarify(items, 0, 0, width, height);
    const groups = /* @__PURE__ */ new Map();
    for (const it of items) {
      const g = it.group ?? "\u2014";
      groups.set(g, [...groups.get(g) ?? [], it]);
    }
    const outer = squarify(
      [...groups.entries()].map(([g, list]) => ({ label: g, value: list.reduce((s, i) => s + i.value, 0), datum: { __group: g } })),
      0,
      0,
      width,
      height
    );
    return outer.flatMap((o) => [
      o,
      ...squarify(groups.get(o.label) ?? [], o.x + 2, o.y + 16, Math.max(0, o.w - 4), Math.max(0, o.h - 18), 1)
    ]);
  }, [data, labelKey, valueKey, groupKey, width, height]);
  const total = tiles.filter((t) => t.depth === (groupKey ? 1 : 0)).reduce((s, t) => s + t.value, 0);
  return /* @__PURE__ */ jsx4(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !data.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs4("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsx4(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Treemap of ${data.length} items.`,
            onPointerLeave: () => setTip(null),
            children: tiles.map((t, i) => {
              const isGroup = groupKey && t.depth === 0;
              const color = isGroup ? "#e5e7eb" : colorAt(palette, i);
              return /* @__PURE__ */ jsxs4("g", { children: [
                /* @__PURE__ */ jsx4(
                  "rect",
                  {
                    x: t.x,
                    y: t.y,
                    width: Math.max(0, t.w - 2),
                    height: Math.max(0, t.h - 2),
                    rx: 3,
                    fill: color,
                    opacity: isGroup ? 0.55 : anim,
                    stroke: "var(--ec-surface, #ffffff)",
                    strokeWidth: 1,
                    style: { cursor: onSeriesClick && !isGroup ? "pointer" : void 0 },
                    onPointerMove: (e) => {
                      if (isGroup) return;
                      const r = svgRef.current?.getBoundingClientRect();
                      setTip({
                        x: e.clientX - (r?.left ?? 0),
                        y: e.clientY - (r?.top ?? 0),
                        title: t.label,
                        rows: [{ name: valueKey, color, formatted: `${formatValue(t.value)}  (${total ? (t.value / total * 100).toFixed(1) : "0"}%)` }]
                      });
                    },
                    onClick: () => !isGroup && onSeriesClick?.({ series: t.label, datum: t.datum, index: i })
                  }
                ),
                t.w > 52 && t.h > 22 && /* @__PURE__ */ jsx4(
                  "text",
                  {
                    x: t.x + 6,
                    y: t.y + (isGroup ? 12 : 15),
                    fontSize: isGroup ? 10 : 11,
                    fontWeight: isGroup ? 700 : 500,
                    fill: isGroup ? "var(--ec-text-muted, #6b7280)" : contrastText(color),
                    pointerEvents: "none",
                    children: t.label.length > Math.floor(t.w / 7) ? t.label.slice(0, Math.floor(t.w / 7)) + "\u2026" : t.label
                  }
                ),
                !isGroup && t.w > 52 && t.h > 34 && /* @__PURE__ */ jsx4(
                  "text",
                  {
                    x: t.x + 6,
                    y: t.y + 29,
                    fontSize: 10,
                    fill: contrastText(color),
                    opacity: 0.85,
                    pointerEvents: "none",
                    children: compact(t.value)
                  }
                )
              ] }, `${t.label}-${i}`);
            })
          }
        ),
        /* @__PURE__ */ jsx4(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
function GaugeInner(props, apiRef) {
  const {
    value,
    min = 0,
    max = 100,
    bands,
    label,
    format,
    target,
    height = 220,
    title,
    subtitle,
    className = "",
    animate = true,
    ariaLabel,
    palette
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef4(null);
  useChartApi(svgRef, apiRef);
  const anim = useAnimation(value, animate);
  const SWEEP = 240 * Math.PI / 180;
  const START = -SWEEP / 2;
  const clamped = Math.max(min, Math.min(max, value));
  const frac = max === min ? 0 : (clamped - min) / (max - min);
  const cx = (width || 0) / 2;
  const cy = height * 0.72;
  const r = Math.min(width || 0, height * 1.35) / 2 - 16;
  const rInner = r * 0.68;
  const angleFor = (t) => START + t * SWEEP;
  const arcs = bands?.length ? bands.map((b, i) => {
    const from = i === 0 ? min : bands[i - 1].upTo;
    return { a0: angleFor((from - min) / (max - min)), a1: angleFor((b.upTo - min) / (max - min)), color: b.color };
  }) : [{ a0: angleFor(0), a1: angleFor(1), color: "#e5e7eb" }];
  return /* @__PURE__ */ jsx4(ChartFrame, { title, subtitle, className, height, empty: false, children: /* @__PURE__ */ jsx4("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: width > 0 && /* @__PURE__ */ jsxs4(
    "svg",
    {
      ref: svgRef,
      width,
      height,
      role: "img",
      "aria-label": ariaLabel ?? `${label ?? title ?? "Gauge"}: ${value} of ${max}.`,
      children: [
        arcs.map((a, i) => /* @__PURE__ */ jsx4("path", { d: arcPath(cx, cy, r, rInner, a.a0, a.a1), fill: a.color, opacity: 0.35 }, i)),
        /* @__PURE__ */ jsx4(
          "path",
          {
            d: arcPath(cx, cy, r, rInner, angleFor(0), angleFor(frac * anim)),
            fill: bands?.length ? bands.find((b) => clamped <= b.upTo)?.color ?? colorAt(palette, 0) : colorAt(palette, 0)
          }
        ),
        target != null && /* @__PURE__ */ jsx4(
          "line",
          {
            x1: cx + rInner * Math.sin(angleFor((target - min) / (max - min))),
            y1: cy - rInner * Math.cos(angleFor((target - min) / (max - min))),
            x2: cx + r * Math.sin(angleFor((target - min) / (max - min))),
            y2: cy - r * Math.cos(angleFor((target - min) / (max - min))),
            stroke: "var(--ec-text, #111827)",
            strokeWidth: 2
          }
        ),
        /* @__PURE__ */ jsx4(
          "text",
          {
            x: cx,
            y: cy - 4,
            textAnchor: "middle",
            fontSize: 26,
            fontWeight: 700,
            fill: "var(--ec-text, #111827)",
            children: format ? format(value) : compact(value)
          }
        ),
        label && /* @__PURE__ */ jsx4(
          "text",
          {
            x: cx,
            y: cy + 16,
            textAnchor: "middle",
            fontSize: 11,
            fill: "var(--ec-text-muted, #6b7280)",
            children: label
          }
        ),
        /* @__PURE__ */ jsx4("text", { x: cx - r + 6, y: cy + 18, fontSize: 10, fill: "var(--ec-text-muted, #9ca3af)", children: compact(min) }),
        /* @__PURE__ */ jsx4("text", { x: cx + r - 6, y: cy + 18, textAnchor: "end", fontSize: 10, fill: "var(--ec-text-muted, #9ca3af)", children: compact(max) })
      ]
    }
  ) }) });
}
var WaterfallChart = React4.forwardRef(WaterfallInner);
var HeatmapChart = React4.forwardRef(HeatmapInner);
var TreemapChart = React4.forwardRef(TreemapInner);
var GaugeChart = React4.forwardRef(GaugeInner);

// src/StatCharts.tsx
import React6, { useImperativeHandle as useImperativeHandle4, useMemo as useMemo5, useRef as useRef6, useState as useState6 } from "react";

// src/stats.ts
function quantileSorted(sorted, p) {
  const n = sorted.length;
  if (!n) return NaN;
  if (n === 1) return sorted[0];
  const h = (n - 1) * Math.max(0, Math.min(1, p));
  const lo = Math.floor(h);
  const hi = Math.ceil(h);
  return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
}
function boxStats(values) {
  const clean = values.filter((v) => Number.isFinite(v)).sort((a, b) => a - b);
  if (!clean.length) return null;
  const q1 = quantileSorted(clean, 0.25);
  const median = quantileSorted(clean, 0.5);
  const q3 = quantileSorted(clean, 0.75);
  const iqr = q3 - q1;
  const loFence = q1 - 1.5 * iqr;
  const hiFence = q3 + 1.5 * iqr;
  const inside = clean.filter((v) => v >= loFence && v <= hiFence);
  const outliers = clean.filter((v) => v < loFence || v > hiFence);
  return {
    min: clean[0],
    q1,
    median,
    q3,
    max: clean[clean.length - 1],
    lowerWhisker: inside.length ? inside[0] : clean[0],
    upperWhisker: inside.length ? inside[inside.length - 1] : clean[clean.length - 1],
    outliers,
    mean: clean.reduce((s, v) => s + v, 0) / clean.length,
    count: clean.length
  };
}
function binWidth(sorted, method = "freedman-diaconis") {
  const n = sorted.length;
  if (n < 2) return 1;
  const lo = sorted[0];
  const hi = sorted[n - 1];
  const span = hi - lo || 1;
  if (method === "sturges") return span / (Math.ceil(Math.log2(n)) + 1);
  if (method === "scott") {
    const mean = sorted.reduce((s, v) => s + v, 0) / n;
    const sd = Math.sqrt(sorted.reduce((s, v) => s + (v - mean) ** 2, 0) / (n - 1)) || 1;
    return 3.49 * sd / Math.cbrt(n);
  }
  const iqr = quantileSorted(sorted, 0.75) - quantileSorted(sorted, 0.25);
  if (iqr <= 0) return span / (Math.ceil(Math.log2(n)) + 1);
  return 2 * iqr / Math.cbrt(n);
}
function histogram(values, opts = {}) {
  const clean = values.filter((v) => Number.isFinite(v)).sort((a, b) => a - b);
  if (!clean.length) return [];
  const lo = opts.min ?? clean[0];
  const hi = opts.max ?? clean[clean.length - 1];
  const span = hi - lo || 1;
  const count = opts.bins ?? Math.max(1, Math.min(80, Math.ceil(span / binWidth(clean, opts.method))));
  const step = span / count;
  const bins = Array.from({ length: count }, (_, i) => ({
    x0: lo + i * step,
    x1: lo + (i + 1) * step,
    count: 0,
    values: []
  }));
  for (const v of clean) {
    if (v < lo || v > hi) continue;
    const idx = Math.min(count - 1, Math.floor((v - lo) / step));
    bins[idx].count++;
    bins[idx].values.push(v);
  }
  return bins;
}
function linearRegression(points) {
  const pts = points.filter((p) => Number.isFinite(p.x) && Number.isFinite(p.y));
  const n = pts.length;
  if (n < 2) return null;
  const sx = pts.reduce((s, p) => s + p.x, 0);
  const sy = pts.reduce((s, p) => s + p.y, 0);
  const sxy = pts.reduce((s, p) => s + p.x * p.y, 0);
  const sxx = pts.reduce((s, p) => s + p.x * p.x, 0);
  const denom = n * sxx - sx * sx;
  if (!denom) return null;
  const slope = (n * sxy - sx * sy) / denom;
  const intercept = (sy - slope * sx) / n;
  const meanY = sy / n;
  const ssTot = pts.reduce((s, p) => s + (p.y - meanY) ** 2, 0);
  const ssRes = pts.reduce((s, p) => s + (p.y - (slope * p.x + intercept)) ** 2, 0);
  return { slope, intercept, r2: ssTot ? 1 - ssRes / ssTot : 1 };
}

// src/sync.tsx
import { createContext, useCallback as useCallback2, useContext, useMemo as useMemo4, useRef as useRef5, useState as useState5 } from "react";
import { jsx as jsx5 } from "react/jsx-runtime";
var SyncCtx = createContext(null);
function ChartSyncProvider({ children }) {
  const [hover, setHoverState] = useState5({});
  const [brush, setBrushState] = useState5({});
  const setHover = useCallback2((group, key) => {
    setHoverState((prev) => prev[group] === key ? prev : { ...prev, [group]: key });
  }, []);
  const setBrush = useCallback2((group, range) => {
    setBrushState((prev) => ({ ...prev, [group]: range }));
  }, []);
  const value = useMemo4(() => ({ hover, brush, setHover, setBrush }), [hover, brush, setHover, setBrush]);
  return /* @__PURE__ */ jsx5(SyncCtx.Provider, { value, children });
}
function useBrushPublish(group) {
  const ctx = useContext(SyncCtx);
  const idRef = useRef5(`c${Math.random().toString(36).slice(2, 9)}`);
  const publishHover = useCallback2((key) => {
    if (group && ctx) ctx.setHover(group, key);
  }, [group, ctx]);
  const publishBrush = useCallback2((from, to) => {
    if (group && ctx) ctx.setBrush(group, from == null ? null : { from, to, origin: idRef.current });
  }, [group, ctx]);
  return {
    chartId: idRef.current,
    hoverKey: group && ctx ? ctx.hover[group] ?? null : null,
    brush: group && ctx ? ctx.brush[group] ?? null : null,
    publishHover,
    publishBrush,
    /** True when the active brush came from another chart in the group. */
    brushIsForeign: group && ctx ? (ctx.brush[group]?.origin ?? idRef.current) !== idRef.current : false
  };
}
function useChartSync(group) {
  const ctx = useContext(SyncCtx);
  return {
    hover: ctx?.hover[group] ?? null,
    brush: ctx?.brush[group] ?? null,
    clearBrush: () => ctx?.setBrush(group, null)
  };
}

// src/StatCharts.tsx
import { Fragment as Fragment2, jsx as jsx6, jsxs as jsxs5 } from "react/jsx-runtime";
function useApi(svgRef, apiRef) {
  useImperativeHandle4(apiRef, () => ({
    toSVG: () => svgToString(svgRef.current),
    toPNG: (s = 2) => svgToPNG(svgRef.current, s),
    download: (f = "chart", fmt = "png") => downloadChart(svgRef.current, f, fmt),
    resetZoom: () => {
    },
    getHiddenSeries: () => [],
    setHiddenSeries: () => {
    }
  }), []);
}
function CandlestickInner(props, apiRef) {
  const {
    data,
    xKey,
    openKey,
    highKey,
    lowKey,
    closeKey,
    style = "candle",
    volumeKey,
    upColor = SEMANTIC.positive,
    downColor = SEMANTIC.negative,
    height = 340,
    yAxis = {},
    padding,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true,
    onSeriesClick,
    syncGroup
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef6(null);
  const [tip, setTip] = useState6(null);
  useApi(svgRef, apiRef);
  const { publishHover, hoverKey } = useBrushPublish(syncGroup);
  const pad = { top: 12, right: 16, bottom: 40, left: 64, ...padding };
  const volH = volumeKey ? Math.round((height - pad.top - pad.bottom) * 0.22) : 0;
  const plot = {
    x: pad.left,
    y: pad.top,
    w: Math.max(0, width - pad.left - pad.right),
    h: Math.max(0, height - pad.top - pad.bottom - volH)
  };
  const [lo, hi] = extent(data.flatMap((d) => [Number(d[lowKey]), Number(d[highKey])]));
  const [nlo, nhi] = niceDomain(lo, hi, yAxis.tickCount ?? 6);
  const y = linearScale([nlo, nhi], [plot.y + plot.h, plot.y]);
  const x = bandScale(data.map((d) => String(d[xKey])), [plot.x, plot.x + plot.w], { padding: 0.3 });
  const volMax = volumeKey ? extent(data.map((d) => Number(d[volumeKey])))[1] : 0;
  const anim = useAnimation(data.length, animate);
  const bw = Math.max(1, x.bandwidth);
  return /* @__PURE__ */ jsx6(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !data.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs5("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsxs5(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Price chart, ${data.length} periods.`,
            onPointerLeave: () => {
              setTip(null);
              publishHover(null);
            },
            children: [
              /* @__PURE__ */ jsx6(Axis, { scale: y, orient: "left", def: yAxis, plot }),
              /* @__PURE__ */ jsx6(
                Axis,
                {
                  scale: x,
                  orient: "bottom",
                  def: { gridLines: false, labelRotation: data.length > 12 ? 35 : 0 },
                  plot: { ...plot, h: plot.h + volH }
                }
              ),
              data.map((d, i) => {
                const o = Number(d[openKey]);
                const c = Number(d[closeKey]);
                const hiV = Number(d[highKey]);
                const loV = Number(d[lowKey]);
                if (![o, c, hiV, loV].every(Number.isFinite)) return null;
                const up = c >= o;
                const color = up ? upColor : downColor;
                const cx = x(String(d[xKey])) + bw / 2;
                const bodyTop = y(Math.max(o, c));
                const bodyH = Math.max(1, Math.abs(y(o) - y(c)));
                const dim = hoverKey != null && hoverKey !== String(d[xKey]);
                return /* @__PURE__ */ jsxs5(
                  "g",
                  {
                    opacity: (dim ? 0.4 : 1) * anim,
                    onPointerMove: (e) => {
                      const r = svgRef.current?.getBoundingClientRect();
                      publishHover(String(d[xKey]));
                      setTip({
                        x: e.clientX - (r?.left ?? 0),
                        y: e.clientY - (r?.top ?? 0),
                        title: String(d[xKey]),
                        rows: [
                          { name: "Open", color, formatted: formatValue(o) },
                          { name: "High", color, formatted: formatValue(hiV) },
                          { name: "Low", color, formatted: formatValue(loV) },
                          { name: "Close", color, formatted: formatValue(c) },
                          ...volumeKey ? [{ name: "Volume", color: SEMANTIC.neutral, formatted: compact(Number(d[volumeKey])) }] : []
                        ]
                      });
                    },
                    onClick: () => onSeriesClick?.({ series: "price", datum: d, index: i }),
                    style: { cursor: onSeriesClick ? "pointer" : void 0 },
                    children: [
                      /* @__PURE__ */ jsx6("line", { x1: cx, x2: cx, y1: y(hiV), y2: y(loV), stroke: color, strokeWidth: 1.5 }),
                      style === "candle" ? /* @__PURE__ */ jsx6(
                        "rect",
                        {
                          x: cx - bw / 2,
                          y: bodyTop,
                          width: bw,
                          height: bodyH,
                          fill: up ? color : color,
                          fillOpacity: up ? 0.25 : 1,
                          stroke: color,
                          strokeWidth: 1.5
                        }
                      ) : /* @__PURE__ */ jsxs5(Fragment2, { children: [
                        /* @__PURE__ */ jsx6("line", { x1: cx - bw / 2, x2: cx, y1: y(o), y2: y(o), stroke: color, strokeWidth: 1.5 }),
                        /* @__PURE__ */ jsx6("line", { x1: cx, x2: cx + bw / 2, y1: y(c), y2: y(c), stroke: color, strokeWidth: 1.5 })
                      ] }),
                      volumeKey && volMax > 0 && /* @__PURE__ */ jsx6(
                        "rect",
                        {
                          x: cx - bw / 2,
                          y: height - pad.bottom - Number(d[volumeKey]) / volMax * volH * anim,
                          width: bw,
                          height: Number(d[volumeKey]) / volMax * volH * anim,
                          fill: color,
                          opacity: 0.35
                        }
                      )
                    ]
                  },
                  i
                );
              })
            ]
          }
        ),
        /* @__PURE__ */ jsx6(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
function BoxPlotInner(props, apiRef) {
  const {
    data,
    groupKey,
    valueKey,
    height = 320,
    yAxis = {},
    palette,
    padding,
    showPoints = false,
    showMean = true,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef6(null);
  const [tip, setTip] = useState6(null);
  useApi(svgRef, apiRef);
  const boxes = useMemo5(() => {
    const groups = /* @__PURE__ */ new Map();
    for (const d of data) {
      const g = String(d[groupKey]);
      const v = Number(d[valueKey]);
      if (!Number.isFinite(v)) continue;
      groups.set(g, [...groups.get(g) ?? [], v]);
    }
    return [...groups.entries()].map(([name, values]) => ({ name, stats: boxStats(values), values })).filter((b) => b.stats);
  }, [data, groupKey, valueKey]);
  const pad = { top: 12, right: 16, bottom: 44, left: 64, ...padding };
  const plot = { x: pad.left, y: pad.top, w: Math.max(0, width - pad.left - pad.right), h: Math.max(0, height - pad.top - pad.bottom) };
  const all = boxes.flatMap((b) => [b.stats.lowerWhisker, b.stats.upperWhisker, ...b.stats.outliers]);
  const [lo, hi] = extent(all);
  const [nlo, nhi] = niceDomain(lo, hi, yAxis.tickCount ?? 6);
  const y = linearScale([nlo, nhi], [plot.y + plot.h, plot.y]);
  const x = bandScale(boxes.map((b) => b.name), [plot.x, plot.x + plot.w], { padding: 0.45 });
  const anim = useAnimation(boxes.length, animate);
  const bw = Math.max(4, x.bandwidth);
  return /* @__PURE__ */ jsx6(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !boxes.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs5("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsxs5(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Box plot of ${boxes.length} groups.`,
            onPointerLeave: () => setTip(null),
            children: [
              /* @__PURE__ */ jsx6(Axis, { scale: y, orient: "left", def: yAxis, plot }),
              /* @__PURE__ */ jsx6(Axis, { scale: x, orient: "bottom", def: { gridLines: false }, plot }),
              boxes.map((b, i) => {
                const color = colorAt(palette, i);
                const cx = x(b.name) + bw / 2;
                const s = b.stats;
                const yQ3 = y(s.q3);
                const yQ1 = y(s.q1);
                return /* @__PURE__ */ jsxs5(
                  "g",
                  {
                    opacity: anim,
                    onPointerMove: (e) => {
                      const r = svgRef.current?.getBoundingClientRect();
                      setTip({
                        x: e.clientX - (r?.left ?? 0),
                        y: e.clientY - (r?.top ?? 0),
                        title: `${b.name}  (n=${s.count})`,
                        rows: [
                          { name: "Max (whisker)", color, formatted: formatValue(s.upperWhisker) },
                          { name: "Q3", color, formatted: formatValue(s.q3) },
                          { name: "Median", color, formatted: formatValue(s.median) },
                          { name: "Q1", color, formatted: formatValue(s.q1) },
                          { name: "Min (whisker)", color, formatted: formatValue(s.lowerWhisker) },
                          ...s.outliers.length ? [{ name: "Outliers", color: SEMANTIC.negative, formatted: String(s.outliers.length) }] : []
                        ]
                      });
                    },
                    onClick: () => onSeriesClick?.({ series: b.name, datum: { group: b.name, ...s }, index: i }),
                    style: { cursor: onSeriesClick ? "pointer" : void 0 },
                    children: [
                      /* @__PURE__ */ jsx6("line", { x1: cx, x2: cx, y1: y(s.upperWhisker), y2: yQ3, stroke: color, strokeWidth: 1.5 }),
                      /* @__PURE__ */ jsx6("line", { x1: cx, x2: cx, y1: yQ1, y2: y(s.lowerWhisker), stroke: color, strokeWidth: 1.5 }),
                      /* @__PURE__ */ jsx6("line", { x1: cx - bw / 4, x2: cx + bw / 4, y1: y(s.upperWhisker), y2: y(s.upperWhisker), stroke: color, strokeWidth: 1.5 }),
                      /* @__PURE__ */ jsx6("line", { x1: cx - bw / 4, x2: cx + bw / 4, y1: y(s.lowerWhisker), y2: y(s.lowerWhisker), stroke: color, strokeWidth: 1.5 }),
                      /* @__PURE__ */ jsx6(
                        "rect",
                        {
                          x: cx - bw / 2,
                          y: yQ3,
                          width: bw,
                          height: Math.max(1, yQ1 - yQ3),
                          rx: 2,
                          fill: color,
                          fillOpacity: 0.25,
                          stroke: color,
                          strokeWidth: 1.5
                        }
                      ),
                      /* @__PURE__ */ jsx6("line", { x1: cx - bw / 2, x2: cx + bw / 2, y1: y(s.median), y2: y(s.median), stroke: color, strokeWidth: 2.5 }),
                      showMean && /* @__PURE__ */ jsx6(
                        "path",
                        {
                          d: `M${cx},${y(s.mean) - 4} L${cx + 4},${y(s.mean)} L${cx},${y(s.mean) + 4} L${cx - 4},${y(s.mean)} Z`,
                          fill: "var(--ec-surface, #fff)",
                          stroke: color,
                          strokeWidth: 1.5
                        }
                      ),
                      s.outliers.map((o, k) => /* @__PURE__ */ jsx6("circle", { cx, cy: y(o), r: 2.5, fill: "none", stroke: SEMANTIC.negative, strokeWidth: 1.2 }, k)),
                      showPoints && b.values.map((v, k) => /* @__PURE__ */ jsx6("circle", { cx: cx + bw * 0.7, cy: y(v), r: 1.8, fill: color, opacity: 0.4 }, `p${k}`))
                    ]
                  },
                  b.name
                );
              })
            ]
          }
        ),
        /* @__PURE__ */ jsx6(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
function HistogramInner(props, apiRef) {
  const {
    data,
    valueKey,
    bins,
    method,
    color,
    cumulative = false,
    height = 300,
    xAxis = {},
    yAxis = {},
    padding,
    palette,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef6(null);
  const [tip, setTip] = useState6(null);
  useApi(svgRef, apiRef);
  const values = useMemo5(() => data.map((d) => Number(d[valueKey])), [data, valueKey]);
  const hist = useMemo5(() => histogram(values, { bins, method }), [values, bins, method]);
  const pad = { top: 12, right: cumulative ? 52 : 16, bottom: 44, left: 60, ...padding };
  const plot = { x: pad.left, y: pad.top, w: Math.max(0, width - pad.left - pad.right), h: Math.max(0, height - pad.top - pad.bottom) };
  const maxCount = Math.max(...hist.map((b) => b.count), 1);
  const [nlo, nhi] = niceDomain(0, maxCount, yAxis.tickCount ?? 5);
  const y = linearScale([nlo, nhi], [plot.y + plot.h, plot.y]);
  const x = linearScale([hist[0]?.x0 ?? 0, hist[hist.length - 1]?.x1 ?? 1], [plot.x, plot.x + plot.w]);
  const yCum = linearScale([0, 100], [plot.y + plot.h, plot.y]);
  const anim = useAnimation(hist.length, animate);
  const fill = color ?? colorAt(palette, 0);
  const total = hist.reduce((s, b) => s + b.count, 0);
  let running = 0;
  const cumPts = hist.map((b) => {
    running += b.count;
    return { x: x(b.x1), y: yCum(total ? running / total * 100 : 0) };
  });
  return /* @__PURE__ */ jsx6(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !hist.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs5("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsxs5(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Histogram, ${hist.length} bins over ${total} values.`,
            onPointerLeave: () => setTip(null),
            children: [
              /* @__PURE__ */ jsx6(Axis, { scale: y, orient: "left", def: yAxis, plot }),
              /* @__PURE__ */ jsx6(Axis, { scale: x, orient: "bottom", def: { ...xAxis, gridLines: false }, plot }),
              cumulative && /* @__PURE__ */ jsx6(Axis, { scale: yCum, orient: "right", def: { gridLines: false, format: (v) => `${v}%` }, plot }),
              hist.map((b, i) => {
                const bx = x(b.x0);
                const bwid = Math.max(1, x(b.x1) - x(b.x0) - 1);
                const bh = (plot.y + plot.h - y(b.count)) * anim;
                return /* @__PURE__ */ jsx6(
                  "rect",
                  {
                    x: bx,
                    y: plot.y + plot.h - bh,
                    width: bwid,
                    height: bh,
                    rx: 1.5,
                    fill,
                    fillOpacity: 0.85,
                    onPointerMove: (e) => {
                      const r = svgRef.current?.getBoundingClientRect();
                      setTip({
                        x: e.clientX - (r?.left ?? 0),
                        y: e.clientY - (r?.top ?? 0),
                        title: `${compact(b.x0)} \u2013 ${compact(b.x1)}`,
                        rows: [
                          { name: "Count", color: fill, formatted: String(b.count) },
                          { name: "Share", color: fill, formatted: `${total ? (b.count / total * 100).toFixed(1) : 0}%` }
                        ]
                      });
                    },
                    onClick: () => onSeriesClick?.({ series: "bin", datum: b, index: i }),
                    style: { cursor: onSeriesClick ? "pointer" : void 0 }
                  },
                  i
                );
              }),
              cumulative && cumPts.length > 1 && /* @__PURE__ */ jsx6(
                "path",
                {
                  d: cumPts.map((p, i) => `${i ? "L" : "M"}${p.x},${p.y}`).join(""),
                  fill: "none",
                  stroke: SEMANTIC.neutral,
                  strokeWidth: 2,
                  opacity: anim
                }
              )
            ]
          }
        ),
        /* @__PURE__ */ jsx6(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
var CandlestickChart = React6.forwardRef(CandlestickInner);
var BoxPlotChart = React6.forwardRef(BoxPlotInner);
var HistogramChart = React6.forwardRef(HistogramInner);

// src/ShapeCharts.tsx
import React7, { useImperativeHandle as useImperativeHandle5, useMemo as useMemo6, useRef as useRef7, useState as useState7 } from "react";

// src/layouts.ts
function sankeyLayout(linksIn, width, height, opts = {}) {
  var _a;
  const nodeWidth = opts.nodeWidth ?? 14;
  const nodePadding = opts.nodePadding ?? 10;
  const iterations = opts.iterations ?? 24;
  const valid = linksIn.filter((l) => l.source !== l.target && Number(l.value) > 0);
  if (!valid.length || width <= 0 || height <= 0) return { nodes: [], links: [] };
  const ids = [...new Set(valid.flatMap((l) => [l.source, l.target]))];
  const nodes = new Map(
    ids.map((id, index) => [id, { id, depth: 0, x0: 0, x1: 0, y0: 0, y1: 0, value: 0, index }])
  );
  const outgoing = /* @__PURE__ */ new Map();
  const incoming = /* @__PURE__ */ new Map();
  for (const l of valid) {
    outgoing.set(l.source, [...outgoing.get(l.source) ?? [], l]);
    incoming.set(l.target, [...incoming.get(l.target) ?? [], l]);
  }
  const depthOf = new Map(ids.map((id) => [id, 0]));
  for (let pass = 0; pass < ids.length; pass++) {
    let changed = false;
    for (const l of valid) {
      const d = (depthOf.get(l.source) ?? 0) + 1;
      if (d > (depthOf.get(l.target) ?? 0)) {
        depthOf.set(l.target, d);
        changed = true;
      }
    }
    if (!changed) break;
  }
  const maxDepth = Math.max(...depthOf.values(), 0);
  for (const id of ids) {
    const n = nodes.get(id);
    n.depth = (outgoing.get(id)?.length ?? 0) === 0 ? maxDepth : depthOf.get(id);
    n.value = Math.max(
      (outgoing.get(id) ?? []).reduce((s, l) => s + l.value, 0),
      (incoming.get(id) ?? []).reduce((s, l) => s + l.value, 0)
    );
  }
  const layers = [];
  for (const n of nodes.values()) (layers[_a = n.depth] ?? (layers[_a] = [])).push(n);
  const layerStep = layers.length > 1 ? (width - nodeWidth) / (layers.length - 1) : 0;
  const scale = Math.min(
    ...layers.map((layer) => {
      const total = layer.reduce((s, n) => s + n.value, 0);
      const free = height - (layer.length - 1) * nodePadding;
      return total > 0 ? free / total : Infinity;
    })
  );
  layers.forEach((layer, d) => {
    let y = 0;
    layer.sort((a, b) => b.value - a.value);
    for (const n of layer) {
      n.x0 = d * layerStep;
      n.x1 = n.x0 + nodeWidth;
      n.y0 = y;
      n.y1 = y + Math.max(1, n.value * scale);
      y = n.y1 + nodePadding;
    }
  });
  const centre = (n) => (n.y0 + n.y1) / 2;
  for (let it = 0; it < iterations; it++) {
    const alpha = 0.9 * (1 - it / iterations);
    for (const layer of layers) {
      for (const n of layer) {
        const rel = [...incoming.get(n.id) ?? [], ...outgoing.get(n.id) ?? []];
        if (!rel.length) continue;
        const totalV = rel.reduce((s, l) => s + l.value, 0);
        const target = rel.reduce((s, l) => {
          const other = nodes.get(l.source === n.id ? l.target : l.source);
          return s + centre(other) * l.value;
        }, 0) / (totalV || 1);
        const dy = (target - centre(n)) * alpha;
        n.y0 += dy;
        n.y1 += dy;
      }
      layer.sort((a, b) => a.y0 - b.y0);
      let y = 0;
      for (const n of layer) {
        const push = y - n.y0;
        if (push > 0) {
          n.y0 += push;
          n.y1 += push;
        }
        y = n.y1 + nodePadding;
      }
      const overflow = y - nodePadding - height;
      if (overflow > 0) {
        let yy = height;
        for (let i = layer.length - 1; i >= 0; i--) {
          const n = layer[i];
          const pull = n.y1 - yy;
          if (pull > 0) {
            n.y0 -= pull;
            n.y1 -= pull;
          }
          yy = n.y0 - nodePadding;
        }
      }
    }
  }
  const outAt = /* @__PURE__ */ new Map();
  const inAt = /* @__PURE__ */ new Map();
  const links = valid.map((l, index) => {
    const source = nodes.get(l.source);
    const target = nodes.get(l.target);
    const w = Math.max(1, l.value * scale);
    const sy = source.y0 + (outAt.get(l.source) ?? 0);
    const ty = target.y0 + (inAt.get(l.target) ?? 0);
    outAt.set(l.source, (outAt.get(l.source) ?? 0) + w);
    inAt.set(l.target, (inAt.get(l.target) ?? 0) + w);
    return { source, target, value: l.value, width: w, y0: sy + w / 2, y1: ty + w / 2, index };
  });
  return { nodes: [...nodes.values()], links };
}
function sankeyPath(l) {
  const x0 = l.source.x1;
  const x1 = l.target.x0;
  const mid = (x0 + x1) / 2;
  return `M${x0},${l.y0} C${mid},${l.y0} ${mid},${l.y1} ${x1},${l.y1}`;
}
function chordLayout(matrix, names, padAngle = 0.03) {
  const n = matrix.length;
  if (!n) return { groups: [], ribbons: [] };
  const totals = matrix.map((row, i) => row.reduce((s, v, j) => s + Math.max(0, v) + (i === j ? 0 : Math.max(0, matrix[j][i])), 0));
  const grand = totals.reduce((s, v) => s + v, 0);
  if (!grand) return { groups: [], ribbons: [] };
  const available = Math.PI * 2 - padAngle * n;
  const groups = [];
  let angle = 0;
  for (let i = 0; i < n; i++) {
    const span = totals[i] / grand * available;
    groups.push({ index: i, name: names[i] ?? String(i), startAngle: angle, endAngle: angle + span, value: totals[i] });
    angle += span + padAngle;
  }
  const cursor = groups.map((g) => g.startAngle);
  const subArc = (i, value) => {
    const span = totals[i] ? value / totals[i] * (groups[i].endAngle - groups[i].startAngle) : 0;
    const start = cursor[i];
    cursor[i] += span;
    return { index: i, startAngle: start, endAngle: start + span };
  };
  const ribbons = [];
  for (let i = 0; i < n; i++) {
    for (let j = 0; j < n; j++) {
      const v = Math.max(0, matrix[i][j]);
      if (!v) continue;
      const source = subArc(i, v);
      const target = subArc(j, v);
      ribbons.push({ source, target, value: v });
    }
  }
  return { groups, ribbons };
}
function chordRibbonPath(r, radius, cx, cy) {
  const pt = (a) => [cx + radius * Math.sin(a), cy - radius * Math.cos(a)];
  const [sx0, sy0] = pt(r.source.startAngle);
  const [sx1, sy1] = pt(r.source.endAngle);
  const [tx0, ty0] = pt(r.target.startAngle);
  const [tx1, ty1] = pt(r.target.endAngle);
  const largeS = r.source.endAngle - r.source.startAngle > Math.PI ? 1 : 0;
  const largeT = r.target.endAngle - r.target.startAngle > Math.PI ? 1 : 0;
  return `M${sx0},${sy0} A${radius},${radius} 0 ${largeS} 1 ${sx1},${sy1} Q${cx},${cy} ${tx0},${ty0} A${radius},${radius} 0 ${largeT} 1 ${tx1},${ty1} Q${cx},${cy} ${sx0},${sy0} Z`;
}
function projection(name = "mercator") {
  const rad = Math.PI / 180;
  if (name === "equirectangular") {
    return (([lon, lat]) => [lon * rad, -lat * rad]);
  }
  if (name === "naturalEarth") {
    return (([lon, lat]) => {
      const phi = lat * rad;
      const p2 = phi * phi;
      const p4 = p2 * p2;
      return [
        lon * rad * (0.8707 - 0.131979 * p2 + p4 * (-0.013791 + p4 * (3971e-6 - 1529e-6 * p2))),
        -phi * (1.007226 + p2 * (0.015085 + p4 * (-0.044475 + 0.028874 * p2 - 5916e-6 * p4)))
      ];
    });
  }
  return (([lon, lat]) => {
    const phi = Math.max(-85, Math.min(85, lat)) * rad;
    return [lon * rad, -Math.log(Math.tan(Math.PI / 4 + phi / 2))];
  });
}
function coordsOf(geometry) {
  const out = [];
  const walk = (c) => {
    if (typeof c?.[0] === "number") {
      out.push(c);
      return;
    }
    if (Array.isArray(c)) c.forEach(walk);
  };
  walk(geometry?.coordinates);
  return out;
}
function fitFeatures(features, proj, w, h, margin = 8) {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (const f of features) {
    for (const c of coordsOf(f.geometry)) {
      const [x, y] = proj(c);
      if (!Number.isFinite(x) || !Number.isFinite(y)) continue;
      if (x < x0) x0 = x;
      if (x > x1) x1 = x;
      if (y < y0) y0 = y;
      if (y > y1) y1 = y;
    }
  }
  if (!Number.isFinite(x0)) return { scale: 1, tx: 0, ty: 0 };
  const scale = Math.min((w - margin * 2) / (x1 - x0 || 1), (h - margin * 2) / (y1 - y0 || 1));
  return {
    scale,
    tx: margin + (w - margin * 2 - (x1 - x0) * scale) / 2 - x0 * scale,
    ty: margin + (h - margin * 2 - (y1 - y0) * scale) / 2 - y0 * scale
  };
}
function geoPath(geometry, proj, fit) {
  const at = (c) => {
    const [x, y] = proj(c);
    return `${(x * fit.scale + fit.tx).toFixed(2)},${(y * fit.scale + fit.ty).toFixed(2)}`;
  };
  const ring = (r) => r.map((c, i) => `${i ? "L" : "M"}${at(c)}`).join("") + "Z";
  switch (geometry?.type) {
    case "Polygon":
      return geometry.coordinates.map(ring).join(" ");
    case "MultiPolygon":
      return geometry.coordinates.flatMap((p) => p.map(ring)).join(" ");
    case "LineString":
      return geometry.coordinates.map((c, i) => `${i ? "L" : "M"}${at(c)}`).join("");
    case "MultiLineString":
      return geometry.coordinates.map((l) => l.map((c, i) => `${i ? "L" : "M"}${at(c)}`).join("")).join(" ");
    default:
      return "";
  }
}
function geoCentroid(geometry, proj, fit) {
  const cs = coordsOf(geometry);
  if (!cs.length) return null;
  let sx = 0, sy = 0, n = 0;
  for (const c of cs) {
    const [x, y] = proj(c);
    if (!Number.isFinite(x) || !Number.isFinite(y)) continue;
    sx += x;
    sy += y;
    n++;
  }
  if (!n) return null;
  return [sx / n * fit.scale + fit.tx, sy / n * fit.scale + fit.ty];
}

// src/ShapeCharts.tsx
import { jsx as jsx7, jsxs as jsxs6 } from "react/jsx-runtime";
function useApi2(svgRef, apiRef) {
  useImperativeHandle5(apiRef, () => ({
    toSVG: () => svgToString(svgRef.current),
    toPNG: (s = 2) => svgToPNG(svgRef.current, s),
    download: (f = "chart", fmt = "png") => downloadChart(svgRef.current, f, fmt),
    resetZoom: () => {
    },
    getHiddenSeries: () => [],
    setHiddenSeries: () => {
    }
  }), []);
}
function FunnelInner(props, apiRef) {
  const {
    data,
    labelKey,
    valueKey,
    fromFirst = true,
    height = 320,
    palette,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef7(null);
  const [tip, setTip] = useState7(null);
  useApi2(svgRef, apiRef);
  const stages = useMemo6(
    () => data.map((d) => ({ label: String(d[labelKey]), value: Number(d[valueKey]) || 0, datum: d })).filter((s) => s.value >= 0),
    [data, labelKey, valueKey]
  );
  const anim = useAnimation(stages.length, animate);
  const top = stages[0]?.value || 1;
  const rowH = stages.length ? (height - 16) / stages.length : 0;
  const maxW = Math.max(0, width - 180);
  return /* @__PURE__ */ jsx7(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !stages.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs6("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsx7(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Funnel of ${stages.length} stages.`,
            onPointerLeave: () => setTip(null),
            children: stages.map((s, i) => {
              const next = stages[i + 1];
              const wTop = s.value / top * maxW * anim;
              const wBot = (next?.value ?? s.value) / top * maxW * anim;
              const cx = width / 2;
              const y0 = 8 + i * rowH;
              const y1 = y0 + rowH - 4;
              const color = colorAt(palette, i);
              const prev = i > 0 ? stages[i - 1].value : s.value;
              const rate = fromFirst ? top ? s.value / top * 100 : 0 : prev ? s.value / prev * 100 : 0;
              return /* @__PURE__ */ jsxs6(
                "g",
                {
                  onPointerMove: (e) => {
                    const r = svgRef.current?.getBoundingClientRect();
                    setTip({
                      x: e.clientX - (r?.left ?? 0),
                      y: e.clientY - (r?.top ?? 0),
                      title: s.label,
                      rows: [
                        { name: valueKey, color, formatted: formatValue(s.value) },
                        { name: fromFirst ? "of first" : "of previous", color, formatted: `${rate.toFixed(1)}%` },
                        ...next ? [{ name: "Drop-off", color: "#C0392B", formatted: formatValue(s.value - next.value) }] : []
                      ]
                    });
                  },
                  onClick: () => onSeriesClick?.({ series: s.label, datum: s.datum, index: i }),
                  style: { cursor: onSeriesClick ? "pointer" : void 0 },
                  children: [
                    /* @__PURE__ */ jsx7(
                      "path",
                      {
                        d: `M${cx - wTop / 2},${y0} L${cx + wTop / 2},${y0} L${cx + wBot / 2},${y1} L${cx - wBot / 2},${y1} Z`,
                        fill: color,
                        fillOpacity: 0.9
                      }
                    ),
                    /* @__PURE__ */ jsx7(
                      "text",
                      {
                        x: cx,
                        y: y0 + rowH / 2,
                        textAnchor: "middle",
                        fontSize: 11,
                        fontWeight: 600,
                        fill: contrastText(color),
                        pointerEvents: "none",
                        children: compact(s.value)
                      }
                    ),
                    /* @__PURE__ */ jsx7("text", { x: 8, y: y0 + rowH / 2 + 4, fontSize: 11, fill: "var(--ec-text, #374151)", pointerEvents: "none", children: s.label }),
                    /* @__PURE__ */ jsxs6(
                      "text",
                      {
                        x: width - 8,
                        y: y0 + rowH / 2 + 4,
                        textAnchor: "end",
                        fontSize: 11,
                        fill: "var(--ec-text-muted, #6b7280)",
                        pointerEvents: "none",
                        children: [
                          rate.toFixed(0),
                          "%"
                        ]
                      }
                    )
                  ]
                },
                s.label
              );
            })
          }
        ),
        /* @__PURE__ */ jsx7(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
function RadarInner(props, apiRef) {
  const {
    data,
    axisKey,
    valueKeys,
    max,
    shape = "polygon",
    height = 320,
    palette,
    legend,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef7(null);
  const [tip, setTip] = useState7(null);
  useApi2(svgRef, apiRef);
  const axes = useMemo6(() => data.map((d) => String(d[axisKey])), [data, axisKey]);
  const hi = max ?? (extent(data.flatMap((d) => valueKeys.map((k) => Number(d[k]))))[1] || 1);
  const anim = useAnimation(`${data.length}:${valueKeys.join()}`, animate);
  const cx = width / 2;
  const cy = height / 2;
  const r = Math.max(0, Math.min(width, height) / 2 - 34);
  const step = axes.length ? Math.PI * 2 / axes.length : 0;
  const pt = (i, frac) => [
    cx + r * frac * Math.sin(i * step),
    cy - r * frac * Math.cos(i * step)
  ];
  const rings = [0.25, 0.5, 0.75, 1];
  return /* @__PURE__ */ jsxs6(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !axes.length || !valueKeys.length,
      emptyMessage,
      children: [
        /* @__PURE__ */ jsxs6("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
          width > 0 && /* @__PURE__ */ jsxs6(
            "svg",
            {
              ref: svgRef,
              width,
              height,
              role: "img",
              "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Radar over ${axes.length} axes.`,
              onPointerLeave: () => setTip(null),
              children: [
                rings.map((f, i) => shape === "circle" ? /* @__PURE__ */ jsx7("circle", { cx, cy, r: r * f, fill: "none", stroke: "var(--ec-grid, #e5e7eb)" }, i) : /* @__PURE__ */ jsx7(
                  "polygon",
                  {
                    points: axes.map((_, k) => pt(k, f).join(",")).join(" "),
                    fill: "none",
                    stroke: "var(--ec-grid, #e5e7eb)"
                  },
                  i
                )),
                axes.map((a, i) => {
                  const [ex, ey] = pt(i, 1);
                  const [lx, ly] = pt(i, 1.14);
                  return /* @__PURE__ */ jsxs6("g", { children: [
                    /* @__PURE__ */ jsx7("line", { x1: cx, y1: cy, x2: ex, y2: ey, stroke: "var(--ec-grid, #e5e7eb)" }),
                    /* @__PURE__ */ jsx7(
                      "text",
                      {
                        x: lx,
                        y: ly + 4,
                        textAnchor: Math.abs(lx - cx) < 6 ? "middle" : lx > cx ? "start" : "end",
                        fontSize: 11,
                        fill: "var(--ec-text-muted, #6b7280)",
                        children: a
                      }
                    )
                  ] }, a);
                }),
                valueKeys.map((key, si) => {
                  const color = colorAt(palette, si);
                  const pts = data.map((d, i) => pt(i, Math.max(0, Math.min(1, (Number(d[key]) || 0) / hi)) * anim));
                  return /* @__PURE__ */ jsxs6("g", { children: [
                    /* @__PURE__ */ jsx7(
                      "polygon",
                      {
                        points: pts.map((p) => p.join(",")).join(" "),
                        fill: color,
                        fillOpacity: 0.18,
                        stroke: color,
                        strokeWidth: 2
                      }
                    ),
                    pts.map((p, i) => /* @__PURE__ */ jsx7(
                      "circle",
                      {
                        cx: p[0],
                        cy: p[1],
                        r: 3,
                        fill: color,
                        onPointerMove: (e) => {
                          const rect = svgRef.current?.getBoundingClientRect();
                          setTip({
                            x: e.clientX - (rect?.left ?? 0),
                            y: e.clientY - (rect?.top ?? 0),
                            title: axes[i],
                            rows: valueKeys.map((k, ki) => ({
                              name: k,
                              color: colorAt(palette, ki),
                              formatted: formatValue(Number(data[i][k]))
                            }))
                          });
                        }
                      },
                      i
                    ))
                  ] }, key);
                })
              ]
            }
          ),
          /* @__PURE__ */ jsx7(ChartTooltip, { state: tip, width, height })
        ] }),
        (legend?.position ?? "bottom") !== "none" && /* @__PURE__ */ jsx7(
          Legend,
          {
            items: valueKeys.map((k, i) => ({ name: k, color: colorAt(palette, i) })),
            position: legend?.position ?? "bottom",
            toggleable: false
          }
        )
      ]
    }
  );
}
function SankeyInner(props, apiRef) {
  const {
    data,
    sourceKey,
    targetKey,
    valueKey,
    nodeWidth = 14,
    nodePadding = 12,
    height = 360,
    palette,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef7(null);
  const [tip, setTip] = useState7(null);
  const [activeNode, setActiveNode] = useState7(null);
  useApi2(svgRef, apiRef);
  const margin = { left: 4, right: 4, top: 8, bottom: 8 };
  const innerW = Math.max(0, width - margin.left - margin.right - 120);
  const innerH = Math.max(0, height - margin.top - margin.bottom);
  const layout = useMemo6(
    () => sankeyLayout(
      data.map((d) => ({ source: String(d[sourceKey]), target: String(d[targetKey]), value: Number(d[valueKey]) || 0 })),
      innerW,
      innerH,
      { nodeWidth, nodePadding }
    ),
    [data, sourceKey, targetKey, valueKey, innerW, innerH, nodeWidth, nodePadding]
  );
  const anim = useAnimation(data.length, animate);
  return (
    // `empty` must test the input, not the layout: the layout needs a measured
    // container width, and ChartFrame renders the empty state *instead of* the
    // container — so keying off the layout would never let the width arrive.
    /* @__PURE__ */ jsx7(
      ChartFrame,
      {
        title,
        subtitle,
        className,
        height,
        empty: !data.length,
        emptyMessage,
        children: /* @__PURE__ */ jsxs6("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
          width > 0 && /* @__PURE__ */ jsx7(
            "svg",
            {
              ref: svgRef,
              width,
              height,
              role: "img",
              "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Flow diagram, ${layout.nodes.length} nodes and ${layout.links.length} links.`,
              onPointerLeave: () => {
                setTip(null);
                setActiveNode(null);
              },
              children: /* @__PURE__ */ jsxs6("g", { transform: `translate(${margin.left},${margin.top})`, children: [
                layout.links.map((l) => {
                  const color = colorAt(palette, l.source.index);
                  const dim = activeNode != null && activeNode !== l.source.id && activeNode !== l.target.id;
                  return /* @__PURE__ */ jsx7(
                    "path",
                    {
                      d: sankeyPath(l),
                      fill: "none",
                      stroke: color,
                      strokeWidth: Math.max(1, l.width),
                      strokeOpacity: (dim ? 0.08 : 0.34) * anim,
                      onPointerMove: (e) => {
                        const r = svgRef.current?.getBoundingClientRect();
                        setTip({
                          x: e.clientX - (r?.left ?? 0),
                          y: e.clientY - (r?.top ?? 0),
                          title: `${l.source.id} \u2192 ${l.target.id}`,
                          rows: [{ name: valueKey, color, formatted: formatValue(l.value) }]
                        });
                      },
                      onClick: () => onSeriesClick?.({ series: `${l.source.id}->${l.target.id}`, datum: data[l.index], index: l.index }),
                      style: { cursor: onSeriesClick ? "pointer" : void 0 }
                    },
                    l.index
                  );
                }),
                layout.nodes.map((n) => {
                  const color = colorAt(palette, n.index);
                  return /* @__PURE__ */ jsxs6(
                    "g",
                    {
                      onPointerEnter: () => setActiveNode(n.id),
                      onPointerMove: (e) => {
                        const r = svgRef.current?.getBoundingClientRect();
                        setTip({
                          x: e.clientX - (r?.left ?? 0),
                          y: e.clientY - (r?.top ?? 0),
                          title: n.id,
                          rows: [{ name: "Throughput", color, formatted: formatValue(n.value) }]
                        });
                      },
                      children: [
                        /* @__PURE__ */ jsx7(
                          "rect",
                          {
                            x: n.x0,
                            y: n.y0,
                            width: Math.max(1, n.x1 - n.x0),
                            height: Math.max(1, n.y1 - n.y0),
                            fill: color,
                            rx: 2,
                            opacity: anim
                          }
                        ),
                        /* @__PURE__ */ jsx7(
                          "text",
                          {
                            x: n.x1 + 6,
                            y: (n.y0 + n.y1) / 2 + 4,
                            fontSize: 11,
                            fill: "var(--ec-text, #374151)",
                            pointerEvents: "none",
                            children: n.id
                          }
                        )
                      ]
                    },
                    n.id
                  );
                })
              ] })
            }
          ),
          /* @__PURE__ */ jsx7(ChartTooltip, { state: tip, width, height })
        ] })
      }
    )
  );
}
function ChordInner(props, apiRef) {
  const {
    data,
    sourceKey,
    targetKey,
    valueKey,
    height = 360,
    palette,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef7(null);
  const [tip, setTip] = useState7(null);
  const [activeGroup, setActiveGroup] = useState7(null);
  useApi2(svgRef, apiRef);
  const { matrix, names } = useMemo6(() => {
    const ns = [...new Set(data.flatMap((d) => [String(d[sourceKey]), String(d[targetKey])]))];
    const idx = new Map(ns.map((n, i) => [n, i]));
    const m = ns.map(() => ns.map(() => 0));
    for (const d of data) {
      const i = idx.get(String(d[sourceKey]));
      const j = idx.get(String(d[targetKey]));
      m[i][j] += Number(d[valueKey]) || 0;
    }
    return { matrix: m, names: ns };
  }, [data, sourceKey, targetKey, valueKey]);
  const layout = useMemo6(() => chordLayout(matrix, names), [matrix, names]);
  const anim = useAnimation(data.length, animate);
  const cx = width / 2;
  const cy = height / 2;
  const rOuter = Math.max(0, Math.min(width, height) / 2 - 46);
  const rInner = rOuter - 12;
  const arc = (a0, a1, r0, r1) => {
    const p = (r, a) => `${cx + r * Math.sin(a)},${cy - r * Math.cos(a)}`;
    const large = a1 - a0 > Math.PI ? 1 : 0;
    return `M${p(r1, a0)} A${r1},${r1} 0 ${large} 1 ${p(r1, a1)} L${p(r0, a1)} A${r0},${r0} 0 ${large} 0 ${p(r0, a0)} Z`;
  };
  return /* @__PURE__ */ jsx7(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !layout.groups.length,
      emptyMessage,
      children: /* @__PURE__ */ jsxs6("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsx7(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Chord diagram of ${names.length} groups.`,
            onPointerLeave: () => {
              setTip(null);
              setActiveGroup(null);
            },
            children: /* @__PURE__ */ jsxs6("g", { opacity: anim, children: [
              layout.ribbons.map((rb, i) => {
                const color = colorAt(palette, rb.source.index);
                const dim = activeGroup != null && activeGroup !== rb.source.index && activeGroup !== rb.target.index;
                return /* @__PURE__ */ jsx7(
                  "path",
                  {
                    d: chordRibbonPath(rb, rInner, cx, cy),
                    fill: color,
                    fillOpacity: dim ? 0.05 : 0.42,
                    stroke: color,
                    strokeOpacity: dim ? 0.1 : 0.5,
                    onPointerMove: (e) => {
                      const r = svgRef.current?.getBoundingClientRect();
                      setTip({
                        x: e.clientX - (r?.left ?? 0),
                        y: e.clientY - (r?.top ?? 0),
                        title: `${names[rb.source.index]} \u2192 ${names[rb.target.index]}`,
                        rows: [{ name: valueKey, color, formatted: formatValue(rb.value) }]
                      });
                    },
                    onClick: () => onSeriesClick?.({ series: names[rb.source.index], datum: data[i] ?? {}, index: i }),
                    style: { cursor: onSeriesClick ? "pointer" : void 0 }
                  },
                  i
                );
              }),
              layout.groups.map((g) => {
                const color = colorAt(palette, g.index);
                const mid = (g.startAngle + g.endAngle) / 2;
                const lx = cx + (rOuter + 12) * Math.sin(mid);
                const ly = cy - (rOuter + 12) * Math.cos(mid);
                return /* @__PURE__ */ jsxs6("g", { onPointerEnter: () => setActiveGroup(g.index), children: [
                  /* @__PURE__ */ jsx7("path", { d: arc(g.startAngle, g.endAngle, rInner, rOuter), fill: color }),
                  /* @__PURE__ */ jsx7(
                    "text",
                    {
                      x: lx,
                      y: ly + 4,
                      fontSize: 11,
                      textAnchor: Math.abs(lx - cx) < 8 ? "middle" : lx > cx ? "start" : "end",
                      fill: "var(--ec-text, #374151)",
                      children: g.name
                    }
                  )
                ] }, g.name);
              })
            ] })
          }
        ),
        /* @__PURE__ */ jsx7(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
function GeoInner(props, apiRef) {
  const {
    features,
    data = [],
    idKey,
    valueKey,
    featureIdProperty,
    projectionName = "mercator",
    colorRange = RAMP,
    showLabels = false,
    height = 380,
    title,
    subtitle,
    className = "",
    emptyMessage,
    ariaLabel,
    animate = true,
    onSeriesClick
  } = props;
  const [wrapRef, width] = useWidth();
  const svgRef = useRef7(null);
  const [tip, setTip] = useState7(null);
  useApi2(svgRef, apiRef);
  const anim = useAnimation(features.length, animate);
  const proj = useMemo6(() => projection(projectionName), [projectionName]);
  const fit = useMemo6(() => fitFeatures(features, proj, width || 1, height), [features, proj, width, height]);
  const featureId = (f) => {
    if (featureIdProperty) return String(f.properties?.[featureIdProperty] ?? "");
    return String(f.id ?? f.properties?.id ?? f.properties?.name ?? "");
  };
  const byId = useMemo6(() => {
    const m = /* @__PURE__ */ new Map();
    if (idKey) for (const d of data) m.set(String(d[idKey]), d);
    return m;
  }, [data, idKey]);
  const [lo, hi] = useMemo6(
    () => valueKey ? extent(data.map((d) => Number(d[valueKey]))) : [0, 1],
    [data, valueKey]
  );
  return /* @__PURE__ */ jsx7(
    ChartFrame,
    {
      title,
      subtitle,
      className,
      height,
      empty: !features.length,
      emptyMessage: emptyMessage ?? "No map features supplied.",
      children: /* @__PURE__ */ jsxs6("div", { ref: wrapRef, className: "relative w-full", style: { height }, children: [
        width > 0 && /* @__PURE__ */ jsx7(
          "svg",
          {
            ref: svgRef,
            width,
            height,
            role: "img",
            "aria-label": ariaLabel ?? `${title ? title + ". " : ""}Map of ${features.length} regions.`,
            onPointerLeave: () => setTip(null),
            children: features.map((f, i) => {
              const id = featureId(f);
              const row = byId.get(id);
              const v = row && valueKey ? Number(row[valueKey]) : null;
              const t = v == null || hi === lo ? null : (v - lo) / (hi - lo);
              const fill = t == null ? "#f1f5f9" : mix(colorRange[0], colorRange[1], t);
              const c = showLabels ? geoCentroid(f.geometry, proj, fit) : null;
              return /* @__PURE__ */ jsxs6("g", { children: [
                /* @__PURE__ */ jsx7(
                  "path",
                  {
                    d: geoPath(f.geometry, proj, fit),
                    fill,
                    opacity: anim,
                    stroke: "var(--ec-surface, #ffffff)",
                    strokeWidth: 0.6,
                    onPointerMove: (e) => {
                      const r = svgRef.current?.getBoundingClientRect();
                      setTip({
                        x: e.clientX - (r?.left ?? 0),
                        y: e.clientY - (r?.top ?? 0),
                        title: String(f.properties?.name ?? id ?? "Region"),
                        rows: [{
                          name: valueKey ?? "Value",
                          color: fill,
                          formatted: v == null ? "No data" : formatValue(v)
                        }]
                      });
                    },
                    onClick: () => onSeriesClick?.({ series: id, datum: row ?? {}, index: i }),
                    style: { cursor: onSeriesClick ? "pointer" : void 0 }
                  }
                ),
                c && /* @__PURE__ */ jsx7(
                  "text",
                  {
                    x: c[0],
                    y: c[1],
                    textAnchor: "middle",
                    fontSize: 9,
                    pointerEvents: "none",
                    fill: contrastText(fill),
                    children: String(f.properties?.name ?? id).slice(0, 14)
                  }
                )
              ] }, id || i);
            })
          }
        ),
        /* @__PURE__ */ jsx7(ChartTooltip, { state: tip, width, height })
      ] })
    }
  );
}
var FunnelChart = React7.forwardRef(FunnelInner);
var RadarChart = React7.forwardRef(RadarInner);
var SankeyChart = React7.forwardRef(SankeyInner);
var ChordChart = React7.forwardRef(ChordInner);
var GeoChart = React7.forwardRef(GeoInner);

// src/EnterpriseChart.tsx
import { jsx as jsx8, jsxs as jsxs7 } from "react/jsx-runtime";
var INTERCHANGEABLE = {
  cartesian: ["column", "bar", "line", "area", "scatter"],
  proportional: ["pie", "donut", "rose", "treemap", "funnel"]
};
function EnterpriseChartInner(props, apiRef) {
  const {
    type,
    data = [],
    series,
    xKey,
    yKeys,
    stacked,
    toolbar = false,
    exportFileName = "chart",
    height = 320,
    ...rest
  } = props;
  const innerRef = useRef8(null);
  const [override, setOverride] = useState8(null);
  useEffect2(() => setOverride(null), [type]);
  const activeType = override ?? type;
  const setCurrent = setOverride;
  useImperativeHandle6(apiRef, () => ({
    toSVG: () => innerRef.current?.toSVG() ?? null,
    toPNG: (s) => innerRef.current?.toPNG(s) ?? Promise.resolve(null),
    download: (f, fmt) => innerRef.current?.download(f ?? exportFileName, fmt) ?? Promise.resolve(),
    resetZoom: () => innerRef.current?.resetZoom(),
    getHiddenSeries: () => innerRef.current?.getHiddenSeries() ?? [],
    setHiddenSeries: (n) => innerRef.current?.setHiddenSeries(n)
  }), [exportFileName]);
  const resolvedSeries = useMemo7(() => {
    if (series?.length) return series;
    if (!xKey || !yKeys?.length) return [];
    const seriesType = activeType === "combo" ? "line" : activeType;
    return yKeys.map((k) => ({
      type: ["line", "area", "column", "bar", "scatter", "bubble"].includes(seriesType) ? seriesType : "column",
      xKey,
      yKey: k,
      name: k,
      ...stacked ? { stack: "default", normalized: stacked === "normalized" } : {}
    }));
  }, [series, xKey, yKeys, activeType, stacked]);
  const shared = { data, height, ...rest };
  const switchable = Object.values(INTERCHANGEABLE).find((g) => g.includes(activeType));
  const chart = (() => {
    switch (activeType) {
      case "line":
      case "area":
      case "column":
      case "bar":
      case "scatter":
      case "bubble":
      case "combo":
        return /* @__PURE__ */ jsx8(CartesianChart, { ref: innerRef, ...shared, series: resolvedSeries });
      case "pie":
      case "donut":
      case "rose":
        return /* @__PURE__ */ jsx8(
          PieChart,
          {
            ref: innerRef,
            ...shared,
            labelKey: props.labelKey ?? xKey ?? "label",
            valueKey: props.valueKey ?? yKeys?.[0] ?? "value",
            innerRadius: activeType === "donut" ? props.innerRadius ?? 0.62 : activeType === "rose" ? 0.25 : 0,
            roseType: activeType === "rose"
          }
        );
      case "waterfall":
        return /* @__PURE__ */ jsx8(
          WaterfallChart,
          {
            ref: innerRef,
            ...shared,
            labelKey: props.labelKey ?? xKey ?? "label",
            valueKey: props.valueKey ?? yKeys?.[0] ?? "value"
          }
        );
      case "heatmap":
        return /* @__PURE__ */ jsx8(
          HeatmapChart,
          {
            ref: innerRef,
            ...shared,
            xKey: xKey ?? "x",
            yKey: props.yKey ?? "y",
            valueKey: props.valueKey ?? "value"
          }
        );
      case "treemap":
        return /* @__PURE__ */ jsx8(
          TreemapChart,
          {
            ref: innerRef,
            ...shared,
            labelKey: props.labelKey ?? "label",
            valueKey: props.valueKey ?? "value"
          }
        );
      case "gauge":
        return /* @__PURE__ */ jsx8(GaugeChart, { ref: innerRef, ...rest, height, value: props.value ?? 0 });
      case "candlestick":
      case "ohlc":
        return /* @__PURE__ */ jsx8(
          CandlestickChart,
          {
            ref: innerRef,
            ...shared,
            xKey: xKey ?? "date",
            openKey: props.openKey ?? "open",
            highKey: props.highKey ?? "high",
            lowKey: props.lowKey ?? "low",
            closeKey: props.closeKey ?? "close",
            style: activeType === "ohlc" ? "ohlc" : "candle"
          }
        );
      case "boxplot":
        return /* @__PURE__ */ jsx8(
          BoxPlotChart,
          {
            ref: innerRef,
            ...shared,
            groupKey: props.groupKey ?? xKey ?? "group",
            valueKey: props.valueKey ?? yKeys?.[0] ?? "value"
          }
        );
      case "histogram":
        return /* @__PURE__ */ jsx8(
          HistogramChart,
          {
            ref: innerRef,
            ...shared,
            valueKey: props.valueKey ?? yKeys?.[0] ?? "value",
            method: props.binMethod
          }
        );
      case "funnel":
        return /* @__PURE__ */ jsx8(
          FunnelChart,
          {
            ref: innerRef,
            ...shared,
            labelKey: props.labelKey ?? xKey ?? "label",
            valueKey: props.valueKey ?? yKeys?.[0] ?? "value"
          }
        );
      case "radar":
        return /* @__PURE__ */ jsx8(
          RadarChart,
          {
            ref: innerRef,
            ...shared,
            axisKey: props.axisKey ?? xKey ?? "axis",
            valueKeys: props.valueKeys ?? yKeys ?? []
          }
        );
      case "sankey":
        return /* @__PURE__ */ jsx8(
          SankeyChart,
          {
            ref: innerRef,
            ...shared,
            sourceKey: props.sourceKey ?? "source",
            targetKey: props.targetKey ?? "target",
            valueKey: props.valueKey ?? "value"
          }
        );
      case "chord":
        return /* @__PURE__ */ jsx8(
          ChordChart,
          {
            ref: innerRef,
            ...shared,
            sourceKey: props.sourceKey ?? "source",
            targetKey: props.targetKey ?? "target",
            valueKey: props.valueKey ?? "value"
          }
        );
      case "geo":
        return /* @__PURE__ */ jsx8(GeoChart, { ref: innerRef, ...rest, height, data, features: props.features ?? [] });
      default:
        return null;
    }
  })();
  if (!toolbar) return chart;
  return /* @__PURE__ */ jsxs7("div", { className: "w-full", children: [
    /* @__PURE__ */ jsxs7("div", { className: "mb-1 flex flex-wrap items-center gap-1.5", children: [
      switchable && switchable.length > 1 && /* @__PURE__ */ jsx8(
        "div",
        {
          className: "flex overflow-hidden rounded-md border text-[11px]",
          style: { borderColor: "var(--ec-border, #e5e7eb)" },
          children: switchable.map((t) => /* @__PURE__ */ jsx8(
            "button",
            {
              type: "button",
              onClick: () => setCurrent(t),
              className: "px-2 py-1 capitalize transition-colors",
              style: {
                background: t === activeType ? "var(--ec-accent, #2E5BBA)" : "transparent",
                color: t === activeType ? "#fff" : "var(--ec-text-muted, #6b7280)"
              },
              children: t
            },
            t
          ))
        }
      ),
      /* @__PURE__ */ jsx8("div", { className: "ml-auto flex gap-1.5", children: ["png", "svg"].map((fmt) => /* @__PURE__ */ jsx8(
        "button",
        {
          type: "button",
          onClick: () => void innerRef.current?.download(exportFileName, fmt),
          className: "rounded-md border px-2 py-1 text-[11px] uppercase",
          style: { borderColor: "var(--ec-border, #e5e7eb)", color: "var(--ec-text-muted, #6b7280)" },
          children: fmt
        },
        fmt
      )) })
    ] }),
    chart
  ] });
}
var EnterpriseChart = React8.forwardRef(EnterpriseChartInner);

// src/gridLink.ts
import { useCallback as useCallback3, useEffect as useEffect3, useMemo as useMemo8, useRef as useRef9, useState as useState9 } from "react";
function reduce(values, agg) {
  if (agg === "count") return values.length;
  if (!values.length) return 0;
  switch (agg) {
    case "avg":
      return values.reduce((s, v) => s + v, 0) / values.length;
    case "min":
      return Math.min(...values);
    case "max":
      return Math.max(...values);
    default:
      return values.reduce((s, v) => s + v, 0);
  }
}
function aggregateRows(rows, opts) {
  const { by, value, agg = value ? "sum" : "count", topN, otherLabel = "Other", sortByValue } = opts;
  const valueKeys = value == null ? [] : Array.isArray(value) ? value : [value];
  const buckets = /* @__PURE__ */ new Map();
  for (const row of rows) {
    const k = String(row[by] ?? "\u2014");
    const list = buckets.get(k);
    if (list) list.push(row);
    else buckets.set(k, [row]);
  }
  let out = [...buckets.entries()].map(([key, list]) => {
    const rec = { [by]: key, count: list.length };
    for (const vk of valueKeys) {
      rec[vk] = reduce(
        list.map((r) => Number(r[vk])).filter((n) => Number.isFinite(n)),
        agg
      );
    }
    if (!valueKeys.length) rec.value = list.length;
    return rec;
  });
  const primary = valueKeys[0] ?? "value";
  if (sortByValue || topN) out.sort((a, b) => Number(b[primary]) - Number(a[primary]));
  if (topN && out.length > topN) {
    const keep = out.slice(0, topN);
    const rest = out.slice(topN);
    const merged = { [by]: otherLabel, count: rest.reduce((s, r) => s + Number(r.count ?? 0), 0) };
    for (const vk of valueKeys.length ? valueKeys : ["value"]) {
      if (agg === "avg") {
        const totalN = rest.reduce((s, r) => s + Number(r.count ?? 0), 0);
        merged[vk] = totalN ? rest.reduce((s, r) => s + Number(r[vk]) * Number(r.count ?? 0), 0) / totalN : 0;
      } else if (agg === "min") merged[vk] = Math.min(...rest.map((r) => Number(r[vk])));
      else if (agg === "max") merged[vk] = Math.max(...rest.map((r) => Number(r[vk])));
      else merged[vk] = rest.reduce((s, r) => s + Number(r[vk]), 0);
    }
    out = [...keep, merged];
  }
  return out;
}
function useGridChartLink(opts) {
  const { followSelection = true, ...aggOpts } = opts;
  const [viewRows, setViewRows] = useState9([]);
  const [selectedRows, setSelectedRows] = useState9([]);
  const onViewChanged = useCallback3((rows) => setViewRows(rows), []);
  const onSelectionChanged = useCallback3((rows) => setSelectedRows(rows), []);
  const selectionActive = followSelection && selectedRows.length > 0;
  const sourceRows = selectionActive ? selectedRows : viewRows;
  const key = JSON.stringify(aggOpts);
  const data = useMemo8(
    () => aggregateRows(sourceRows, JSON.parse(key)),
    [sourceRows, key]
  );
  return { data, sourceRows, onViewChanged, onSelectionChanged, selectionActive };
}
function useChartGridFilter(gridRef, field) {
  const [active, setActive] = useState9(null);
  const fieldRef = useRef9(field);
  fieldRef.current = field;
  const apply = useCallback3((value) => {
    const api = gridRef.current;
    if (!api?.setFilterModel) return;
    setActive(value);
    const existing = api.getFilterModel?.() ?? {};
    const next = { ...existing };
    if (value == null) delete next[fieldRef.current];
    else next[fieldRef.current] = { kind: "set", selected: [value] };
    api.setFilterModel(next);
  }, [gridRef]);
  const onSeriesClick = useCallback3((event) => {
    const value = String(event.datum?.[fieldRef.current] ?? event.series);
    apply(active === value ? null : value);
  }, [active, apply]);
  return { onSeriesClick, activeFilter: active, clear: () => apply(null) };
}
function sparklineFrom(rows, valueKey, orderBy) {
  const list = orderBy ? [...rows].sort((a, b) => String(a[orderBy]).localeCompare(String(b[orderBy]), void 0, { numeric: true })) : rows;
  return list.map((r) => Number(r[valueKey])).filter((n) => Number.isFinite(n));
}
function usePolledGridRows(gridRef, intervalMs = 400) {
  const [rows, setRows] = useState9([]);
  useEffect3(() => {
    const tick = () => {
      const next = gridRef.current?.getDisplayedRows() ?? [];
      setRows((prev) => prev.length === next.length && prev[0] === next[0] ? prev : next);
    };
    tick();
    const id = setInterval(tick, intervalMs);
    return () => clearInterval(id);
  }, [gridRef, intervalMs]);
  return rows;
}

// src/Sparkline.tsx
import { useMemo as useMemo9 } from "react";
import { jsx as jsx9, jsxs as jsxs8 } from "react/jsx-runtime";
function Sparkline({
  data,
  type = "line",
  width = 90,
  height = 24,
  color,
  showLast = true,
  baseline,
  className = ""
}) {
  const pts = useMemo9(() => data.filter((n) => Number.isFinite(n)), [data]);
  const geom = useMemo9(() => {
    if (!pts.length) return null;
    const [lo, hi] = extent(pts);
    const pad = 2;
    const span = hi - lo || 1;
    const x = (i) => pad + i / Math.max(1, pts.length - 1) * (width - pad * 2);
    const y = (v) => height - pad - (v - lo) / span * (height - pad * 2);
    return { lo, hi, x, y, span };
  }, [pts, width, height]);
  if (!geom || !pts.length) {
    return /* @__PURE__ */ jsx9("svg", { width, height, className, "aria-hidden": "true" });
  }
  const stroke = color ?? (pts[pts.length - 1] >= pts[0] ? SEMANTIC.positive : SEMANTIC.negative);
  const coords = pts.map((v, i) => ({ x: geom.x(i), y: geom.y(v) }));
  const label = `Trend: ${pts.length} points, ${pts[0]} to ${pts[pts.length - 1]}`;
  if (type === "column" || type === "winloss") {
    const bw = Math.max(1, (width - 4) / pts.length - 1);
    const zeroY = type === "winloss" ? height / 2 : height - 2;
    return /* @__PURE__ */ jsx9("svg", { width, height, className, role: "img", "aria-label": label, children: pts.map((v, i) => {
      const isWin = v >= 0;
      const h = type === "winloss" ? height / 2 - 3 : Math.max(1, (height - 4) * ((v - geom.lo) / (geom.span || 1)));
      return /* @__PURE__ */ jsx9(
        "rect",
        {
          x: 2 + i * (bw + 1),
          y: isWin ? zeroY - h : zeroY,
          width: bw,
          height: h,
          rx: 1,
          fill: color ?? (isWin ? SEMANTIC.positive : SEMANTIC.negative)
        },
        i
      );
    }) });
  }
  return /* @__PURE__ */ jsxs8("svg", { width, height, className, role: "img", "aria-label": label, children: [
    baseline != null && geom.lo <= baseline && baseline <= geom.hi && /* @__PURE__ */ jsx9(
      "line",
      {
        x1: 0,
        x2: width,
        y1: geom.y(baseline),
        y2: geom.y(baseline),
        stroke: "var(--ec-grid, #e5e7eb)",
        strokeDasharray: "2 2"
      }
    ),
    type === "area" && /* @__PURE__ */ jsx9(
      "path",
      {
        d: `${linePath(coords)} L${coords[coords.length - 1].x},${height} L${coords[0].x},${height} Z`,
        fill: stroke,
        opacity: 0.15
      }
    ),
    /* @__PURE__ */ jsx9(
      "path",
      {
        d: linePath(coords),
        fill: "none",
        stroke,
        strokeWidth: 1.5,
        strokeLinecap: "round",
        strokeLinejoin: "round"
      }
    ),
    showLast && /* @__PURE__ */ jsx9("circle", { cx: coords[coords.length - 1].x, cy: coords[coords.length - 1].y, r: 2, fill: stroke })
  ] });
}

// src/streaming.ts
import { useCallback as useCallback4, useEffect as useEffect4, useRef as useRef10, useState as useState10 } from "react";
function useStreamingData(opts = {}) {
  const { maxPoints = 200, throttleMs = 250, initial = [], dedupeBy } = opts;
  const [data, setData] = useState10(() => initial.slice(-maxPoints));
  const [paused, setPaused] = useState10(false);
  const [received, setReceived] = useState10(initial.length);
  const buffer = useRef10([]);
  const timer = useRef10(null);
  const seen = useRef10(/* @__PURE__ */ new Set());
  const pausedRef = useRef10(paused);
  pausedRef.current = paused;
  const flush = useCallback4(() => {
    timer.current = null;
    const incoming = buffer.current;
    buffer.current = [];
    if (!incoming.length) return;
    setData((prev) => {
      const next = prev.concat(incoming);
      return next.length > maxPoints ? next.slice(next.length - maxPoints) : next;
    });
  }, [maxPoints]);
  const push = useCallback4((row) => {
    const rows = Array.isArray(row) ? row : [row];
    if (!rows.length) return;
    setReceived((n) => n + rows.length);
    if (pausedRef.current) return;
    const accepted = dedupeBy ? rows.filter((r) => {
      const k = dedupeBy(r);
      if (seen.current.has(k)) return false;
      seen.current.add(k);
      if (seen.current.size > maxPoints * 4) seen.current = new Set([...seen.current].slice(-maxPoints * 2));
      return true;
    }) : rows;
    if (!accepted.length) return;
    buffer.current.push(...accepted);
    if (throttleMs <= 0) {
      flush();
      return;
    }
    if (timer.current == null) timer.current = setTimeout(flush, throttleMs);
  }, [dedupeBy, flush, maxPoints, throttleMs]);
  const reset = useCallback4((rows = []) => {
    buffer.current = [];
    seen.current = /* @__PURE__ */ new Set();
    if (timer.current != null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
    setData(rows.slice(-maxPoints));
    setReceived(rows.length);
  }, [maxPoints]);
  useEffect4(() => () => {
    if (timer.current != null) clearTimeout(timer.current);
  }, []);
  return { data, push, reset, received, paused, setPaused };
}
function usePolledStream(fetcher, intervalMs, opts = {}) {
  const stream = useStreamingData(opts);
  const [error, setError] = useState10(null);
  const fetcherRef = useRef10(fetcher);
  fetcherRef.current = fetcher;
  const pushRef = useRef10(stream.push);
  pushRef.current = stream.push;
  useEffect4(() => {
    let alive = true;
    const tick = async () => {
      try {
        const rows = await fetcherRef.current();
        if (!alive || rows == null) return;
        pushRef.current(rows);
        setError(null);
      } catch (e) {
        if (alive) setError(e instanceof Error ? e : new Error(String(e)));
      }
    };
    void tick();
    const id = setInterval(tick, intervalMs);
    return () => {
      alive = false;
      clearInterval(id);
    };
  }, [intervalMs]);
  return { ...stream, error };
}
export {
  Axis,
  BoxPlotChart,
  CandlestickChart,
  CartesianChart,
  ChartFrame,
  ChartSyncProvider,
  ChartTooltip,
  ChordChart,
  EnterpriseChart,
  FunnelChart,
  GaugeChart,
  GeoChart,
  HeatmapChart,
  HistogramChart,
  Legend,
  PALETTE,
  PieChart,
  RAMP,
  RadarChart,
  SEMANTIC,
  SankeyChart,
  Sparkline,
  TreemapChart,
  WaterfallChart,
  aggregateRows,
  bandScale,
  binWidth,
  boxStats,
  chordLayout,
  chordRibbonPath,
  colorAt,
  compact,
  contrastText,
  downloadChart,
  extent,
  fitFeatures,
  formatValue,
  geoCentroid,
  geoPath,
  histogram,
  linearRegression,
  linearScale,
  logScale,
  mix,
  niceDomain,
  projection,
  quantileSorted,
  sankeyLayout,
  sankeyPath,
  sparklineFrom,
  svgToPNG,
  svgToString,
  timeScale,
  useAnimation,
  useBrushPublish,
  useChartGridFilter,
  useChartSync,
  useGridChartLink,
  usePolledGridRows,
  usePolledStream,
  useStreamingData,
  useWidth
};
//# sourceMappingURL=index.mjs.map
