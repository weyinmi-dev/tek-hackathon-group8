// Map view — Lagos metro with towers, signal heatmap, outage rings

function NetworkMap({ compact, onSelect, selectedId, mode='engineer' }){
  const [hover, setHover] = React.useState(null);
  return (
    <div style={{
      position:'relative',width:'100%',height:'100%',
      background:'radial-gradient(ellipse at 50% 60%, #0c1220 0%, #06080d 70%)',
      border:'1px solid var(--line)',borderRadius:10,overflow:'hidden'
    }}>
      {/* Grid */}
      <svg viewBox="0 0 100 100" preserveAspectRatio="none" style={{position:'absolute',inset:0,width:'100%',height:'100%',opacity:.4}}>
        <defs>
          <pattern id="mapgrid" width="5" height="5" patternUnits="userSpaceOnUse">
            <path d="M 5 0 L 0 0 0 5" fill="none" stroke="rgba(255,255,255,.04)" strokeWidth="0.2"/>
          </pattern>
        </defs>
        <rect width="100" height="100" fill="url(#mapgrid)"/>
      </svg>

      {/* Lagos shape (abstract) */}
      <svg viewBox="0 0 100 100" preserveAspectRatio="none" style={{position:'absolute',inset:0,width:'100%',height:'100%'}}>
        {/* lagoon */}
        <path d="M 40 55 Q 55 50 70 60 Q 80 65 75 75 Q 60 80 50 72 Q 42 65 40 55 Z"
              fill="rgba(91,140,255,.06)" stroke="rgba(91,140,255,.18)" strokeWidth=".2"/>
        {/* land mass outline */}
        <path d="M 5 18 Q 20 12 38 16 Q 55 18 72 14 Q 88 16 95 28 L 96 50 Q 92 62 86 70 Q 80 82 72 88 Q 55 92 42 88 Q 28 84 18 76 Q 8 68 5 52 Z"
              fill="none" stroke="rgba(255,255,255,.06)" strokeWidth=".25"/>
        {/* roads */}
        <path d="M 8 30 L 95 32" stroke="rgba(255,255,255,.05)" strokeWidth=".15" strokeDasharray="2 1.5"/>
        <path d="M 30 8 L 32 90" stroke="rgba(255,255,255,.05)" strokeWidth=".15" strokeDasharray="2 1.5"/>
        <path d="M 10 50 Q 50 55 95 70" stroke="rgba(255,255,255,.05)" strokeWidth=".15" strokeDasharray="2 1.5" fill="none"/>

        {/* heatmap rings — congestion */}
        <circle cx="74" cy="74" r="14" fill="rgba(255,84,112,.08)"/>
        <circle cx="74" cy="74" r="9"  fill="rgba(255,84,112,.12)"/>
        <circle cx="18" cy="62" r="12" fill="rgba(255,84,112,.06)"/>
        <circle cx="32" cy="34" r="10" fill="rgba(255,181,71,.06)"/>
      </svg>

      {/* Region labels */}
      <div style={{position:'absolute',inset:0,pointerEvents:'none',color:'var(--ink-3)',fontFamily:'var(--mono)'}}>
        {[
          { t:'IKEJA', x:'30%', y:'24%' },
          { t:'AGEGE', x:'12%', y:'18%' },
          { t:'LAGOS WEST', x:'18%', y:'58%' },
          { t:'IKOYI', x:'58%', y:'60%' },
          { t:'V.I.', x:'64%', y:'66%' },
          { t:'LEKKI', x:'78%', y:'70%' },
          { t:'APAPA', x:'26%', y:'70%' },
          { t:'FESTAC', x:'6%', y:'64%' },
        ].map((r,i)=>(
          <div key={i} className="uppr" style={{
            position:'absolute',left:r.x,top:r.y,
            fontSize:9,letterSpacing:'.18em',opacity:.5,
            transform:'translate(-50%,-50%)'
          }}>{r.t}</div>
        ))}
      </div>

      {/* Towers */}
      {TOWERS.map(t => {
        const c = t.status==='critical'?'var(--crit)':t.status==='warn'?'var(--warn)':'var(--accent)';
        const sel = selectedId===t.id;
        return (
          <div key={t.id}
            onMouseEnter={()=>setHover(t)} onMouseLeave={()=>setHover(null)}
            onClick={()=>onSelect && onSelect(t)}
            style={{
              position:'absolute',left:`${t.x}%`,top:`${t.y}%`,
              transform:'translate(-50%,-50%)',cursor:'pointer'
            }}>
            {t.status!=='ok' && (
              <div style={{
                position:'absolute',left:'50%',top:'50%',
                width:24,height:24,marginLeft:-12,marginTop:-12,borderRadius:'50%',
                background:c,opacity:.4,
                animation:'pulse-ring 1.8s infinite'
              }}/>
            )}
            <div style={{
              width:sel?14:10,height:sel?14:10,borderRadius:2,
              background:c,
              boxShadow:`0 0 0 2px var(--bg), 0 0 12px ${c}`,
              transform:'rotate(45deg)',
              transition:'all .15s'
            }}/>
            {sel && (
              <div style={{
                position:'absolute',left:'50%',top:'50%',
                width:36,height:36,marginLeft:-18,marginTop:-18,
                border:'1px solid var(--accent)',borderRadius:'50%',
                pointerEvents:'none'
              }}/>
            )}
          </div>
        );
      })}

      {/* Crowd-sourced reports (small dots) */}
      {mode==='public' && (
        <>
          {[[16,60],[20,64],[22,58],[76,72],[72,76],[80,74]].map(([x,y],i)=>(
            <div key={i} style={{
              position:'absolute',left:`${x}%`,top:`${y}%`,
              transform:'translate(-50%,-50%)',
              width:6,height:6,borderRadius:'50%',
              background:'rgba(91,140,255,.6)',
              boxShadow:'0 0 8px rgba(91,140,255,.6)'
            }}/>
          ))}
        </>
      )}

      {/* Hover tooltip */}
      {hover && (
        <div style={{
          position:'absolute',
          left:`${hover.x}%`,top:`${hover.y}%`,
          transform:`translate(${hover.x>60?'-110%':'10%'}, -50%)`,
          background:'var(--bg-2)',border:'1px solid var(--line-2)',
          padding:'8px 10px',borderRadius:6,minWidth:180,
          fontSize:11,pointerEvents:'none',zIndex:5,
          boxShadow:'0 8px 24px rgba(0,0,0,.5)',
          animation:'fadein .12s ease-out'
        }}>
          <div className="mono" style={{fontSize:10,color:'var(--accent)',marginBottom:3}}>{hover.id}</div>
          <div style={{fontWeight:500,marginBottom:4}}>{hover.name}</div>
          <div className="mono" style={{display:'flex',justifyContent:'space-between',color:'var(--ink-3)',fontSize:10}}>
            <span>SIG {hover.signal}%</span><span>LOAD {hover.load}%</span>
          </div>
          {hover.issue && <div style={{marginTop:6,fontSize:10.5,color:hover.status==='critical'?'var(--crit)':'var(--warn)'}}>⚠ {hover.issue}</div>}
        </div>
      )}

      {/* Compass / scale */}
      {!compact && (
        <>
          <div style={{position:'absolute',top:14,right:14,padding:'6px 10px',background:'rgba(10,14,22,.7)',border:'1px solid var(--line)',borderRadius:6,fontFamily:'var(--mono)',fontSize:10,color:'var(--ink-3)',letterSpacing:'.10em'}}>N ↑ · 6.50°N 3.40°E</div>
          <div style={{position:'absolute',bottom:14,left:14,display:'flex',alignItems:'center',gap:8,fontFamily:'var(--mono)',fontSize:10,color:'var(--ink-3)'}}>
            <div style={{width:60,height:2,background:'var(--ink-3)'}}/>
            <span>5 km</span>
          </div>
          {/* Legend */}
          <div style={{position:'absolute',bottom:14,right:14,padding:'8px 10px',background:'rgba(10,14,22,.7)',border:'1px solid var(--line)',borderRadius:6,display:'flex',flexDirection:'column',gap:4,fontFamily:'var(--mono)',fontSize:10}}>
            <Legend c="var(--accent)" t="OPTIMAL"/>
            <Legend c="var(--warn)" t="DEGRADED"/>
            <Legend c="var(--crit)" t="CRITICAL"/>
            {mode==='public' && <Legend c="var(--info)" t="USER REPORT" round/>}
          </div>
        </>
      )}
    </div>
  );
}
function Legend({ c, t, round }){
  return (
    <div style={{display:'flex',alignItems:'center',gap:6,color:'var(--ink-2)',letterSpacing:'.10em'}}>
      <div style={{width:8,height:8,background:c,transform:round?'none':'rotate(45deg)',borderRadius:round?'50%':0,boxShadow:`0 0 8px ${c}`}}/>
      <span>{t}</span>
    </div>
  );
}

