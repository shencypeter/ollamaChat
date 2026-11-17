<script setup>
import {computed,onMounted,onBeforeUnmount,ref,watch} from 'vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
import {LoaderCircle,MessagesSquare,RotateCcw,Send,UsersRound} from '@lucide/vue'
import {api} from '../api'
import {auth} from '../composables/auth'
import {experience} from '../composables/experience'
import ChatPerformanceDialog from './ChatPerformanceDialog.vue'
import AnswerRevisionDiff from './AnswerRevisionDiff.vue'
import TutorAvatar from './TutorAvatar.vue'
import PhaseAnnotatedAnswer from './PhaseAnnotatedAnswer.vue'
import PhaseAlignmentNotice from './PhaseAlignmentNotice.vue'
const props=defineProps({course:{type:Object,required:true}})
const base=`/courses/${props.course.id}/case-analysis`
const config=ref(null),mode=ref('raw'),tab=ref('chat'),draft=ref(''),error=ref(''),loading=ref(true),busy=ref(false)
const rawId=ref(null),rawMessages=ref([]),expired=ref(false),rawCapacityReached=ref(false),history=ref({sessions:[],activeSessionId:null}),selectedSessionId=ref('')
const records=ref(null),studentId=ref(''),reviewStage=ref('premorbid'),attempt=ref(null)
const annotationDisplayMode=ref('underline')
const selectedRoundId=ref(''),revisionParent=ref(null),performance=ref(null)
const allowedStages=['premorbid','illnessCourse','functionalAssessment','psychosocial']
const canReview=computed(()=>config.value?.canReview&&['teacher','ta'].includes(props.course.courseRole))
const canInspectRequests=computed(()=>config.value?.canInspectRequests===true&&['teacher','ta'].includes(props.course.courseRole)&&!experience.student&&tab.value==='chat')
// Captures belong only to this mounted page. Never attach them to messages/history or a request body.
const requestSnapshots=new Map()
const stageSnapshotKey=(sessionId,replyTo)=>`stage:${sessionId}:${replyTo}`
const rawSnapshotKey=(sessionId,index)=>`raw:${sessionId}:${index}`
function clearRequestSnapshots(){requestSnapshots.clear();performance.value=null}
function clearRawRequestSnapshots(id){for(const key of requestSnapshots.keys())if(key.startsWith(`raw:${id}:`))requestSnapshots.delete(key);performance.value=null}
function saveStageInspection(data,requestId){
 if(!canInspectRequests.value||!data.requestInspection||!requestId)return
 for(const s of data.sessions||[])if(s.messages?.some(m=>m.role==='assistant'&&m.replyTo===requestId))requestSnapshots.set(stageSnapshotKey(s.id,requestId),structuredClone(data.requestInspection))
}
const stages=computed(()=>(config.value?.stages||[]).filter(s=>allowedStages.includes(s.key)))
const session=computed(()=>history.value.sessions?.find(s=>s.id===selectedSessionId.value))
const messages=computed(()=>tab.value==='chat'&&mode.value==='raw'?rawMessages.value:session.value?.messages||[])
const activeStage=computed(()=>tab.value==='review'?reviewStage.value:mode.value)
const formal=computed(()=>tab.value==='review'||mode.value!=='raw')
const rounds=computed(()=>messages.value.filter(m=>m.role==='user'))
const displayRounds=computed(()=>rounds.value.map((round,index)=>({round,number:index+1})).reverse())
const selectedRound=computed(()=>rounds.value.find(m=>m.id===selectedRoundId.value)||rounds.value.at(-1))
const feedback=computed(()=>messages.value.find(m=>m.role==='assistant'&&m.replyTo===selectedRound.value?.id))
const shownMessages=computed(()=>formal.value?[selectedRound.value,feedback.value].filter(Boolean):messages.value)
const revisions=computed(()=>history.value.sessions.flatMap((s,si)=>(s.messages||[]).filter(m=>m.role==='user').map((m,ri)=>({id:m.id,label:`對話 ${si+1} · 第 ${ri+1} 輪 · ${revisionLabel(m)}`,content:m.content}))))
const diffScope=computed(()=>`${tab.value}:${studentId.value}:${tab.value==='review'?reviewStage.value:mode.value}`)
const nextStage=computed(()=>stages.value[stages.value.findIndex(s=>s.key===mode.value)+1])
const readonly=computed(()=>tab.value==='review'||!config.value?.canChat||(mode.value!=='raw'&&session.value?.status==='closed'))
const rawAtRisk=computed(()=>tab.value==='chat'&&mode.value==='raw'&&(rawMessages.value.length>0||busy.value||draft.value.length>0))
const formalDraftAtRisk=computed(()=>tab.value==='chat'&&mode.value!=='raw'&&(draft.value.length>0||revisionParent.value!==null))
const maxCharacters=computed(()=>config.value?.maximumMessageCharacters||8000)
const failedDraft=computed(()=>mode.value!=='raw'&&messages.value.find(m=>m.role==='user'&&m.status==='failed'&&m.content===draft.value&&(m.revisesMessageId||null)===revisionParent.value))
const totals=computed(()=>{
 const result={studentMessages:0,assistantReplies:0,roundTrips:0,revisionCount:0,lastActivityAt:null}
 for(const student of records.value?.students||[]){const s=student.statistics||{};for(const key of ['studentMessages','assistantReplies','roundTrips','revisionCount'])result[key]+=s[key]||0;if(s.lastActivityAt&&(!result.lastActivityAt||s.lastActivityAt>result.lastActivityAt))result.lastActivityAt=s.lastActivityAt}
 return result
})
let generation=0,controller=null,destroyed=false,lifecycleCleanup=Promise.resolve()
function invalidate(){generation++;controller?.abort();controller=new AbortController();busy.value=false;loading.value=false;clearRequestSnapshots();return generation}
function options(options={}){return {...options,signal:controller?.signal}}
function current(g){return !destroyed&&g===generation}
function date(value){return value?new Date(value).toLocaleString('zh-TW'):'尚無紀錄'}
function stageLabel(key){return stages.value.find(s=>s.key===key)?.label||key}
function report(e){error.value=e.status===413?(mode.value==='raw'?'對話已達模型上下文或訊息長度上限，既有對話已保留。請縮短內容或另開新對話；系統不會自動刪除歷史。':'本次作答與階段提示超過模型上下文或訊息長度上限，請縮短本次作答。歷次作答仍保留於資料庫，不會送入模型；重新開始對話不會增加本次容量。'):e.message||'連線失敗，請稍後重試。';if(mode.value==='raw'&&e.status===404){expired.value=true;clearRawRequestSnapshots(rawId.value)}}
function setHistory(data){history.value={sessions:data.sessions||[],activeSessionId:data.activeSessionId||null};selectedSessionId.value=data.activeSessionId||(tab.value==='review'?data.sessions?.[0]?.id:'')||'';selectedRoundId.value=rounds.value.at(-1)?.id||'';performance.value=null}
function clearComposer(){draft.value='';revisionParent.value=null;attempt.value=null}
function selectSession(id,event){if(id===selectedSessionId.value)return;if(!confirmDeparture()){if(event)event.target.value=selectedSessionId.value;return}selectedSessionId.value=id;clearComposer();selectedRoundId.value=rounds.value.at(-1)?.id||'';performance.value=null}
function selectRound(id){selectedRoundId.value=id;performance.value=null}
function revisionLabel(message){
 const byId=new Map(history.value.sessions.flatMap(s=>(s.messages||[]).filter(m=>m.role==='user')).map(m=>[m.id,m])),seen=new Set([message.id]);let parent=message.revisesMessageId,count=0
 while(parent&&!seen.has(parent)){count++;seen.add(parent);parent=byId.get(parent)?.revisesMessageId}
 return count?`修改 ${count} 次`:'原始作答'
}
function reviseAnswer(){
 if(readonly.value||busy.value||selectedRound.value?.status!=='completed')return
 const round=selectedRound.value,hasDraft=draft.value.length>0||revisionParent.value!==null
 if(hasDraft&&(draft.value!==round.content||(revisionParent.value!==null&&revisionParent.value!==round.id))&&!window.confirm('目前尚有未提交的作答或修改。改用這次作答會取代草稿，確定繼續？'))return
 draft.value=round.content;revisionParent.value=round.id;attempt.value=null
}
function cancelRevision(){if(confirmDeparture())clearComposer()}
function openPerformance(message,initialTab='statistics'){const key=formal.value?stageSnapshotKey(session.value?.id,message.replyTo):rawSnapshotKey(rawId.value,rawMessages.value.indexOf(message));performance.value={initialTab,metrics:message.metrics||null,title:formal.value?`第 ${rounds.value.indexOf(selectedRound.value)+1} 輪回饋效能`:'AI 回覆效能',modelName:message.modelName||config.value?.modelName,promptVersion:message.promptVersion,requestInspection:canInspectRequests.value?requestSnapshots.get(key)||null:null}}
async function disposeRaw(strict=false){const id=rawId.value;if(id)try{await api(`${base}/raw/sessions/${id}`,{method:'DELETE'})}catch(e){if(strict&&e.status!==404)throw e}clearRawRequestSnapshots(id);if(rawId.value===id){rawId.value=null;rawMessages.value=[];expired.value=false}}
async function startRaw(g=generation){
 if(!config.value?.canChat)return
 loading.value=true
 try{const data=await api(`${base}/raw/sessions`,options({method:'POST'}));if(current(g)){rawId.value=data.id;expired.value=false;rawCapacityReached.value=false}else if(data.id){void api(`${base}/raw/sessions/${data.id}`,{method:'DELETE'}).catch(()=>{})}}
 catch(e){if(current(g)&&e.name!=='AbortError'){rawCapacityReached.value=e.status===429;report(e)}}finally{if(current(g))loading.value=false}
}
function confirmRaw(){return !rawAtRisk.value||window.confirm('離開純 AI 聊天後，這段暫存對話將清除且無法恢復。確定離開？')}
function confirmDeparture(){return mode.value==='raw'&&tab.value==='chat'?confirmRaw():!formalDraftAtRisk.value||window.confirm('目前尚有未提交的作答或修改。離開後草稿將清除，確定繼續？')}
async function loadStage(g=generation){
 const path=tab.value==='review'?`${base}/records/${studentId.value}/${reviewStage.value}`:`${base}/stages/${mode.value}`
 if(tab.value==='review'&&!studentId.value)return
 loading.value=true
 try{const data=await api(path,options());if(current(g))setHistory(data)}catch(e){if(current(g)&&e.name!=='AbortError')report(e)}finally{if(current(g))loading.value=false}
}
async function chooseMode(key){
 if(mode.value===key&&tab.value==='chat')return
 if(!confirmDeparture())return
 const g=invalidate();try{await disposeRaw(true)}catch(e){if(current(g))report(e);return}if(!current(g))return
 tab.value='chat';mode.value=key;clearComposer();error.value='';setHistory({sessions:[],activeSessionId:null})
 if(key==='raw')await startRaw(g);else await loadStage(g)
}
async function clearRaw(){
 if(rawAtRisk.value&&!window.confirm('清除這段純 AI 聊天並重新開始？清除後無法恢復。'))return
 const g=invalidate();error.value='';try{await disposeRaw(true);if(current(g)){draft.value='';await startRaw(g)}}catch(e){if(current(g))report(e)}
}
async function recoverRawCapacity(){
 if(!window.confirm('將清除本帳號所有課程與分頁的純 AI 聊天暫存，無法恢復，確定？'))return
 const g=invalidate();loading.value=true
 try{
  await api(`${base}/raw/sessions`,options({method:'DELETE',keepalive:false}))
  if(!current(g))return
  rawId.value=null;rawMessages.value=[];draft.value='';attempt.value=null;expired.value=false;rawCapacityReached.value=false;error.value=''
  await startRaw(g)
 }catch(e){if(current(g)&&e.name!=='AbortError')report(e)}finally{if(current(g))loading.value=false}
}
async function send(){
 if(busy.value||loading.value||readonly.value||!draft.value.trim()||draft.value.length>maxCharacters.value||expired.value)return
 if(failedDraft.value){await retryMessage(failedDraft.value);return}
 const g=generation,content=draft.value
 busy.value=true;error.value=''
 try{
  if(mode.value==='raw'){
   if(!rawId.value)throw new Error('請先重新開始純 AI 聊天。')
   const sentSessionId=rawId.value,previousLength=rawMessages.value.length
   const data=await api(`${base}/raw/sessions/${sentSessionId}/messages`,options({method:'POST',body:{content}}))
   if(current(g)){
    if(canInspectRequests.value&&data.requestInspection&&data.sessionId===sentSessionId){const index=data.messages.length-1;if(index>=previousLength&&data.messages[index]?.role==='assistant')requestSnapshots.set(rawSnapshotKey(sentSessionId,index),structuredClone(data.requestInspection))}
    rawMessages.value=data.messages;draft.value=''
   }
  }else{
   if(attempt.value?.content!==content||attempt.value?.revisesMessageId!==revisionParent.value)attempt.value={requestId:crypto.randomUUID(),content,revisesMessageId:revisionParent.value}
   const requestId=attempt.value.requestId
   const data=await api(`${base}/stages/${mode.value}/messages`,options({method:'POST',body:{...attempt.value}}))
   if(current(g)){saveStageInspection(data,requestId);setHistory(data);clearComposer()}
  }
 }catch(e){if(current(g)&&e.name!=='AbortError'){report(e);if(mode.value!=='raw')await loadStage(g)}}finally{if(current(g))busy.value=false}
}
async function retryMessage(message){
 if(busy.value||readonly.value)return
 const g=generation;busy.value=true;error.value=''
 try{const data=await api(`${base}/stages/${mode.value}/messages/${message.id}/retry`,options({method:'POST'}));if(current(g)){saveStageInspection(data,message.id);setHistory(data);if(draft.value===message.content)clearComposer();attempt.value=null}}
 catch(e){if(current(g)&&e.name!=='AbortError'){report(e);await loadStage(g)}}finally{if(current(g))busy.value=false}
}
async function restartStage(){
 if(busy.value||readonly.value||!window.confirm(`重新開始此階段？目前對話將保留為唯讀歷史，新訊息會建立新的對話。${formalDraftAtRisk.value?'未提交的作答草稿將清除。':''}`))return
 const g=generation;busy.value=true;error.value=''
 try{const data=await api(`${base}/stages/${mode.value}/restart`,options({method:'POST'}));if(current(g)){setHistory(data);selectedSessionId.value='';clearComposer()}}
 catch(e){if(current(g))report(e)}finally{if(current(g))busy.value=false}
}
async function review(){
 if(!canReview.value||!confirmDeparture())return
 const g=invalidate();try{await disposeRaw(true)}catch(e){if(current(g))report(e);return}if(!current(g))return
 tab.value='review';clearComposer();error.value='';setHistory({sessions:[],activeSessionId:null});loading.value=true
 try{const data=await api(`${base}/records`,options());if(current(g))records.value=data}catch(e){if(current(g)&&e.name!=='AbortError')report(e)}finally{if(current(g))loading.value=false}
}
async function selectStudent(id){studentId.value=id;clearComposer();error.value='';setHistory({sessions:[],activeSessionId:null});await loadStage(invalidate())}
async function selectReviewStage(){clearComposer();error.value='';setHistory({sessions:[],activeSessionId:null});await loadStage(invalidate())}
function beforeUnload(event){if(rawAtRisk.value||formalDraftAtRisk.value){event.preventDefault();event.returnValue=''}}
function clearRawForPageLifecycle(){
 const id=rawId.value
 // Clear synchronously so BFcache cannot restore a previous Raw conversation or draft.
 rawId.value=null;rawMessages.value=[];clearComposer();expired.value=false;performance.value=null
 if(id){
  // Creation established the CSRF token. keepalive lets this DELETE outlive the document.
  // Browsers may still refuse it or lose connectivity; server idle expiry is the fallback.
  lifecycleCleanup=api(`${base}/raw/sessions/${id}`,{method:'DELETE',keepalive:true}).catch(()=>{})
 }
}
function pageHide(){invalidate();if(tab.value==='chat'&&mode.value==='raw')clearRawForPageLifecycle()}
async function pageShow(event){
 if(!event.persisted)return
 const g=invalidate();error.value='';loading.value=true
 if(tab.value==='chat'&&mode.value==='raw')clearRawForPageLifecycle()
 await lifecycleCleanup
 if(!current(g))return
 if(tab.value==='chat'&&mode.value==='raw')await startRaw(g)
 else if(tab.value==='chat'||studentId.value)await loadStage(g)
 else loading.value=false
}
onBeforeRouteLeave(()=>confirmDeparture())
onBeforeRouteUpdate((to,from)=>to.params.courseId===from.params.courseId&&to.query.section===from.query.section?true:confirmDeparture())
async function loadConfiguration(){
 const g=invalidate();loading.value=true
 try{const data=await api(base,options());if(!current(g))return;config.value=data;if(data.enabled)await startRaw(g)}catch(e){if(current(g)&&e.name!=='AbortError')report(e)}finally{if(current(g))loading.value=false}
}
onMounted(()=>{window.addEventListener('beforeunload',beforeUnload);window.addEventListener('pagehide',pageHide);window.addEventListener('pageshow',pageShow);void loadConfiguration()})
watch(()=>[auth.user?.id,experience.student,props.course.id,props.course.courseRole],()=>{invalidate();if(config.value)config.value={...config.value,canInspectRequests:false}},{flush:'sync'})
onBeforeUnmount(()=>{destroyed=true;invalidate();window.removeEventListener('beforeunload',beforeUnload);window.removeEventListener('pagehide',pageHide);window.removeEventListener('pageshow',pageShow);void disposeRaw()})
</script>

