// Dashboard — KPIs, charts, executive view

function Dashboard(){
  return (
    <>
      <TopBar title="Operations Dashboard" sub="Lagos metro · 24h rolling · auto-refresh 30s"/>
      <div style={{padding:22,display:'flex',flexDirection:'column',gap:14}}>
        <div style={{display:'grid',gridTemplateColumns:'repeat(6, 1fr)',gap:10}}>
          <C.KPI {...KPIS[0]} spark={SPARKS.uptime} color="var(--accent)"/>
          <C.KPI {...KPIS[1]} spark={SPARKS.latency} color="var(--warn)"/>
          <C.KPI {...KPIS[2]} spark={SPARKS.incident} color="var(--crit)"/>
          <C.KPI {...KPIS[3]} spark={SPARKS.towers} color="var(--info)"/>
          <C.KPI {...KPIS[4]} spark={SPARKS.subs} color="var(--crit)"/>
          <C.KPI {...KPIS[5]} spark={SPARKS.queries} color="var(--accent)"/>
        </div>

        <div style={{display:'grid',gridTemplateColumns:'2fr 1fr',gap:14}}>
          <C.Card pad={16}>
            <div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:12}}>
              <div>
                <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:4}}>NETWORK LATENCY</div>
                <div style={{fontSize:14,fontWeight:500}}>p95 across 8 Lagos regions · last 24h</div>
              </div>
              <div style={{display:'flex',gap:6}}>
                {['1H','24H','7D','30D'].map((t,i)=>(
                  <button key={t} style={{
                    appearance:'none',border:'1px solid '+(i===1?'var(--accent-line)':'var(--line)'),
                    background:i===1?'var(--accent-dim)':'transparent',
                    color:i===1?'var(--accent)':'var(--ink-3)',
                    padding:'4px 10px',borderRadius:4,fontSize:10,fontFamily:'var(--mono)',cursor:'pointer'
                  }}>{t}</button>
                ))}
              </div>
            </div>
            <BigChart/>
          </C.Card>
          <C.Card pad={16}>
            <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:12}}>REGIONAL HEALTH</div>
            {['Lagos West','Lekki','Ikeja','Victoria Island','Ikoyi','Apapa','Festac','Agege'].map((r,i)=>{
              const towers = TOWERS.filter(t=>t.region===r);
              const avgSig = towers.length ? Math.round(towers.reduce((a,t)=>a+t.signal,0)/towers.length) : 90;
              const tone = avgSig>75?'ok':avgSig>50?'warn':'crit';
              return (
                <div key={r} style={{padding:'9px 0',borderBottom:i<7?'1px solid var(--line)':0,display:'flex',alignItems:'center',gap:10}}>
                  <div style={{flex:1,fontSize:12.5}}>{r}</div>
                  <div style={{flex:1.5}}><C.Bar pct={avgSig} tone={tone}/></div>
                  <div className="mono" style={{fontSize:11,color:tone==='crit'?'var(--crit)':tone==='warn'?'var(--warn)':'var(--ok)',width:32,textAlign:'right'}}>{avgSig}%</div>
                </div>
              );
            })}
          </C.Card>
        </div>

        <div style={{display:'grid',gridTemplateColumns:'1fr 1fr 1fr',gap:14}}>
          <C.Card pad={16}>
            <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:10}}>INCIDENTS BY TYPE · 7d</div>
            {[['Fiber cut',8,'var(--crit)'],['Power outage',12,'var(--warn)'],['Congestion',24,'var(--info)'],['Equipment',6,'var(--accent)'],['Weather',3,'var(--ink-3)']].map(([k,v,c])=>(
              <div key={k} style={{display:'flex',alignItems:'center',gap:10,padding:'7px 0'}}>
                <div style={{flex:1,fontSize:12}}>{k}</div>
                <div style={{flex:2,height:6,background:'var(--bg-3)',borderRadius:3,overflow:'hidden'}}>
                  <div style={{height:'100%',width:`${(v/24)*100}%`,background:c,borderRadius:3}}/>
                </div>
                <div className="mono" style={{fontSize:11,width:24,textAlign:'right'}}>{v}</div>
              </div>
            ))}
          </C.Card>
          <C.Card pad={16}>
            <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:10}}>COPILOT TOP QUERIES · 24h</div>
            {[
              ['"why is lagos west slow"', 142],
              ['"show outages last 2h"', 98],
              ['"predict next failure"', 76],
              ['"compare lekki vs VI"', 54],
              ['"packet loss ikeja"', 41],
            ].map(([q,n])=>(
              <div key={q} style={{display:'flex',justifyContent:'space-between',padding:'7px 0',fontSize:12,borderBottom:'1px solid var(--line)'}}>
                <span style={{color:'var(--ink-2)',fontFamily:'var(--mono)',fontSize:11}}>{q}</span>
                <span className="mono" style={{color:'var(--accent)'}}>{n}</span>
              </div>
            ))}
          </C.Card>
          <C.Card pad={16}>
            <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:10}}>SLA COMPLIANCE</div>
            <div style={{display:'flex',alignItems:'center',gap:14,marginTop:6}}>
              <Donut pct={99.847} color="var(--accent)" size={84}/>
              <div style={{flex:1}}>
                <div className="mono" style={{fontSize:9.5,color:'var(--ink-3)',marginBottom:4}}>TARGET 99.95%</div>
                <div className="mono" style={{fontSize:11,color:'var(--crit)',marginBottom:8}}>▼ 0.103 BELOW</div>
                <div style={{fontSize:11,color:'var(--ink-2)',lineHeight:1.5}}>Recovery ETA <span className="mono" style={{color:'var(--ink)'}}>2.4h</span> if INC-2841 + 2840 resolve</div>
              </div>
            </div>
          </C.Card>
        </div>
      </div>
    </>
  );
}

