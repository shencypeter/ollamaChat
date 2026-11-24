<script setup>
import {computed,nextTick,onMounted,onBeforeUnmount,ref,useId,watch} from 'vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
import {MessagesSquare,RotateCcw,Send,UsersRound,PencilLine,ClipboardCheck} from '@lucide/vue'
import {api} from '../api'
import {auth} from '../composables/auth'
import {experience} from '../composables/experience'
import ChatPerformanceDialog from './ChatPerformanceDialog.vue'
import ReplyDiagnostics from './ReplyDiagnostics.vue'
import AnswerRevisionDiff from './AnswerRevisionDiff.vue'
import RevisionHistoryList from './RevisionHistoryList.vue'
import TutorAvatar from './TutorAvatar.vue'
import PhaseAnnotatedAnswer from './PhaseAnnotatedAnswer.vue'
import PhaseAlignmentNotice from './PhaseAlignmentNotice.vue'
import AiReplyWaiting from './AiReplyWaiting.vue'
import CaseFormalSubmissions from './CaseFormalSubmissions.vue'
import CaseClassificationRules from './CaseClassificationRules.vue'
import CaseCategoryReference from './CaseCategoryReference.vue'
import CaseStageGuidance from './CaseStageGuidance.vue'
import {caseChatRequestGuide} from '../utils/caseChatRequestGuide'
import {feedbackHistory,mergeFeedbackPages} from '../utils/caseFeedbackHistory'
const props=defineProps({course:{type:Object,required:true}})
const base=`/courses/${props.course.id}/case-analysis`
const config=ref(null),mode=ref('raw'),tab=ref('chat'),draft=ref(''),error=ref(''),loading=ref(true),busy=ref(false)
const failureDiagnostic=ref(null),failureInspection=ref(null),focusedTab=ref('raw'),tabPrefix=`chat-modes-${useId()}`
const selectedModelProfile=ref('')
const enterToSend=ref(false),composing=ref(false)
const chatRoot=ref(null),latestResult=ref(null),replyPending=ref(false),replyFailure=ref(null)
const workspaceView=ref('write'),lastStage=ref('premorbid'),focusedWorkspace=ref('raw')
const workspaceTabId=key=>`${tabPrefix}-workspace-${key}`
const activeWorkspace=computed(()=>tab.value==='review'?'view':mode.value==='raw'?'raw':workspaceView.value)
watch(activeWorkspace,key=>{focusedWorkspace.value=key})
function selectWorkspace(key){if(busy.value||loading.value||!['write','view'].includes(key)||(key==='write'&&!canViewWriting.value))return;workspaceView.value=key}
async function chooseWorkspace(key){
 if(loading.value||(busy.value&&mode.value!=='raw'))return
 if(key==='raw'){if(canUseTesting.value)await chooseMode('raw');return}
 if(!['write','view'].includes(key))return
 if(tab.value==='review'||mode.value==='raw')await chooseMode(lastStage.value,key)
 else selectWorkspace(key)
}
function chooseStage(key){if(tab.value==='review'){reviewStage.value=key;return}void chooseMode(key,workspaceView.value)}
function initializeWorkspace(){workspaceView.value=studentPreview.value?'write':selectedRound.value||readonly.value?'view':'write'}
function navigateWorkspace(event){
 if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return
 const tabs=[...event.currentTarget.querySelectorAll('[role="tab"]')].filter(element=>!element.disabled),index=tabs.indexOf(event.target.closest('[role="tab"]'));if(index<0)return
 event.preventDefault();const next=event.key==='Home'?0:event.key==='End'?tabs.length-1:(index+(event.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length
 tabs[next].focus()
}
const activeTab=computed(()=>tab.value==='review'?reviewStage.value:mode.value)
watch(activeTab,key=>{focusedTab.value=key})
function navigateTabs(event){
 if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return
 const tabs=[...event.currentTarget.querySelectorAll('[role="tab"]')],index=tabs.indexOf(event.target.closest('[role="tab"]'));if(index<0)return
 event.preventDefault();const next=event.key==='Home'?0:event.key==='End'?tabs.length-1:(index+(event.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length
 tabs[next].focus()
}
function openFailureRequest(inspection=failureInspection.value){if(canInspectRequests.value&&inspection)performance.value={initialTab:'request',title:'失敗請求診斷',metrics:null,modelName:inspection.requests?.[0]?.payload?.model||currentModelName.value,promptVersion:inspection.promptVersion,requestInspection:inspection}}
const rawId=ref(null),rawMessages=ref([]),expired=ref(false),rawCapacityReached=ref(false),history=ref({sessions:[],activeSessionId:null}),historyModel=ref('')
const historyLoading=ref(false),historyError=ref('')
const records=ref(null),studentId=ref(''),reviewStage=ref('premorbid'),attempt=ref(null)
const annotationDisplayMode=ref('underline'),formalEnvelope=ref({submissions:[]}),formalReady=ref(false)
const selectedRoundId=ref(''),revisionParent=ref(null),revisionParentNumber=ref(null),performance=ref(null),trashView=ref(false)
const allowedStages=['premorbid','illnessCourse','functionalAssessment','psychosocial']
const requestHints=computed(()=>caseChatRequestGuide(mode.value,config.value?.classifications||[]))
const canReviewFormal=computed(()=>['teacher','ta'].includes(props.course.courseRole)&&!experience.student&&(config.value?.canReviewFormal??config.value?.canReview)===true)
const canReview=computed(()=>(config.value?.canReview||canReviewFormal.value)&&['teacher','ta'].includes(props.course.courseRole)&&!experience.student)
const canInspectRequests=computed(()=>config.value?.canInspectRequests===true&&config.value?.canUseTesting!==false&&['teacher','ta'].includes(props.course.courseRole)&&!experience.student&&tab.value==='chat')
const canUseTesting=computed(()=>['teacher','ta'].includes(props.course.courseRole)&&!experience.student&&config.value?.canUseTesting!==false)
const studentPreview=computed(()=>experience.student&&(props.course.canPractice===true||['teacher','ta'].includes(props.course.courseRole)))
const showDiagnostics=computed(()=>canUseTesting.value||!formal.value)
const canChooseModel=computed(()=>config.value?.canChooseModel===true&&canUseTesting.value&&tab.value==='chat')
const modelProfiles=computed(()=>canChooseModel.value?config.value?.modelProfiles||[]:[])
const currentModelName=computed(()=>modelProfiles.value.find(p=>p.key===selectedModelProfile.value)?.modelName||config.value?.modelName)
function selectedModelBody(){return canChooseModel.value&&selectedModelProfile.value?{modelProfile:selectedModelProfile.value}:{}}
function rulesSaved(data){config.value={...config.value,classifications:data.definitions.filter(item=>item.enabled!==false)};attempt.value=null}
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
const entries=computed(()=>feedbackHistory(history.value.sessions))
const historyModels=computed(()=>[...new Map((history.value.pagination?.models||entries.value.map(entry=>({key:entry.modelKey,modelName:entry.modelName}))).map(item=>[item.key,{key:item.key,label:item.modelName?(config.value?.modelProfiles||[]).find(p=>p.modelName===item.modelName)?.label||item.modelName:'未記錄模型'}])).values()])
const matchingEntries=computed(()=>entries.value.filter(entry=>!historyModel.value||entry.modelKey===historyModel.value))
const messages=computed(()=>tab.value==='chat'&&mode.value==='raw'?rawMessages.value:history.value.sessions.flatMap(s=>s.messages||[]))
const activeStage=computed(()=>tab.value==='review'?reviewStage.value:mode.value)
const formal=computed(()=>tab.value==='review'||mode.value!=='raw')
const rounds=computed(()=>entries.value.map(entry=>entry.round))
const trashedRounds=computed(()=>matchingEntries.value.map(entry=>entry.round).filter(m=>m.trashedAt))
const historyTotal=computed(()=>history.value.pagination?.totalCount??visibleRounds.value.length)
const trashTotal=computed(()=>history.value.pagination?.trashCount??trashedRounds.value.length)
const visibleRounds=computed(()=>matchingEntries.value.map(entry=>entry.round).filter(m=>Boolean(m.trashedAt)===trashView.value))
const displayRounds=computed(()=>matchingEntries.value.filter(({round})=>Boolean(round.trashedAt)===trashView.value).slice().reverse())
const selectedRound=computed(()=>visibleRounds.value.find(m=>m.id===selectedRoundId.value)||visibleRounds.value.at(-1))
const selectedEntry=computed(()=>entries.value.find(entry=>entry.round.id===selectedRound.value?.id))
const session=computed(()=>history.value.sessions.find(s=>s.id===(selectedEntry.value?.sessionId||history.value.activeSessionId)))
const feedback=computed(()=>selectedEntry.value?.feedback)
const roundClassifications=computed(()=>feedback.value?.ruleSnapshot||selectedRound.value?.ruleSnapshot||config.value?.classifications||[])
const shownMessages=computed(()=>formal.value?[selectedRound.value,feedback.value].filter(Boolean):messages.value)
const revisions=computed(()=>history.value.sessions.flatMap(s=>(s.messages||[]).filter(m=>m.role==='user').map(m=>({id:m.id,label:date(m.createdAt),content:m.content,trashedAt:m.trashedAt})).filter(m=>!m.trashedAt)))
const roundOptions=computed(()=>displayRounds.value.map(({round,number})=>({id:round.id,title:`#${number}`,details:[date(round.createdAt)],removable:canTrashRound(round),removeDisabled:round.status==='pending'})))
const diffScope=computed(()=>`${tab.value}:${studentId.value}:${tab.value==='review'?reviewStage.value:mode.value}`)
const nextStage=computed(()=>stages.value[stages.value.findIndex(s=>s.key===mode.value)+1])
const canManageRounds=computed(()=>formal.value&&tab.value==='chat'&&config.value?.canChat===true&&!experience.student&&['teacher','ta','student'].includes(props.course.courseRole))
const submittedRoundIds=computed(()=>new Set(formalEnvelope.value.submissions.map(item=>item.analysisMessageId)))
function canTrashRound(round){return Boolean(round&&canManageRounds.value&&!trashView.value&&!round.trashedAt&&!submittedRoundIds.value.has(round.id)&&(props.course.courseRole!=='student'||formalReady.value))}
const readonly=computed(()=>experience.student||tab.value==='review'||!config.value?.canChat||(formal.value&&trashView.value))
const canViewWriting=computed(()=>!readonly.value||studentPreview.value)
const showComposerError=computed(()=>config.value?.enabled&&tab.value==='chat'&&canViewWriting.value&&(!formal.value||workspaceView.value==='write'))
const rawAtRisk=computed(()=>tab.value==='chat'&&mode.value==='raw'&&(rawMessages.value.length>0||busy.value||draft.value.length>0))
const formalDraftAtRisk=computed(()=>tab.value==='chat'&&mode.value!=='raw'&&(draft.value.length>0||revisionParent.value!==null))
const maxCharacters=computed(()=>config.value?.maximumMessageCharacters||8000)
function canRetryMessage(message){if(history.value.pagination)return history.value.pagination.activeLastMessageId===message.id;const active=history.value.sessions.find(s=>s.id===history.value.activeSessionId);return active?.status==='active'&&active.messages?.at(-1)?.id===message.id}
const failedDraft=computed(()=>mode.value!=='raw'&&messages.value.find(m=>m.role==='user'&&!m.trashedAt&&m.status==='failed'&&canRetryMessage(m)&&m.content===draft.value&&(m.revisesMessageId||null)===revisionParent.value&&(!canChooseModel.value||!m.modelName||m.modelName===currentModelName.value)))
const totals=computed(()=>{
 const result={studentMessages:0,assistantReplies:0,roundTrips:0,revisionCount:0,lastActivityAt:null}
 for(const student of records.value?.students||[]){const s=student.statistics||{};for(const key of ['studentMessages','assistantReplies','roundTrips','revisionCount'])result[key]+=s[key]||0;if(s.lastActivityAt&&(!result.lastActivityAt||s.lastActivityAt>result.lastActivityAt))result.lastActivityAt=s.lastActivityAt}
 return result
})
let generation=0,controller=null,destroyed=false,lifecycleCleanup=Promise.resolve(),historyGeneration=0,historyController=null
function resetHistoryFetch(){historyGeneration++;historyController?.abort();historyLoading.value=false;historyError.value=''}
function invalidate(){resetHistoryFetch();latestResult.value=null;replyPending.value=false;replyFailure.value=null;failureDiagnostic.value=null;failureInspection.value=null;trashView.value=false;historyModel.value='';generation++;controller?.abort();controller=new AbortController();busy.value=false;loading.value=false;clearRequestSnapshots();return generation}
function options(options={}){return {...options,signal:controller?.signal}}
function current(g){return !destroyed&&g===generation}
function date(value){return value?new Date(value).toLocaleString('zh-TW'):'尚無紀錄'}
function stageLabel(key){return stages.value.find(s=>s.key===key)?.label||key}
function report(e){failureDiagnostic.value=canInspectRequests.value?e.diagnostic||null:null;failureInspection.value=canInspectRequests.value?e.requestInspection||null:null;error.value=e.status===413?(mode.value==='raw'?'對話已達模型上下文或訊息長度上限，既有對話已保留。請縮短內容或另開新對話；系統不會自動刪除歷史。':'本次作答與階段提示超過目前設定的輸入預算，尚未送至模型。請縮短本次作答；歷次作答仍保留於資料庫，不會送入模型；篩選歷史不會改變本次容量。'):e.message||'連線失敗，請稍後重試。';if(mode.value==='raw'&&e.status===404){expired.value=true;clearRawRequestSnapshots(rawId.value)}}
function reportReplyFailure(e){report(e);replyFailure.value={message:error.value,diagnostic:failureDiagnostic.value,inspection:failureInspection.value};error.value=''}
function setHistory(data,preferred){history.value={sessions:data.sessions||[],activeSessionId:data.activeSessionId||null,pagination:data.pagination||null};if(data.pagination){historyModel.value=data.pagination.modelKey||'';trashView.value=data.pagination.trashed===true}if(historyModel.value&&!historyModels.value.some(item=>item.key===historyModel.value))historyModel.value='';if(preferred&&!data.pagination&&!visibleRounds.value.some(round=>round.id===preferred))historyModel.value='';selectedRoundId.value=visibleRounds.value.some(round=>round.id===preferred)?preferred:visibleRounds.value.at(-1)?.id||'';performance.value=null}
function historyUrl(path,cursor=null,unfiltered=false,target=null){const query=new URLSearchParams({paged:'true'});if(!unfiltered){if(historyModel.value)query.set('model',historyModel.value);if(trashView.value)query.set('trashed','true')}if(cursor!==null)query.set('before',String(cursor));if(target)query.set('target',target);return `${path}?${query}`}
function mutationUrl(path,unfiltered=false){return history.value.pagination?historyUrl(path,null,unfiltered):path}
function mergeMutation(data,preferred,removed=null){
 resetHistoryFetch()
 if(!data.pagination||!history.value.pagination||data.pagination.focused||history.value.pagination.focused||data.pagination.modelKey!==historyModel.value||data.pagination.trashed!==trashView.value){setHistory(data,preferred);return}
 if(removed)history.value={...history.value,sessions:history.value.sessions.map(s=>({...s,messages:s.messages.filter(m=>m.id!==removed&&m.replyTo!==removed)}))}
 const previousIds=new Set(displayRounds.value.map(e=>e.round.id)),incoming=feedbackHistory(data.sessions)
 if(previousIds.size&&incoming.length&&!incoming.some(e=>previousIds.has(e.round.id))){setHistory(data,preferred);return}
 const merged=mergeFeedbackPages(history.value,data);setHistory(merged,preferred)
 const loaded=displayRounds.value.map(entry=>entry.number)
 history.value.pagination={...history.value.pagination,nextCursor:loaded.length<historyTotal.value&&loaded.length?Math.min(...loaded):null}
}
async function loadMoreHistory(){
 const cursor=history.value.pagination?.nextCursor
 if(cursor==null||historyLoading.value||loading.value||busy.value)return
 const g=generation,h=historyGeneration;historyLoading.value=true;historyError.value='';historyController?.abort();historyController=new AbortController()
 try{const data=await api(historyUrl(`${base}/stages/${mode.value}`,cursor),{signal:historyController.signal});if(current(g)&&h===historyGeneration)history.value=mergeFeedbackPages(history.value,data)}
 catch(e){if(current(g)&&h===historyGeneration&&e.name!=='AbortError')historyError.value=e.message||'載入更多歷程失敗，請重試。'}
 finally{if(current(g)&&h===historyGeneration)historyLoading.value=false}
}
function clearComposer(){draft.value='';revisionParent.value=null;revisionParentNumber.value=null;attempt.value=null;replyFailure.value=null}
function filterHistory(event){const key=event.target.value;if(busy.value||loading.value||(key&&!historyModels.value.some(item=>item.key===key))){event.target.value=historyModel.value;return}const previous=selectedRoundId.value;historyModel.value=key;selectedRoundId.value=visibleRounds.value.at(-1)?.id||'';performance.value=null;if(history.value.pagination)void loadStage(generation,false,previous)}
function selectRound(id){if(busy.value||loading.value||!visibleRounds.value.some(round=>round.id===id))return;selectedRoundId.value=id;performance.value=null;workspaceView.value='view'}
function toggleTrash(){if(busy.value||loading.value)return;const previous=selectedRoundId.value;trashView.value=!trashView.value;selectedRoundId.value=visibleRounds.value.at(-1)?.id||'';performance.value=null;workspaceView.value='view';if(history.value.pagination)void loadStage(generation,false,previous)}
function removeRound(id){const round=visibleRounds.value.find(item=>item.id===id);if(round)void moveRound(false,round)}
async function moveRound(restore=false,round=selectedRound.value){
 if(!canManageRounds.value||busy.value||loading.value||!round||round.status==='pending'||Boolean(round.trashedAt)!==restore||(!restore&&!canTrashRound(round)))return
 if(!restore&&!window.confirm(`將 #${entries.value.find(e=>e.round.id===round.id)?.number} 作答及配對 AI 回饋移至垃圾桶？內容仍會保留，可隨時還原；正式學習統計不變。`))return
 const g=generation,sessionId=entries.value.find(entry=>entry.round.id===round.id)?.sessionId,previousSelected=selectedRound.value?.id,oldRounds=[...visibleRounds.value],oldIndex=oldRounds.findIndex(m=>m.id===round.id),fromCard=document.activeElement?.hasAttribute('data-remove-round')
 busy.value=true;error.value=''
 try{
  const data=await api(mutationUrl(`${base}/stages/${mode.value}/messages/${round.id}/${restore?'restore':'trash'}`),options({method:'POST'}))
  if(!current(g))return
  mergeMutation(data,previousSelected,round.id)
  if(!restore&&revisionParent.value===round.id){revisionParent.value=null;revisionParentNumber.value=null;attempt.value=null}
  latestResult.value=null;performance.value=null;failureDiagnostic.value=null;failureInspection.value=null;requestSnapshots.delete(stageSnapshotKey(sessionId,round.id))
  if(restore&&!trashTotal.value){trashView.value=false;if(data.pagination)await loadStage(g,false,undefined,round.id,false);if(!current(g))return;selectedRoundId.value=visibleRounds.value.some(m=>m.id===round.id)?round.id:visibleRounds.value.at(-1)?.id||''}
  else selectedRoundId.value=previousSelected!==round.id&&visibleRounds.value.some(item=>item.id===previousSelected)?previousSelected:visibleRounds.value[Math.min(oldIndex,visibleRounds.value.length-1)]?.id||''
 }catch(e){if(current(g)&&e.name!=='AbortError')report(e)}finally{if(current(g)){busy.value=false;if(fromCard){await nextTick();if(current(g)){const target=[...(chatRoot.value?.querySelectorAll('[data-round]')||[])].find(element=>element.dataset.round===selectedRound.value?.id);(target||chatRoot.value?.querySelector('[data-action="toggle-trash"]'))?.focus({preventScroll:true})}}}}
}
function resultAnchor(message,index){return `${tabPrefix}-reply-${message.id||`${rawId.value}-${index}`}`}
function rememberStageResult(data,requestId){
 for(const saved of data.sessions||[]){
  const reply=saved.messages?.find(m=>m.role==='assistant'&&m.replyTo===requestId)
  if(reply){latestResult.value={anchor:resultAnchor(reply,1),roundId:requestId,number:entries.value.find(entry=>entry.round.id===requestId)?.number};return}
 }
}
async function visitLatestResult(){
 const result=latestResult.value;if(!result||busy.value)return
 if(result.roundId){if(!visibleRounds.value.some(round=>round.id===result.roundId)){historyModel.value='';trashView.value=false;if(history.value.pagination)await loadStage(generation,false,undefined,result.roundId)}if(latestResult.value!==result)return;selectRound(result.roundId)}
 await nextTick();if(latestResult.value!==result)return
 const behavior=window.matchMedia?.('(prefers-reduced-motion: reduce)').matches?'auto':'smooth'
 const target=[...(chatRoot.value?.querySelectorAll('[data-result-anchor]')||[])].find(el=>el.id===result.anchor)
 if(target){target.focus({preventScroll:true});target.scrollIntoView({behavior,block:'start'})}
}
async function reviseAnswer(){
 if(readonly.value||busy.value||loading.value||selectedRound.value?.status!=='completed')return
 const round=selectedRound.value,hasDraft=draft.value.length>0||revisionParent.value!==null
 if(hasDraft&&(draft.value!==round.content||(revisionParent.value!==null&&revisionParent.value!==round.id))&&!window.confirm('目前尚有未提交的作答或修改。改用這次作答會取代草稿，確定繼續？'))return
 draft.value=round.content;revisionParent.value=round.id;revisionParentNumber.value=selectedEntry.value?.number;attempt.value=null;workspaceView.value='write'
 await nextTick()
 const editor=chatRoot.value?.querySelector('#case-chat-draft')
 if(editor){editor.focus({preventScroll:true});editor.scrollIntoView?.({behavior:window.matchMedia?.('(prefers-reduced-motion: reduce)').matches?'auto':'smooth',block:'center'})}
}
function cancelRevision(){if(confirmDeparture())clearComposer()}
function openPerformance(message,initialTab='statistics'){const key=formal.value?stageSnapshotKey(session.value?.id,message.replyTo):rawSnapshotKey(rawId.value,rawMessages.value.indexOf(message));performance.value={initialTab,metrics:message.metrics||null,title:formal.value?`第 ${selectedEntry.value?.number} 輪回饋效能`:'AI 回覆效能',modelName:message.modelName||null,promptVersion:message.promptVersion,requestInspection:canInspectRequests.value?requestSnapshots.get(key)||null:null}}
async function disposeRaw(strict=false){const id=rawId.value;if(id)try{await api(`${base}/raw/sessions/${id}`,{method:'DELETE'})}catch(e){if(strict&&e.status!==404)throw e}clearRawRequestSnapshots(id);if(rawId.value===id){rawId.value=null;rawMessages.value=[];expired.value=false}}
async function startRaw(g=generation){
 if(!config.value?.canChat){if(current(g))loading.value=false;return}
 loading.value=true
 try{const data=await api(`${base}/raw/sessions`,options({method:'POST'}));if(current(g)){rawId.value=data.id;expired.value=false;rawCapacityReached.value=false}else if(data.id){void api(`${base}/raw/sessions/${data.id}`,{method:'DELETE'}).catch(()=>{})}}
 catch(e){if(current(g)&&e.name!=='AbortError'){rawCapacityReached.value=e.status===429;report(e)}}finally{if(current(g))loading.value=false}
}
function confirmRaw(){return !rawAtRisk.value||window.confirm('離開純 AI 聊天後，這段暫存對話將清除且無法恢復。確定離開？')}
function confirmDeparture(){return mode.value==='raw'&&tab.value==='chat'?confirmRaw():!formalDraftAtRisk.value||window.confirm('目前尚有未提交的作答或修改。離開後草稿將清除，確定繼續？')}
async function chooseModel(event){
 const key=event.target.value,restore=()=>{event.target.value=selectedModelProfile.value}
 if(!canChooseModel.value||busy.value||loading.value||(key&&!modelProfiles.value.some(p=>p.key===key))){restore();return}
 if(key===selectedModelProfile.value)return
 selectedModelProfile.value=key;attempt.value=null;error.value='';replyFailure.value=null;failureDiagnostic.value=null;failureInspection.value=null;performance.value=null
}
async function loadStage(g=generation,initialize=false,previous=selectedRoundId.value,target=null,unfilteredTarget=true){
 if(tab.value==='review'){loading.value=false;return}
 const oldPage=history.value.pagination;resetHistoryFetch();const h=historyGeneration,path=historyUrl(`${base}/stages/${mode.value}`,null,Boolean(target)&&unfilteredTarget,target)
 historyController=new AbortController();loading.value=true
 try{const data=await api(path,{signal:historyController.signal});if(current(g)&&h===historyGeneration){setHistory(data,target);if(initialize)initializeWorkspace()}}catch(e){if(current(g)&&h===historyGeneration&&e.name!=='AbortError'){if(oldPage){historyModel.value=oldPage.modelKey;trashView.value=oldPage.trashed;selectedRoundId.value=previous}if(!target||e.status!==404)report(e)}}finally{if(current(g)&&h===historyGeneration)loading.value=false}
}
async function chooseMode(key,workspace=null){
 if(mode.value===key&&tab.value==='chat'){if(workspace)selectWorkspace(workspace);return}
 if(!confirmDeparture())return
 const g=invalidate();loading.value=true;try{await disposeRaw(true)}catch(e){if(current(g)){loading.value=false;report(e)}return}if(!current(g))return
 tab.value='chat';mode.value=key;if(key!=='raw')lastStage.value=key;clearComposer();error.value='';setHistory({sessions:[],activeSessionId:null})
 if(key==='raw')await startRaw(g);else{await loadStage(g,workspace===null);if(current(g)&&workspace)workspaceView.value=workspace==='write'&&!canViewWriting.value?'view':workspace}
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
function composerKeydown(event){
 if(mode.value!=='raw'||!enterToSend.value||event.key!=='Enter'||event.shiftKey||event.ctrlKey||event.metaKey||event.altKey||event.isComposing||event.keyCode===229||composing.value)return
 event.preventDefault()
 if(!event.repeat)void send()
}
async function send(){
 if(busy.value||loading.value||readonly.value||!draft.value.trim()||draft.value.length>maxCharacters.value||expired.value)return
 if(formal.value)workspaceView.value='write'
 if(failedDraft.value){await retryMessage(failedDraft.value);return}
 const g=generation,content=draft.value
 busy.value=true;replyPending.value=true;latestResult.value=null;replyFailure.value=null;failureDiagnostic.value=null;failureInspection.value=null;error.value=''
 try{
  if(mode.value==='raw'){
   if(!rawId.value)throw new Error('請先重新開始純 AI 聊天。')
   const sentSessionId=rawId.value,previousLength=rawMessages.value.length
   const data=await api(`${base}/raw/sessions/${sentSessionId}/messages`,options({method:'POST',body:{content,...selectedModelBody()}}))
   if(current(g)){
    if(canInspectRequests.value&&data.requestInspection&&data.sessionId===sentSessionId){const index=data.messages.length-1;if(index>=previousLength&&data.messages[index]?.role==='assistant')requestSnapshots.set(rawSnapshotKey(sentSessionId,index),structuredClone(data.requestInspection))}
    rawMessages.value=data.messages.map((message,index)=>{
     const modelName=message.modelName||rawMessages.value[index]?.modelName||(index>=previousLength?data.modelName:null)
     return modelName?{...message,modelName}:message
    });draft.value=''
    const index=rawMessages.value.length-1;if(data.sessionId===sentSessionId&&index>=previousLength&&rawMessages.value[index]?.role==='assistant')latestResult.value={anchor:resultAnchor(rawMessages.value[index],index)}
   }
  }else{
   if(attempt.value?.content!==content||attempt.value?.revisesMessageId!==revisionParent.value)attempt.value={requestId:crypto.randomUUID(),content,revisesMessageId:revisionParent.value}
   const requestId=attempt.value.requestId
   const data=await api(mutationUrl(`${base}/stages/${mode.value}/messages`,true),options({method:'POST',body:{...attempt.value,...selectedModelBody()}}))
   if(current(g)){saveStageInspection(data,requestId);mergeMutation(data,requestId);clearComposer();rememberStageResult(data,requestId)}
  }
 }catch(e){if(current(g)&&e.name!=='AbortError'){reportReplyFailure(e);if(mode.value!=='raw')await loadStage(g,false,undefined,history.value.pagination?attempt.value?.requestId:null)}}finally{if(current(g)){busy.value=false;replyPending.value=false}}
}
async function retryMessage(message){
 if(busy.value||loading.value||readonly.value||message.trashedAt||!canRetryMessage(message))return
 if(formal.value)workspaceView.value='write'
 const g=generation;busy.value=true;replyPending.value=true;latestResult.value=null;replyFailure.value=null;failureDiagnostic.value=null;failureInspection.value=null;error.value=''
 try{const profile=modelProfiles.value.find(p=>p.modelName===message.modelName);const data=await api(mutationUrl(`${base}/stages/${mode.value}/messages/${message.id}/retry`,true),options({method:'POST',...(profile?{body:{modelProfile:profile.key}}:{})}));if(current(g)){saveStageInspection(data,message.id);mergeMutation(data,message.id);if(draft.value===message.content)clearComposer();attempt.value=null;rememberStageResult(data,message.id)}}
 catch(e){if(current(g)&&e.name!=='AbortError'){reportReplyFailure(e);await loadStage(g,false,undefined,history.value.pagination?message.id:null)}}finally{if(current(g)){busy.value=false;replyPending.value=false}}
}
async function review(){
 if(!canReview.value||!confirmDeparture())return
 const g=invalidate();try{await disposeRaw(true)}catch(e){if(current(g))report(e);return}if(!current(g))return
 tab.value='review';clearComposer();error.value='';setHistory({sessions:[],activeSessionId:null});loading.value=true
 records.value=null
 try{if(config.value?.canReview){const data=await api(`${base}/records`,options());if(current(g))records.value=data}}catch(e){if(current(g)&&e.name!=='AbortError')report(e)}finally{if(current(g))loading.value=false}
}
function selectStudent(id){studentId.value=id}
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
 try{const data=await api(base,options());if(!current(g))return;config.value=data;if(data.enabled){if(canUseTesting.value)await startRaw(g);else{mode.value=stages.value[0]?.key||'premorbid';focusedTab.value=mode.value;await loadStage(g,true)}}}catch(e){if(current(g)&&e.name!=='AbortError')report(e)}finally{if(current(g))loading.value=false}
}
onMounted(()=>{window.addEventListener('beforeunload',beforeUnload);window.addEventListener('pagehide',pageHide);window.addEventListener('pageshow',pageShow);void loadConfiguration()})
watch(()=>[auth.user?.id,experience.student,props.course.id,props.course.courseRole],()=>{invalidate();selectedModelProfile.value='';attempt.value=null;if(config.value)config.value={...config.value,canInspectRequests:false,canChooseModel:false,modelProfiles:[]};void disposeRaw()},{flush:'sync'})
onBeforeUnmount(()=>{destroyed=true;invalidate();window.removeEventListener('beforeunload',beforeUnload);window.removeEventListener('pagehide',pageHide);window.removeEventListener('pageshow',pageShow);void disposeRaw()})
</script>

<template>
 <section ref="chatRoot" class="surface case-analysis" aria-label="個案分析 AI 助教">
  <div class="section-title"><h2><MessagesSquare :size="22" aria-hidden="true"/> 個案分析 AI 助教</h2><span v-if="canUseTesting&&tab==='chat'&&currentModelName" class="muted" data-current-model>模型：{{currentModelName}}</span><button v-if="config?.enabled&&canReview" type="button" class="button secondary" data-action="review" :aria-pressed="tab==='review'" :disabled="busy||loading" @click="review"><UsersRound :size="17" aria-hidden="true"/> 正式提交與統計</button></div>
  <div v-if="error&&!showComposerError" data-chat-error="page" class="error-box" role="alert"><p>{{error}}</p><div v-if="canInspectRequests&&failureDiagnostic" data-failure-diagnostic><strong>教師診斷：{{failureDiagnostic.code}}</strong><p>{{failureDiagnostic.reason}}</p><p v-if="failureDiagnostic.upstreamStatus">模型服務 HTTP {{failureDiagnostic.upstreamStatus}}</p><small>診斷僅保留於本頁，不寫入聊天歷史；模型未提供的原因不會推測。</small></div><button v-if="canInspectRequests&&failureInspection" class="button secondary" type="button" data-action="failed-request" @click="openFailureRequest()">查看失敗請求</button></div>
  <p v-if="!config&&loading" role="status">載入 AI 助教中…</p>
  <button v-if="!config&&!loading" class="button secondary" @click="loadConfiguration">重新載入 AI 助教</button>
  <p v-if="config&&!config.enabled" class="empty-inline">此課程尚未啟用個案分析 AI 助教。</p>
  <template v-if="config?.enabled">
   <p v-if="config.isPractice&&!studentPreview" class="practice-notice">教師／助教教學練習：階段對話僅供本人練習，不計入正式學生統計。各階段系統目前為 AI 提供，尚未經過專業調教與驗證。</p>
   <div v-if="studentPreview" class="practice-notice" data-student-writing-preview><p>學生作答預覽：可查看各階段作答要求，無法在此輸入或送出內容。模型測試請返回教學模式。</p><a class="button primary" data-action="return-teaching" :href="`/courses/${course.id}?section=case-analysis&view=teaching`">返回教學模式做模型測試</a></div>
   <p v-else-if="!config.canChat" class="practice-notice">目前為唯讀檢視，無法傳送訊息或建立對話。</p>
   <nav class="workspace-tabs" role="tablist" aria-label="學習工作區" data-workspace-navigation @keydown="navigateWorkspace">
    <button v-for="item in [{key:'write',label:'作答'},{key:'view',label:'回饋'}]" :key="item.key" :id="workspaceTabId(item.key)" type="button" role="tab" class="mode-tab" :data-workspace-tab="item.key" :aria-selected="activeWorkspace===item.key" :aria-controls="`${tabPrefix}-workspace-panel`" :tabindex="focusedWorkspace===item.key?0:-1" :disabled="loading||(busy&&mode!=='raw')||(item.key==='write'&&!canViewWriting)" @focus="focusedWorkspace=item.key" @click="chooseWorkspace(item.key)"><component :is="item.key==='write'?PencilLine:ClipboardCheck" class="workspace-tab-icon" :size="17" aria-hidden="true"/>{{item.label}}</button>
    <button v-if="canUseTesting" :id="workspaceTabId('raw')" type="button" role="tab" class="mode-tab" data-mode="raw" :aria-selected="activeWorkspace==='raw'" :aria-controls="`${tabPrefix}-workspace-panel`" :tabindex="focusedWorkspace==='raw'?0:-1" :disabled="busy||loading" @focus="focusedWorkspace='raw'" @click="chooseWorkspace('raw')"><MessagesSquare class="workspace-tab-icon" :size="17" aria-hidden="true"/>AI 對話測試</button>
   </nav>
   <div :id="`${tabPrefix}-workspace-panel`" role="tabpanel" :aria-labelledby="workspaceTabId(activeWorkspace)" class="workspace-panel">
   <nav v-if="formal" class="chat-modes" role="tablist" aria-label="分析階段" data-phase-navigation @keydown="navigateTabs">
    <button v-for="stage in stages" :key="stage.key" class="mode-tab" role="tab" :id="`${tabPrefix}-${stage.key}`" :aria-label="stage.label" :aria-controls="`${tabPrefix}-panel`" :tabindex="focusedTab===stage.key?0:-1" @focus="focusedTab=stage.key" :data-mode="stage.key" :aria-selected="activeTab===stage.key" @click="chooseStage(stage.key)"><span class="phase-tab-badge" :style="{'--stage-badge-color':`var(--code-${stage.label[0]})`}">{{stage.label[0]}}</span>{{' '}}<span>{{stage.label.slice(1).trim()}}</span></button>
   </nav>
   <div :role="formal?'tabpanel':undefined" :tabindex="formal?0:undefined" :id="`${tabPrefix}-panel`" :aria-labelledby="formal?`${tabPrefix}-${activeTab}`:undefined" class="mode-panel">
   <CaseStageGuidance v-if="formal&&canUseTesting" :stage="activeTab" teacher/>
   <CaseClassificationRules v-if="formal&&course.courseRole==='teacher'&&config.canMaintainRules===true&&!experience.student" :course="course" :config="config" @saved="rulesSaved"/>
   <template v-if="tab==='review'">
    <p class="field-hint">僅查閱學生正式提交的作答與 AI 回饋；私人分析草稿不開放查閱。下方統計僅顯示聚合數字。</p>
    <details v-if="records" class="aggregate-statistics"><summary>AI 分析使用統計（不含私人作答內容）</summary>
     <p class="statistics-summary">全課程合計：學生訊息 {{totals.studentMessages}} · AI 回覆 {{totals.assistantReplies}} · 往返 {{totals.roundTrips}} · 修改 {{totals.revisionCount}} · 最近活動 {{date(totals.lastActivityAt)}}</p>
     <div class="table-scroll"><table class="data-table"><thead><tr><th>學員</th><th>學生訊息</th><th>AI 回覆</th><th>往返</th><th>修改次數</th><th>最近活動</th><th>各階段統計</th></tr></thead><tbody><tr v-for="student in records.students" :key="student.id"><td><button class="button secondary" :data-student="student.id" @click="selectStudent(student.id)">{{student.studentNumber}} {{student.displayName}}</button></td><td>{{student.statistics?.studentMessages||0}}</td><td>{{student.statistics?.assistantReplies||0}}</td><td>{{student.statistics?.roundTrips||0}}</td><td>{{student.statistics?.revisionCount||0}}</td><td>{{date(student.statistics?.lastActivityAt)}}</td><td><div v-for="stage in student.stages" :key="stage.stage">{{stageLabel(stage.stage)}}：訊息 {{stage.statistics?.studentMessages||0}} / 回覆 {{stage.statistics?.assistantReplies||0}} / 往返 {{stage.statistics?.roundTrips||0}} / 修改 {{stage.statistics?.revisionCount||0}} · {{date(stage.statistics?.lastActivityAt)}}</div></td></tr></tbody></table></div>
     <p v-if="!records.students?.length" class="empty-inline">目前尚無正式學生紀錄。</p>
    </details>
    <button v-if="studentId" class="button secondary" type="button" @click="studentId=''">顯示全部學生</button>
    <CaseFormalSubmissions v-if="canReviewFormal" :course="course" :config="config" :stage="reviewStage" :student-id="studentId" review/>
    <p v-else class="field-hint">目前角色僅可查看聚合使用統計，無正式作答批閱權限。</p>
   </template>
   <template v-else-if="mode==='raw'">
    <p class="field-hint">此為自由 AI 聊天室, 用來測試不同模型環境的回應. 可在允許長度內自由測試 AI 回應效能</p>
    <button v-if="config.canChat" type="button" class="button secondary" data-action="clear-raw" @click="clearRaw"><RotateCcw :size="16" aria-hidden="true"/> {{expired?'對話已逾時，重新開始':'清除並重新開始'}}</button>
    <button v-if="config.canChat&&rawCapacityReached" type="button" class="button secondary" data-action="recover-raw" :disabled="loading||busy" @click="recoverRawCapacity">清除本帳號的暫存聊天並重新開始</button>
   </template>
   <template v-else>
    <p class="field-hint">{{stageLabel(mode)}}的每份作答與配對 AI 回饋各自保留，可依模型篩選。每次只將本次作答與當前階段提示送給 AI，歷次版本不會再次送入模型。</p>
   </template>
   <p v-if="loading" role="status">載入對話中…</p>
   <div v-else-if="tab==='chat'" :class="{'feedback-workspace':formal,'is-writing':formal&&workspaceView==='write'}">
     <label v-if="formal&&(entries.length||historyModels.length)" v-show="workspaceView==='view'" class="history-select">模型篩選<select data-history-model-filter :value="historyModel" :disabled="busy||loading" @change="filterHistory"><option value="">全部模型</option><option v-for="item in historyModels" :key="item.key" :value="item.key">{{item.label}}</option></select></label>
    <div v-if="formal" v-show="workspaceView==='view'" class="feedback-rail">
    <aside class="round-sidebar" aria-label="回饋輪次">
     <h3>{{trashView?'垃圾桶':'作答版本'}} <span class="muted">{{historyTotal}}</span></h3>
     <button type="button" class="button secondary trash-toggle" data-action="toggle-trash" :aria-pressed="trashView" :disabled="busy||loading" @click="toggleTrash">{{trashView?'返回回饋輪次':'垃圾桶'}}（{{trashTotal}}）</button>
     <p class="muted trash-notice">垃圾桶只整理顯示，作答與回饋仍會保留，正式學習統計不變。</p>
     <RevisionHistoryList :options="roundOptions" :model-value="selectedRound?.id||''" :disabled="busy||loading" @update:model-value="selectRound" @remove="removeRound"/>
     <p v-if="history.pagination&&historyTotal" class="muted" role="status">已載入 {{visibleRounds.length}} / {{historyTotal}} 筆</p>
     <p v-if="historyError" data-history-error class="error-text" role="alert">{{historyError}}</p>
     <button v-if="history.pagination?.nextCursor!=null" type="button" class="button primary" data-action="load-more-history" :disabled="busy||loading||historyLoading" @click="loadMoreHistory">{{historyLoading?'載入中…':historyError?'重試載入更多':'載入更多（5 筆）'}}</button>
     <p v-if="!visibleRounds.length" class="empty-inline">{{trashView?'此篩選下的垃圾桶目前沒有作答。':historyModel?'此模型尚無作答紀錄。':'完成 AI 分析後，版本會列在這裡。'}}</p>
     <AnswerRevisionDiff v-if="!trashView" :key="diffScope" :revisions="revisions"/>
    </aside>
    </div>
    <div class="round-detail">
    <section v-show="!formal||workspaceView==='view'" :id="formal?`${workspaceTabId('view')}-panel`:undefined"  class="feedback-reading">
     <CaseFormalSubmissions v-if="formal" :course="course" :config="config" :stage="mode" :selected-round="selectedRound" :draft="draft" :busy="busy||loading||trashView" @loaded="formalEnvelope=$event" @ready="formalReady=$event"/>

     <div v-if="formal&&selectedRound" class="round-heading"><h3>#{{selectedEntry?.number}}</h3><span class="muted">{{date(selectedRound.createdAt)}}</span><button v-if="canManageRounds&&trashView" type="button" class="button secondary" data-action="restore-round" :disabled="busy||loading||selectedRound.status==='pending'" @click="moveRound(true)">還原此輪作答</button></div>
     <div class="chat-log" role="log" aria-live="polite" aria-label="對話內容" :aria-busy="busy">
      <p v-if="!shownMessages.length&&!busy" class="empty-inline">{{formal?'尚無回饋，請切換至「作答」完成 AI 分析。':'尚無訊息。'}}</p>
      <article v-for="(message,index) in shownMessages" :key="message.id||index" :id="message.role==='assistant'?resultAnchor(message,index):undefined" :data-result-anchor="message.role==='assistant'?'':undefined" :tabindex="message.role==='assistant'?-1:undefined" class="chat-message" :class="message.role==='user'?'from-user':'from-assistant has-diagnostics'"><div class="message-author"><TutorAvatar v-if="message.role==='assistant'"/><strong>{{message.role==='user'?(formal?'學生作答':'使用者'):(formal?'AI 助教回饋':'AI 助教')}}</strong></div><PhaseAnnotatedAnswer v-model:display-mode="annotationDisplayMode" v-if="formal&&message.role==='user'" :key="`${diffScope}:${session?.id}:${message.id}`" :content="message.content" :stage="activeStage" :annotations="feedback?.annotations||[]" :classifications="roundClassifications"><template v-if="!readonly" #actions><button type="button" class="button secondary" data-action="revise-answer" :disabled="busy||loading||selectedRound.status!=='completed'" @click="reviseAnswer">複製到輸入框</button></template></PhaseAnnotatedAnswer><p v-else class="message-content">{{message.content}}</p><PhaseAlignmentNotice :display-mode="annotationDisplayMode" v-if="formal&&message.role==='assistant'&&message.replyTo===selectedRound?.id" :content="selectedRound.content" :stage="activeStage" :annotations="message.annotations||[]" :classifications="roundClassifications"/><small v-if="message.createdAt" class="muted">{{date(message.createdAt)}}</small><span v-if="message.status==='pending'" role="status"> 等待回覆中</span><span v-if="message.status==='failed'" class="error-text"> 回覆失敗，原訊息已保留</span><button v-if="message.role==='user'&&message.status==='failed'&&!readonly&&canRetryMessage(message)" class="button secondary" :disabled="busy" data-action="retry-message" @click="retryMessage(message)">重試回覆</button><ReplyDiagnostics v-if="message.role==='assistant'&&showDiagnostics" :model-name="message.modelName||''" :can-inspect-requests="canInspectRequests" @performance="openPerformance(message)" @request="openPerformance(message,'request')"/></article>
     </div>
     <button v-if="formal&&!trashView&&feedback&&nextStage&&tab==='chat'" type="button" class="button secondary" data-action="next-stage" @click="chooseStage(nextStage.key)">下一階段：{{nextStage.label}}</button>
    </section>
   <form v-if="tab==='chat'&&canViewWriting" v-show="!formal||workspaceView==='write'" :id="formal?`${workspaceTabId('write')}-panel`:undefined"  class="chat-composer" @submit.prevent="send">
    <CaseStageGuidance v-if="formal&&!canUseTesting" :stage="mode"/>
    <h3 v-if="formal" class="phase-composer-title">{{stageLabel(mode)}} · 作答與 AI 分析</h3>
    <p v-if="formal&&course.courseRole==='student'&&!config.isPractice&&!studentPreview" class="field-hint">先取得 AI 分析並修改作答；再切換「回饋」檢閱，準備完成後按「正式提交」供教師查閱。</p>
    <p v-if="revisionParent" class="revision-notice">以 #{{revisionParentNumber}} 為基礎撰寫新版本；分析後保留既有版本。<button type="button" class="button secondary" :disabled="busy" @click="cancelRevision">取消修改</button></p>
    <label for="case-chat-draft">{{formal?'本階段作答':'訊息'}}<textarea id="case-chat-draft" v-model="draft" :rows="formal?10:4" :maxlength="maxCharacters" :disabled="readonly||busy||loading||expired" :placeholder="studentPreview?'學生將在此輸入本階段作答（預覽無法輸入）':'輸入訊息…'" @keydown="composerKeydown" @compositionstart="composing=true" @compositionend="composing=false" @blur="composing=false" /></label>
    <div class="composer-input-options"><small class="muted">{{draft.length}} / {{maxCharacters}} 字</small><label v-if="mode==='raw'" class="choice-label enter-send-toggle" title="開啟後按 Enter 傳送，Shift+Enter 換行"><input v-model="enterToSend" type="checkbox" role="switch" aria-label="Enter 傳送" data-enter-send :disabled="readonly||busy||loading||expired"/><span>Enter 傳送</span><small v-if="enterToSend" class="muted">Shift+Enter 換行</small></label></div>
    <div v-if="error&&showComposerError" data-chat-error="composer" class="error-box" role="alert"><p>{{error}}</p><div v-if="canInspectRequests&&failureDiagnostic" data-failure-diagnostic><strong>教師診斷：{{failureDiagnostic.code}}</strong><p>{{failureDiagnostic.reason}}</p><p v-if="failureDiagnostic.upstreamStatus">模型服務 HTTP {{failureDiagnostic.upstreamStatus}}</p><small>診斷僅保留於本頁，不寫入聊天歷史；模型未提供的原因不會推測。</small></div><button v-if="canInspectRequests&&failureInspection" class="button secondary" type="button" data-action="failed-request" @click="openFailureRequest()">查看失敗請求</button></div>
    <div v-if="replyPending||latestResult||replyFailure" class="composer-feedback">
     <AiReplyWaiting :class="replyPending||replyFailure?'chat-message from-assistant':'reply-ready'" :formal="formal" :hints="requestHints" :failed="!!replyFailure" :completed="!replyPending&&!replyFailure&&!!latestResult" :result-href="latestResult?`#${latestResult.anchor}`:undefined" :result-label="`${formal?'查看最新回饋':'查看最新回覆'}${latestResult?.number?`（#${latestResult.number}）`:''}`" @view-result="visitLatestResult"><template #failure><div v-if="replyFailure" class="reply-failure-details"><p>{{replyFailure.message}}</p><div v-if="canInspectRequests&&replyFailure.diagnostic" data-failure-diagnostic><strong>教師診斷：{{replyFailure.diagnostic.code}}</strong><p>{{replyFailure.diagnostic.reason}}</p><p v-if="replyFailure.diagnostic.upstreamStatus">模型服務 HTTP {{replyFailure.diagnostic.upstreamStatus}}</p><small>診斷僅保留於本頁，不寫入聊天歷史；模型未提供的原因不會推測。</small></div><div class="reply-failure-actions"><button type="button" class="button primary" data-action="retry-reply" :disabled="readonly||busy||loading||expired||!draft.trim()||draft.length>maxCharacters" @click="send">{{formal&&failedDraft?'重試回覆':'重新送出'}}</button><button v-if="canInspectRequests&&replyFailure.inspection" type="button" class="button secondary" data-action="failed-request" @click="openFailureRequest(replyFailure.inspection)">查看失敗請求</button></div></div></template></AiReplyWaiting>
    </div>
    <div class="composer-actions">
     <label v-if="canChooseModel" class="model-select">本次回覆模型<select data-model-select :value="selectedModelProfile" :disabled="busy||loading||expired" @change="chooseModel"><option value="">環境預設（{{config.modelName}}）</option><option v-for="profile in modelProfiles" :key="profile.key" :value="profile.key">{{profile.label}} · {{profile.modelName}}</option></select></label>
     <button type="submit" class="button primary" :disabled="readonly||busy||loading||expired||!draft.trim()||draft.length>maxCharacters"><Send :size="16" aria-hidden="true"/> {{busy?'AI 回覆中…':failedDraft?'重試這則訊息':attempt?'重送同一則訊息':formal?'取得 AI 分析':'傳送'}}</button>
    </div>
    <small v-if="canChooseModel" class="field-hint">每次提交只送至所選模型；切換保留草稿與對話，歷次回饋保留當時模型。</small>
   </form>
   <CaseCategoryReference v-if="formal&&canViewWriting" v-show="workspaceView==='write'" :visible="workspaceView==='write'" :stage="mode" :classifications="config.classifications||[]"/>
    </div>
   </div>
   <ChatPerformanceDialog :open="!!performance" :metrics="performance?.metrics||null" :title="performance?.title" :model-name="performance?.modelName" :prompt-version="performance?.promptVersion" :initial-tab="performance?.initialTab" :can-inspect-requests="canInspectRequests" :request-inspection="canInspectRequests?performance?.requestInspection||null:null" @close="performance=null"/>
   </div>
   </div>
  </template>
 </section>
</template>

<style scoped>
.reply-failure-details{line-height:1.8}.reply-failure-details p{margin:8px 0}.reply-failure-details [data-failure-diagnostic]{border-top:1px solid var(--line);padding-top:12px;margin-top:12px}.reply-failure-details small{display:block;color:var(--muted)}.reply-failure-actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:16px}
.workspace-tab-icon{flex:none}
.composer-input-options{display:flex;align-items:center;justify-content:space-between;gap:8px 16px;flex-wrap:wrap;margin-top:8px}.enter-send-toggle{display:flex;align-items:center;gap:8px;flex-wrap:wrap;min-height:36px;font-size:.8rem;font-weight:500;cursor:pointer}.enter-send-toggle input{margin:0}.enter-send-toggle small{font-size:.75rem;font-weight:400}
.phase-tab-badge{display:inline-flex;align-items:center;justify-content:center;flex:none;min-width:26px;height:26px;padding:2px 6px;border-radius:6px;background:var(--stage-badge-color);color:#fff;font-size:.85rem;font-weight:700;line-height:1}
.trash-toggle{min-height:44px}.trash-notice{font-size:.8rem;line-height:1.7;margin:10px 0}

.model-select{display:grid;gap:8px;margin:18px 0;max-width:650px}.model-select select{width:100%;min-width:0}.feedback-workspace{display:grid;grid-template-columns:minmax(200px,240px) minmax(0,1fr);gap:20px;margin:20px 0;align-items:start}.round-sidebar,.category-sidebar{min-width:0;border:1px solid var(--line);border-radius:10px;padding:14px;background:var(--surface)}.round-sidebar h3,.category-sidebar h3,.round-heading h3{margin:0;font-size:1rem}.round-sidebar select{width:100%;min-width:0;max-width:100%}.round-detail{min-width:0}.round-heading{display:flex;gap:12px;align-items:center;flex-wrap:wrap}.round-heading .button{margin-left:auto}.feedback-workspace .chat-log{max-height:none;margin:16px 0;overflow:visible}.feedback-workspace .chat-message{width:100%;max-width:100%;box-sizing:border-box}.revision-notice{padding:12px;background:var(--accent-soft);overflow-wrap:anywhere}.revision-notice .button{margin-left:8px}@media(max-width:1050px){.feedback-workspace{grid-template-columns:minmax(0,1fr)}.round-heading .button{margin-left:0}.round-sidebar{padding:12px}}
.case-analysis{padding:24px;min-width:0}.section-title,.chat-modes,.chat-history-actions,.composer-actions{display:flex;align-items:center;gap:10px;flex-wrap:wrap}.section-title{justify-content:space-between}.section-title h2{display:flex;align-items:center;gap:8px;margin:0}.chat-modes{margin:20px 0}.chat-modes .button[aria-pressed=true]{background:var(--action);color:#fff;border-color:var(--action)}.practice-notice{padding:12px 16px;background:var(--accent-soft);border-left:4px solid var(--accent);border-radius:6px}.chat-log{display:flex;flex-direction:column;gap:14px;margin:20px 0;max-height:65vh;overflow-y:auto;overflow-wrap:anywhere}.chat-message{padding:14px 16px;border:1px solid var(--line);border-radius:10px;background:var(--surface);max-width:95%;min-width:0}.from-user{align-self:flex-end;background:var(--accent-soft)}.from-assistant{align-self:flex-start;background:color-mix(in srgb,#438b88 12%,var(--surface));border-color:color-mix(in srgb,#438b88 30%,var(--line));border-radius:18px 18px 18px 5px}.message-author{display:flex;align-items:center;gap:12px}.from-assistant .message-content{line-height:1.9}.from-assistant .message-author{margin-bottom:12px}.message-content{white-space:pre-wrap;overflow-wrap:anywhere;margin:8px 0}.chat-message .button{margin:8px}.chat-composer{border-top:1px solid var(--line);padding-top:16px}.chat-composer label,.history-select,.review-stage{display:grid;gap:8px}.chat-composer textarea{resize:vertical;min-height:100px}.feedback-workspace .chat-composer textarea{min-height:260px;line-height:1.8}.phase-composer-title{margin:0 0 12px;font-size:1.1rem}.aggregate-statistics>summary{cursor:pointer;min-height:32px;line-height:32px}.round-detail>.category-sidebar{margin:18px 0}.composer-actions{justify-content:space-between;margin-top:10px}.history-select,.review-stage{margin:16px 0;max-width:450px}.error-text{color:var(--danger)}.data-table td{vertical-align:top}.statistics-summary{line-height:1.8}.button{white-space:normal}.table-scroll{max-width:100%;overflow-x:auto}@media(max-width:600px){.case-analysis{padding:16px}.chat-modes{display:grid;grid-template-columns:1fr 1fr}.chat-modes button:first-child{grid-column:1/-1}.chat-modes .button{min-height:44px}.chat-message{max-width:100%}.section-title{align-items:flex-start;flex-direction:column}}

.chat-modes{display:flex;flex-wrap:nowrap;gap:0;overflow-x:auto;max-width:100%;margin:20px 0 0;border-bottom:1px solid var(--line);scrollbar-width:thin}.mode-tab{display:inline-flex;align-items:center;justify-content:center;gap:7px;flex-shrink:0;min-height:48px;padding:12px 16px;border:0;border-bottom:3px solid transparent;background:transparent;color:var(--muted);font:inherit;font-weight:600;white-space:nowrap;cursor:pointer}.mode-tab[aria-selected=true]{border-bottom-color:var(--accent);color:var(--accent);background:var(--accent-soft)}.mode-tab:hover{color:var(--text)}.mode-tab:focus-visible,.mode-panel:focus-visible{outline:2px solid var(--accent);outline-offset:-3px}.mode-panel{padding-top:18px;min-width:0}.error-box p{margin:6px 0}.error-box [data-failure-diagnostic]{border-top:1px solid currentColor;margin-top:10px;padding-top:10px;overflow-wrap:anywhere}.error-box .button{margin-top:10px}
.composer-actions{align-items:end}.model-select{flex:1 1 280px;min-width:0;max-width:540px}.model-select select{width:100%;min-width:0}.chat-composer>.field-hint{display:block;margin-top:8px}@media(max-width:600px){.model-select{max-width:none;flex-basis:100%}.composer-actions>.button{width:100%}}
.composer-feedback .chat-message{max-width:100%}.reply-ready{padding:12px 14px;border:1px solid var(--line);border-radius:8px;background:var(--accent-soft);line-height:1.8}.from-assistant[data-result-anchor]{scroll-margin-top:100px}.from-assistant[data-result-anchor]:focus{outline:2px solid var(--accent);outline-offset:3px}

.feedback-rail{display:grid;gap:14px;min-width:0;align-self:start}.category-empty{font-size:.85rem;line-height:1.7;margin:12px 0 0}.category-host{min-width:0}
@media(max-width:1050px){.feedback-rail{position:static;max-height:none;overflow:visible}}
.chat-message.has-diagnostics{position:relative;padding-right:72px}.has-diagnostics>.reply-diagnostics{position:absolute;top:14px;right:12px}
@media(max-width:600px){.chat-message.has-diagnostics{min-height:125px;padding-right:76px}}

.feedback-workspace>.history-select{grid-column:1/-1;width:min(100%,52rem);min-width:0;max-width:none;margin:0}
.feedback-workspace>.history-select select{width:100%;min-width:0;max-width:100%;box-sizing:border-box}
.workspace-tabs{display:flex;gap:0;overflow-x:auto;max-width:100%;border-bottom:1px solid var(--line);min-width:0;margin-top:20px;scrollbar-width:thin}.workspace-panel{min-width:0}.workspace-tabs .mode-tab{flex:0 0 auto;min-width:100px}.feedback-workspace.is-writing{grid-template-columns:minmax(0,1fr)}.feedback-reading{min-width:0}.feedback-reading>.category-sidebar{margin:18px 0}.workspace-tabs .mode-tab:disabled{cursor:default}
</style>
