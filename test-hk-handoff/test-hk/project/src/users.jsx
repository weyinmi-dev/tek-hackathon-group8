// Users & RBAC

function UsersPage({ role }){
  const [sel, setSel] = React.useState(USERS[0]);
  const caps = ROLE_CAPS[sel.role] || [];
  return (
    <>
      <TopBar title="Users & Roles" sub={`${USERS.length} accounts · 4 roles · OAuth2 / Azure AD federated`}
        right={<C.Btn primary>+ Invite user</C.Btn>}/>
      <div style={{padding:22,display:'grid',gridTemplateColumns:'1fr 360px',gap:14}}>
        <C.Card pad={0}>
          <div style={{padding:'12px 14px',borderBottom:'1px solid var(--line)',display:'grid',gridTemplateColumns:'2fr 1fr 1.2fr 1.2fr 1fr',gap:10,fontSize:10,fontFamily:'var(--mono)',color:'var(--ink-3)',letterSpacing:'.12em',textTransform:'uppercase'}}>
            <span>USER</span><span>ROLE</span><span>TEAM</span><span>REGION</span><span>LAST ACTIVE</span>
          </div>
          {USERS.map((u,i)=>{
            const active = sel.handle===u.handle;
            return (
              <button key={u.handle} onClick={()=>setSel(u)} style={{
                appearance:'none',width:'100%',textAlign:'left',
                padding:'12px 14px',borderBottom:i<USERS.length-1?'1px solid var(--line)':0,
                display:'grid',gridTemplateColumns:'2fr 1fr 1.2fr 1.2fr 1fr',gap:10,
                background:active?'var(--bg-2)':'transparent',
                border:'none',
                borderLeft:'3px solid '+(active?'var(--accent)':'transparent'),
                color:'var(--ink)',cursor:'pointer',alignItems:'center',fontSize:12.5
              }}>
                <div style={{display:'flex',alignItems:'center',gap:10}}>
                  <div style={{width:28,height:28,borderRadius:5,background:'var(--bg-3)',border:'1px solid var(--line-2)',display:'grid',placeItems:'center',fontFamily:'var(--mono)',fontSize:10,fontWeight:600}}>{u.init}</div>
                  <div>
                    <div style={{fontWeight:500}}>{u.name}</div>
                    <div className="mono" style={{fontSize:10,color:'var(--ink-3)',marginTop:1}}>{u.handle}</div>
                  </div>
                </div>
                <C.Pill tone={u.role==='admin'?'crit':u.role==='manager'?'warn':u.role==='engineer'?'accent':'neutral'}>{u.role}</C.Pill>
                <span style={{color:'var(--ink-2)'}}>{u.team}</span>
                <span style={{color:'var(--ink-2)'}}>{u.region}</span>
                <span className="mono" style={{fontSize:10.5,color:u.last==='active now'?'var(--ok)':'var(--ink-3)'}}>
                  {u.last==='active now' && <span style={{display:'inline-block',width:6,height:6,borderRadius:'50%',background:'var(--ok)',marginRight:6,boxShadow:'0 0 6px var(--ok)'}}/>}
                  {u.last}
                </span>
              </button>
            );
          })}
        </C.Card>

        <div style={{display:'flex',flexDirection:'column',gap:14}}>
          <C.Card pad={16}>
            <div style={{display:'flex',gap:12,alignItems:'center',marginBottom:14}}>
              <div style={{width:48,height:48,borderRadius:8,background:'linear-gradient(135deg,var(--bg-3),var(--bg-2))',border:'1px solid var(--line-2)',display:'grid',placeItems:'center',fontFamily:'var(--mono)',fontSize:16,fontWeight:600}}>{sel.init}</div>
              <div>
                <div style={{fontSize:14,fontWeight:600}}>{sel.name}</div>
                <div className="mono" style={{fontSize:10.5,color:'var(--ink-3)',marginTop:2}}>{sel.handle}@telco.lag</div>
              </div>
            </div>
            <Row k="Role" v={sel.role}/>
            <Row k="Team" v={sel.team}/>
            <Row k="Region scope" v={sel.region}/>
            <Row k="MFA" v="Enabled · TOTP"/>
            <Row k="Last login" v={sel.last} last/>
          </C.Card>

          <C.Section label="CAPABILITIES (RBAC)">
            <C.Card pad={14}>
              <div style={{display:'flex',flexWrap:'wrap',gap:6}}>
                {(caps[0]==='*' ? ['copilot.read','copilot.write','tower.diagnose','alerts.read','alerts.ack','alerts.assign','rbac.update','reports.export','audit.read','users.manage','*.admin'] : caps).map(c=>(
                  <span key={c} className="mono" style={{
                    fontSize:10.5,padding:'3px 8px',borderRadius:3,
                    background:'var(--accent-dim)',color:'var(--accent)',
                    border:'1px solid var(--accent-line)'
                  }}>{c}</span>
                ))}
              </div>
              <div className="mono" style={{fontSize:10,color:'var(--ink-3)',marginTop:12}}>
                {sel.role==='admin' ? '⚠ ADMIN — wildcard scope. All actions audited.' : 'Permissions granted via role · auditable in Audit Log'}
              </div>
            </C.Card>
          </C.Section>
        </div>
      </div>
    </>
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

window.UsersPage = UsersPage;
