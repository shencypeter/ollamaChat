<script setup>
import {FilePenLine} from '@lucide/vue'
import {inject,computed,onMounted,onBeforeUnmount,ref,watch} from 'vue'
import {useRoute,onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
import {api} from '../api'
import ReportModeFields from '../components/ReportModeFields.vue'
import ReportEvaluation from '../components/ReportEvaluation.vue'
import FilePreview from '../components/FilePreview.vue'
import {setCourseExperience} from '../composables/experience'
import {formatDate,toLocalInput,toUtc} from '../utils/assignments'
import {activityReturnLocation} from '../utils/courseActivityNavigation'
const roster=ref([])
const selectedCourse=inject('selectedCourse',null)
const route=useRoute(),report=ref(null),submissions=ref([]),error=ref(''),notice=ref(''),busy=ref(false),file=ref(null),editing=ref(false),form=ref(null),baselines=ref({}),ready=ref(false)
const staff=computed(()=>['teacher','ta'].includes(report.value?.courseRole))
const canGrade=computed(()=>staff.value&&!selectedCourse?.value?.taRestrictions?.includes('grading'))
const base=`/reports/${route.params.reportId}`
function changed(s){const old=baselines.value[s.id];return old && (s.score!==old.score || JSON.stringify(s.members)!==old.members || s.feedback!==old.feedback)}
const gradeDirty=computed(()=>staff.value && submissions.value.some(changed))
const submittedCount=computed(()=>roster.value.filter(r=>r.submitted).length)
const groupReview=computed(()=>staff.value&&report.value?.submissionMode==='group')
const selectedGroupId=ref(''),feedbackOpen=ref({}),adjustmentOpen=ref({})
const groupRows=computed(()=>roster.value.map((row,index)=>({...row,key:row.groupId||`roster-${index}`})).sort((a,b)=>Number(b.submitted)-Number(a.submitted)||a.name.localeCompare(b.name,'zh-Hant',{numeric:true})||a.key.localeCompare(b.key)))
const selectedGroup=computed(()=>groupRows.value.find(row=>row.key===selectedGroupId.value))
const selectedIndex=computed(()=>groupRows.value.findIndex(row=>row.key===selectedGroupId.value))
const visibleSubmissions=computed(()=>groupReview.value?submissions.value.filter(s=>s.id===selectedGroup.value?.submissionId):submissions.value)
watch(groupRows,rows=>{if(!rows.some(row=>row.key===selectedGroupId.value))selectedGroupId.value=rows[0]?.key||''})
const publicationComplete=computed(()=>roster.value.length>0&&roster.value.every(row=>row.submitted&&row.submissionId&&submissions.value.some(s=>s.id===row.submissionId&&!s.returned&&s.score!=null&&s.score!=='')))
const publicationSubmissions=computed(()=>submissions.value.filter(s=>roster.value.some(row=>row.submissionId===s.id)))
const canPublishAll=computed(()=>canGrade.value&&publicationComplete.value&&!gradeDirty.value&&!busy.value&&report.value?.status!=='draft')
function turnGroup(offset){const row=groupRows.value[selectedIndex.value+offset];if(row)selectedGroupId.value=row.key}
const hasOverride=m=>m.score!=null&&m.score!==''
function reasonRequired(s,m){return hasOverride(m)||JSON.parse(baselines.value[s.id]?.members||'[]').some(old=>old.id===m.id&&hasOverride(old))}
const finalDraft=(s,m)=>hasOverride(m)?m.score:(s.score==null||s.score===''?'待評分':s.score)
function submissionStatus(r){return r.submitted?(r.late?'逾期繳交':'已繳交'):(report.value.dueAt && Date.parse(report.value.dueAt)<=Date.now()?'逾期未繳':'未繳交')}
async function load(savedId=null){
 const freshReport=await api(base)
 if(route.params.courseId && freshReport.courseId!==route.params.courseId){report.value=null;throw new Error('此評量不屬於指定課程。')}
 report.value=freshReport;setCourseExperience(staff.value||report.value.isPractice)
 const freshRoster=await api(base+'/roster')
 const fresh=staff.value && selectedCourse?.value?.taRestrictions?.includes('grading')?[]:await api(base+'/submissions')
 roster.value=freshRoster
 submissions.value=fresh.map(s=>{const old=submissions.value.find(x=>x.id===s.id);if(s.id!==savedId && old && changed(old))return old;baselines.value[s.id]={score:s.score,feedback:s.feedback,members:JSON.stringify(s.members)};return s})
 ready.value=true
}
async function run(action){busy.value=true;error.value='';notice.value='';try{await action()}catch(e){error.value=e.message}finally{busy.value=false}}
async function transition(action){if(!confirm(action==='publish'?'發布後學生可繳交，作業內容會鎖定。確定發布？':'確定關閉繳交？'))return;await run(async()=>{await api(base+'/'+action,{method:'POST'});await load()})}
function edit(){const {weight,...settings}=report.value;form.value={...settings,groupIds:[...report.value.groupIds],dimensions:[...report.value.dimensions],opensAt:toLocalInput(report.value.opensAt),dueAt:toLocalInput(report.value.dueAt),expectedVersion:report.value.version};editing.value=true}
async function save(){await run(async()=>{await api(base,{method:'PUT',body:{...form.value,opensAt:toUtc(form.value.opensAt),dueAt:toUtc(form.value.dueAt)}});editing.value=false;await load();notice.value='PDF 作業草稿已儲存。'})}
async function submit(){if(!confirm('確定將這份 PDF 正式繳交？'))return;await run(async()=>{const body=new FormData();body.append('file',file.value);await api(base+'/submit',{method:'POST',body});file.value=null;await load();notice.value='PDF 報告已繳交。'})}
async function download(s,previous=null){await run(async()=>{const blob=await api(`${base}/submissions/${s.id}/download`+(previous?'?revision='+previous.revision:''),{method:'POST',download:true});const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=previous?.fileName||s.fileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)})}
async function returnFile(s){if(confirm('退回此版本供修改？'))await run(async()=>{await api(base+'/submissions/'+s.id+'/return?version='+s.version,{method:'POST'});await load(s.id)})}
async function grade(s){await run(async()=>{await api(`${base}/submissions/${s.id}/grade`,{method:'PUT',body:{adjustments:s.members.map(m=>({studentId:m.id,score:m.score===''?null:m.score,reason:m.reason||''})),publish:false,score:s.score,feedback:s.feedback,expectedVersion:s.version}});await load(s.id);notice.value='評分草稿已儲存。'})}
async function publishAll(){if(!canPublishAll.value||!confirm('確定一次發布本作業全班已儲存的成績與評語？'))return;await run(async()=>{await api(base+'/grades/publish',{method:'POST',body:{expectedVersion:report.value.version,submissions:publicationSubmissions.value.map(s=>({id:s.id,expectedVersion:s.version}))}});await load();notice.value='本作業全班成績與評語已一次發布。'})}
function guard(){return !(editing.value||file.value||gradeDirty.value)||confirm('有未儲存的作業設定、成績或尚未繳交的檔案，確定離開？')}
onBeforeRouteLeave(guard);onBeforeRouteUpdate(guard)
function unload(event){if(editing.value||file.value||gradeDirty.value){event.preventDefault();event.returnValue=''}}
window.addEventListener('beforeunload',unload);onBeforeUnmount(()=>window.removeEventListener('beforeunload',unload))
onMounted(()=>run(load))
</script>
<template>
 <RouterLink :to="report?activityReturnLocation(report.courseId,report.unitId):'/courses'" class="back-link">← {{report?.unitId?'返回所屬單元':'返回課程內容'}}</RouterLink>
 <p v-if="error" class="error-box" role="alert">{{error}}</p><button v-if="error&&!ready" class="button secondary" :disabled="busy" @click="run(load)">重新載入作業</button>
 <p v-if="notice" class="success-box" role="status">{{notice}}</p><p v-if="busy&&!ready" role="status">載入作業與繳交名單中…</p>
 <template v-if="report">
  <section class="page-heading"><div><p class="eyebrow">PDF REPORT ASSIGNMENT</p><h1>{{report.title}}</h1><p class="muted">{{report.submissionMode==='group'?'小組 PDF 作業':'個人 PDF 作業'}} · 人工閱讀與給分 · 滿分 {{report.maxScore}}</p></div><span class="status-badge">{{{draft:'草稿',published:'已發布',closed:'已關閉'}[report.status]}}</span></section>
  <section class="surface form-surface"><h2>作業說明</h2><p class="studio-prose">{{report.instructions}}</p><p>開放 {{formatDate(report.opensAt)}} · 截止 {{formatDate(report.dueAt)}}</p>
   <template v-if="report.submissionMode==='group'"><p class="field-hint">每組繳交一份 PDF；依繳交時組員名單給分。小組基礎成績為預設分數，教師可填寫原因後個別調整。</p><p class="field-hint">組員回饋：{{{off:'不收集',simple:'組員互評（供教師評分參考）',consensus:'組員互評＋同分意願（供教師評分參考）'}[report.evaluationMode]}}。回饋不會自動決定成績。</p></template>
   <div v-if="staff&&!selectedCourse?.taRestrictions?.includes('assessments')" class="studio-actions"><button v-if="report.status==='draft'" class="button secondary" :disabled="busy" @click="edit">編輯作業草稿</button><button v-if="report.status==='draft'" class="button primary" :disabled="busy || editing" @click="transition('publish')">發布 PDF 作業</button><button v-if="report.status==='published'" class="button secondary" :disabled="busy" @click="transition('close')">關閉繳交</button></div>
  </section>
  <section v-if="editing" class="surface form-surface"><form @submit.prevent="save"><ReportModeFields :form="form" :course="report.courseId"/><label>作業名稱<input v-model="form.title" required maxlength="200" /></label><label>作業說明<textarea v-model="form.instructions" rows="5" maxlength="24000"></textarea></label><label>滿分<input v-model.number="form.maxScore" type="number" min="0.01" max="999999" step="0.01" required /></label><label>開放時間<input v-model="form.opensAt" type="datetime-local" /></label><label>截止時間<input v-model="form.dueAt" type="datetime-local" /></label><label><input type="checkbox" v-model="form.notifyStudents"/>發布時通知學生</label><button class="button primary" :disabled="busy">儲存作業草稿</button><button type="button" class="button secondary" @click="editing=false">取消</button></form></section>
  <template v-if="ready">
   <section v-if="!staff && !report.isPractice && ((!submissions.length && (report.submissionMode==='individual' || roster.some(r=>r.canSubmit))) || submissions.some(s=>s.canResubmit))" class="surface form-surface"><h2>繳交 PDF 報告</h2><form @submit.prevent="submit"><label>PDF 報告（最多 20 MB）<input type="file" accept=".pdf,application/pdf" required :disabled="busy || (report.status!=='published' && !submissions.some(s=>s.canResubmit))" @change="file=$event.target.files[0]" /></label><p class="field-hint">繳交後會保留版本。教師退回後可以重新繳交。</p><button class="button primary" :disabled="busy || !file || (report.status!=='published' && !submissions.some(s=>s.canResubmit))">繳交 PDF 報告</button></form></section>
   <section v-if="roster.length&&!groupReview" class="surface form-surface"><h2>{{report.submissionMode==='group'?'指定小組繳交總覽':'繳交總覽'}}</h2><p>已繳交 {{submittedCount}} / {{roster.length}} {{report.submissionMode==='group'?'組':'人'}} · 未繳交 {{roster.length-submittedCount}} {{report.submissionMode==='group'?'組':'人'}}</p><p v-if="staff" class="field-hint">未繳交者列入總覽；收到正式繳交後才可閱讀檔案與評分。</p>
    <div class="table-scroll"><table class="report-roster-table"><thead><tr><th scope="col">{{report.submissionMode==='group'?'小組':'學生'}}</th><th scope="col">繳交狀態</th></tr></thead><tbody><tr v-for="(r,i) in roster" :key="i"><th scope="row">{{r.name}}<span v-if="r.dissolved" class="muted">（小組已解散）</span></th><td><span class="status-badge">{{submissionStatus(r)}}</span></td></tr></tbody></table></div>
   </section>
   <section class="surface form-surface"><h2><FilePenLine v-if="staff" :size="20" />{{staff?'作業評閱與給分':'我的繳交與成績'}}</h2>
    <div v-if="canGrade" class="report-publication"><p class="field-hint">先逐{{groupReview?'組':'人'}}儲存評分草稿，最後統一本作業全班發布。未繳交者不會產生繳交或成績。</p><button type="button" data-testid="publish-all" class="button primary" :disabled="!canPublishAll" @click="publishAll">全班發布成績</button><p v-if="!publicationComplete" class="field-hint">尚有未繳交、退回或未評分的{{groupReview?'小組':'學生'}}，完成全部評分後才能發布。</p><p v-else-if="gradeDirty" class="field-hint">請先儲存所有未儲存的評分變更。</p></div>
    <div :class="{'report-review-layout':groupReview}">
    <aside v-if="groupReview" class="report-review-sidebar" aria-label="指定小組繳交名單"><p class="muted">已繳交 {{submittedCount}} / {{roster.length}} 組 · 未繳交 {{roster.length-submittedCount}} 組</p><section v-for="submitted in [true,false]" :key="String(submitted)"><h3>{{submitted?'已繳交':'未繳交'}} {{groupRows.filter(row=>row.submitted===submitted).length}} 組</h3><button v-for="row in groupRows.filter(row=>row.submitted===submitted)" :key="row.key" type="button" class="report-group-select" :class="{active:selectedGroupId===row.key}" :aria-pressed="selectedGroupId===row.key" @click="selectedGroupId=row.key"><strong>{{row.name}}<span v-if="row.dissolved">（小組已解散）</span></strong><span>{{submissionStatus(row)}}</span></button></section></aside>
    <div class="report-review-content">
    <nav v-if="groupReview&&selectedGroup" class="report-review-navigation" aria-label="切換小組"><button type="button" class="button secondary" :disabled="selectedIndex<=0" @click="turnGroup(-1)">上一組</button><span>{{selectedIndex+1}} / {{groupRows.length}}</span><button type="button" class="button secondary" :disabled="selectedIndex>=groupRows.length-1" @click="turnGroup(1)">下一組</button></nav>
    <section v-if="groupReview&&selectedGroup&&!visibleSubmissions.length" class="report-missing-submission" aria-label="唯讀繳交狀態"><h3>{{selectedGroup.name}}</h3><p v-if="!selectedGroup.submitted" class="submission-notice">此小組尚未繳交 PDF。目前僅顯示唯讀狀態，收到正式繳交後才能閱讀及評分。</p><p v-else class="submission-notice">{{canGrade?'無法取得此組的繳交紀錄，請重新載入作業。':'您沒有此課程的評分權限。'}}</p><p class="muted">作業說明：{{report.instructions}}</p></section>
    <article v-for="s in visibleSubmissions" :key="s.id" class="question-editor">
     <h3>{{s.studentNumber}} {{s.studentName}}</h3><p v-if="s.members.length">繳交時組員：{{s.members.map(m=>m.name+(m.isLeader?'（組長）':'')).join('、')}}</p>
     <p v-for="h in s.history" :key="h.revision">第 {{h.revision}} 版 · {{h.submittedBy}} · {{h.fileName}} · {{formatDate(h.submittedAt)}} <button class="button secondary" :disabled="busy" @click="download(s,h)">下載第 {{h.revision}} 版</button></p>
     <p>第 {{s.revision}} 版 · {{s.submittedBy}} 上傳並繳交 · {{s.returned?'退回修改':'正式繳交'}} · {{s.fileName}} · {{formatDate(s.submittedAt)}}</p>
     <FilePreview :endpoint="`${base}/submissions/${s.id}/preview`" :file-name="s.fileName" content-type="application/pdf" /><button class="button secondary" :disabled="busy" @click="download(s)">下載／閱讀 PDF</button>
     <ReportEvaluation v-if="!staff&&report.evaluationMode!=='off'&&s.members.length" :base="base" :submission="s" :report="report" :staff="false"/>
     <details v-if="canGrade&&report.evaluationMode!=='off'&&s.members.length" class="report-feedback-details" @toggle="feedbackOpen[s.id]=$event.target.open"><summary>組員貢獻回饋{{report.evaluationMode==='consensus'?'與同分意願':''}}（評分參考）</summary><ReportEvaluation v-if="feedbackOpen[s.id]" :base="base" :submission="s" :report="report" :staff="true"/></details>
     <button v-if="canGrade && !s.returned && s.publishedScore==null" class="button secondary" :disabled="busy" @click="returnFile(s)">退回修改</button>
     <form v-if="!s.returned && canGrade" class="report-grading" @submit.prevent="grade(s)">
      <h4>教師評分{{changed(s)?' · 有未儲存變更':''}}</h4>
      <label>{{s.members.length?'小組基礎成績':'作業得分'}}<input v-model.number="s.score" :aria-label="`${s.studentName} ${s.members.length?'小組基礎成績':'作業得分'}`" type="number" min="0" :max="report.maxScore" step="0.01" required /></label>
      <p v-if="s.members.length" class="field-hint">成績分配：{{s.members.some(hasOverride)?'含個別調整':'全組同分'}}。未填個別成績者採小組基礎成績；個別成績是最終分數，不是加減分。</p>
      <details v-if="s.members.length" class="report-adjustments" :open="adjustmentOpen[s.id]??s.members.some(hasOverride)" @toggle="adjustmentOpen[s.id]=$event.target.open"><summary>個別成績調整（選用） · {{s.members.some(hasOverride)?`已有 ${s.members.filter(hasOverride).length} 位個別調整`:'全組同分'}}</summary><p class="field-hint">預設全組採小組基礎成績。已有的個別調整會保留，調整或取消時須填寫原因。</p><fieldset v-for="m in s.members" :key="m.id" class="report-member-grade"><legend>{{m.name}}{{m.isLeader?'（組長）':''}}</legend><p><strong>個人成績草稿：{{finalDraft(s,m)}}</strong> / {{report.maxScore}} · {{hasOverride(m)?'個別調整':'採小組基礎成績'}}</p>
       <label>個別成績（留白採小組基礎成績）<input type="number" :aria-label="`${m.name} 個別成績`" min="0" :max="report.maxScore" step="0.01" v-model.number="m.score"></label><label>調整原因<input v-model="m.reason" :aria-label="`${m.name} 調整原因`" :required="reasonRequired(s,m)" maxlength="500"></label><p class="field-hint">設定或取消個別調整都須說明原因。原因供教學端留存；學生看到自己的已發布成績。</p><p>此組員已發布成績：{{m.publishedScore??'未發布'}}</p>
      </fieldset></details>
      <label>教師評語（隨成績發布）<textarea v-model="s.feedback" maxlength="16000" rows="3"></textarea></label><div class="studio-actions"><button class="button primary" :disabled="busy">儲存評分草稿</button></div><p class="field-hint">草稿儲存後，使用上方「全班發布成績」統一發布本作業分數與評語。</p><p>{{s.members.length?'已發布小組基礎成績':'已發布得分'}}：{{s.publishedScore??'未發布'}}</p>
     </form>
     <div v-else><p v-if="s.returned" class="submission-notice">此版本已退回修改，重新繳交前無法評分。</p><p>{{staff?'評分草稿':'我的已發布成績'}}：{{s.score??(staff?'待評分':'尚未發布')}} / {{report.maxScore}}</p><p class="studio-prose">{{s.feedback}}</p></div>
    </article>
    <p v-if="!submissions.length&&(!groupReview||!selectedGroup)" class="empty-inline">{{staff&&!canGrade?'您沒有此課程的評分權限。':'目前沒有可見的繳交紀錄。'}}</p>
    </div></div>
   </section>
  </template>
  <RouterLink :to="{path:`/courses/${report.courseId}`,query:{section:'grades'}}" class="button secondary">查看課程成績</RouterLink>
 </template>