<template>
 <section class="surface case-analysis" aria-label="個案分析 AI 助教">
  <div class="section-title"><h2><MessagesSquare :size="22" aria-hidden="true"/> 個案分析 AI 助教</h2><span v-if="config?.modelName" class="muted">模型：{{config.modelName}}</span></div>
  <p v-if="error" class="error-box" role="alert">{{error}}</p>
  <p v-if="!config&&loading" role="status">載入 AI 助教中…</p>
  <button v-if="!config&&!loading" class="button secondary" @click="loadConfiguration">重新載入 AI 助教</button>
  <p v-if="config&&!config.enabled" class="empty-inline">此課程尚未啟用個案分析 AI 助教。</p>
  <template v-if="config?.enabled">
   <p v-if="config.isPractice" class="practice-notice">教師／助教教學練習：階段對話僅供本人練習，不計入正式學生統計。各階段系統目前為 AI 提供，尚未經過專業調教與驗證。</p>
   <p v-if="!config.canChat" class="practice-notice">目前為唯讀檢視，無法傳送訊息或建立對話。</p>
   <nav class="chat-modes" aria-label="聊天模式">
    <button class="button secondary" data-mode="raw" :aria-pressed="tab==='chat'&&mode==='raw'" @click="chooseMode('raw')">純 AI 聊天（無教學提示詞）</button>
    <button v-for="stage in stages" :key="stage.key" class="button secondary" :data-mode="stage.key" :aria-pressed="tab==='chat'&&mode===stage.key" @click="chooseMode(stage.key)">{{stage.label}}</button>
    <button v-if="canReview" class="button secondary" data-action="review" :aria-pressed="tab==='review'" @click="review"><UsersRound :size="17" aria-hidden="true"/> 學生紀錄與統計</button>
   </nav>
   <template v-if="tab==='review'">
    <p class="field-hint">僅顯示本課正式學生的四階段紀錄；純 AI 聊天與教師／助教練習不列入。</p>
    <template v-if="records">
     <p class="statistics-summary">全課程合計：學生訊息 {{totals.studentMessages}} · AI 回覆 {{totals.assistantReplies}} · 往返 {{totals.roundTrips}} · 修改 {{totals.revisionCount}} · 最近活動 {{date(totals.lastActivityAt)}}</p>
     <div class="table-scroll"><table class="data-table"><thead><tr><th>學員</th><th>學生訊息</th><th>AI 回覆</th><th>往返</th><th>修改次數</th><th>最近活動</th><th>各階段統計</th></tr></thead><tbody><tr v-for="student in records.students" :key="student.id"><td><button class="button secondary" :data-student="student.id" @click="selectStudent(student.id)">{{student.studentNumber}} {{student.displayName}}</button></td><td>{{student.statistics?.studentMessages||0}}</td><td>{{student.statistics?.assistantReplies||0}}</td><td>{{student.statistics?.roundTrips||0}}</td><td>{{student.statistics?.revisionCount||0}}</td><td>{{date(student.statistics?.lastActivityAt)}}</td><td><div v-for="stage in student.stages" :key="stage.stage">{{stageLabel(stage.stage)}}：訊息 {{stage.statistics?.studentMessages||0}} / 回覆 {{stage.statistics?.assistantReplies||0}} / 往返 {{stage.statistics?.roundTrips||0}} / 修改 {{stage.statistics?.revisionCount||0}} · {{date(stage.statistics?.lastActivityAt)}}</div></td></tr></tbody></table></div>
     <p v-if="!records.students?.length" class="empty-inline">目前尚無正式學生紀錄。</p>
    </template>
    <label v-if="studentId" class="review-stage">檢視階段<select v-model="reviewStage" @change="selectReviewStage"><option v-for="stage in stages" :key="stage.key" :value="stage.key">{{stage.label}}</option></select></label>
   </template>
   <template v-else-if="mode==='raw'">
    <p class="field-hint">純 AI 聊天沒有教學提示詞。對話僅暫存在本次伺服器記憶中；重整、離開或清除後無法恢復，不列入學生紀錄與統計。</p>
    <button v-if="config.canChat" type="button" class="button secondary" data-action="clear-raw" @click="clearRaw"><RotateCcw :size="16" aria-hidden="true"/> {{expired?'對話已逾時，重新開始':'清除並重新開始'}}</button>
    <button v-if="config.canChat&&rawCapacityReached" type="button" class="button secondary" data-action="recover-raw" :disabled="loading||busy" @click="recoverRawCapacity">清除本帳號的暫存聊天並重新開始</button>
   </template>
   <template v-else>
    <p class="field-hint">{{stageLabel(mode)}}對話會保存，可返回繼續或檢視歷史。每輪只將本次作答與目前階段提示送給 AI，歷次作答與回饋不會再次送入模型。重新開始會保留舊對話。</p>
    <div class="chat-history-actions"><button v-if="config.canChat" class="button secondary" :disabled="busy||loading||session?.status==='closed'" data-action="restart-stage" @click="restartStage">重新開始此階段</button><button v-if="config.canChat&&session?.status==='closed'" class="button secondary" @click="selectSession(history.activeSessionId||'')">返回目前對話</button></div>
   </template>
   <p v-if="loading" role="status">載入對話中…</p>
   <div v-else-if="tab==='chat'||studentId" :class="{'feedback-workspace':formal}">
    <aside v-if="formal" class="round-sidebar" aria-label="回饋輪次">
     <h3>回饋輪次 <span class="muted">{{rounds.length}}</span></h3>
     <label v-if="history.sessions?.length" class="history-select">對話紀錄<select :value="selectedSessionId" :disabled="busy||loading" @change="selectSession($event.target.value,$event)"><option v-if="!history.activeSessionId&&tab==='chat'" value="">新對話（送出後建立）</option><option v-for="(item,index) in history.sessions" :key="item.id" :value="item.id">對話 {{index+1}} · {{item.status==='closed'?'已結束・唯讀':'進行中'}}</option></select></label>
     <nav class="round-list" aria-label="選擇回饋輪次"><button v-for="({round,number}) in displayRounds" :key="round.id" type="button" class="round-option" :data-round="round.id" :aria-pressed="selectedRound?.id===round.id" @click="selectRound(round.id)"><strong>第 {{number}} 輪 · {{revisionLabel(round)}}</strong><span>{{round.status==='failed'?'回覆失敗':round.status==='pending'?'等待回覆中':'已提交'}}</span><small>{{date(round.createdAt)}}</small></button></nav>
     <p v-if="!rounds.length" class="empty-inline">提交作答後，回饋輪次會列在這裡。</p>
     <AnswerRevisionDiff :key="diffScope" :revisions="revisions"/>
    </aside>
    <div class="round-detail">
     <div v-if="formal&&selectedRound" class="round-heading"><h3>第 {{rounds.indexOf(selectedRound)+1}} 輪回饋</h3><span class="muted">{{revisionLabel(selectedRound)}}</span><button v-if="!readonly" type="button" class="button secondary" data-action="revise-answer" :disabled="busy||selectedRound.status!=='completed'" @click="reviseAnswer">修改這次作答</button></div>
     <div class="chat-log" role="log" aria-live="polite" aria-label="對話內容" :aria-busy="busy">
      <p v-if="!shownMessages.length&&!busy" class="empty-inline">尚無訊息。</p>
      <article v-for="(message,index) in shownMessages" :key="message.id||index" class="chat-message" :class="message.role==='user'?'from-user':'from-assistant'"><div class="message-author"><TutorAvatar v-if="message.role==='assistant'"/><strong>{{message.role==='user'?(formal?'學生作答':'使用者'):(formal?'AI 助教回饋':'AI 助教')}}</strong></div><PhaseAnnotatedAnswer v-model:display-mode="annotationDisplayMode" v-if="formal&&message.role==='user'&&feedback?.replyTo===message.id" :key="`${diffScope}:${session?.id}:${message.id}`" :content="message.content" :stage="activeStage" :annotations="feedback.annotations||[]" :classifications="config.classifications||[]"/><p v-else class="message-content">{{message.content}}</p><PhaseAlignmentNotice :display-mode="annotationDisplayMode" v-if="formal&&message.role==='assistant'&&message.replyTo===selectedRound?.id" :content="selectedRound.content" :stage="activeStage" :annotations="message.annotations||[]" :classifications="config.classifications||[]"/><small v-if="message.createdAt" class="muted">{{date(message.createdAt)}}</small><span v-if="message.status==='pending'" role="status"> 等待回覆中</span><span v-if="message.status==='failed'" class="error-text"> 回覆失敗，原訊息已保留</span><button v-if="message.role==='user'&&message.status==='failed'&&!readonly" class="button secondary" :disabled="busy" data-action="retry-message" @click="retryMessage(message)">重試回覆</button><button v-if="message.role==='assistant'" type="button" class="button secondary" data-action="performance" @click="openPerformance(message)">查看回覆效能</button><button v-if="message.role==='assistant'&&canInspectRequests" type="button" class="button secondary" data-action="request-inspection" @click="openPerformance(message,'request')">查看 Ollama 請求</button></article>
     </div>
     <div v-if="busy" class="chat-message from-assistant reply-loading" data-reply-loading role="status" aria-live="polite"><div class="message-author"><TutorAvatar/><strong>{{formal?'AI 助教回饋':'AI 助教'}}</strong></div><div class="reply-loading-content"><LoaderCircle class="reply-spinner" :size="30" aria-hidden="true"/><span>AI 助教正在回覆…</span></div></div>
     <button v-if="formal&&feedback&&nextStage&&tab==='chat'" type="button" class="button secondary" data-action="next-stage" @click="chooseMode(nextStage.key)">下一階段：{{nextStage.label}}</button>
    </div>
   </div>
   <form v-if="tab==='chat'&&!readonly" class="chat-composer" @submit.prevent="send">
    <p v-if="revisionParent" class="revision-notice">正在修改第 {{rounds.findIndex(r=>r.id===revisionParent)+1}} 輪作答；提交後會保留原文並新增回饋輪次。<button type="button" class="button secondary" :disabled="busy" @click="cancelRevision">取消修改</button></p>
    <label for="case-chat-draft">訊息<textarea id="case-chat-draft" v-model="draft" rows="4" :maxlength="maxCharacters" :disabled="busy||loading||expired" placeholder="輸入訊息…" /></label>
    <div class="composer-actions"><small class="muted">{{draft.length}} / {{maxCharacters}} 字</small><button class="button primary" :disabled="busy||loading||expired||!draft.trim()||draft.length>maxCharacters"><Send :size="16" aria-hidden="true"/> {{busy?'AI 回覆中…':failedDraft?'重試這則訊息':attempt?'重送同一則訊息':formal?'提交並取得 AI 助教回饋':'傳送'}}</button></div>
   </form>
   <ChatPerformanceDialog :open="!!performance" :metrics="performance?.metrics||null" :title="performance?.title" :model-name="performance?.modelName" :prompt-version="performance?.promptVersion" :initial-tab="performance?.initialTab" :can-inspect-requests="canInspectRequests" :request-inspection="canInspectRequests?performance?.requestInspection||null:null" @close="performance=null"/>
  </template>
 </section>
