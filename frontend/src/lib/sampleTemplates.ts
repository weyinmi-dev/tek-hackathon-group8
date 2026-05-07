// Generates ready-to-ingest sample log files for the /api/network/ingest pipeline.
// Each row covers BOTH the operations (network) and energy domains so a single
// upload exercises the full picture from the project diagram — network maps,
// alerts, anomalies, optimizations, dashboard insights and copilot KB.
//
// Operations schema (consumed today by NetworkLogColumns.cs):
//   - required: timestamp (ISO-8601), tower_code
//   - optional: signal_pct (0..100), load_pct (0..100), latency_ms (>=0), status
//
// Energy schema (mirrors EnergySiteDto / EnergyAnomalyDto on the wire):
//   - energy_source (grid | generator | battery | solar)
//   - batt_pct (0..100), diesel_pct (0..100), solar_kw (>=0)
//   - grid_up (true | false)
//   - fuel_litres (>=0), gen_runtime_hrs (>=0), uptime_pct (0..100)
//   - cost_ngn (>=0)  — daily cost contribution at this sample
//
// The current network parser silently ignores any column it does not know
// about (CsvHelper config sets MissingFieldFound = null and the HeaderIndex
// only resolves canonical names). So the extra energy columns are forward-
// compatible: they ride along today and become first-class as soon as an
// energy parser is wired into NetworkLogParserRegistry.
//
// Tower codes come from the seeded fleet (NetworkSeeder.cs) — the ops side
// resolves them to towers, and the energy side maps them 1:1 to sites since
// EnergySiteDto.id is the same tower code.
//
// Row mix is intentional, scoped to span every kind of anomaly the system
// understands today:
//   - TWR-LEK-003     critical fiber cut + genset overrun  (ops crit, energy gen-overuse)
//   - TWR-LAG-W-014   backhaul flapping + battery low      (ops warn, energy battery-degrade)
//   - TWR-IKJ-019     congestion overflow                  (ops warn)
//   - TWR-LAG-W-022   elevated packet loss + sensor drop   (ops warn, energy sensor-offline)
//   - TWR-AGE-009     diesel-driven load + fuel theft      (ops warn, energy fuel-theft)
//   - TWR-APP-004     grid outage, solar holding           (energy predicted-fault)
//   - TWR-OJO-002     crowd-sourced report spike           (ops info)
//   - TWR-VI-002 / TWR-IKJ-007 / TWR-LEK-014 / TWR-IKO-011 / TWR-VI-005
//                     healthy baselines on both sides      (no alerts)

type EnergySource = "grid" | "generator" | "battery" | "solar";

type SampleRow = {
  // Operations
  timestamp: string;
  tower_code: string;
  signal_pct: number;
  load_pct: number;
  latency_ms: number;
  status: string;
  // Energy
  energy_source: EnergySource;
  batt_pct: number;
  diesel_pct: number;
  solar_kw: number;        // current generation, kW
  grid_up: boolean;
  fuel_litres: number;     // litres remaining in tank
  gen_runtime_hrs: number; // generator hours since last service
  uptime_pct: number;
  cost_ngn: number;        // daily-cost contribution at this sample, NGN
};

const HEADERS = [
  "timestamp", "tower_code",
  "signal_pct", "load_pct", "latency_ms", "status",
  "energy_source", "batt_pct", "diesel_pct", "solar_kw", "grid_up",
  "fuel_litres", "gen_runtime_hrs", "uptime_pct", "cost_ngn",
] as const;

function isoMinusMinutes(base: Date, minutes: number): string {
  const d = new Date(base.getTime() - minutes * 60_000);
  return d.toISOString();
}

// Ergonomic builder — defaults the healthy/baseline values, callers override
// only the fields that change for a given scenario. Keeps row construction
// terse without obscuring intent.
type RowOverrides = Partial<SampleRow> & Pick<SampleRow, "timestamp" | "tower_code">;

function row(over: RowOverrides): SampleRow {
  return {
    signal_pct: 88,
    load_pct: 55,
    latency_ms: 42,
    status: "ok",
    energy_source: "grid",
    batt_pct: 92,
    diesel_pct: 80,
    solar_kw: 8.0,
    grid_up: true,
    fuel_litres: 480,
    gen_runtime_hrs: 612,
    uptime_pct: 99,
    cost_ngn: 18_000,
    ...over,
  };
}