</template>
<style scoped>
.report-publication{padding:12px 0 18px;border-bottom:1px solid var(--line);margin-bottom:18px}
.report-review-layout{display:grid;grid-template-columns:minmax(180px,260px) minmax(0,1fr);gap:22px;align-items:start}
.report-review-sidebar{border-right:1px solid var(--line);padding-right:16px;max-height:75vh;overflow:auto}
.report-review-sidebar h3{font-size:14px;margin:18px 0 8px}
.report-group-select{display:flex;flex-direction:column;gap:5px;width:100%;padding:12px;text-align:left;border:1px solid var(--line);border-radius:8px;background:var(--surface);color:var(--text);margin:6px 0;cursor:pointer}
.report-group-select span{font-size:12px;color:var(--muted)}
.report-group-select.active{border-color:var(--accent);background:var(--accent-soft)}
.report-review-content{min-width:0}.report-review-navigation{display:flex;justify-content:space-between;align-items:center;gap:8px;margin-bottom:16px}
.report-review-content .question-editor{margin-top:0}.report-adjustments,.report-feedback-details{border:1px solid var(--line);border-radius:8px;padding:12px;margin:14px 0}
summary{cursor:pointer;font-weight:600}.report-missing-submission{padding:12px 0}
@media(max-width:760px){.report-review-layout{grid-template-columns:1fr}.report-review-sidebar{border-right:0;border-bottom:1px solid var(--line);padding:0 0 12px;max-height:260px}.report-review-navigation{flex-wrap:wrap}}
</style>
