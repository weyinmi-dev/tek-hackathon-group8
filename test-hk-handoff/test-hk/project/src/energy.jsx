// Energy / Power Management module — sites, anomalies, optimization

const SRC_COLOR = {
  grid:'var(--info)', generator:'var(--warn)', battery:'var(--accent)', solar:'#f5d76e'
};
const SRC_LABEL = { grid:'GRID', generator:'GEN', battery:'BATT', solar:'SOLAR' };
const HEALTH_TONE = { ok:'ok', degraded:'warn', critical:'crit' };

function HealthDot({ h }){
  const c = h==='critical'?'var(--crit)':h==='degraded'?'var(--warn)':'var(--ok)';
  return <span style={{display:'inline-block',width:8,height:8,borderRadius:'50%',background:c,boxShadow:`0 0 8px ${c}`}}/>;
}

function SourceIcon({ src, size=10 }){
  const c = SRC_COLOR[src];
  return (
    <span className="mono uppr" style={{
      fontSize:9,letterSpacing:'.10em',padding:'2px 6px',borderRadius:3,
      color:c,background:`color-mix(in oklch, ${c} 14%, transparent)`,
      border:`1px solid color-mix(in oklch, ${c} 30%, transparent)`,
      fontWeight:600
    }}>{SRC_LABEL[src]}</span>
  );
}

