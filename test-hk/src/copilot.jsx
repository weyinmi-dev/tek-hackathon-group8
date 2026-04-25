// Copilot — natural language network intelligence with live Claude

const SUGGESTED = [
  "Why is Lagos West slow?",
  "Show all outages in the last 2 hours",
  "Which tower is causing packet loss in Ikeja?",
  "Predict the next likely failure",
  "Compare Lekki vs Victoria Island latency",
];

// Semantic Kernel "skills" — fired in sequence to demonstrate agentic flow
const SKILL_PLAN = [
  { skill:'IntentParser',         fn:'parseQuery',        ms:420 },
  { skill:'NetworkDiagnostics',   fn:'queryMetrics',      ms:680 },
  { skill:'OutageAnalyzer',       fn:'detectAnomalies',   ms:540 },
  { skill:'RootCauseEngine',      fn:'inferCause',        ms:760 },
  { skill:'RecommendationSkill',  fn:'suggestActions',    ms:480 },
];

function Copilot({ embedded, role }){
  const [messages, setMessages] = React.useState([
    {
      role:'system',
      content:'TelcoPilot · v1.4 · Powered by Azure OpenAI + Semantic Kernel · Context: Lagos metro NOC',
    },
  ]);
  const [input, setInput] = React.useState('');
  const [busy, setBusy] = React.useState(false);
  const [skillState, setSkillState] = React.useState([]);
  const scrollRef = React.useRef(null);

  React.useEffect(()=>{
    if (scrollRef.current) scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  },[messages,skillState,busy]);

  const ask = async (q) => {
    if (!q.trim() || busy) return;
    setInput('');
    setBusy(true);
    setMessages(m => [...m, { role:'user', content:q }]);

    // Step through SK skills with delays
    setSkillState([]);
    for (let i=0;i<SKILL_PLAN.length;i++){
      const s = SKILL_PLAN[i];
      setSkillState(prev => [...prev, { ...s, status:'running' }]);
      await new Promise(r=>setTimeout(r, s.ms));
      setSkillState(prev => prev.map((p,idx)=> idx===i?{...p,status:'done'}:p));
    }

    // Decide on charts/maps to attach based on query keywords
    const ql = q.toLowerCase();
    const attachments = [];
    if (/lagos west|surulere|mushin|yaba/.test(ql)) attachments.push('lagosWestChart','miniMap-lagosWest');
    if (/lekki|fiber|packet/.test(ql))            attachments.push('lekkiChart','miniMap-lekki');
    if (/outage|incident|down/.test(ql))          attachments.push('outageTable');
    if (/predict|fail|forecast/.test(ql))         attachments.push('predictChart');
    if (/ikeja|allen/.test(ql))                   attachments.push('miniMap-ikeja','ikejaChart');
    if (attachments.length===0)                   attachments.push('lagosWestChart','outageTable');

    // Live Claude call — TelcoPilot system prompt
    let answer = '';
    try {
      const sys = `You are TelcoPilot, an AI assistant embedded in a telco Network Operations Center for a Lagos, Nigeria metro carrier.

Live network state (JSON):
${JSON.stringify({towers:TOWERS.slice(0,8), alerts:ALERTS.slice(0,4)})}

Format your reply in this exact structure, using plain text (no markdown headers):

ROOT CAUSE
<2-3 sentences identifying the most likely cause, citing specific tower IDs (e.g. TWR-LEK-003) and metrics>

AFFECTED
<bullet list of 2-4 items: regions, tower IDs, subscriber counts>

RECOMMENDED ACTIONS
<numbered list of 3 concrete actions an engineer can take now>

CONFIDENCE
<single number 0-100 followed by " %" and a one-line justification>

Keep total reply under 180 words. Be specific. Cite tower IDs.`;
      answer = await window.claude.complete({
        messages:[{ role:'user', content: q }],
        system: sys,
      });
    } catch(e){
      answer = `ROOT CAUSE
Backhaul fiber degradation on TG-LEK-A serving TWR-LEK-003 (Lekki Phase 1) — packet loss climbed from 2% to 60% in the last 12 minutes. Correlated with civil works permit issued in the area at 16:50.

AFFECTED
• Lekki Phase 1 — 14,200 subscribers
• Spillover to TWR-LEK-008 (Phase 2) at 88% load
• 4G voice + data, no impact on 5G NSA

RECOMMENDED ACTIONS
1. Dispatch field-team-3 to fiber junction LJ-7 (ETA 22 min)
2. Auto-shed traffic from LEK-003 → LEK-008, LEK-014
3. Open ticket with civil-works contractor — request immediate halt

CONFIDENCE
92 % — pattern matches 11 prior fiber-cut incidents this quarter.`;
    }

    setMessages(m => [...m, { role:'assistant', content: answer, attachments, query:q }]);
    setBusy(false);
    setSkillState([]);
  };

  return (
    <div style={{display:'flex',flexDirection:'column',height:'100%',minHeight:0}}>
      {!embedded && <TopBar title="Copilot" sub="Natural language interface · Azure OpenAI + Semantic Kernel · 5 active skills"/>}

      <div ref={scrollRef} style={{flex:1,overflowY:'auto',padding:embedded?14:'22px 22px 14px',display:'flex',flexDirection:'column',gap:16}}>
        {messages.map((m,i)=>(
          <Message key={i} m={m} embedded={embedded}/>
        ))}
        {busy && <SkillTrace steps={skillState}/>}
      </div>

      {/* Suggestions */}
      {messages.length<=1 && !busy && (
        <div style={{padding:embedded?'0 14px 10px':'0 22px 10px',display:'flex',flexWrap:'wrap',gap:6}}>
          {SUGGESTED.map(s=>(
            <button key={s} onClick={()=>ask(s)} style={{
              appearance:'none',border:'1px solid var(--line-2)',background:'var(--bg-1)',
              color:'var(--ink-2)',padding:'6px 10px',borderRadius:14,fontSize:11.5,
              fontFamily:'var(--mono)',cursor:'pointer',transition:'all .15s'
            }}
            onMouseEnter={e=>{e.currentTarget.style.borderColor='var(--accent-line)';e.currentTarget.style.color='var(--accent)'}}
            onMouseLeave={e=>{e.currentTarget.style.borderColor='var(--line-2)';e.currentTarget.style.color='var(--ink-2)'}}
            >→ {s}</button>
          ))}
        </div>
      )}

      {/* Composer */}
      <div style={{padding:embedded?14:'14px 22px 22px',borderTop:'1px solid var(--line)',background:'var(--bg-1)'}}>
        <div style={{
          display:'flex',alignItems:'center',gap:10,
          padding:'10px 12px',background:'var(--bg-2)',
          border:'1px solid var(--line-2)',borderRadius:8,
          transition:'border .15s'
        }}>
          <span className="mono" style={{color:'var(--accent)',fontSize:13,fontWeight:600}}>›</span>
          <input
            value={input}
            onChange={e=>setInput(e.target.value)}
            onKeyDown={e=>{ if(e.key==='Enter') ask(input); }}
            placeholder='Ask: "why is Lagos West slow?"'
            style={{
              flex:1,border:0,background:'transparent',outline:'none',
              fontSize:13.5,color:'var(--ink)',fontFamily:'var(--sans)'
            }}
            disabled={busy}/>
          <span className="mono" style={{fontSize:10,color:'var(--ink-3)'}}>⏎</span>
          <C.Btn primary small onClick={()=>ask(input)} disabled={busy}>
            {busy ? 'Thinking…' : 'Ask'}
          </C.Btn>
        </div>
        <div className="mono" style={{fontSize:9.5,color:'var(--ink-3)',marginTop:8,letterSpacing:'.08em'}}>
          QUERIES LOGGED · RBAC: {role.toUpperCase()} · MODEL: claude-haiku-4-5 (proxy: azure-openai)
        </div>
      </div>
    </div>
  );
}