function MapPage({ role }){
  const [sel, setSel] = React.useState(TOWERS.find(t=>t.status==='critical'));
  const [mode, setMode] = React.useState('engineer');
  return (
    <>
      <TopBar title="Network Map" sub="Lagos metro · 1,284 active towers · live signal & outage overlay"
        right={
          <div style={{display:'flex',gap:6,padding:3,background:'var(--bg-1)',border:'1px solid var(--line)',borderRadius:7}}>
            {[['engineer','Engineer'],['public','Public']].map(([k,l])=>(
              <button key={k} onClick={()=>setMode(k)} style={{
                appearance:'none',border:0,padding:'5px 12px',borderRadius:5,fontSize:11,fontWeight:500,
                background:mode===k?'var(--bg-3)':'transparent',
                color:mode===k?'var(--ink)':'var(--ink-3)',cursor:'pointer'
              }}>{l}</button>
            ))}
          </div>
        }/>
      <div style={{padding:22,display:'grid',gridTemplateColumns:'1fr 320px',gap:14,height:'calc(100vh - 67px)'}}>
        <NetworkMap onSelect={setSel} selectedId={sel?.id} mode={mode}/>
        <div style={{display:'flex',flexDirection:'column',gap:14,overflowY:'auto'}}>
          {sel && (
            <C.Card pad={14}>
              <div style={{display:'flex',justifyContent:'space-between',alignItems:'flex-start',marginBottom:10}}>
                <div>
                  <div className="mono" style={{fontSize:10,color:'var(--accent)',marginBottom:3}}>{sel.id}</div>
                  <div style={{fontSize:14,fontWeight:600}}>{sel.name}</div>
                  <div className="mono" style={{fontSize:10,color:'var(--ink-3)',marginTop:2}}>{sel.region.toUpperCase()}</div>
                </div>
                <C.Pill tone={sel.status==='critical'?'crit':sel.status==='warn'?'warn':'ok'} dot>{sel.status}</C.Pill>
              </div>
              {sel.issue && (
                <div style={{padding:10,background:'var(--bg-3)',borderRadius:6,fontSize:11.5,marginBottom:10,borderLeft:`2px solid ${sel.status==='critical'?'var(--crit)':'var(--warn)'}`}}>
                  {sel.issue}
                </div>
              )}
              <div style={{display:'grid',gridTemplateColumns:'1fr 1fr',gap:10,marginTop:6}}>
                <Metric label="SIGNAL" v={sel.signal} unit="%" tone={sel.signal>70?'ok':sel.signal>40?'warn':'crit'}/>
                <Metric label="LOAD" v={sel.load} unit="%" tone={sel.load>85?'crit':sel.load>70?'warn':'ok'}/>
              </div>
              <div style={{display:'flex',gap:6,marginTop:14}}>
                <C.Btn primary small>Diagnose</C.Btn>
                <C.Btn small>Dispatch</C.Btn>
                <C.Btn ghost small>History</C.Btn>
              </div>
            </C.Card>
          )}
          <C.Section label="REGIONS">
            <C.Card pad={0}>
              {['Lagos West','Ikeja','Lekki','Victoria Island','Ikoyi','Apapa','Festac','Agege'].map((r,i)=>{
                const towers = TOWERS.filter(t=>t.region===r);
                const crit = towers.filter(t=>t.status==='critical').length;
                const warn = towers.filter(t=>t.status==='warn').length;
                const tone = crit?'crit':warn?'warn':'ok';
                return (
                  <div key={r} style={{padding:'10px 14px',borderBottom:i<7?'1px solid var(--line)':0,display:'flex',justifyContent:'space-between',alignItems:'center'}}>
                    <div>
                      <div style={{fontSize:12.5}}>{r}</div>
                      <div className="mono" style={{fontSize:10,color:'var(--ink-3)',marginTop:2}}>{towers.length} towers</div>
                    </div>
                    <C.Pill tone={tone} dot>{crit?`${crit} crit`:warn?`${warn} warn`:'ok'}</C.Pill>
                  </div>
                );
              })}
            </C.Card>
          </C.Section>
        </div>
      </div>
    </>
  );
}
function Metric({label,v,unit,tone}){
  return (
    <div>
      <div className="mono uppr" style={{fontSize:9,color:'var(--ink-3)',letterSpacing:'.12em',marginBottom:4}}>{label}</div>
      <div className="mono" style={{fontSize:18,fontWeight:600,marginBottom:5,color:tone==='crit'?'var(--crit)':tone==='warn'?'var(--warn)':'var(--ok)'}}>{v}<span style={{color:'var(--ink-3)',fontSize:11,marginLeft:2}}>{unit}</span></div>
      <C.Bar pct={v} tone={tone}/>
    </div>
  );
}

window.NetworkMap = NetworkMap;
window.MapPage = MapPage;
