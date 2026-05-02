// Static demo data for the Energy / Anomalies / Optimize pages.
// Backend currently has no /sites or /anomalies endpoints — these come straight
// from the design handoff (test-hk-handoff/src/data.jsx) so the energy module
// renders identically to the prototype until the real APIs land.

export type SiteHealth = "ok" | "degraded" | "critical";
export type SiteSource = "grid" | "generator" | "battery" | "solar";

export type EnergySite = {
  id: string;
  name: string;
  region: string;
  source: SiteSource;
  battPct: number;
  dieselPct: number;
  solarKw: number;
  gridUp: boolean;
  dailyDiesel: number;
  costNGN: number;
  health: SiteHealth;
  anomaly: string | null;
  uptime: number;
  solar: boolean;
};

export type EnergyKpi = {
  label: string;
  value: string;
  unit: string;
  delta: string;
  trend: "up" | "down";
  sub: string;
};

export type AnomalyKind =
  | "fuel-theft"
  | "sensor-offline"
  | "gen-overuse"
  | "battery-degrade"
  | "predicted-fault";

export type AnomalyEvent = {
  id: string;
  site: string;
  kind: AnomalyKind;
  sev: "critical" | "warn" | "info";
  t: string;
  detail: string;
  conf: number;
};

export const SITES: EnergySite[] = [
  { id:"TWR-LAG-W-014", name:"Surulere", region:"Lagos West",      source:"generator", battPct:62, dieselPct:38, solarKw:0,    gridUp:false, dailyDiesel:84, costNGN:148000, health:"critical", anomaly:"Fuel drop −18L overnight (theft suspected)", uptime:97.2, solar:false },
  { id:"TWR-LAG-W-022", name:"Mushin",   region:"Lagos West",      source:"battery",   battPct:74, dieselPct:71, solarKw:2.8,  gridUp:false, dailyDiesel:42, costNGN:74000,  health:"degraded", anomaly:"Battery cycle count 1,840 — replace in 30d", uptime:99.1, solar:true },
  { id:"TWR-LAG-W-031", name:"Yaba N.",  region:"Lagos West",      source:"grid",      battPct:88, dieselPct:84, solarKw:3.4,  gridUp:true,  dailyDiesel:18, costNGN:32000,  health:"degraded", anomaly:"Predicted gen fault 2h (thermal trend)", uptime:99.4, solar:true },
  { id:"TWR-IKJ-007",   name:"Ikeja GRA",region:"Ikeja",           source:"grid",      battPct:92, dieselPct:96, solarKw:4.2,  gridUp:true,  dailyDiesel:6,  costNGN:11000,  health:"ok",       anomaly:null, uptime:99.9, solar:true },
  { id:"TWR-IKJ-019",   name:"Allen",    region:"Ikeja",           source:"grid",      battPct:81, dieselPct:80, solarKw:3.1,  gridUp:true,  dailyDiesel:12, costNGN:21000,  health:"ok",       anomaly:null, uptime:99.7, solar:true },
  { id:"TWR-IKJ-021",   name:"Maryland", region:"Ikeja",           source:"grid",      battPct:90, dieselPct:88, solarKw:3.8,  gridUp:true,  dailyDiesel:8,  costNGN:14000,  health:"ok",       anomaly:null, uptime:99.8, solar:true },
  { id:"TWR-LEK-003",   name:"Lekki P1", region:"Lekki",           source:"battery",   battPct:24, dieselPct:12, solarKw:0,    gridUp:false, dailyDiesel:96, costNGN:172000, health:"critical", anomaly:"Diesel critically low — refuel ETA 4h", uptime:96.4, solar:false },
  { id:"TWR-LEK-008",   name:"Lekki P2", region:"Lekki",           source:"generator", battPct:58, dieselPct:54, solarKw:2.4,  gridUp:false, dailyDiesel:62, costNGN:108000, health:"degraded", anomaly:"Gen runtime +28% vs baseline", uptime:98.8, solar:true },
  { id:"TWR-LEK-014",   name:"Ajah",     region:"Lekki",           source:"solar",     battPct:84, dieselPct:90, solarKw:5.6,  gridUp:false, dailyDiesel:14, costNGN:24000,  health:"ok",       anomaly:null, uptime:99.6, solar:true },
  { id:"TWR-VI-002",    name:"V.I. 2",   region:"Victoria Island", source:"grid",      battPct:94, dieselPct:91, solarKw:4.8,  gridUp:true,  dailyDiesel:4,  costNGN:7000,   health:"ok",       anomaly:null, uptime:99.95, solar:true },
  { id:"TWR-VI-005",    name:"Eko",      region:"Victoria Island", source:"grid",      battPct:88, dieselPct:85, solarKw:4.4,  gridUp:true,  dailyDiesel:7,  costNGN:12000,  health:"ok",       anomaly:null, uptime:99.92, solar:true },
  { id:"TWR-IKO-011",   name:"Ikoyi S.", region:"Ikoyi",           source:"grid",      battPct:86, dieselPct:79, solarKw:3.6,  gridUp:true,  dailyDiesel:9,  costNGN:16000,  health:"ok",       anomaly:null, uptime:99.85, solar:true },
  { id:"TWR-APP-004",   name:"Apapa",    region:"Apapa",           source:"generator", battPct:68, dieselPct:62, solarKw:0,    gridUp:false, dailyDiesel:54, costNGN:94000,  health:"degraded", anomaly:null, uptime:99.0, solar:false },
  { id:"TWR-AGE-009",   name:"Agege",    region:"Agege",           source:"battery",   battPct:72, dieselPct:78, solarKw:2.6,  gridUp:false, dailyDiesel:24, costNGN:42000,  health:"ok",       anomaly:null, uptime:99.5, solar:true },
  { id:"TWR-OJO-002",   name:"Festac",   region:"Festac",          source:"generator", battPct:48, dieselPct:44, solarKw:0,    gridUp:false, dailyDiesel:72, costNGN:126000, health:"critical", anomaly:"Fuel sensor offline 3h — manual check req.", uptime:97.8, solar:false },
];

