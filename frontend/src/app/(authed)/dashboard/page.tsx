"use client";

import { observer } from "mobx-react-lite";
import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { NetworkMap } from "@/components/NetworkMap";
import { Card, KPI, Pill } from "@/components/UI";
import { Copilot } from "@/components/Copilot";
import { api } from "@/lib/api";
import { useSyncStore } from "@/lib/stores/StoreProvider";
import type { Alert, MapResponse, MetricsResponse, Tower } from "@/lib/types";

const SPARK_COLORS = [
  "var(--accent)",
  "var(--warn)",
  "var(--crit)",
  "var(--info)",
  "var(--crit)",
  "var(--accent)",
];

/**
 * Seconds each alert spends crossing the live feed. THIS is the speed knob — raise it to slow the
 * ticker down, lower it to speed it up.
 *
 * It is per-alert, not a fixed duration for the whole strip, and that matters. The strip is the alert
 * list rendered twice (so the loop is seamless), so a fixed duration meant the more alerts you had,
 * the further the strip had to travel in the same time — the ticker sped up exactly when there was
 * most to read, and a busy NOC got the least legible feed. Pinning the time *per item* keeps the
 * scroll speed constant no matter how many alerts are live.
 */
const TICKER_SECONDS_PER_ALERT = 18;

/** Floor for a near-empty feed, so one or two alerts don't strobe past. */
const TICKER_MIN_SECONDS = 60;

