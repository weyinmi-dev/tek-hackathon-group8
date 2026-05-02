// Command Center — hero screen, split between map + copilot + alerts feed

function CommandCenter({ role }){
  const [sel, setSel] = React.useState(TOWERS.find(t=>t.status==='critical'));
  return (
    <>
      <TopBar title="Command Center" sub="Real-time NOC view · map · copilot · alerts in one frame"
        right={
          <div style={{display:'flex',gap:8}}>
            <C.Pill tone="crit" dot>2 CRITICAL</C.Pill>
            <C.Pill tone="warn" dot>5 WARN</C.Pill>
            <C.Pill tone="ok" dot>SLA 99.85%</C.Pill>
          </div>
        }/>
      <div style={{
        padding:14,
        display:'grid',
        gridTemplateColumns:'1.4fr 1fr',
        gridTemplateRows:'auto 1fr',
        gap:12,
        height:'calc(100vh - 67px)'
      }}>
        {/* KPI strip across both columns */}
        <div style={{gridColumn:'1 / -1',display:'grid',gridTemplateColumns:'repeat(6,1fr)',gap:10}}>
          <C.KPI {...KPIS[0]} spark={SPARKS.uptime} color="var(--accent)"/>
          <C.KPI {...KPIS[1]} spark={SPARKS.latency} color="var(--warn)"/>
          <C.KPI {...KPIS[2]} spark={SPARKS.incident} color="var(--crit)"/>
          <C.KPI {...KPIS[3]} spark={SPARKS.towers} color="var(--info)"/>
          <C.KPI {...KPIS[4]} spark={SPARKS.subs} color="var(--crit)"/>
          <C.KPI {...KPIS[5]} spark={SPARKS.queries} color="var(--accent)"/>
        </div>

        {/* Map (left, large) */}
        <div style={{position:'relative',minHeight:0,display:'flex',flexDirection:'column',gap:10}}>
          <div style={{flex:1,minHeight:0,position:'relative'}}>
            <NetworkMap onSelect={setSel} selectedId={sel?.id}/>
          </div>
          {/* Alert ticker */}
          <C.Card pad={0} style={{overflow:'hidden'}}>
            <div style={{display:'flex',alignItems:'center'}}>
              <div className="mono uppr" style={{
                fontSize:9.5,color:'var(--crit)',letterSpacing:'.14em',
                padding:'10px 12px',borderRight:'1px solid var(--line)',
                display:'flex',alignItems:'center',gap:6,flexShrink:0,
                background:'rgba(255,84,112,.08)'
              }}>
                <span className="dot crit"/>LIVE FEED
              </div>
              <div style={{flex:1,overflow:'hidden',position:'relative',height:38}}>
                <div style={{display:'flex',gap:32,position:'absolute',whiteSpace:'nowrap',animation:'ticker 60s linear infinite',padding:'10px 0',fontFamily:'var(--mono)',fontSize:11}}>
                  {[...ALERTS,...ALERTS].map((a,i)=>(
                    <span key={i} style={{display:'inline-flex',alignItems:'center',gap:8}}>
                      <span style={{color:a.sev==='critical'?'var(--crit)':a.sev==='warn'?'var(--warn)':'var(--info)'}}>● {a.id}</span>
                      <span style={{color:'var(--ink-2)'}}>{a.title}</span>
                      <span style={{color:'var(--ink-3)'}}>· {a.tower}</span>
                      <span style={{color:'var(--ink-3)'}}>· {a.time}</span>
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </C.Card>
        </div>

        {/* Right column — Copilot */}
        <C.Card pad={0} style={{display:'flex',flexDirection:'column',minHeight:0,overflow:'hidden'}}>
          <div style={{padding:'12px 14px',borderBottom:'1px solid var(--line)',display:'flex',alignItems:'center',justifyContent:'space-between'}}>
            <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',display:'flex',alignItems:'center',gap:8}}>
              <span style={{display:'inline-block',width:6,height:6,borderRadius:'50%',background:'var(--accent)',boxShadow:'0 0 8px var(--accent)'}}/>
              COPILOT · ASK THE NETWORK
            </div>
            <div className="mono" style={{fontSize:9.5,color:'var(--ink-3)'}}>5 SK SKILLS · AZURE OPENAI</div>
          </div>
          <Copilot embedded role={role}/>
        </C.Card>
      </div>
    </>
  );
}

window.CommandCenter = CommandCenter;
