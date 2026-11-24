// Sessions are storage metadata. Each visible item owns one immutable answer
// and the reply from that same session, regardless of the model used next.
export function feedbackHistory(sessions=[]){
 const entries=[];let previousTime=-Infinity
 for(const session of [...sessions].reverse()){
  const replies=new Map((session.messages||[]).filter(m=>m.role==='assistant'&&m.replyTo).map(m=>[m.replyTo,m]))
  for(const round of session.messages||[]){
   if(round.role!=='user')continue
   const feedback=replies.get(round.id),parsed=Date.parse(round.createdAt||feedback?.createdAt)
   const timestamp=Number.isFinite(parsed)?parsed:previousTime
   previousTime=timestamp
   const modelName=feedback?.modelName?.trim()||round.modelName?.trim()||null
   entries.push({round,feedback,sessionId:session.id,sessionStatus:session.status,modelName,modelKey:modelName?`model:${modelName}`:'unknown',timestamp,index:entries.length})
  }
 }
 return entries.sort((a,b)=>a.round.historyNumber&&b.round.historyNumber?a.round.historyNumber-b.round.historyNumber:(a.timestamp===b.timestamp?0:a.timestamp<b.timestamp?-1:1)||a.index-b.index).map((entry,index)=>({...entry,number:entry.round.historyNumber||index+1}))
}
export function mergeFeedbackPages(previous,page){
 const sessions=new Map((previous.sessions||[]).map(s=>[s.id,{...s,messages:[...(s.messages||[])]}]))
 for(const incoming of page.sessions||[]){
  const messages=new Map((sessions.get(incoming.id)?.messages||[]).map(m=>[m.id,m]))
  for(const message of incoming.messages||[])messages.set(message.id,message)
  sessions.set(incoming.id,{...incoming,messages:[...messages.values()].sort((a,b)=>(a.sequence||0)-(b.sequence||0))})
 }
 return {...page,sessions:[...sessions.values()]}
}