function buildRows(): SampleRow[] {
  // Anchor every sample at the moment the user clicks the button so the events
  // look "fresh" — the most recent row is ~now, the oldest ~3 hours ago.
  const now = new Date();
  const rows: SampleRow[] = [];

  // 1. Critical fiber cut at TWR-LEK-003 — ops crit + genset overrun energy-side.
  for (let i = 0; i < 6; i++) {
    rows.push(row({
      timestamp: isoMinusMinutes(now, 5 * i + 1),
      tower_code: "TWR-LEK-003",
      signal_pct: 18 - Math.min(8, i),
      load_pct: 12,
      latency_ms: 420 + i * 40,
      status: i === 0 ? "fiber_cut" : "critical",
      energy_source: "generator",
      batt_pct: 38 - i,
      diesel_pct: 64 - i * 2,
      solar_kw: 0.0,
      grid_up: false,
      fuel_litres: 280 - i * 6,
      gen_runtime_hrs: 1480 + i,        // well past 1000h → gen-overuse
      uptime_pct: 81,
      cost_ngn: 41_000 + i * 1_500,
    }));
  }

  // 2. Backhaul flapping at TWR-LAG-W-014 — ops warn + battery degrade.
  for (let i = 0; i < 8; i++) {
    const flap = i % 2 === 0;
    rows.push(row({
      timestamp: isoMinusMinutes(now, 8 * i + 2),
      tower_code: "TWR-LAG-W-014",
      signal_pct: flap ? 22 : 58,
      load_pct: flap ? 36 : 64,
      latency_ms: flap ? 380 : 95,
      status: flap ? "backhaul_degraded" : "recovering",
      energy_source: flap ? "battery" : "grid",
      batt_pct: flap ? 12 : 28,         // sub-30% pinned → battery-degrade
      diesel_pct: 55,
      solar_kw: 4.2,
      grid_up: !flap,
      fuel_litres: 220,
      gen_runtime_hrs: 740,
      uptime_pct: 88,
      cost_ngn: 22_500,
    }));
  }

  // 3. Congestion overflow at TWR-IKJ-019 — ops warn-tier sustained high load.
  for (let i = 0; i < 6; i++) {
    rows.push(row({
      timestamp: isoMinusMinutes(now, 6 * i + 4),
      tower_code: "TWR-IKJ-019",
      signal_pct: 62 - i,
      load_pct: 88 + Math.min(8, i),
      latency_ms: 165 + i * 5,
      status: "congested",
      energy_source: "grid",
      batt_pct: 86,
      diesel_pct: 72,
      solar_kw: 9.1,
      grid_up: true,
      fuel_litres: 410,
      gen_runtime_hrs: 510,
      uptime_pct: 97,
      cost_ngn: 19_800,
    }));
  }

  // 4. Elevated packet loss at TWR-LAG-W-022 — ops warn + energy sensor-offline.
  for (let i = 0; i < 5; i++) {
    rows.push(row({
      timestamp: isoMinusMinutes(now, 7 * i + 3),
      tower_code: "TWR-LAG-W-022",
      signal_pct: 55 - i,
      load_pct: 81,
      latency_ms: 210 + i * 15,
      status: i >= 2 ? "sensor_offline" : "packet_loss",
      energy_source: "grid",
      batt_pct: i >= 2 ? 0 : 78,        // 0% pinned across multiple samples → sensor-offline
      diesel_pct: i >= 2 ? 0 : 66,
      solar_kw: i >= 2 ? 0.0 : 7.8,
      grid_up: true,
      fuel_litres: 360,
      gen_runtime_hrs: 425,
      uptime_pct: 94,
      cost_ngn: 18_400,
    }));
  }

  // 5. Diesel-driven load spike at TWR-AGE-009 — ops warn + fuel theft.
  for (let i = 0; i < 4; i++) {
    rows.push(row({
      timestamp: isoMinusMinutes(now, 9 * i + 5),
      tower_code: "TWR-AGE-009",
      signal_pct: 72,
      load_pct: 79 + i,
      latency_ms: 140,
      status: i >= 1 ? "fuel_theft_suspected" : "elevated_load",
      energy_source: "generator",
      batt_pct: 64,
      diesel_pct: 70 - i * 18,          // 70 → 16: ~54pt drop in ~30min → fuel-theft
      solar_kw: 0.0,
      grid_up: false,
      fuel_litres: 380 - i * 90,        // 380 → 110, mirrors the % drop
      gen_runtime_hrs: 902,
      uptime_pct: 92,
      cost_ngn: 27_500 + i * 2_000,
    }));
  }

  // 6. Grid outage at TWR-APP-004 — solar carrying, predicted fault on battery.
  for (let i = 0; i < 4; i++) {
    rows.push(row({
      timestamp: isoMinusMinutes(now, 10 * i + 7),
      tower_code: "TWR-APP-004",
      signal_pct: 84,
      load_pct: 71,
      latency_ms: 78,
      status: "grid_outage",
      energy_source: "solar",
      batt_pct: 58 - i * 6,             // declining → predicted-fault candidate
      diesel_pct: 88,
      solar_kw: 11.4 - i * 0.5,
      grid_up: false,
      fuel_litres: 520,
      gen_runtime_hrs: 198,
      uptime_pct: 96,
      cost_ngn: 14_900,
    }));
  }

  // 7. Crowd-sourced report row at TWR-OJO-002 — info-tier, useful for KB.
  rows.push(row({
    timestamp: isoMinusMinutes(now, 11),
    tower_code: "TWR-OJO-002",
    signal_pct: 68,
    load_pct: 79,
    latency_ms: 175,
    status: "user_reports_spike",
    energy_source: "grid",
    batt_pct: 81,
    diesel_pct: 58,
    solar_kw: 6.4,
    grid_up: true,
    fuel_litres: 295,
    gen_runtime_hrs: 388,
    uptime_pct: 95,
    cost_ngn: 17_200,
  }));

  // 8. Healthy baselines (negative samples — analyzer should NOT alert on these).
  const healthy: Array<[string, EnergySource, number]> = [
    ["TWR-VI-002",  "grid",      14.2],
    ["TWR-IKJ-007", "grid",       9.8],
    ["TWR-LEK-014", "solar",     16.5],
    ["TWR-IKO-011", "grid",       8.6],
    ["TWR-VI-005",  "grid",      12.0],
  ];
  for (const [code, source, solarKw] of healthy) {
    for (let i = 0; i < 3; i++) {
      rows.push(row({
        timestamp: isoMinusMinutes(now, 12 * i + 6),
        tower_code: code,
        signal_pct: 90 + (i % 2 === 0 ? 0 : -1),
        load_pct: 58 + (i % 2 === 0 ? 0 : 2),
        latency_ms: 38 + i * 2,
        status: "ok",
        energy_source: source,
        batt_pct: 94 - (i % 2),
        diesel_pct: 82,
        solar_kw: solarKw,
        grid_up: true,
        fuel_litres: 460,
        gen_runtime_hrs: 540 + i,
        uptime_pct: 99,
        cost_ngn: 16_500,
      }));
    }
  }

  // Sort chronologically so the file reads top-down old → new.
  rows.sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  return rows;
}

