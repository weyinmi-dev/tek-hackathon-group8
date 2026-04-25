// Simulated network data for TelcoPilot demo
// Lagos metro area — towers, alerts, KPIs, audit log, users.

const TOWERS = [
  { id:'TWR-LAG-W-014', name:'Lagos West / Surulere', lat:6.500, lng:3.353, x:18, y:62, status:'critical', signal:32, load:94, region:'Lagos West', issue:'Backhaul fiber degradation' },
  { id:'TWR-LAG-W-022', name:'Mushin Tower', lat:6.530, lng:3.355, x:24, y:55, status:'warn', signal:58, load:81, region:'Lagos West', issue:'Elevated packet loss' },
  { id:'TWR-LAG-W-031', name:'Yaba North', lat:6.510, lng:3.378, x:32, y:60, status:'warn', signal:64, load:74, region:'Lagos West', issue:'Predicted failure 2h' },
  { id:'TWR-IKJ-007',  name:'Ikeja GRA', lat:6.605, lng:3.349, x:30, y:30, status:'ok', signal:88, load:54, region:'Ikeja', issue:null },
  { id:'TWR-IKJ-019',  name:'Ikeja Allen', lat:6.598, lng:3.358, x:36, y:34, status:'warn', signal:62, load:78, region:'Ikeja', issue:'Packet loss anomaly' },
  { id:'TWR-IKJ-021',  name:'Maryland Mall', lat:6.572, lng:3.371, x:42, y:42, status:'ok', signal:91, load:48, region:'Ikeja', issue:null },
  { id:'TWR-LEK-003',  name:'Lekki Phase 1', lat:6.448, lng:3.475, x:74, y:74, status:'critical', signal:18, load:12, region:'Lekki', issue:'60% packet loss — fiber cut' },
  { id:'TWR-LEK-008',  name:'Lekki Phase 2', lat:6.439, lng:3.523, x:82, y:78, status:'warn', signal:55, load:88, region:'Lekki', issue:'Congestion overflow from LEK-003' },
  { id:'TWR-LEK-014',  name:'Ajah Junction', lat:6.466, lng:3.601, x:92, y:82, status:'ok', signal:84, load:62, region:'Lekki', issue:null },
  { id:'TWR-VI-002',   name:'Victoria Island', lat:6.428, lng:3.421, x:64, y:70, status:'ok', signal:93, load:59, region:'Victoria Island', issue:null },
  { id:'TWR-VI-005',   name:'Eko Hotel', lat:6.421, lng:3.428, x:66, y:74, status:'ok', signal:90, load:66, region:'Victoria Island', issue:null },
  { id:'TWR-IKO-011',  name:'Ikoyi South', lat:6.452, lng:3.435, x:60, y:64, status:'ok', signal:87, load:51, region:'Ikoyi', issue:null },
  { id:'TWR-APP-004',  name:'Apapa Port', lat:6.450, lng:3.365, x:28, y:74, status:'ok', signal:82, load:71, region:'Apapa', issue:null },
  { id:'TWR-AGE-009',  name:'Agege', lat:6.625, lng:3.319, x:14, y:22, status:'ok', signal:86, load:45, region:'Agege', issue:null },
  { id:'TWR-OJO-002',  name:'Festac Town', lat:6.469, lng:3.290, x:8, y:68, status:'warn', signal:60, load:79, region:'Festac', issue:'Crowd-sourced reports +40%' },
];

const ALERTS = [
  { id:'INC-2841', sev:'critical', title:'60% packet loss — Lekki Phase 1', region:'Lekki', tower:'TWR-LEK-003', time:'2m ago', cause:'Probable fiber cut on TG-LEK-A backhaul', users:14200, status:'active', confidence:0.92 },
  { id:'INC-2840', sev:'critical', title:'3 towers offline — power cluster', region:'Lagos West', tower:'TWR-LAG-W-014 +2', time:'8m ago', cause:'Grid failure — IKEDC sector 7', users:38400, status:'active', confidence:0.88 },
  { id:'INC-2839', sev:'warn',     title:'Predicted failure window', region:'Lagos West', tower:'TWR-LAG-W-031', time:'14m ago', cause:'Thermal trend + load → 87% probability of fault by 18:42', users:0, status:'active', confidence:0.87 },
  { id:'INC-2838', sev:'warn',     title:'Crowd-sourced signal drop', region:'Festac', tower:'TWR-OJO-002', time:'22m ago', cause:'42 user reports in 10min radius', users:1800, status:'investigating', confidence:0.71 },
  { id:'INC-2837', sev:'info',     title:'Latency anomaly cleared', region:'Ikeja', tower:'TWR-IKJ-019', time:'47m ago', cause:'Auto-resolved — load shed to TWR-IKJ-021', users:0, status:'resolved', confidence:0.99 },
  { id:'INC-2836', sev:'warn',     title:'Backhaul jitter elevated', region:'Lagos West', tower:'TWR-LAG-W-022', time:'1h ago', cause:'Microwave link MW-7 — weather correlated', users:6200, status:'monitoring', confidence:0.78 },
];

