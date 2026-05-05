// Generates ready-to-ingest sample log files for the /api/network/ingest pipeline.
//
// Schema mirrors NetworkLogColumns.cs in the backend:
//   - required: timestamp (ISO-8601), tower_code
//   - optional: signal_pct (0..100), load_pct (0..100), latency_ms (>=0), status
//
// Tower codes come from the seeded fleet (NetworkSeeder.cs) so the rows resolve
// to known towers and the analyzer / decision stages produce meaningful alerts
// and optimizations rather than firing against unknown sites.
//
// Row mix is intentional:
//   - healthy baselines on TWR-VI-002 / TWR-IKJ-007 / TWR-LEK-014  (no anomaly)
//   - degraded congestion on TWR-IKJ-019 / TWR-LAG-W-022           (warn-tier)
//   - critical fiber cut on TWR-LEK-003                            (sustained crit)
//   - intermittent backhaul on TWR-LAG-W-014                       (flapping)
//   - elevated diesel-driven load on TWR-AGE-009                   (warn-tier)

type SampleRow = {
  timestamp: string;
  tower_code: string;
  signal_pct: number;
  load_pct: number;
  latency_ms: number;
  status: string;
};

const HEADERS = ["timestamp", "tower_code", "signal_pct", "load_pct", "latency_ms", "status"] as const;

function isoMinusMinutes(base: Date, minutes: number): string {
  const d = new Date(base.getTime() - minutes * 60_000);
  return d.toISOString();
}

function buildRows(): SampleRow[] {
  // Anchor every sample run at the moment the user clicks the button so the
  // events look "fresh" (the most recent row is ~now, the oldest ~3 hours ago).
  const now = new Date();
  const rows: SampleRow[] = [];

  // 1. Critical fiber cut at TWR-LEK-003 — stable critical signal collapse.
  for (let i = 0; i < 6; i++) {
    rows.push({
      timestamp: isoMinusMinutes(now, 5 * i + 1),
      tower_code: "TWR-LEK-003",
      signal_pct: 18 - Math.min(8, i),         // 18 → 10
      load_pct: 12,
      latency_ms: 420 + i * 40,                // 420 → 620
      status: i === 0 ? "fiber_cut" : "critical",
    });
  }

  // 2. Backhaul flapping at TWR-LAG-W-014 — alternating critical/recovering.
  for (let i = 0; i < 8; i++) {
    const flap = i % 2 === 0;
    rows.push({
      timestamp: isoMinusMinutes(now, 8 * i + 2),
      tower_code: "TWR-LAG-W-014",
      signal_pct: flap ? 22 : 58,
      load_pct: flap ? 36 : 64,
      latency_ms: flap ? 380 : 95,
      status: flap ? "backhaul_degraded" : "recovering",
    });
  }

  // 3. Congestion overflow at TWR-IKJ-019 — warn-tier sustained high load.
  for (let i = 0; i < 6; i++) {
    rows.push({
      timestamp: isoMinusMinutes(now, 6 * i + 4),
      tower_code: "TWR-IKJ-019",
      signal_pct: 62 - i,                      // 62 → 57
      load_pct: 88 + Math.min(8, i),           // 88 → 96
      latency_ms: 165 + i * 5,
      status: "congested",
    });
  }

  // 4. Elevated packet loss at TWR-LAG-W-022.
  for (let i = 0; i < 5; i++) {
    rows.push({
      timestamp: isoMinusMinutes(now, 7 * i + 3),
      tower_code: "TWR-LAG-W-022",
      signal_pct: 55 - i,
      load_pct: 81,
      latency_ms: 210 + i * 15,
      status: "packet_loss",
    });
  }

  // 5. Diesel-driven load spike at TWR-AGE-009 (energy-correlated warn).
  for (let i = 0; i < 4; i++) {
    rows.push({
      timestamp: isoMinusMinutes(now, 9 * i + 5),
      tower_code: "TWR-AGE-009",
      signal_pct: 72,
      load_pct: 79 + i,
      latency_ms: 140,
      status: "elevated_load",
    });
  }

  // 6. Healthy baselines (negative samples — analyzer should NOT alert).
  const healthy: Array<[string, number, number, number]> = [
    ["TWR-VI-002",  92, 59, 38],
    ["TWR-IKJ-007", 88, 54, 41],
    ["TWR-LEK-014", 91, 62, 35],
    ["TWR-IKO-011", 87, 51, 44],
    ["TWR-VI-005",  90, 66, 39],
  ];
  for (const [code, sig, load, lat] of healthy) {
    for (let i = 0; i < 3; i++) {
      rows.push({
        timestamp: isoMinusMinutes(now, 12 * i + 6),
        tower_code: code,
        signal_pct: sig + (i % 2 === 0 ? 0 : -1),
        load_pct: load + (i % 2 === 0 ? 0 : 2),
        latency_ms: lat + i * 2,
        status: "ok",
      });
    }
  }

  // 7. Crowd-sourced report row at TWR-OJO-002 — useful for KB ingestion path.
  rows.push({
    timestamp: isoMinusMinutes(now, 11),
    tower_code: "TWR-OJO-002",
    signal_pct: 68,
    load_pct: 79,
    latency_ms: 175,
    status: "user_reports_spike",
  });

  // Most-recent first → most-recent last so the file reads chronologically.
  rows.sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  return rows;
}

function escapeCsvCell(value: string): string {
  if (value.includes(",") || value.includes("\"") || value.includes("\n")) {
    return `"${value.replace(/"/g, "\"\"")}"`;
  }
  return value;
}

export function buildSampleCsv(): string {
  const rows = buildRows();
  const lines: string[] = [HEADERS.join(",")];
  for (const r of rows) {
    lines.push([
      r.timestamp,
      r.tower_code,
      String(r.signal_pct),
      String(r.load_pct),
      String(r.latency_ms),
      escapeCsvCell(r.status),
    ].join(","));
  }
  return lines.join("\n") + "\n";
}

// TxtNetworkLogParser is the tab-delimited variant of the CSV parser. Same
// canonical headers, tabs instead of commas, no cell quoting (status values
// are already token-shaped so tabs/quotes don't appear inside cells).
export function buildSampleTxt(): string {
  const rows = buildRows();
  const lines: string[] = [HEADERS.join("\t")];
  for (const r of rows) {
    lines.push([
      r.timestamp,
      r.tower_code,
      String(r.signal_pct),
      String(r.load_pct),
      String(r.latency_ms),
      r.status,
    ].join("\t"));
  }
  return lines.join("\n") + "\n";
}

export function downloadBlob(filename: string, content: string, mime: string): void {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  // Give the browser a tick to start the download before we revoke.
  setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

export function downloadSampleTemplates(): void {
  downloadBlob("network-events-sample.csv", buildSampleCsv(), "text/csv;charset=utf-8");
  downloadBlob("network-events-sample.txt", buildSampleTxt(), "text/tab-separated-values;charset=utf-8");
}
