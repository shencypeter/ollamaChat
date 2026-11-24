<script setup>
import {computed,onBeforeUnmount,ref,watch} from 'vue'
import {api} from '../api'
import {auth} from '../composables/auth'
import {experience} from '../composables/experience'
import PhaseAnnotatedAnswer from './PhaseAnnotatedAnswer.vue'
import PhaseAlignmentNotice from './PhaseAlignmentNotice.vue'
import AnswerRevisionDiff from './AnswerRevisionDiff.vue'
const props=defineProps({course:{type:Object,required:true},config:{type:Object,required:true},stage:{type:String,required:true},selectedRound:Object,draft:{type:String,default:''},busy:Boolean,review:Boolean,studentId:{type:String,default:''}})
const emit=defineEmits(['loaded','ready'])
const envelope=ref({submissions:[],assignments:[]}),loading=ref(false),loaded=ref(false),saving=ref(false),error=ref(''),selectedId=ref(''),assignmentId=ref(''),reason=ref(''),displayMode=ref('underline')
let generation=0,controller,request=null,destroyed=false
const base=computed(()=>`/courses/${props.course.id}/case-analysis/submissions`)
const staff=computed(()=>['teacher','ta'].includes(props.course.courseRole)&&!experience.student)
const teachingPractice=computed(()=>!props.review&&(props.config.isPractice===true||['teacher','ta'].includes(props.course.courseRole)))
const canRead=computed(()=>!experience.student&&(props.review?staff.value&&(props.config.canReviewFormal??props.config.canReview)===true:props.course.courseRole==='student'))
const canSubmit=computed(()=>props.course.courseRole==='student'&&!experience.student&&!props.config.isPractice&&props.config.canChat===true&&props.config.canSubmitFormal!==false&&!props.review)
const versions=computed(()=>envelope.value.submissions.filter(s=>s.stage===props.stage&&(!props.review||!props.studentId||s.studentId===props.studentId)).sort((a,b)=>props.review&&!props.studentId?String(b.submittedAt||'').localeCompare(String(a.submittedAt||''))||b.version-a.version:b.version-a.version))
const selected=computed(()=>versions.value.find(s=>s.id===selectedId.value)||versions.value[0])
const formalRevisions=computed(()=>versions.value.filter(item=>item.studentId===selected.value?.studentId).sort((a,b)=>a.version-b.version).map(item=>({id:item.id,label:date(item.submittedAt),content:item.answer})))
const latest=computed(()=>versions.value[0])
const draftMatches=computed(()=>!props.draft||props.draft===props.selectedRound?.content)
const ready=computed(()=>canSubmit.value&&loaded.value&&!props.busy&&!loading.value&&!saving.value&&props.selectedRound?.status==='completed'&&!props.selectedRound?.trashedAt&&draftMatches.value&&(!latest.value||latest.value.status==='returned'))
const canReturn=computed(()=>props.review&&staff.value&&props.config.canChat!==false&&(props.config.canReviewFormal??props.config.canReview)===true&&selected.value?.status==='submitted'&&!versions.value.some(s=>s.studentId===selected.value.studentId&&s.version>selected.value.version))
const classifications=computed(()=>selected.value?.ruleSnapshot||props.config.classifications||[])
function date(value){return value?new Date(value).toLocaleString('zh-TW'):'尚無紀錄'}
function accept(data,preferred){envelope.value={submissions:data.submissions||[],assignments:data.assignments||[]};if(!envelope.value.assignments.some(item=>item.id===assignmentId.value))assignmentId.value='';selectedId.value=preferred||'';emit('loaded',envelope.value)}
async function load(){
 const g=++generation;controller?.abort();controller=new AbortController();error.value='';loading.value=true;loaded.value=false;request=null;accept({})
 if(!canRead.value){loading.value=false;return}
 try{const data=await api(base.value,{signal:controller.signal});if(!destroyed&&g===generation){accept(data);loaded.value=true}}catch(e){if(!destroyed&&g===generation&&e.name!=='AbortError')error.value=e.message||'正式提交載入失敗。'}finally{if(!destroyed&&g===generation)loading.value=false}
}
async function submit(){
 if(!ready.value)return
 if(!window.confirm(`確認正式提交此階段作答？提交後將保存目前分析版本，需教師退回後才能重新提交。`))return
 const key=JSON.stringify([props.selectedRound.id,assignmentId.value,latest.value?.id||null]);if(request?.key!==key)request={key,id:crypto.randomUUID()}
 const g=generation;saving.value=true;error.value=''
 try{const data=await api(base.value,{method:'POST',signal:controller.signal,body:{requestId:request.id,messageId:props.selectedRound.id,assignmentId:assignmentId.value||null,expectedPreviousId:latest.value?.id||null}});if(!destroyed&&g===generation){accept(data);request=null}}
 catch(e){if(!destroyed&&g===generation&&e.name!=='AbortError')error.value=e.message||'正式提交失敗，請重試。'}finally{if(!destroyed&&g===generation)saving.value=false}
}
async function returnSubmission(){
 if(!canReturn.value||!reason.value.trim()||saving.value)return
 const g=generation,id=selected.value.id;saving.value=true;error.value=''
 try{const data=await api(`${base.value}/${id}/return`,{method:'POST',signal:controller.signal,body:{reason:reason.value.trim()}});if(!destroyed&&g===generation){accept(data,id);reason.value=''}}
 catch(e){if(!destroyed&&g===generation&&e.name!=='AbortError')error.value=e.message||'退回失敗，請重試。'}finally{if(!destroyed&&g===generation)saving.value=false}
}
watch(loaded,value=>emit('ready',value),{immediate:true,flush:'sync'})
watch(()=>[props.course.id,props.course.courseRole,auth.user?.id,experience.student,props.review,canRead.value],()=>{saving.value=false;reason.value='';void load()},{immediate:true})
watch(()=>props.selectedRound?.id,()=>{request=null})
watch(()=>[props.stage,props.studentId],()=>{selectedId.value='';reason.value='';request=null})
onBeforeUnmount(()=>{destroyed=true;generation++;controller?.abort()})
</script>