</template>

<style scoped>
.reply-loading{margin:16px 0;width:100%;box-sizing:border-box}.reply-loading-content{display:flex;align-items:center;justify-content:center;gap:12px;min-height:110px;color:var(--text)}.reply-spinner{flex:none;color:var(--accent);animation:reply-spin 1s linear infinite}@keyframes reply-spin{to{transform:rotate(360deg)}}@media(prefers-reduced-motion:reduce){.reply-spinner{animation:none}}
.feedback-workspace{display:grid;grid-template-columns:minmax(180px,240px) minmax(0,1fr);gap:20px;margin:20px 0;align-items:start}.round-sidebar{min-width:0;border:1px solid var(--line);border-radius:10px;padding:14px;background:var(--surface)}.round-sidebar h3,.round-heading h3{margin:0;font-size:1rem}.round-sidebar .history-select{margin:14px 0}.round-sidebar select{width:100%;min-width:0;max-width:100%}.round-list{display:grid;gap:8px;max-height:54vh;overflow-y:auto;margin:14px 0}.round-option{display:grid;gap:5px;padding:12px;text-align:left;border:1px solid var(--line);border-radius:8px;background:var(--surface);color:inherit;overflow-wrap:anywhere;cursor:pointer;min-height:44px}.round-option[aria-pressed=true]{border-color:var(--accent);background:var(--accent-soft)}.round-option:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.round-option span,.round-option small{font-size:.8rem}.round-detail{min-width:0}.round-heading{display:flex;gap:12px;align-items:center;flex-wrap:wrap}.round-heading .button{margin-left:auto}.feedback-workspace .chat-log{max-height:none;margin:16px 0}.feedback-workspace .chat-message{width:100%;max-width:100%;box-sizing:border-box}.revision-notice{padding:12px;background:var(--accent-soft);overflow-wrap:anywhere}.revision-notice .button{margin-left:8px}@media(max-width:760px){.feedback-workspace{grid-template-columns:minmax(0,1fr)}.round-list{max-height:220px}.round-heading .button{margin-left:0}.round-sidebar{padding:12px}}
.case-analysis{padding:24px;min-width:0}.section-title,.chat-modes,.chat-history-actions,.composer-actions{display:flex;align-items:center;gap:10px;flex-wrap:wrap}.section-title{justify-content:space-between}.section-title h2{display:flex;align-items:center;gap:8px;margin:0}.chat-modes{margin:20px 0}.chat-modes .button[aria-pressed=true]{background:#5b5fc7;color:#fff;border-color:#5b5fc7}.practice-notice{padding:12px 16px;background:var(--accent-soft);border-left:4px solid var(--accent);border-radius:6px}.chat-log{display:flex;flex-direction:column;gap:14px;margin:20px 0;max-height:65vh;overflow-y:auto;overflow-wrap:anywhere}.chat-message{padding:14px 16px;border:1px solid var(--line);border-radius:10px;background:var(--surface);max-width:95%;min-width:0}.from-user{align-self:flex-end;background:var(--accent-soft)}.from-assistant{align-self:flex-start;background:color-mix(in srgb,#438b88 12%,var(--surface));border-color:color-mix(in srgb,#438b88 30%,var(--line));border-radius:18px 18px 18px 5px}.message-author{display:flex;align-items:center;gap:12px}.from-assistant .message-content{line-height:1.9}.from-assistant .message-author{margin-bottom:12px}.message-content{white-space:pre-wrap;overflow-wrap:anywhere;margin:8px 0}.chat-message .button{margin:8px}.chat-composer{border-top:1px solid var(--line);padding-top:16px}.chat-composer label,.history-select,.review-stage{display:grid;gap:8px}.chat-composer textarea{resize:vertical;min-height:100px}.composer-actions{justify-content:space-between;margin-top:10px}.history-select,.review-stage{margin:16px 0;max-width:450px}.error-text{color:var(--danger)}.data-table td{vertical-align:top}.statistics-summary{line-height:1.8}.button{white-space:normal}.table-scroll{max-width:100%;overflow-x:auto}@media(max-width:600px){.case-analysis{padding:16px}.chat-modes{display:grid;grid-template-columns:1fr 1fr}.chat-modes button:first-child{grid-column:1/-1}.chat-modes .button{min-height:44px}.chat-message{max-width:100%}.section-title{align-items:flex-start;flex-direction:column}}
</style>
