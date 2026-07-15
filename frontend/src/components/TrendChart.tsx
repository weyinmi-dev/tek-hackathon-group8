"use client";

import { useId, useMemo, useState } from "react";

/**
 * Multi-series time-series chart for the site telemetry trends.
 *
 * Hand-rolled SVG, like every other chart in this app — there is no chart library here and adding
 * one for this would be a bigger change than the feature warrants.
 *
 * The one thing it does that the existing one-off charts don't: it treats a null reading as a gap,
 * not a zero. A feed may omit any metric on any poll, and drawing that omission as a plunge to zero
 * would invent an outage that never happened. Gaps break the line.
 */

export type TrendSeries = {
  key: string;
  label: string;
  color: string;
  /** null = not reported at that timestamp. Must be the same length as `timestamps`. */
  values: (number | null)[];
  unit?: string;
};

type Props = {
  timestamps: string[];
  series: TrendSeries[];
  height?: number;
  /** Pin the y-axis, e.g. [0, 100] for a percentage so a flat 99% doesn't fill the frame. */
  domain?: [number, number];
  emptyMessage?: string;
};

export function TrendChart({
  timestamps,
  series,
  height = 180,
  domain,
  emptyMessage = "No telemetry in this range.",
}: Props) {
  const gradientId = useId();
  const [hidden, setHidden] = useState<Set<string>>(new Set());

  const visible = series.filter((s) => !hidden.has(s.key));

  const bounds = useMemo(() => {
    const all = visible
      .flatMap((s) => s.values)
      .filter((v): v is number => v != null && Number.isFinite(v));

    if (all.length === 0) return null;
    if (domain) return { min: domain[0], max: domain[1] };

    let min = Math.min(...all);
    let max = Math.max(...all);

    // A perfectly flat series has zero range and would divide by zero. Give it a band to sit in.
    if (min === max) {
      const pad = Math.abs(min) * 0.1 || 1;
      min -= pad;
      max += pad;
    } else {
      const pad = (max - min) * 0.1;
      min -= pad;
      max += pad;
    }

    return { min, max };
  }, [visible, domain]);

  const hasData = timestamps.length > 1 && bounds != null;

  const W = 100;
  const H = 100;

  const x = (i: number) => (timestamps.length === 1 ? 0 : (i / (timestamps.length - 1)) * W);
  const y = (v: number) => {
    if (!bounds) return H;
    const t = (v - bounds.min) / (bounds.max - bounds.min);
    return H - t * H;
  };

  /**
   * Splits a series into contiguous runs of reported values. Each run becomes its own polyline, so
   * a gap in the data renders as a gap in the line rather than a straight edge across it.
   */
  const segments = (values: (number | null)[]): string[] => {
    const runs: string[] = [];
    let current: string[] = [];

    values.forEach((v, i) => {
      if (v == null || !Number.isFinite(v)) {
        if (current.length > 1) runs.push(current.join(" "));
        current = [];
        return;
      }
      current.push(`${x(i).toFixed(2)},${y(v).toFixed(2)}`);
    });

    if (current.length > 1) runs.push(current.join(" "));
    return runs;
  };

  /** A lone reported point between two gaps has no line to be part of — draw it as a dot. */
  const isolatedPoints = (values: (number | null)[]) =>
    values
      .map((v, i) => ({ v, i }))
      .filter(({ v, i }) => {
        if (v == null || !Number.isFinite(v)) return false;
        const prev = values[i - 1];
        const next = values[i + 1];
        return (prev == null || !Number.isFinite(prev)) && (next == null || !Number.isFinite(next));
      });

  return (
    <div>
      {/* Legend doubles as a series toggle — with seven metrics on one site, being able to
          isolate one is the difference between a chart and a scribble. */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: 12, marginBottom: 10 }}>
        {series.map((s) => {
          const off = hidden.has(s.key);
          return (
            <button
              key={s.key}
              type="button"
              onClick={() =>
                setHidden((prev) => {
                  const next = new Set(prev);
                  if (next.has(s.key)) next.delete(s.key);
                  else next.add(s.key);
                  return next;
                })
              }
              className="mono uppr"
              style={{
                display: "flex",
                alignItems: "center",
                gap: 6,
                background: "none",
                border: "none",
                padding: 0,
                cursor: "pointer",
                fontSize: 9,
                letterSpacing: ".12em",
                color: off ? "var(--ink-4)" : "var(--ink-2)",
                opacity: off ? 0.5 : 1,
              }}
            >
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: 2,
                  background: off ? "var(--ink-4)" : s.color,
                  boxShadow: off ? "none" : `0 0 8px ${s.color}`,
                }}
              />
              {s.label}
            </button>
          );
        })}
      </div>

      {!hasData ? (
        <div
          className="mono"
          style={{
            height,
            display: "grid",
            placeItems: "center",
            color: "var(--ink-4)",
            fontSize: 11,
            border: "1px dashed var(--line)",
            borderRadius: 8,
          }}
        >
          {emptyMessage}
        </div>
      ) : (
        <svg
          viewBox={`0 0 ${W} ${H}`}
          preserveAspectRatio="none"
          style={{ width: "100%", height, display: "block", overflow: "visible" }}
        >
          <defs>
            <linearGradient id={`${gradientId}-fade`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.10" />
              <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
            </linearGradient>
          </defs>

          {/* Horizontal guides at quarters. Vector-effect keeps them hairline despite the
              non-uniform viewBox scaling. */}
          {[0, 25, 50, 75, 100].map((p) => (
            <line
              key={p}
              x1={0}
              x2={W}
              y1={p}
              y2={p}
              stroke="var(--line)"
              strokeWidth={1}
              vectorEffect="non-scaling-stroke"
              opacity={p === 0 || p === 100 ? 0.6 : 0.3}
            />
          ))}

          {visible.map((s) =>
            segments(s.values).map((points, idx) => (
              <polyline
                key={`${s.key}-${idx}`}
                points={points}
                fill="none"
                stroke={s.color}
                strokeWidth={1.6}
                strokeLinejoin="round"
                strokeLinecap="round"
                vectorEffect="non-scaling-stroke"
              />
            )),
          )}

          {visible.map((s) =>
            isolatedPoints(s.values).map(({ v, i }) => (
              <circle
                key={`${s.key}-pt-${i}`}
                cx={x(i)}
                cy={y(v as number)}
                r={2}
                fill={s.color}
                vectorEffect="non-scaling-stroke"
              />
            )),
          )}
        </svg>
      )}

      {hasData && (
        <div
          className="mono"
          style={{
            display: "flex",
            justifyContent: "space-between",
            marginTop: 6,
            fontSize: 9,
            color: "var(--ink-4)",
            letterSpacing: ".1em",
          }}
        >
          <span>{formatTick(timestamps[0])}</span>
          <span>
            {bounds && `${round(bounds.min)} – ${round(bounds.max)}`}
          </span>
          <span>{formatTick(timestamps[timestamps.length - 1])}</span>
        </div>
      )}
    </div>
  );
}

/** Time-range selector shared by every trend on the site page. */
export function RangePicker({
  hours,
  onChange,
}: {
  hours: number;
  onChange: (hours: number) => void;
}) {
  const options: [string, number][] = [
    ["6H", 6],
    ["24H", 24],
    ["7D", 168],
    ["30D", 720],
  ];

  return (
    <div style={{ display: "flex", gap: 4 }}>
      {options.map(([label, value]) => {
        const on = hours === value;
        return (
          <button
            key={value}
            type="button"
            onClick={() => onChange(value)}
            className="mono uppr"
            style={{
              padding: "4px 9px",
              fontSize: 9,
              letterSpacing: ".12em",
              borderRadius: 6,
              cursor: "pointer",
              border: `1px solid ${on ? "var(--accent-line)" : "var(--line)"}`,
              background: on ? "var(--accent-dim)" : "transparent",
              color: on ? "var(--accent)" : "var(--ink-3)",
            }}
          >
            {label}
          </button>
        );
      })}
    </div>
  );
}

function round(v: number): string {
  if (Math.abs(v) >= 100) return v.toFixed(0);
  if (Math.abs(v) >= 10) return v.toFixed(1);
  return v.toFixed(2);
}

function formatTick(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