<template>
 <section v-if="!teachingPractice" class="formal-submissions" aria-label="正式提交">
  <h3>{{review?'學生正式提交':'正式提交與版本'}}</h3>
  <p class="field-hint">AI 分析完成後仍是本人練習紀錄，按「正式提交」才會供教師查閱。正式版本會保留當時作答與 AI 回饋。</p>
  <p v-if="error" role="alert" class="error-text">{{error}} <button type="button" class="button secondary" :disabled="saving" @click="load">重新載入正式版本</button></p>
  <p v-if="loading" role="status">載入正式版本中…</p>
  <template v-if="canSubmit">
   <label v-if="envelope.assignments.length" class="assignment-select">關聯作業（選填）<select v-model="assignmentId" :disabled="saving||busy"><option value="">課內階段作答</option><option v-for="item in envelope.assignments" :key="item.id" :value="item.id">{{item.title}}</option></select></label>
   <p v-if="!draftMatches" class="field-hint" data-formal-draft-warning>輸入內容已變更，請先取得新的 AI 分析，再正式提交相符版本。</p>
   <p v-else-if="selectedRound?.status!=='completed'" class="field-hint">請先完成此階段的 AI 分析並選擇要提交的分析版本。</p>
   <p v-if="latest?.status==='submitted'" class="field-hint">目前已有正式版本，需教師退回後才能重新提交。</p>
   <button type="button" class="button primary" data-action="submit-formal" :disabled="!ready" @click="submit">{{saving?'正式提交中…':'正式提交'}}<span v-if="selectedRound">（所選分析版本）</span></button>
  </template>
  <p v-else-if="!review" class="field-hint">教學練習與唯讀預覽不會建立正式提交。</p>
  <label v-if="versions.length" class="formal-version-select">{{review?'選擇學生與正式版本':'正式版本歷程'}}<select :value="selected?.id" :disabled="saving" @change="selectedId=$event.target.value;reason='' "><option v-for="item in versions" :key="item.id" :value="item.id">{{review?`${item.studentName||item.studentId} · `:''}}{{date(item.submittedAt)}} · 正式第 {{item.version}} 版 · {{item.status==='returned'?'已退回':'已正式提交'}}</option></select></label>
  <AnswerRevisionDiff v-if="formalRevisions.length>=2" :key="`${stage}:${selected?.studentId}`" :revisions="formalRevisions"/>
  <article v-if="selected" :key="selected.id" class="formal-snapshot" :data-submission="selected.id">
   <h4>{{date(selected.submittedAt)}}</h4>
   <p class="muted">正式第 {{selected.version}} 版 · {{selected.status==='returned'?'已退回':'已正式提交'}}</p>
   <p v-if="selected.status==='returned'" class="return-reason">退回時間：{{date(selected.returnedAt)}}<br>退回理由：{{selected.returnReason}}</p>
   <details :open="review"><summary>查看正式作答與 AI 回饋</summary><PhaseAnnotatedAnswer v-model:display-mode="displayMode" :content="selected.answer" :stage="stage" :annotations="selected.annotations||[]" :classifications="classifications"/><div class="formal-feedback"><h4>AI 助教回饋</h4><p>{{selected.feedback}}</p><PhaseAlignmentNotice :display-mode="displayMode" :content="selected.answer" :stage="stage" :annotations="selected.annotations||[]" :classifications="classifications"/></div></details>
   <div v-if="canReturn" class="return-editor"><label>退回理由（必填）<textarea v-model="reason" rows="3" maxlength="2000" :disabled="saving"/></label><button type="button" class="button secondary" data-action="return-formal" :disabled="saving||!reason.trim()" @click="returnSubmission">退回修改</button></div>
  </article>
  <p v-else-if="!loading" class="empty-inline">此階段尚無正式提交。</p>
 </section>
</template>

<style scoped>
.formal-submissions{min-width:0;border:1px solid var(--line);border-radius:10px;padding:16px;margin:18px 0}.formal-submissions h3{font-size:1.05rem;margin:0 0 10px}.formal-submissions label{display:grid;gap:8px;margin:12px 0}.formal-submissions select,.formal-submissions textarea{width:100%;min-width:0;box-sizing:border-box}.formal-submissions textarea{resize:vertical}.formal-snapshot{margin-top:14px;overflow-wrap:anywhere}.formal-snapshot h4{margin:12px 0}.formal-feedback p{white-space:pre-wrap;line-height:1.8}.formal-snapshot summary{cursor:pointer;padding:10px 0}.return-reason{padding:12px;background:var(--accent-soft);white-space:pre-wrap}.return-editor{border-top:1px solid var(--line);margin-top:16px}.error-text{color:var(--danger)}
</style>