function Message({ m, embedded }){
  if (m.role==='system') {
    return <div className="mono" style={{fontSize:10.5,color:'var(--ink-3)',padding:'0 4px',letterSpacing:'.04em',animation:'fadein .3s'}}>⌁ {m.content}</div>;
  }
  if (m.role==='user') {
    return (
      <div style={{alignSelf:'flex-end',maxWidth:'72%',animation:'fadein .25s'}}>
        <div style={{
          padding:'10px 14px',borderRadius:'10px 10px 2px 10px',
          background:'var(--bg-3)',border:'1px solid var(--line-2)',
          fontSize:13.5,lineHeight:1.5
        }}>{m.content}</div>
        <div className="mono uppr" style={{fontSize:9,color:'var(--ink-3)',marginTop:4,textAlign:'right',letterSpacing:'.10em'}}>YOU · {new Date().toTimeString().slice(0,5)}</div>
      </div>
    );
  }
  // assistant
  return (
    <div style={{alignSelf:'flex-start',maxWidth:'92%',width:'100%',animation:'fadein .35s'}}>
      <div className="mono uppr" style={{fontSize:9,color:'var(--accent)',marginBottom:6,letterSpacing:'.14em',display:'flex',alignItems:'center',gap:6}}>
        <span style={{width:6,height:6,borderRadius:'50%',background:'var(--accent)',boxShadow:'0 0 8px var(--accent)'}}/>
        TELCOPILOT · ANSWER
      </div>
      <div style={{
        padding:'14px 16px',borderRadius:'2px 10px 10px 10px',
        background:'var(--bg-1)',border:'1px solid var(--line-2)',
        borderLeft:'2px solid var(--accent)',
        fontSize:13.5,lineHeight:1.6,
        whiteSpace:'pre-wrap'
      }}>
        <FormattedAnswer text={m.content}/>
        {m.attachments && <Attachments list={m.attachments}/>}
      </div>
    </div>
  );
}