// ── Energy Sites page ───────────────────────────────────────────────────────
function EnergyPage({ role }){
  const [sel, setSel] = React.useState(SITES.find(s=>s.health==='critical'));
  const [filter, setFilter] = React.useState('all');
  const list = filter==='all' ? SITES : SITES.filter(s=>s.health===filter);
  const counts = { ok:SITES.filter(s=>s.health==='ok').length, degraded:SITES.filter(s=>s.health==='degraded').length, critical:SITES.filter(s=>s.health==='critical').length };

  return (
    <>
      <TopBar title="Energy Sites" sub={`${SITES.length} active sites · grid + diesel + battery + solar orchestration`}
        right={
          <div style={{display:'flex',gap:6,padding:3,background:'var(--bg-1)',border:'1px solid var(--line)',borderRadius:7}}>
            {[['all','All',SITES.length],['critical','Critical',counts.critical],['degraded','Degraded',counts.degraded],['ok','Healthy',counts.ok]].map(([k,l,n])=>(
              <button key={k} onClick={()=>setFilter(k)} style={{
                appearance:'none',border:0,padding:'5px 12px',borderRadius:5,fontSize:11,fontWeight:500,
                background:filter===k?'var(--bg-3)':'transparent',
                color:filter===k?'var(--ink)':'var(--ink-3)',cursor:'pointer',
                display:'flex',alignItems:'center',gap:6
              }}>{l} <span className="mono" style={{fontSize:9.5,color:'var(--ink-3)'}}>{n}</span></button>
            ))}
          </div>
        }/>
      <div style={{padding:22,display:'flex',flexDirection:'column',gap:14}}>
        <div style={{display:'grid',gridTemplateColumns:'repeat(6,1fr)',gap:10}}>
          {ENERGY_KPIS.map((k,i)=>(
            <C.KPI key={k.label} {...k}
              spark={[DIESEL_TRACE.slice(0,15),COST_WITH_AI.slice(0,15),[58,60,62,63,64,65,65,66,66,67,67,68,68,68,68],SPARKS.uptime,[5,5,4,4,4,3,3,3,3,3,3,3,3,3,3],[88.2,88.1,88,87.9,87.8,87.7,87.7,87.6,87.6,87.5,87.5,87.4,87.4,87.4,87.4]][i]}
              color={['var(--accent)','var(--accent)','#f5d76e','var(--info)','var(--ok)','var(--warn)'][i]}/>
          ))}
        </div>

        <div style={{display:'grid',gridTemplateColumns:'1fr 380px',gap:14,minHeight:0}}>
          {/* Sites table */}
          <C.Card pad={0}>
            <div style={{padding:'12px 14px',borderBottom:'1px solid var(--line)',display:'grid',gridTemplateColumns:'1.6fr 90px 60px 1fr 1fr 90px 80px',gap:10,fontFamily:'var(--mono)',fontSize:10,color:'var(--ink-3)',letterSpacing:'.12em',textTransform:'uppercase'}}>
              <span>SITE</span><span>SOURCE</span><span>GRID</span><span>BATTERY</span><span>DIESEL</span><span>COST/D</span><span>HEALTH</span>
            </div>
            <div style={{maxHeight:'calc(100vh - 380px)',overflowY:'auto'}}>
              {list.map((s,i)=>{
                const active = sel?.id===s.id;
                return (
                  <button key={s.id} onClick={()=>setSel(s)} style={{
                    appearance:'none',width:'100%',textAlign:'left',cursor:'pointer',
                    padding:'12px 14px',borderBottom:i<list.length-1?'1px solid var(--line)':0,
                    display:'grid',gridTemplateColumns:'1.6fr 90px 60px 1fr 1fr 90px 80px',gap:10,
                    background:active?'var(--bg-2)':'transparent',
                    border:'none',
                    borderLeft:'3px solid '+(active?'var(--accent)':'transparent'),
                    color:'var(--ink)',alignItems:'center',fontSize:12.5
                  }}>
                    <div>
                      <div className="mono" style={{fontSize:10,color:'var(--accent)',marginBottom:2}}>{s.id}</div>
                      <div style={{display:'flex',alignItems:'center',gap:6}}>
                        <span style={{fontWeight:500}}>{s.name}</span>
                        <span style={{color:'var(--ink-3)',fontSize:11}}>· {s.region}</span>
                        {s.solar && <span style={{fontSize:11,color:'#f5d76e'}}>☼</span>}
                      </div>
                    </div>
                    <SourceIcon src={s.source}/>
                    <span className="mono" style={{fontSize:10.5,color:s.gridUp?'var(--ok)':'var(--ink-3)'}}>{s.gridUp?'● UP':'○ DOWN'}</span>
                    <BarRow pct={s.battPct} tone={s.battPct<30?'crit':s.battPct<60?'warn':'ok'}/>
                    <BarRow pct={s.dieselPct} tone={s.dieselPct<20?'crit':s.dieselPct<50?'warn':'ok'}/>
                    <span className="mono" style={{fontSize:11,color:'var(--ink-2)'}}>₦{(s.costNGN/1000).toFixed(0)}K</span>
                    <span style={{display:'flex',alignItems:'center',gap:6}}><HealthDot h={s.health}/> <span className="mono" style={{fontSize:10,textTransform:'uppercase',color:s.health==='critical'?'var(--crit)':s.health==='degraded'?'var(--warn)':'var(--ok)'}}>{s.health}</span></span>
                  </button>
                );
              })}
            </div>
          </C.Card>

          {/* Site detail */}
          {sel && (
            <div style={{display:'flex',flexDirection:'column',gap:14}}>
              <C.Card pad={16}>
                <div style={{display:'flex',justifyContent:'space-between',alignItems:'flex-start',marginBottom:10}}>
                  <div>
                    <div className="mono" style={{fontSize:10,color:'var(--accent)',marginBottom:3}}>{sel.id}</div>
                    <div style={{fontSize:15,fontWeight:600}}>{sel.name}</div>
                    <div className="mono" style={{fontSize:10,color:'var(--ink-3)',marginTop:2,letterSpacing:'.10em'}}>{sel.region.toUpperCase()}</div>
                  </div>
                  <C.Pill tone={HEALTH_TONE[sel.health]} dot>{sel.health}</C.Pill>
                </div>
                {sel.anomaly && (
                  <div style={{padding:10,background:'var(--bg-3)',borderRadius:6,fontSize:11.5,marginBottom:10,borderLeft:`2px solid ${sel.health==='critical'?'var(--crit)':'var(--warn)'}`}}>
                    ⚠ {sel.anomaly}
                  </div>
                )}
                <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.14em',marginTop:4,marginBottom:8}}>POWER MIX · NOW</div>
                <PowerMixBar src={sel.source}/>
                <div style={{display:'grid',gridTemplateColumns:'1fr 1fr',gap:10,marginTop:14}}>
                  <Metric label="BATTERY" v={sel.battPct} unit="%" tone={sel.battPct<30?'crit':sel.battPct<60?'warn':'ok'}/>
                  <Metric label="DIESEL" v={sel.dieselPct} unit="%" tone={sel.dieselPct<20?'crit':sel.dieselPct<50?'warn':'ok'}/>
                  <Metric label="SOLAR" v={sel.solarKw} unit="kW" tone={sel.solarKw>3?'ok':sel.solarKw>0?'warn':'crit'}/>
                  <Metric label="UPTIME" v={sel.uptime} unit="%" tone={sel.uptime>99?'ok':sel.uptime>97?'warn':'crit'}/>
                </div>
                <div style={{display:'flex',gap:6,marginTop:14}}>
                  <C.Btn primary small>Switch Source</C.Btn>
                  <C.Btn small>Dispatch Refuel</C.Btn>
                  <C.Btn ghost small>Open in Copilot →</C.Btn>
                </div>
              </C.Card>

              <C.Section label="24H DIESEL · LITERS">
                <C.Card pad={14}>
                  <SiteDieselChart pct={sel.dieselPct} health={sel.health}/>
                </C.Card>
              </C.Section>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

function BarRow({ pct, tone }){
  const c = tone==='crit'?'var(--crit)':tone==='warn'?'var(--warn)':'var(--ok)';
  return (
    <div style={{display:'flex',alignItems:'center',gap:8}}>
      <div style={{flex:1,height:4,background:'var(--bg-3)',borderRadius:2,overflow:'hidden'}}>
        <div style={{height:'100%',width:`${pct}%`,background:c,borderRadius:2}}/>
      </div>
      <span className="mono" style={{fontSize:10.5,color:c,width:32,textAlign:'right'}}>{pct}%</span>
    </div>
  );
}

function Metric({label,v,unit,tone}){
  return (
    <div>
      <div className="mono uppr" style={{fontSize:9,color:'var(--ink-3)',letterSpacing:'.12em',marginBottom:4}}>{label}</div>
      <div className="mono" style={{fontSize:18,fontWeight:600,marginBottom:5,color:tone==='crit'?'var(--crit)':tone==='warn'?'var(--warn)':'var(--ok)'}}>{v}<span style={{color:'var(--ink-3)',fontSize:11,marginLeft:2}}>{unit}</span></div>
    </div>
  );
}

function PowerMixBar({ src }){
  // Visual: 4 segments, active source brightened
  const segs = [
    { k:'grid', label:'GRID', pct:src==='grid'?100:0 },
    { k:'generator', label:'DIESEL', pct:src==='generator'?100:0 },
    { k:'battery', label:'BATTERY', pct:src==='battery'?100:0 },
    { k:'solar', label:'SOLAR', pct:src==='solar'?100:0 },
  ];
  return (
    <div style={{display:'flex',gap:6}}>
      {segs.map(s=>{
        const active = s.pct>0;
        const c = SRC_COLOR[s.k];
        return (
          <div key={s.k} style={{flex:1,padding:8,borderRadius:5,
            background: active ? `color-mix(in oklch, ${c} 18%, transparent)` : 'var(--bg-3)',
            border: `1px solid ${active?c:'var(--line)'}`,
            textAlign:'center'}}>
            <div className="mono uppr" style={{fontSize:9,color:active?c:'var(--ink-3)',letterSpacing:'.12em',fontWeight:600}}>{s.label}</div>
            <div className="mono" style={{fontSize:10,color:'var(--ink-3)',marginTop:3}}>{active?'ACTIVE':'idle'}</div>
          </div>
        );
      })}
    </div>
  );
}

function SiteDieselChart({ pct, health }){
  // Use FUEL_TRACE_THEFT for critical sites, DIESEL_TRACE/10 for others
  const data = health==='critical' ? FUEL_TRACE_THEFT : Array.from({length:24},(_,i)=>{
    return Math.max(20,(pct + 4*Math.sin(i/4) - i*0.4));
  });
  const max = 100;
  const W=300, H=80;
  return (
    <div>
      <svg viewBox={`0 0 ${W} ${H}`} style={{width:'100%',height:80,display:'block'}}>
        <line x1="0" y1="0" x2={W} y2="0" stroke="var(--line)" strokeWidth=".5"/>
        <polyline
          points={data.map((v,i)=>`${(i/(data.length-1))*W},${H-(v/max)*H}`).join(' ')}
          fill="none" stroke={health==='critical'?'var(--crit)':'var(--accent)'} strokeWidth="1.5"/>
        {/* mark theft event */}
        {health==='critical' && (
          <g>
            <circle cx={(18/23)*W} cy={H-(61/100)*H} r="3" fill="var(--crit)"/>
            <text x={(18/23)*W} y={H-(61/100)*H-6} textAnchor="middle" fill="var(--crit)" fontSize="8" fontFamily="var(--mono)">⚠ THEFT</text>
          </g>
        )}
      </svg>
      <div className="mono" style={{fontSize:9.5,color:'var(--ink-3)',display:'flex',justifyContent:'space-between',marginTop:4}}>
        <span>−24h</span><span>−12h</span><span>NOW</span>
      </div>
    </div>
  );
}

// ── Anomalies page ──────────────────────────────────────────────────────────
const ANOMALY_LABELS = {
  'fuel-theft':       { label:'Fuel theft', icon:'⛽', tone:'crit' },
  'sensor-offline':   { label:'Sensor offline', icon:'⌁', tone:'warn' },
  'gen-overuse':      { label:'Generator overuse', icon:'⚙', tone:'warn' },
  'battery-degrade':  { label:'Battery degradation', icon:'▥', tone:'info' },
  'predicted-fault':  { label:'Predicted fault', icon:'⌖', tone:'warn' },
};

function AnomaliesPage({ role }){
  const [sel, setSel] = React.useState(ANOMALY_EVENTS[0]);
  return (
    <>
      <TopBar title="Anomaly Detection" sub="Isolation Forest + statistical models · fuel theft · battery degradation · gen misuse"
        right={
          <div style={{display:'flex',gap:8}}>
            <C.Pill tone="crit" dot>{ANOMALY_EVENTS.filter(a=>a.sev==='critical').length} CRITICAL</C.Pill>
            <C.Pill tone="warn" dot>{ANOMALY_EVENTS.filter(a=>a.sev==='warn').length} WARN</C.Pill>
            <C.Pill tone="info">7D AVG ↓2</C.Pill>
          </div>
        }/>
      <div style={{padding:22,display:'grid',gridTemplateColumns:'1fr 380px',gap:14,height:'calc(100vh - 67px)'}}>
        <div style={{display:'flex',flexDirection:'column',gap:10,overflowY:'auto',paddingRight:4}}>
          {ANOMALY_EVENTS.map(a=>{
            const meta = ANOMALY_LABELS[a.kind];
            const active = sel.id===a.id;
            return (
              <button key={a.id} onClick={()=>setSel(a)} style={{
                appearance:'none',textAlign:'left',cursor:'pointer',
                background:active?'var(--bg-2)':'var(--bg-1)',
                border:'1px solid '+(active?'var(--accent-line)':'var(--line)'),
                borderRadius:8,padding:14,
                borderLeft:`3px solid ${a.sev==='critical'?'var(--crit)':a.sev==='warn'?'var(--warn)':'var(--info)'}`
              }}>
                <div style={{display:'flex',alignItems:'center',gap:10,marginBottom:8}}>
                  <span style={{fontSize:16}}>{meta.icon}</span>
                  <C.Pill tone={meta.tone} dot>{meta.label}</C.Pill>
                  <span className="mono" style={{fontSize:10,color:'var(--ink-3)'}}>{a.id}</span>
                  <span style={{flex:1}}/>
                  <span className="mono" style={{fontSize:10,color:'var(--ink-3)'}}>{a.t}</span>
                </div>
                <div style={{fontSize:13,color:'var(--ink)',lineHeight:1.5,marginBottom:6}}>
                  <span className="mono" style={{color:'var(--accent)',fontSize:11,marginRight:8}}>{a.site}</span>
                  {a.detail}
                </div>
                <div className="mono" style={{fontSize:10,color:'var(--ink-3)',display:'flex',gap:14,marginTop:8}}>
                  <span>conf {Math.round(a.conf*100)}%</span>
                  <span>· model: {a.kind==='fuel-theft'?'IsolationForest-v3':a.kind==='predicted-fault'?'Prophet+RuleHybrid':'StatThreshold'}</span>
                </div>
              </button>
            );
          })}
        </div>

        {sel && (
          <div style={{display:'flex',flexDirection:'column',gap:14,overflowY:'auto'}}>
            <C.Card pad={16}>
              <div className="mono" style={{fontSize:10,color:'var(--accent)',marginBottom:4}}>{sel.id}</div>
              <div style={{fontSize:15,fontWeight:600,marginBottom:10}}>{ANOMALY_LABELS[sel.kind].label}</div>
              <Row k="Site" v={sel.site}/>
              <Row k="Detected" v={sel.t+' WAT'}/>
              <Row k="Severity" v={sel.sev}/>
              <Row k="Confidence" v={Math.round(sel.conf*100)+'%'} last/>
            </C.Card>

            <C.Section label="SIGNATURE">
              <C.Card pad={14}>
                {sel.kind==='fuel-theft' && (
                  <>
                    <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:8}}>FUEL LEVEL · 24H</div>
                    <SiteDieselChart pct={60} health="critical"/>
                    <div style={{marginTop:10,padding:10,background:'var(--bg-3)',borderRadius:5,fontSize:11.5,lineHeight:1.5,color:'var(--ink-2)'}}>
                      Detected −18L drop in 6 minutes at 04:18, outside the scheduled refill window (06:00–08:00). No work order open. Pattern matches 11 prior theft incidents in this region.
                    </div>
                  </>
                )}
                {sel.kind==='predicted-fault' && (
                  <>
                    <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:8}}>FAULT PROBABILITY · NEXT 4H</div>
                    <PredFaultMini/>
                  </>
                )}
                {(sel.kind==='gen-overuse'||sel.kind==='battery-degrade'||sel.kind==='sensor-offline') && (
                  <div style={{padding:10,background:'var(--bg-3)',borderRadius:5,fontSize:11.5,lineHeight:1.5,color:'var(--ink-2)'}}>
                    {sel.detail}
                  </div>
                )}
              </C.Card>
            </C.Section>

            {role!=='viewer' && (
              <div style={{display:'flex',gap:6,flexWrap:'wrap'}}>
                <C.Btn primary>Acknowledge</C.Btn>
                {sel.kind==='fuel-theft' && <C.Btn>Dispatch Security</C.Btn>}
                <C.Btn>Create Work Order</C.Btn>
                <C.Btn ghost>Suppress 24h</C.Btn>
              </div>
            )}
          </div>
        )}
      </div>
    </>
  );
}

