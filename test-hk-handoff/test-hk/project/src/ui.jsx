// Reusable UI primitives: Card, Pill, Spark, IconBtn, etc.

const C = {
  Card({ children, style, pad=16, className='', ...rest }) {
    return (
      <div className={"tp-card "+className} style={{
        background:'var(--bg-1)',
        border:'1px solid var(--line)',
        borderRadius:10,
        padding:pad,
        ...style,
      }} {...rest}>{children}</div>
    );
  },
  Section({ label, right, children, style }){
    return (
      <div style={{display:'flex',flexDirection:'column',gap:10,...style}}>
        <div style={{display:'flex',alignItems:'baseline',justifyContent:'space-between'}}>
          <div className="mono uppr" style={{fontSize:10,color:'var(--ink-3)',letterSpacing:'.12em'}}>{label}</div>
          {right}
        </div>
        {children}
      </div>
    );
  },
  Pill({ tone='info', children, dot=false, style }){
    const map = {
      ok:'var(--ok)', warn:'var(--warn)', crit:'var(--crit)', info:'var(--info)',
      neutral:'var(--ink-2)', accent:'var(--accent)'
    };
    const c = map[tone] || map.info;
    return (
      <span className="mono uppr" style={{
        display:'inline-flex',alignItems:'center',gap:6,
        height:20,padding:'0 8px',borderRadius:4,
        fontSize:9.5,letterSpacing:'.10em',fontWeight:600,
        color:c,
        background:`color-mix(in oklch, ${c} 14%, transparent)`,
        border:`1px solid color-mix(in oklch, ${c} 30%, transparent)`,
        ...style
      }}>
        {dot && <span style={{width:5,height:5,borderRadius:'50%',background:c,boxShadow:`0 0 6px ${c}`}}/>}
        {children}
      </span>
    );
  },
  KPI({ label, value, unit, delta, trend, sub, spark, color }){
    return (
      <C.Card pad={14} style={{display:'flex',flexDirection:'column',gap:8,minHeight:118,position:'relative',overflow:'hidden'}}>
        <div className="mono uppr" style={{fontSize:9.5,color:'var(--ink-3)',letterSpacing:'.12em'}}>{label}</div>
        <div style={{display:'flex',alignItems:'baseline',gap:6}}>
          <div className="mono" style={{fontSize:30,fontWeight:600,color:'var(--ink)',letterSpacing:'-.02em',lineHeight:1}}>{value}</div>
          {unit && <div className="mono" style={{fontSize:13,color:'var(--ink-3)'}}>{unit}</div>}
        </div>
        <div style={{display:'flex',alignItems:'center',justifyContent:'space-between',gap:8}}>
          <div className="mono" style={{fontSize:10,color:'var(--ink-3)'}}>{sub}</div>
          {delta && (
            <div className="mono" style={{
              fontSize:10,fontWeight:600,
              color:trend==='up'?'var(--ok)':'var(--crit)'
            }}>
              {trend==='up'?'▲':'▼'} {delta}
            </div>
          )}
        </div>
        {spark && <C.Spark data={spark} color={color||'var(--accent)'} />}
      </C.Card>
    );
  },
  Spark({ data, color='var(--accent)', height=28 }){
    const min = Math.min(...data), max = Math.max(...data);
    const range = max-min || 1;
    const w = 100;
    const pts = data.map((v,i)=>{
      const x = (i/(data.length-1))*w;
      const y = height - ((v-min)/range)*height;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    }).join(' ');
    const area = `0,${height} ${pts} ${w},${height}`;
    const id = 'sp'+Math.random().toString(36).slice(2,7);
    return (
      <svg viewBox={`0 0 ${w} ${height}`} preserveAspectRatio="none" style={{width:'100%',height,display:'block',marginTop:4}}>
        <defs>
          <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity=".35"/>
            <stop offset="100%" stopColor={color} stopOpacity="0"/>
          </linearGradient>
        </defs>
        <polygon points={area} fill={`url(#${id})`} />
        <polyline points={pts} fill="none" stroke={color} strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round"/>
      </svg>
    );
  },
  Btn({ children, onClick, primary, ghost, small, style, ...rest }){
    const base = {
      appearance:'none', border:'1px solid var(--line-2)',
      background:'var(--bg-2)', color:'var(--ink)',
      borderRadius:6, padding: small?'5px 10px':'7px 12px',
      fontSize: small?11:12, fontWeight:500, cursor:'pointer',
      display:'inline-flex',alignItems:'center',gap:6,
      transition:'all .12s'
    };
    const variants = primary ? {
      background:'var(--accent)', color:'#001a10', border:'1px solid transparent', fontWeight:600
    } : ghost ? {
      background:'transparent', border:'1px solid transparent', color:'var(--ink-2)'
    } : {};
    return <button onClick={onClick} style={{...base,...variants,...style}} {...rest}>{children}</button>;
  },
  Crosshair({ color='var(--accent)', size=12 }){
    return (
      <svg width={size} height={size} viewBox="0 0 12 12" style={{display:'block'}}>
        <circle cx="6" cy="6" r="2" fill="none" stroke={color} strokeWidth="1"/>
        <line x1="6" y1="0" x2="6" y2="3" stroke={color} strokeWidth="1"/>
        <line x1="6" y1="9" x2="6" y2="12" stroke={color} strokeWidth="1"/>
        <line x1="0" y1="6" x2="3" y2="6" stroke={color} strokeWidth="1"/>
        <line x1="9" y1="6" x2="12" y2="6" stroke={color} strokeWidth="1"/>
      </svg>
    );
  },
  Bar({ pct, tone='accent' }){
    const c = tone==='crit'?'var(--crit)':tone==='warn'?'var(--warn)':tone==='info'?'var(--info)':'var(--accent)';
    return (
      <div style={{height:4,background:'var(--bg-3)',borderRadius:2,overflow:'hidden'}}>
        <div style={{height:'100%',width:`${pct}%`,background:c,borderRadius:2,transition:'width .4s'}}/>
      </div>
    );
  },
  Divider({ vertical, style }){
    return vertical
      ? <div style={{width:1,alignSelf:'stretch',background:'var(--line)',...style}}/>
      : <div style={{height:1,background:'var(--line)',...style}}/>;
  },
};

window.C = C;
