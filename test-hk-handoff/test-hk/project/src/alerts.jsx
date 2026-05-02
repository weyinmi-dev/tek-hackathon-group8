// Alerts feed — incidents, severity filters, AI summaries

function AlertsPage({ role }){
  const [filter, setFilter] = React.useState('all');
  const [sel, setSel] = React.useState(ALERTS[0]);
  const filtered = filter==='all' ? ALERTS : ALERTS.filter(a=>a.sev===filter);

  return (
    <>
      <TopBar title="Smart Alerts" sub={`${ALERTS.filter(a=>a.status==='active').length} active · AI-summarized · pattern detection enabled`}
        right={
          <div style={{display:'flex',gap:6,padding:3,background:'var(--bg-1)',border:'1px solid var(--line)',borderRadius:7}}>
            {[['all','All',ALERTS.length],['critical','Critical',ALERTS.filter(a=>a.sev==='critical').length],['warn','Warn',ALERTS.filter(a=>a.sev==='warn').length],['info','Info',ALERTS.filter(a=>a.sev==='info').length]].map(([k,l,n])=>(
              <button key={k} onClick={()=>setFilter(k)} style={{
                appearance:'none',border:0,padding:'5px 12px',borderRadius:5,fontSize:11,fontWeight:500,
                background:filter===k?'var(--bg-3)':'transparent',
                color:filter===k?'var(--ink)':'var(--ink-3)',cursor:'pointer',
                display:'flex',alignItems:'center',gap:6
              }}>{l} <span className="mono" style={{fontSize:9.5,color:'var(--ink-3)'}}>{n}</span></button>
            ))}
          </div>
        }/>
      <div style={{padding:22,display:'grid',gridTemplateColumns:'1fr 380px',gap:14,height:'calc(100vh - 67px)'}}>
        <div style={{display:'flex',flexDirection:'column',gap:10,overflowY:'auto',paddingRight:4}}>
          {filtered.map(a => (
            <button key={a.id} onClick={()=>setSel(a)} style={{
              appearance:'none',textAlign:'left',cursor:'pointer',
              background:sel?.id===a.id?'var(--bg-2)':'var(--bg-1)',
              border:'1px solid '+(sel?.id===a.id?'var(--accent-line)':'var(--line)'),
              borderRadius:8,padding:14,position:'relative',
              borderLeft:`3px solid ${a.sev==='critical'?'var(--crit)':a.sev==='warn'?'var(--warn)':'var(--info)'}`
            }}>
              <div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:8}}>
                <div style={{display:'flex',alignItems:'center',gap:8}}>
                  <C.Pill tone={a.sev==='critical'?'crit':a.sev==='warn'?'warn':'info'} dot>{a.sev}</C.Pill>
                  <span className="mono" style={{fontSize:10,color:'var(--ink-3)'}}>{a.id}</span>
                  <span className="mono" style={{fontSize:10,color:'var(--ink-3)'}}>· {a.region}</span>
                </div>
                <span className="mono" style={{fontSize:10,color:'var(--ink-3)'}}>{a.time}</span>
              </div>
              <div style={{fontSize:14,fontWeight:500,marginBottom:6,color:'var(--ink)'}}>{a.title}</div>
              <div style={{fontSize:12,color:'var(--ink-2)',lineHeight:1.5}}>
                <span style={{color:'var(--accent)',fontFamily:'var(--mono)',fontSize:10,marginRight:6}}>AI</span>
                {a.cause}
              </div>
              <div style={{display:'flex',gap:14,marginTop:10,fontSize:10.5,color:'var(--ink-3)',fontFamily:'var(--mono)'}}>
                <span>👥 {a.users>0?a.users.toLocaleString()+' affected':'no users impacted'}</span>
                <span>· {a.tower}</span>
                <span style={{marginLeft:'auto',color:'var(--ink-2)'}}>conf {Math.round(a.confidence*100)}%</span>
              </div>
            </button>
          ))}
        </div>
        {sel && (
          <div style={{display:'flex',flexDirection:'column',gap:14,overflowY:'auto'}}>
            <C.Card pad={16}>
              <div style={{marginBottom:10}}>
                <C.Pill tone={sel.sev==='critical'?'crit':sel.sev==='warn'?'warn':'info'} dot>{sel.id}</C.Pill>
              </div>
              <div style={{fontSize:16,fontWeight:600,marginBottom:6}}>{sel.title}</div>
              <div className="mono" style={{fontSize:10.5,color:'var(--ink-3)'}}>RAISED {sel.time} · {sel.region.toUpperCase()}</div>
            </C.Card>
            <C.Section label="AI ROOT-CAUSE">
              <C.Card pad={14}>
                <div style={{fontSize:12.5,lineHeight:1.6,color:'var(--ink-2)'}}>{sel.cause}</div>
                <div style={{marginTop:12,padding:'8px 10px',background:'var(--bg-3)',borderRadius:5,fontSize:11,fontFamily:'var(--mono)',color:'var(--accent)'}}>
                  pattern_match: 11 prior fiber-cut incidents
                </div>
              </C.Card>
            </C.Section>
            <C.Section label="IMPACT">
              <C.Card pad={14}>
                <Row k="Subscribers affected" v={sel.users.toLocaleString()}/>
                <Row k="Tower(s)" v={sel.tower}/>
                <Row k="Confidence" v={Math.round(sel.confidence*100)+'%'}/>
                <Row k="Status" v={sel.status} last/>
              </C.Card>
            </C.Section>
            {role!=='viewer' && (
              <div style={{display:'flex',gap:6,flexWrap:'wrap'}}>
                <C.Btn primary>Acknowledge</C.Btn>
                <C.Btn>Assign</C.Btn>
                <C.Btn>Dispatch field</C.Btn>
                <C.Btn ghost>Open in Copilot →</C.Btn>
              </div>
            )}
          </div>
        )}
      </div>
    </>
  );
}
function Row({k,v,last}){
  return (
    <div style={{display:'flex',justifyContent:'space-between',padding:'8px 0',borderBottom:last?0:'1px solid var(--line)',fontSize:12}}>
      <span style={{color:'var(--ink-3)'}}>{k}</span>
      <span className="mono">{v}</span>
    </div>
  );
}

window.AlertsPage = AlertsPage;