function PredFaultMini(){
  const W=300, H=80;
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{width:'100%',height:80,display:'block'}}>
      <line x1={W/2} y1="0" x2={W/2} y2={H} stroke="var(--line-2)" strokeDasharray="2 2"/>
      <text x={W/2+4} y="10" fill="var(--ink-3)" fontSize="8" fontFamily="var(--mono)">NOW</text>
      <polyline points={`0,72 60,68 100,62 ${W/2},54`} fill="none" stroke="var(--ink-2)" strokeWidth="1.5"/>
      <polyline points={`${W/2},54 200,38 250,20 ${W},10`} fill="none" stroke="var(--warn)" strokeWidth="1.5" strokeDasharray="3 2"/>
      <polygon points={`${W/2},54 200,38 250,20 ${W},10 ${W},${H} ${W/2},${H}`} fill="rgba(255,181,71,.10)"/>
      <text x={W-4} y={20} textAnchor="end" fill="var(--warn)" fontSize="9" fontFamily="var(--mono)">87% by 18:42</text>
    </svg>
  );
}

function Row({k,v,last}){
  return (
    <div style={{display:'flex',justifyContent:'space-between',padding:'8px 0',borderBottom:last?0:'1px solid var(--line)',fontSize:12}}>
      <span style={{color:'var(--ink-3)'}}>{k}</span>
      <span className="mono" style={{textTransform:'capitalize'}}>{v}</span>
    </div>
  );
}