function Donut({pct,color,size=80}){
  const r = (size-12)/2;
  const c = 2*Math.PI*r;
  const off = c - (pct/100)*c;
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
      <circle cx={size/2} cy={size/2} r={r} fill="none" stroke="var(--bg-3)" strokeWidth="6"/>
      <circle cx={size/2} cy={size/2} r={r} fill="none" stroke={color} strokeWidth="6"
        strokeDasharray={c} strokeDashoffset={off} strokeLinecap="round"
        transform={`rotate(-90 ${size/2} ${size/2})`}/>
      <text x={size/2} y={size/2+4} textAnchor="middle" fontFamily="var(--mono)" fontSize="13" fontWeight="600" fill="var(--ink)">{pct}%</text>
    </svg>
  );
}

function BigChart(){
  // Multi-line: 3 regions
  const series = [
    { name:'Lagos West', color:'var(--crit)', data:[34,36,40,44,52,62,76,90,108,124,138,142,140,138,135,132] },
    { name:'Ikeja',      color:'var(--warn)', data:[28,30,32,34,36,40,44,48,52,55,58,56,52,50,48,46] },
    { name:'V.I. / Ikoyi', color:'var(--accent)', data:[22,23,24,24,25,26,28,30,32,34,36,34,32,30,28,28] },
  ];
  const max = 160;
  const W=800, H=180;
  return (
    <div>
      <svg viewBox={`0 0 ${W} ${H}`} style={{width:'100%',height:180,display:'block'}}>
        {/* horizontal grid */}
        {[40,80,120,160].map(v=>(
          <g key={v}>
            <line x1="40" y1={H-(v/max)*H+20} x2={W} y2={H-(v/max)*H+20} stroke="var(--line)" strokeWidth=".5" strokeDasharray="2 3"/>
            <text x="0" y={H-(v/max)*H+24} fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">{v}ms</text>
          </g>
        ))}
        {series.map(s=>{
          const pts = s.data.map((v,i)=>{
            const x = 40 + (i/(s.data.length-1))*(W-40);
            const y = H-(v/max)*H+20;
            return `${x},${y}`;
          }).join(' ');
          return (
            <g key={s.name}>
              <polyline points={pts} fill="none" stroke={s.color} strokeWidth="1.5" strokeLinecap="round"/>
              <circle cx={40 + (W-40)} cy={H-(s.data[s.data.length-1]/max)*H+20} r="3" fill={s.color}/>
            </g>
          );
        })}
        {/* x labels */}
        {['00:00','06:00','12:00','18:00','NOW'].map((t,i,a)=>(
          <text key={t} x={40 + (i/(a.length-1))*(W-40)} y={H-2} textAnchor="middle" fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">{t}</text>
        ))}
      </svg>
      <div style={{display:'flex',gap:18,marginTop:8}}>
        {series.map(s=>(
          <div key={s.name} style={{display:'flex',alignItems:'center',gap:6,fontSize:11.5,color:'var(--ink-2)'}}>
            <span style={{width:10,height:2,background:s.color}}/>{s.name}
          </div>
        ))}
      </div>
    </div>
  );
}

window.Dashboard = Dashboard;