function FormattedAnswer({ text }){
  // Highlight section headers + tower IDs
  const parts = text.split('\n').map((line,i) => {
    if (/^(ROOT CAUSE|AFFECTED|RECOMMENDED ACTIONS|CONFIDENCE)$/i.test(line.trim())) {
      return <div key={i} className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.14em',marginTop:i>0?12:0,marginBottom:4}}>{line.trim()}</div>;
    }
    // tower id pattern
    const segs = line.split(/(TWR-[A-Z]+-[A-Z0-9]*-?\d+|TWR-[A-Z]+-\d+|INC-\d+|\d+\s*%)/g);
    return (
      <div key={i} style={{minHeight: line.trim()?'auto':4}}>
        {segs.map((s,j)=>{
          if (/^TWR-/.test(s) || /^INC-/.test(s)) {
            return <span key={j} className="mono" style={{color:'var(--accent)',background:'var(--accent-dim)',padding:'1px 5px',borderRadius:3,fontSize:11.5,whiteSpace:'nowrap'}}>{s}</span>;
          }
          if (/\d+\s*%/.test(s)) {
            return <span key={j} className="mono" style={{color:'var(--warn)',fontWeight:600}}>{s}</span>;
          }
          return <span key={j}>{s}</span>;
        })}
      </div>
    );
  });
  return <div>{parts}</div>;
}

function SkillTrace({ steps }){
  return (
    <div style={{padding:14,background:'var(--bg-1)',border:'1px dashed var(--accent-line)',borderRadius:8,display:'flex',flexDirection:'column',gap:8,animation:'fadein .2s'}}>
      <div className="mono uppr" style={{fontSize:9.5,color:'var(--accent)',letterSpacing:'.14em',display:'flex',alignItems:'center',gap:8}}>
        <span style={{display:'inline-block',width:10,height:10,border:'1.5px solid var(--accent)',borderTopColor:'transparent',borderRadius:'50%',animation:'spin .8s linear infinite'}}/>
        SEMANTIC KERNEL · EXECUTING
      </div>
      {steps.map((s,i)=>(
        <div key={i} className="mono" style={{display:'flex',alignItems:'center',gap:10,fontSize:11.5,color:s.status==='done'?'var(--ink-2)':'var(--ink)'}}>
          <span style={{
            width:14,height:14,borderRadius:3,
            background:s.status==='done'?'var(--accent-dim)':'var(--bg-3)',
            color:s.status==='done'?'var(--accent)':'var(--ink-3)',
            display:'grid',placeItems:'center',fontSize:9,fontWeight:700,
            border:'1px solid '+(s.status==='done'?'var(--accent-line)':'var(--line-2)')
          }}>{s.status==='done'?'✓':i+1}</span>
          <span style={{color:'var(--ink-3)'}}>{s.skill}.</span>
          <span>{s.fn}()</span>
          <span style={{flex:1,height:1,background:'var(--line)'}}/>
          <span style={{color:s.status==='done'?'var(--ok)':'var(--ink-3)',fontSize:10}}>
            {s.status==='done'?`${s.ms}ms`:'…'}
          </span>
        </div>
      ))}
    </div>
  );
}

function Attachments({ list }){
  return (
    <div style={{marginTop:14,display:'flex',flexDirection:'column',gap:10}}>
      {list.includes('lagosWestChart') && <ChartCard title="LATENCY · LAGOS WEST · LAST 2H" data={[34,38,42,48,55,62,72,84,96,110,124,138,142,140,138,135]} unit="ms" threshold={80}/>}
      {list.includes('lekkiChart') && <ChartCard title="PACKET LOSS · TWR-LEK-003" data={[2,2,3,2,4,8,18,32,48,58,60,60,58,55,52,50]} unit="%" threshold={20} tone="crit"/>}
      {list.includes('ikejaChart') && <ChartCard title="JITTER · TWR-IKJ-019" data={[8,9,10,12,15,18,22,28,32,30,28,25,22,20,18,16]} unit="ms" threshold={20} tone="warn"/>}
      {list.includes('predictChart') && <PredictChart/>}
      {list.includes('outageTable') && <OutageTable/>}
      {list.some(a=>a.startsWith('miniMap')) && <MiniMap focus={list.find(a=>a.startsWith('miniMap')).split('-')[1]}/>}
    </div>
  );
}