// ── Optimization page ───────────────────────────────────────────────────────
function OptimizePage(){
  const [solar, setSolar] = React.useState(44);
  const [diesel, setDiesel] = React.useState(900);
  const [batt, setBatt] = React.useState(70);

  // Simple model: more solar % + more battery threshold → less diesel cost
  const baseCost = 21; // ₦M/day baseline
  const solarSavings = solar * 0.12;
  const battSavings = (batt - 50) * 0.04;
  const dieselFactor = (diesel - 700) * 0.002;
  const optimizedCost = Math.max(8, baseCost - solarSavings - battSavings + dieselFactor);
  const annualSavingsM = (baseCost - optimizedCost) * 365;

  return (
    <>
      <TopBar title="Cost Optimization" sub="Simulate solar adoption · diesel pricing · battery thresholds — 30d horizon"/>
      <div style={{padding:22,display:'grid',gridTemplateColumns:'380px 1fr',gap:14}}>
        <div style={{display:'flex',flexDirection:'column',gap:14}}>
          <C.Section label="SCENARIO INPUTS">
            <C.Card pad={16}>
              <Slider label="Sites on solar" value={solar} unit="%" min={20} max={100} onChange={setSolar}/>
              <Slider label="Diesel price" value={diesel} unit="₦/L" min={700} max={1400} onChange={setDiesel}/>
              <Slider label="Battery switch threshold" value={batt} unit="%" min={30} max={90} onChange={setBatt}/>
            </C.Card>
          </C.Section>

          <C.Section label="PROJECTED IMPACT · 30D">
            <C.Card pad={16}>
              <BigStat label="Daily OPEX" value={`₦${optimizedCost.toFixed(1)}M`} delta={`-₦${(baseCost-optimizedCost).toFixed(1)}M`} good/>
              <BigStat label="Annual savings" value={`₦${(annualSavingsM/1000).toFixed(2)}B`} delta={`vs baseline`} good/>
              <BigStat label="Diesel reduction" value={`-${(solar*0.5+(batt-50)*0.3).toFixed(0)}%`} delta="fleet-wide" good/>
              <BigStat label="CO₂ avoided · yr" value={`${(solar*42).toFixed(0)} t`} delta="based on diesel L → CO₂kg" good last/>
            </C.Card>
          </C.Section>
        </div>

        <div style={{display:'flex',flexDirection:'column',gap:14}}>
          <C.Card pad={16}>
            <div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:14}}>
              <div>
                <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:4}}>OPEX PROJECTION · BASELINE vs AI-OPTIMIZED</div>
                <div style={{fontSize:14,fontWeight:500}}>30-day forward simulation · ₦M/day</div>
              </div>
              <C.Pill tone="accent" dot>SAVING ₦{(baseCost-optimizedCost).toFixed(1)}M/DAY</C.Pill>
            </div>
            <CostChart base={COST_BASELINE} opt={COST_WITH_AI} optScale={(baseCost-optimizedCost)/baseCost}/>
          </C.Card>

          <div style={{display:'grid',gridTemplateColumns:'1fr 1fr',gap:14}}>
            <C.Card pad={16}>
              <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:10}}>FLEET ENERGY MIX · NOW</div>
              <MixDonut/>
            </C.Card>
            <C.Card pad={16}>
              <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:10}}>RECOMMENDED ACTIONS</div>
              {[
                ['Convert 12 high-diesel sites to hybrid solar','+₦4.2M/d savings','accent'],
                ['Raise battery threshold 50→70% on 24 sites','+₦1.1M/d savings','accent'],
                ['Replace 8 batteries (cycle >2000)','prevents 4 outages/m','warn'],
                ['Renegotiate diesel contract — Lekki cluster','-₦80/L est.','info'],
              ].map(([t,v,tone],i)=>(
                <div key={i} style={{padding:'10px 0',borderBottom:i<3?'1px solid var(--line)':0,display:'flex',justifyContent:'space-between',alignItems:'center',gap:10}}>
                  <span style={{fontSize:12,color:'var(--ink-2)',flex:1,lineHeight:1.4}}>{t}</span>
                  <C.Pill tone={tone}>{v}</C.Pill>
                </div>
              ))}
            </C.Card>
          </div>
        </div>
      </div>
    </>
  );
}