function CommandCenterPage() {
  // Re-fetch when an upload lands anywhere in the app — SyncStore bumps `version` and every
  // page that renders synchronised data reloads itself. No manual refresh, no websocket.
  const sync = useSyncStore();
  const [metrics, setMetrics] = useState<MetricsResponse | null>(null);
  const [map, setMap] = useState<MapResponse | null>(null);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [sel, setSel] = useState<Tower | null>(null);

  // Fetch the three feeds independently. Previously this used
  // `Promise.all([metrics, map, alerts])` with a swallowing catch — if any
  // single endpoint failed (e.g. alerts → 500 from a transient OSM / Redis
  // hiccup in geo enrichment), the catch would fire and ALL THREE state
  // values would stay at their initial empty/null, leaving the dashboard
  // completely blank with no error visible to the operator.
  //
  // We now treat each feed as independent: a failure on one logs a console
  // warning and leaves the other two panels rendered. Geo enrichment on the
  // alerts endpoint is best-effort, so it should never fail in practice —
  // but if it ever does, the map and metrics still render.
  useEffect(() => {
    let alive = true;
    async function loadOne<T>(
      label: string,
      fn: () => Promise<T>,
      apply: (v: T) => void,
    ): Promise<void> {
      try {
        const v = await fn();
        if (alive) apply(v);
      } catch (e) {
        console.warn(`[dashboard] ${label} fetch failed:`, e);
      }
    }
    async function load(): Promise<void> {
      await Promise.allSettled([
        loadOne("metrics", () => api.metrics(), setMetrics),
        loadOne(
          "map",
          () => api.map(),
          (mp) => {
            setMap(mp);
            // Only override the selection if nothing's selected yet — preserves the
            // operator's pin across 30s refreshes.
            setSel(
              (cur) =>
                cur ??
                mp.towers.find((t) => t.status === "critical") ??
                mp.towers[0] ??
                null,
            );
          },
        ),
        loadOne("alerts", () => api.alerts(), setAlerts),
      ]);
    }
    void load();
    const i = setInterval(() => void load(), 30_000);
    return () => {
      alive = false;
      clearInterval(i);
    };
  }, [sync.version]);

  // Constant scroll speed regardless of how many alerts are live — see TICKER_SECONDS_PER_ALERT.
  const tickerSeconds = Math.max(
    TICKER_MIN_SECONDS,
    alerts.length * TICKER_SECONDS_PER_ALERT,
  );

  const sparks = metrics?.sparks;
  const sparkBy = (i: number): number[] => {
    if (!sparks) return [];
    return (
      [
        sparks.uptime,
        sparks.latency,
        sparks.incident,
        sparks.towers,
        sparks.subs,
        sparks.queries,
      ][i] ?? []
    );
  };

  const crit = alerts.filter((a) => a.sev === "critical").length;
  const warn = alerts.filter((a) => a.sev === "warn").length;

  return (
    <>
      <TopBar
        title="Command Center"
        sub="Real-time NOC view · map · copilot · alerts"
        right={
          <div style={{ display: "flex", gap: 8 }}>
            <Pill tone="crit" dot>
              {crit} CRITICAL
            </Pill>
            <Pill tone="warn" dot>
              {warn} WARN
            </Pill>
            <Pill tone="ok" dot>
              SLA 99.85%
            </Pill>
          </div>
        }
      />
      <div
        style={{
          padding: 14,
          display: "grid",
          gridTemplateColumns: "1.4fr 1fr",
          gridTemplateRows: "auto 1fr",
          gap: 12,
          height: "calc(100vh - 67px)",
        }}
      >
        <div
          style={{
            gridColumn: "1 / -1",
            display: "grid",
            gridTemplateColumns: "repeat(6,1fr)",
            gap: 10,
          }}
        >
          {(metrics?.kpis ?? []).map((k, i) => (
            <KPI
              key={k.label}
              {...k}
              spark={sparkBy(i)}
              color={SPARK_COLORS[i]}
            />
          ))}
        </div>

        {/* Map (left) */}
        <div
          style={{
            position: "relative",
            minHeight: 0,
            display: "flex",
            flexDirection: "column",
            gap: 10,
          }}
        >
          <div style={{ flex: 1, minHeight: 0, position: "relative" }}>
            {map && (
              <NetworkMap
                towers={map.towers}
                onSelect={setSel}
                selectedId={sel?.id}
              />
            )}
          </div>
          <Card pad={0} style={{ overflow: "hidden" }}>
            <div style={{ display: "flex", alignItems: "center" }}>
              <div
                className="mono uppr"
                style={{
                  fontSize: 9.5,
                  color: "var(--crit)",
                  letterSpacing: ".14em",
                  padding: "10px 12px",
                  borderRight: "1px solid var(--line)",
                  display: "flex",
                  alignItems: "center",
                  gap: 6,
                  flexShrink: 0,
                  background: "rgba(255,84,112,.08)",
                }}
              >
                <span className="dot crit" />
                LIVE FEED
              </div>
              <div
                style={{
                  flex: 1,
                  overflow: "hidden",
                  position: "relative",
                  height: 38,
                }}
              >
                <div
                  style={{
                    display: "flex",
                    gap: 32,
                    position: "absolute",
                    whiteSpace: "nowrap",
                    animation: `ticker ${tickerSeconds}s linear infinite`,
                    padding: "10px 0",
                    fontFamily: "var(--mono)",
                    fontSize: 11,
                  }}
                >
                  {[...alerts, ...alerts].map((a, i) => (
                    <span
                      key={i}
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 8,
                      }}
                    >
                      <span
                        style={{
                          color:
                            a.sev === "critical"
                              ? "var(--crit)"
                              : a.sev === "warn"
                                ? "var(--warn)"
                                : "var(--info)",
                        }}
                      >
                        ● {a.id}
                      </span>
                      <span style={{ color: "var(--ink-2)" }}>{a.title}</span>
                      <span style={{ color: "var(--ink-3)" }}>· {a.tower}</span>
                      <span style={{ color: "var(--ink-3)" }}>· {a.time}</span>
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </Card>
        </div>

        {/* Copilot (right) */}
        <Card
          pad={0}
          style={{
            display: "flex",
            flexDirection: "column",
            minHeight: 0,
            overflow: "hidden",
          }}
        >
          <div
            style={{
              padding: "12px 14px",
              borderBottom: "1px solid var(--line)",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
            }}
          >
            <div
              className="mono uppr"
              style={{
                fontSize: 10,
                color: "var(--ink-3)",
                letterSpacing: ".14em",
                display: "flex",
                alignItems: "center",
                gap: 8,
              }}
            >
              <span
                style={{
                  display: "inline-block",
                  width: 6,
                  height: 6,
                  borderRadius: "50%",
                  background: "var(--accent)",
                  boxShadow: "0 0 8px var(--accent)",
                }}
              />
              COPILOT · ASK THE NETWORK
            </div>
            <div
              className="mono"
              style={{ fontSize: 9.5, color: "var(--ink-3)" }}
            >
              3 SK SKILLS · AZURE OPENAI
            </div>
          </div>
          <Copilot embedded />
        </Card>
      </div>
    </>
  );
}

export default observer(CommandCenterPage);
