// Sidebar navigation + top status bar

function Sidebar({ route, setRoute, role, user }){
  const nav = [
    { id:'command',  label:'Command Center', icon:'◉', section:'OPS' },
    { id:'copilot',  label:'Copilot',        icon:'✦', section:'OPS' },
    { id:'map',      label:'Network Map',    icon:'◎', section:'OPS' },
    { id:'alerts',   label:'Alerts',         icon:'△', section:'OPS', badge:'14' },
    { id:'energy',   label:'Energy Sites',   icon:'⚡', section:'ENERGY' },
    { id:'anomalies',label:'Anomalies',      icon:'⚠', section:'ENERGY', badge:'3' },
    { id:'optimize', label:'Optimization',   icon:'∿', section:'ENERGY' },
    { id:'dashboard',label:'Dashboard',      icon:'▤', section:'INSIGHTS' },
    { id:'users',    label:'Users & Roles',  icon:'◆', section:'ADMIN', adminOnly:false },
    { id:'audit',    label:'Audit Log',      icon:'≡', section:'ADMIN' },
  ];
  const sections = ['OPS','ENERGY','INSIGHTS','ADMIN'];

  return (
    <aside style={{
      borderRight:'1px solid var(--line)',
      background:'var(--bg-1)',
      display:'flex',flexDirection:'column',
      position:'sticky',top:0,height:'100vh'
    }}>
      {/* Brand */}
      <div style={{padding:'18px 18px 14px',borderBottom:'1px solid var(--line)',display:'flex',alignItems:'center',gap:10}}>
        <div style={{
          width:28,height:28,borderRadius:6,
          background:'var(--accent)',color:'#001a10',
          display:'grid',placeItems:'center',fontWeight:700,
          fontFamily:'var(--mono)',fontSize:14,letterSpacing:'-.02em',
          boxShadow:'0 0 24px var(--accent-dim)'
        }}>◉</div>
        <div style={{display:'flex',flexDirection:'column',lineHeight:1.1}}>
          <div style={{fontWeight:600,fontSize:14,letterSpacing:'-.01em'}}>TelcoPilot</div>
          <div className="mono uppr" style={{fontSize:9,color:'var(--ink-3)',letterSpacing:'.14em',marginTop:2}}>NOC · LAGOS</div>
        </div>
      </div>

      {/* Nav */}
      <nav style={{flex:1,overflowY:'auto',padding:'10px 10px 14px'}}>
        {sections.map(s => (
          <div key={s} style={{marginTop:14}}>
            <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',padding:'4px 10px 6px',letterSpacing:'.14em'}}>{s}</div>
            {nav.filter(n=>n.section===s).map(n => {
              const active = route===n.id;
              return (
                <button key={n.id} onClick={()=>setRoute(n.id)}
                  style={{
                    width:'100%',textAlign:'left',
                    display:'flex',alignItems:'center',gap:10,
                    padding:'8px 10px',borderRadius:6,
                    background: active ? 'var(--bg-3)' : 'transparent',
                    border:'1px solid '+(active?'var(--line-2)':'transparent'),
                    color: active ? 'var(--ink)' : 'var(--ink-2)',
                    cursor:'pointer',fontSize:13,fontWeight:active?500:400,
                    position:'relative'
                  }}>
                  <span style={{
                    width:18,height:18,display:'grid',placeItems:'center',
                    color: active?'var(--accent)':'var(--ink-3)',
                    fontFamily:'var(--mono)',fontSize:12
                  }}>{n.icon}</span>
                  <span style={{flex:1}}>{n.label}</span>
                  {n.badge && (
                    <span className="mono" style={{
                      fontSize:9.5,padding:'2px 6px',borderRadius:3,
                      background:'var(--crit)',color:'#fff',fontWeight:600
                    }}>{n.badge}</span>
                  )}
                  {active && <span style={{position:'absolute',left:0,top:8,bottom:8,width:2,background:'var(--accent)',borderRadius:2}}/>}
                </button>
              );
            })}
          </div>
        ))}
      </nav>

      {/* User */}
      <div style={{padding:12,borderTop:'1px solid var(--line)',display:'flex',gap:10,alignItems:'center'}}>
        <div style={{
          width:32,height:32,borderRadius:6,
          background:'linear-gradient(135deg,var(--bg-3),var(--bg-2))',
          border:'1px solid var(--line-2)',
          display:'grid',placeItems:'center',
          fontFamily:'var(--mono)',fontSize:11,fontWeight:600,color:'var(--ink)'
        }}>{user.init}</div>
        <div style={{flex:1,minWidth:0}}>
          <div style={{fontSize:12,fontWeight:500,whiteSpace:'nowrap',overflow:'hidden',textOverflow:'ellipsis'}}>{user.name}</div>
          <div className="mono uppr" style={{fontSize:9,color:'var(--accent)',letterSpacing:'.12em',marginTop:1}}>● {role}</div>
        </div>
      </div>
    </aside>
  );
}

function TopBar({ title, sub, right }){
  const [now, setNow] = React.useState(()=>new Date());
  React.useEffect(()=>{
    const i = setInterval(()=>setNow(new Date()),1000);
    return ()=>clearInterval(i);
  },[]);
  const t = now.toTimeString().slice(0,8);
  return (
    <div style={{
      display:'flex',alignItems:'center',justifyContent:'space-between',
      padding:'14px 22px',borderBottom:'1px solid var(--line)',
      background:'var(--bg)',position:'sticky',top:0,zIndex:5,
      backdropFilter:'blur(8px)'
    }}>
      <div style={{display:'flex',flexDirection:'column',lineHeight:1.2}}>
        <div style={{fontSize:18,fontWeight:600,letterSpacing:'-.01em'}}>{title}</div>
        {sub && <div className="mono" style={{fontSize:11,color:'var(--ink-3)',marginTop:3}}>{sub}</div>}
      </div>
      <div style={{display:'flex',alignItems:'center',gap:14}}>
        {right}
        <div className="mono" style={{display:'flex',alignItems:'center',gap:8,fontSize:11,color:'var(--ink-2)',padding:'6px 10px',background:'var(--bg-1)',border:'1px solid var(--line)',borderRadius:6}}>
          <span className="dot"/>
          <span>LIVE · WAT {t}</span>
        </div>
      </div>
    </div>
  );
}

window.Sidebar = Sidebar;
window.TopBar = TopBar;