function Slider({label,value,unit,min,max,onChange}){
  return (
    <div style={{padding:'10px 0',borderBottom:'1px solid var(--line)'}}>
      <div style={{display:'flex',justifyContent:'space-between',marginBottom:8,fontSize:11.5}}>
        <span style={{color:'var(--ink-2)'}}>{label}</span>
        <span className="mono" style={{color:'var(--accent)',fontWeight:600}}>{typeof value==='number'?value.toLocaleString():value}{unit}</span>
      </div>
      <input type="range" min={min} max={max} value={value} onChange={e=>onChange(Number(e.target.value))} style={{width:'100%',accentColor:'var(--accent)'}}/>
      <div className="mono" style={{display:'flex',justifyContent:'space-between',fontSize:9.5,color:'var(--ink-3)',marginTop:2}}>
        <span>{min.toLocaleString()}{unit}</span><span>{max.toLocaleString()}{unit}</span>
      </div>
    </div>
  );
}

function BigStat({label,value,delta,good,last}){
  return (
    <div style={{padding:'10px 0',borderBottom:last?0:'1px solid var(--line)'}}>
      <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.12em',marginBottom:4}}>{label}</div>
      <div style={{display:'flex',alignItems:'baseline',gap:8}}>
        <div className="mono" style={{fontSize:24,fontWeight:600,color:good?'var(--accent)':'var(--ink)',letterSpacing:'-.02em'}}>{value}</div>
        <div className="mono" style={{fontSize:11,color:good?'var(--ok)':'var(--ink-3)'}}>{delta}</div>
      </div>
    </div>
  );
}

