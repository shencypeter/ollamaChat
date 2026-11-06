export function quizScoringPlan(maxScore,questions){
 const max=Number(maxScore),cents=Math.round(max*100),toCents=value=>Math.round(Number(value)*100)
 const valid=value=>Number.isFinite(Number(value)) && Number(value)>0 && Math.abs(Number(value)*100-toCents(value))<1e-7
 if(!valid(maxScore)||!questions.length)return {error:'請設定有效測驗滿分與題目。',rows:[]}
 const provided=questions.map(q=>q.points==null||q.points===''?null:q.points)
 if(provided.some(p=>p!==null&&!valid(p)))return {error:'每題配分須為正數，最多兩位小數。',rows:[]}
 let count=provided.filter(p=>p===null).length,remaining=cents-provided.reduce((n,p)=>n+(p===null?0:toCents(p)),0)
 if((count===0&&remaining!==0)||(count>0&&remaining<count))return {error:'題目配分合計須等於測驗滿分；未設定題目需保留足夠配分。',rows:[]}
 const each=count?Math.floor(remaining/count):0
 const questionPoints=provided.map(p=>{if(p!==null)return toCents(p)/100;count--;const value=count===0?remaining:each;remaining-=value;return value/100})
 const types=[['single','單選題'],['multiple','多選題'],['true_false','是非題'],['essay','申論題']]
 const rows=types.map(([type,label])=>{const values=questions.map((q,i)=>(q.questionType||'essay')===type?questionPoints[i]:null).filter(p=>p!==null);return {type,label,count:values.length,total:Math.round(values.reduce((a,b)=>a+b,0)*100)/100,each:values.length&&values.every(v=>v===values[0])?values[0]:null}}).filter(r=>r.count)
 return {error:'',rows,questionPoints,total:max,objectiveTotal:Math.round(rows.filter(r=>r.type!=='essay').reduce((a,b)=>a+b.total,0)*100)/100,essayTotal:rows.find(r=>r.type==='essay')?.total||0}
}