function ChartCard({ title, data, unit, threshold, tone='accent' }){
  const max = Math.max(...data, threshold||0)*1.1;
  const c = tone==='crit'?'var(--crit)':tone==='warn'?'var(--warn)':'var(--accent)';
  return (
    <div style={{background:'var(--bg-2)',border:'1px solid var(--line)',borderRadius:6,padding:12}}>
      <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:8,display:'flex',justifyContent:'space-between'}}>
        <span>{title}</span>
        <span style={{color:c}}>{data[data.length-1]}{unit}</span>
      </div>
      <svg viewBox="0 0 200 60" style={{width:'100%',height:60,display:'block'}}>
        {threshold && (
          <line x1="0" y1={60-(threshold/max)*60} x2="200" y2={60-(threshold/max)*60} stroke="var(--line-2)" strokeWidth=".5" strokeDasharray="2 2"/>
        )}
        <polyline
          points={data.map((v,i)=>`${(i/(data.length-1))*200},${60-(v/max)*60}`).join(' ')}
          fill="none" stroke={c} strokeWidth="1.5"/>
        {data.map((v,i)=>{
          const breach = threshold && v>threshold;
          return breach ? <circle key={i} cx={(i/(data.length-1))*200} cy={60-(v/max)*60} r="1.5" fill={c}/> : null;
        })}
      </svg>
      <div className="mono" style={{fontSize:9.5,color:'var(--ink-3)',display:'flex',justifyContent:'space-between',marginTop:4}}>
        <span>−2h</span><span>−1h</span><span>now</span>
      </div>
    </div>
  );
}

function PredictChart(){
  return (
    <div style={{background:'var(--bg-2)',border:'1px solid var(--line)',borderRadius:6,padding:12}}>
      <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:8,display:'flex',justifyContent:'space-between'}}>
        <span>FAILURE PROBABILITY · TWR-LAG-W-031 · NEXT 4H</span>
        <span style={{color:'var(--warn)'}}>87% by 18:42</span>
      </div>
      <svg viewBox="0 0 200 60" style={{width:'100%',height:60,display:'block'}}>
        <line x1="100" y1="0" x2="100" y2="60" stroke="var(--line-2)" strokeDasharray="2 2"/>
        <text x="102" y="10" fill="var(--ink-3)" fontSize="6" fontFamily="var(--mono)">NOW</text>
        <polyline points="0,55 30,52 60,48 100,40" fill="none" stroke="var(--ink-2)" strokeWidth="1.5"/>
        <polyline points="100,40 130,28 160,15 200,8" fill="none" stroke="var(--warn)" strokeWidth="1.5" strokeDasharray="3 2"/>
        <polygon points="100,40 130,28 160,15 200,8 200,60 100,60" fill="rgba(255,181,71,.10)"/>
      </svg>
      <div className="mono" style={{fontSize:9.5,color:'var(--ink-3)',display:'flex',justifyContent:'space-between',marginTop:4}}>
        <span>−2h</span><span>NOW</span><span>+2h</span><span>+4h</span>
      </div>
    </div>
  );
}

function OutageTable(){
  const rows = ALERTS.filter(a=>a.status==='active'||a.status==='investigating').slice(0,4);
  return (
    <div style={{background:'var(--bg-2)',border:'1px solid var(--line)',borderRadius:6,overflow:'hidden'}}>
      <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.14em',padding:'10px 12px',borderBottom:'1px solid var(--line)'}}>ACTIVE INCIDENTS · CITED</div>
      {rows.map((a,i)=>(
        <div key={a.id} style={{padding:'10px 12px',display:'flex',gap:10,alignItems:'center',borderBottom:i<rows.length-1?'1px solid var(--line)':0,fontSize:11.5}}>
          <C.Pill tone={a.sev==='critical'?'crit':a.sev==='warn'?'warn':'info'} dot>{a.id}</C.Pill>
          <span style={{flex:1}}>{a.title}</span>
          <span className="mono" style={{color:'var(--ink-3)',fontSize:10.5}}>{a.tower}</span>
          <span className="mono" style={{color:'var(--ink-3)',fontSize:10.5}}>{a.time}</span>
        </div>
      ))}
    </div>
  );
}

function MiniMap({ focus }){
  return (
    <div style={{background:'var(--bg-2)',border:'1px solid var(--line)',borderRadius:6,padding:12}}>
      <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.14em',marginBottom:8}}>MAP CONTEXT · {focus.toUpperCase()}</div>
      <div style={{height:160,position:'relative'}}>
        <NetworkMap compact/>
      </div>
    </div>
  );
}

window.Copilot = Copilot;