function CostChart({ base, opt, optScale }){
  const W=600, H=180;
  const max = 25;
  // Re-scale opt per slider
  const optAdj = opt.map(v => Math.max(8, v * (1 - optScale * 0.3)));
  const pts = (arr) => arr.map((v,i)=>`${40+(i/(arr.length-1))*(W-40)},${H-(v/max)*H+20}`).join(' ');
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{width:'100%',height:180,display:'block'}}>
      {[5,10,15,20].map(v=>(
        <g key={v}>
          <line x1="40" y1={H-(v/max)*H+20} x2={W} y2={H-(v/max)*H+20} stroke="var(--line)" strokeWidth=".5" strokeDasharray="2 3"/>
          <text x="0" y={H-(v/max)*H+24} fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">₦{v}M</text>
        </g>
      ))}
      <polyline points={pts(base)} fill="none" stroke="var(--ink-3)" strokeWidth="1.5" strokeDasharray="4 3"/>
      <polyline points={pts(optAdj)} fill="none" stroke="var(--accent)" strokeWidth="2"/>
      {/* Fill between */}
      <polygon points={`${pts(base)} ${pts(optAdj).split(' ').reverse().join(' ')}`} fill="rgba(0,229,160,.08)"/>
      {[0,7,14,21,29].map((d,i,a)=>(
        <text key={d} x={40 + (d/29)*(W-40)} y={H-2} textAnchor="middle" fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">d+{d}</text>
      ))}
      <g transform={`translate(${W-180}, 30)`}>
        <rect width="170" height="44" fill="rgba(10,14,22,.7)" stroke="var(--line)" rx="4"/>
        <line x1="10" y1="16" x2="28" y2="16" stroke="var(--ink-3)" strokeWidth="1.5" strokeDasharray="4 3"/>
        <text x="34" y="19" fill="var(--ink-2)" fontSize="10" fontFamily="var(--mono)">Baseline</text>
        <line x1="10" y1="32" x2="28" y2="32" stroke="var(--accent)" strokeWidth="2"/>
        <text x="34" y="35" fill="var(--ink-2)" fontSize="10" fontFamily="var(--mono)">AI-optimized</text>
      </g>
    </svg>
  );
}

