// App shell — auth gate, routing, role/theme tweaks

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "role": "engineer",
  "theme": "dark",
  "demo": "fiber-cut"
}/*EDITMODE-END*/;

function App(){
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);
  const [authed, setAuthed] = React.useState(false);
  const [route, setRoute] = React.useState('command');

  React.useEffect(()=>{
    document.body.className = t.theme==='light' ? 'theme-light' : 'theme-dark';
  },[t.theme]);

  const userByRole = {
    engineer: USERS[0],
    manager:  USERS[1],
    admin:    USERS[2],
  };
  const user = userByRole[t.role] || USERS[0];

  if (!authed) {
    return (
      <>
        <Login onAuth={()=>setAuthed(true)}/>
        <TweaksPanel title="Tweaks">
          <TweakSection label="Theme">
            <TweakRadio label="Mode" value={t.theme} options={['dark','light']} onChange={v=>setTweak('theme',v)}/>
          </TweakSection>
        </TweaksPanel>
      </>
    );
  }

  const Page = ({
    command:   <CommandCenter role={t.role}/>,
    copilot:   <Copilot role={t.role}/>,
    map:       <MapPage role={t.role}/>,
    alerts:    <AlertsPage role={t.role}/>,
    dashboard: <Dashboard/>,
    users:     <UsersPage role={t.role}/>,
    audit:     <AuditPage/>,
  })[route] || <CommandCenter role={t.role}/>;

  return (
    <>
      <div className="app">
        <Sidebar route={route} setRoute={setRoute} role={t.role} user={user}/>
        <main style={{display:'flex',flexDirection:'column',minHeight:0,overflowY:'auto',height:'100vh'}}>
          {Page}
        </main>
      </div>

      <TweaksPanel title="Tweaks">
        <TweakSection label="Demo Persona (RBAC)">
          <TweakRadio label="Role"
            value={t.role}
            options={['engineer','manager','admin']}
            onChange={v=>setTweak('role',v)}/>
          <div className="mono" style={{fontSize:10,color:'rgba(41,38,27,.5)',marginTop:4,letterSpacing:'.04em'}}>
            Switches user, sidebar, and gates write actions.
          </div>
        </TweakSection>
        <TweakSection label="Theme">
          <TweakRadio label="Mode" value={t.theme} options={['dark','light']} onChange={v=>setTweak('theme',v)}/>
        </TweakSection>
        <TweakSection label="Demo">
          <TweakButton label="Sign out" secondary onClick={()=>setAuthed(false)}/>
        </TweakSection>
      </TweaksPanel>
    </>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