export const ENERGY_KPIS: EnergyKpi[] = [
  { label:"Diesel · 24h",      value:"8,420", unit:"L",     delta:"-18%",   trend:"up",   sub:"vs baseline · saved ₦2.1M" },
  { label:"OPEX · today",      value:"₦14.7", unit:"M",     delta:"-₦3.2M", trend:"up",   sub:"AI optimization active" },
  { label:"Sites on Solar",    value:"68",    unit:"/ 154", delta:"+4",     trend:"up",   sub:"44% renewable mix" },
  { label:"Fleet Uptime",      value:"99.21", unit:"%",     delta:"-0.14",  trend:"down", sub:"4 sites in critical" },
  { label:"Theft Events · 7d", value:"3",     unit:"",      delta:"-2",     trend:"up",   sub:"Surulere, Festac, Apapa" },
  { label:"Battery Health",    value:"87.4",  unit:"%",     delta:"-0.6",   trend:"down", sub:"avg fleet SoH" },
];

export const DIESEL_TRACE = [820,790,760,740,720,700,680,520,420,360,310,280,260,250,240,260,310,400,520,640,720,780,810,840];
export const FUEL_TRACE_THEFT = [86,86,85,85,84,84,84,83,83,82,82,81,81,80,80,80,79,79,61,60,60,59,59,58];
export const COST_BASELINE = [21,22,21,22,23,21,22,22,21,22,23,21,22,21,22,22,23,21,22,22,21,23,22,21,22,22,23,21,22,22];
export const COST_WITH_AI  = [21,20,19,18,18,17,17,16,16,16,15,15,15,15,14,14,14,15,15,14,14,15,14,14,15,14,15,14,14,15];

export const ANOMALY_EVENTS: AnomalyEvent[] = [
  { id:"ANO-441", site:"TWR-LAG-W-014", kind:"fuel-theft",      sev:"critical", t:"04:18", detail:"Fuel level dropped 18L in 6 minutes — outside refill window. No work order.", conf:0.94 },
  { id:"ANO-440", site:"TWR-OJO-002",   kind:"sensor-offline",  sev:"warn",     t:"14:02", detail:"Fuel sensor stopped reporting 3h ago. Manual reading recommended.", conf:0.88 },
  { id:"ANO-439", site:"TWR-LEK-008",   kind:"gen-overuse",     sev:"warn",     t:"09:45", detail:"Generator runtime +28% vs 30d baseline. Possible inefficient load profile.", conf:0.81 },
  { id:"ANO-438", site:"TWR-LAG-W-022", kind:"battery-degrade", sev:"info",     t:"08:11", detail:"Cycle count 1,840 — projected end-of-life in 30 days.", conf:0.91 },
  { id:"ANO-437", site:"TWR-LAG-W-031", kind:"predicted-fault", sev:"warn",     t:"06:30", detail:"Generator thermal trend + load → 87% fault probability by 18:42.", conf:0.87 },
  { id:"ANO-436", site:"TWR-APP-004",   kind:"fuel-theft",      sev:"info",     t:"02:14", detail:"Minor anomaly: fuel level −4L outside refill window. Below alert threshold.", conf:0.62 },
];