function MixDonut(){
  const data = [
    { k:'Diesel', v:38, c:'var(--warn)' },
    { k:'Grid',   v:31, c:'var(--info)' },
    { k:'Battery',v:18, c:'var(--accent)' },
    { k:'Solar',  v:13, c:'#f5d76e' },
  ];
  const size=140, r=58, cx=size/2, cy=size/2;
  let acc = 0;
  return (
    <div style={{display:'flex',alignItems:'center',gap:14}}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        {data.map((d,i)=>{
          const start = acc, end = acc + d.v;
          acc = end;
          const a0 = (start/100)*Math.PI*2 - Math.PI/2;
          const a1 = (end/100)*Math.PI*2 - Math.PI/2;
          const large = (end-start)>50?1:0;
          const x0 = cx + r*Math.cos(a0), y0 = cy + r*Math.sin(a0);
          const x1 = cx + r*Math.cos(a1), y1 = cy + r*Math.sin(a1);
          return (
            <path key={d.k} d={`M ${cx} ${cy} L ${x0} ${y0} A ${r} ${r} 0 ${large} 1 ${x1} ${y1} Z`} fill={d.c} opacity=".85"/>
          );
        })}
        <circle cx={cx} cy={cy} r="34" fill="var(--bg-1)"/>
        <text x={cx} y={cy-2} textAnchor="middle" fontFamily="var(--mono)" fontSize="14" fontWeight="600" fill="var(--ink)">154</text>
        <text x={cx} y={cy+10} textAnchor="middle" fontFamily="var(--mono)" fontSize="8" fill="var(--ink-3)" letterSpacing="1">SITES</text>
      </svg>
      <div style={{flex:1,display:'flex',flexDirection:'column',gap:6}}>
        {data.map(d=>(
          <div key={d.k} style={{display:'flex',alignItems:'center',gap:8,fontSize:11.5}}>
            <span style={{width:8,height:8,borderRadius:2,background:d.c}}/>
            <span style={{flex:1,color:'var(--ink-2)'}}>{d.k}</span>
            <span className="mono" style={{color:'var(--ink)'}}>{d.v}%</span>
          </div>
        ))}
      </div>
    </div>
  );
}

window.EnergyPage = EnergyPage;
window.AnomaliesPage = AnomaliesPage;
window.OptimizePage = OptimizePage;