const KPIS = [
  { label:'Network Uptime', value:'99.847', unit:'%', delta:'-0.03', trend:'down', sub:'24h rolling' },
  { label:'Avg Latency', value:'42', unit:'ms', delta:'+8', trend:'down', sub:'p95 across LAG metro' },
  { label:'Active Incidents', value:'14', unit:'', delta:'+3', trend:'down', sub:'2 critical, 5 warn, 7 info' },
  { label:'Towers Online', value:'1,284', unit:'/ 1,291', delta:'-3', trend:'down', sub:'Lagos metro' },
  { label:'Subscribers Affected', value:'52.6', unit:'K', delta:'+14.2K', trend:'down', sub:'last 60 min' },
  { label:'Copilot Queries', value:'2,841', unit:'', delta:'+412', trend:'up', sub:'today' },
];

const AUDIT = [
  { t:'17:42:08', actor:'oluwaseun.a', role:'engineer', action:'copilot.query', target:'Why is Lagos West slow?', ip:'10.4.22.91' },
  { t:'17:41:55', actor:'system',     role:'system',   action:'alert.raised',  target:'INC-2841 Lekki packet loss', ip:'-' },
  { t:'17:40:12', actor:'amaka.o',    role:'manager',  action:'incident.assign', target:'INC-2840 → field-team-3', ip:'10.4.22.14' },
  { t:'17:38:44', actor:'oluwaseun.a',role:'engineer', action:'tower.diagnose',target:'TWR-LEK-003 deep probe', ip:'10.4.22.91' },
  { t:'17:35:20', actor:'system',     role:'system',   action:'sk.skill.run',  target:'NetworkDiagnostics.analyzeRegion(Lagos-West)', ip:'-' },
  { t:'17:31:02', actor:'tunde.b',    role:'admin',    action:'rbac.update',   target:'Granted engineer role → ifeanyi.k', ip:'10.4.22.5' },
  { t:'17:28:51', actor:'oluwaseun.a',role:'engineer', action:'copilot.query', target:'Show outages last 2h on 4G', ip:'10.4.22.91' },
  { t:'17:24:33', actor:'system',     role:'system',   action:'alert.predict', target:'TWR-LAG-W-031 failure 87% by 18:42', ip:'-' },
  { t:'17:18:19', actor:'amaka.o',    role:'manager',  action:'report.export', target:'Weekly NOC summary.pdf', ip:'10.4.22.14' },
  { t:'17:12:07', actor:'tunde.b',    role:'admin',    action:'auth.login',    target:'OAuth2 / Azure AD', ip:'10.4.22.5' },
];

const USERS = [
  { name:'Oluwaseun Adebayo', handle:'oluwaseun.a', role:'engineer', team:'NOC Tier 2',   region:'Lagos Metro',  last:'active now',    init:'OA' },
  { name:'Amaka Okonkwo',     handle:'amaka.o',     role:'manager',  team:'NOC Leadership',region:'All regions',  last:'active now',    init:'AO' },
  { name:'Tunde Bakare',      handle:'tunde.b',     role:'admin',    team:'Platform',     region:'All regions',  last:'2h ago',       init:'TB' },
  { name:'Ifeanyi Kalu',      handle:'ifeanyi.k',   role:'engineer', team:'NOC Tier 1',   region:'Lekki / VI',   last:'12m ago',      init:'IK' },
  { name:'Halima Yusuf',      handle:'halima.y',    role:'engineer', team:'Field Ops',    region:'Ikeja',        last:'34m ago',      init:'HY' },
  { name:'Chioma Eze',        handle:'chioma.e',    role:'manager',  team:'Customer Ops', region:'All regions',  last:'1h ago',       init:'CE' },
  { name:'Babatunde Olu',     handle:'baba.o',      role:'engineer', team:'NOC Tier 2',   region:'Lagos West',   last:'48m ago',      init:'BO' },
  { name:'Kemi Adekunle',     handle:'kemi.a',      role:'viewer',   team:'Executive',    region:'All regions',  last:'1d ago',       init:'KA' },
];

const ROLE_CAPS = {
  engineer: ['copilot.read','copilot.write','tower.diagnose','alerts.read','alerts.ack','map.read'],
  manager:  ['copilot.read','copilot.write','alerts.read','alerts.assign','reports.export','users.read','map.read','dashboard.read'],
  admin:    ['*'],
  viewer:   ['dashboard.read','alerts.read','map.read'],
};

// Sparkline data — pre-baked traces for KPI cards
const SPARKS = {
  uptime:  [99.92,99.91,99.92,99.93,99.92,99.90,99.91,99.92,99.90,99.88,99.87,99.85,99.85,99.84,99.85],
  latency: [34,35,33,34,36,35,38,40,42,44,46,42,41,42,42],
  incident:[7,8,8,9,10,10,11,11,12,12,13,14,14,14,14],
  towers:  [1289,1289,1290,1290,1291,1291,1290,1289,1289,1287,1286,1285,1285,1284,1284],
  subs:    [10,12,14,18,22,28,30,34,40,44,48,50,52,52,52],
  queries: [800,900,1000,1100,1300,1500,1700,1900,2100,2300,2500,2700,2800,2820,2841],
};

Object.assign(window, { TOWERS, ALERTS, KPIS, AUDIT, USERS, ROLE_CAPS, SPARKS });