function escapeCsvCell(value: string): string {
  if (value.includes(",") || value.includes("\"") || value.includes("\n")) {
    return `"${value.replace(/"/g, "\"\"")}"`;
  }
  return value;
}

function rowCells(r: SampleRow): string[] {
  return [
    r.timestamp,
    r.tower_code,
    String(r.signal_pct),
    String(r.load_pct),
    String(r.latency_ms),
    r.status,
    r.energy_source,
    String(r.batt_pct),
    String(r.diesel_pct),
    r.solar_kw.toFixed(1),
    String(r.grid_up),
    String(r.fuel_litres),
    String(r.gen_runtime_hrs),
    String(r.uptime_pct),
    String(r.cost_ngn),
  ];
}

export function buildSampleCsv(): string {
  const rows = buildRows();
  const lines: string[] = [HEADERS.join(",")];
  for (const r of rows) {
    lines.push(rowCells(r).map(escapeCsvCell).join(","));
  }
  return lines.join("\n") + "\n";
}

// TxtNetworkLogParser is the tab-delimited variant of the CSV parser. Same
// canonical headers, tabs instead of commas, no cell quoting (status / source
// values are token-shaped so tabs/quotes never appear inside cells).
export function buildSampleTxt(): string {
  const rows = buildRows();
  const lines: string[] = [HEADERS.join("\t")];
  for (const r of rows) {
    lines.push(rowCells(r).join("\t"));
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
