<script setup>
import {computed,onMounted,onBeforeUnmount,ref,watch} from 'vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
import {api} from '../api'
import {experience} from '../composables/experience'
import {filterGradeItems,formatGrade as percent,gradeCategories as categories,gradeItems,gradeSummary,scoreStatus} from '../utils/gradebookView'
const props=defineProps({course:Object})
const practice=computed(()=>props.course.canPractice && experience.student)
const base=`/courses/${props.course.id}/gradebook`
const data=ref(null),curriculum=ref(null),unitError=ref(''),error=ref(''),notice=ref(''),busy=ref(false),selected=ref(''),drafts=ref({}),draftVersions=ref({}),form=ref(null)
const view=ref('overview'),unitFilter=ref(''),typeFilter=ref(''),activityFilter=ref('')
const rows=computed(()=>gradeItems(data.value?.items||[],curriculum.value))
const activityOptions=computed(()=>filterGradeItems(rows.value,{unit:unitFilter.value,type:typeFilter.value}))
const filtered=computed(()=>filterGradeItems(activityOptions.value,{activity:activityFilter.value}))
const summaries=computed(()=>data.value?.students.map(student=>({...student,...gradeSummary(filtered.value,data.value.scores,student.id)}))||[])
const detailPage=ref(1),pageSize=ref(25)
const detailRows=computed(()=>data.value?.students.flatMap(student=>filtered.value.map(item=>({student,item,score:scoreFor(item.id,student.id)})))||[])
const detailPageCount=computed(()=>Math.max(1,Math.ceil(detailRows.value.length/pageSize.value)))
const visibleDetailRows=computed(()=>detailRows.value.slice((detailPage.value-1)*pageSize.value,detailPage.value*pageSize.value))
watch([filtered,pageSize,()=>data.value?.students],()=>{detailPage.value=1})
watch([unitFilter,typeFilter],()=>{if(!activityOptions.value.some(item=>item.id===activityFilter.value))activityFilter.value=''})
const item=computed(()=>data.value?.items.find(x=>x.id===selected.value))
const linked=computed(()=>!!(item.value?.sourceAssignmentId || item.value?.sourceReportId))
const paperExam=computed(()=>!linked.value&&['midterm','final'].includes(item.value?.category))
const dirty=computed(()=>Object.keys(drafts.value).length>0 || !!form.value)
const writable=computed(()=>data.value?.staff && !props.course.taRestrictions?.includes('grading') && props.course.status!=='archived')
function scoreFor(itemId,studentId){return data.value?.scores.find(x=>x.itemId===itemId&&(!studentId||x.studentId===studentId))}
function entry(studentId){return scoreFor(selected.value,studentId)}
function value(studentId){return studentId in drafts.value?drafts.value[studentId]:entry(studentId)?.score??''}
function editScore(studentId,text){const old=entry(studentId)?.score;const next=text===''?'':Number(text);if(next===(old??'')){delete drafts.value[studentId];delete draftVersions.value[studentId]}else{if(!(studentId in drafts.value))draftVersions.value[studentId]=entry(studentId)?.version??0;drafts.value[studentId]=next}}
async function load(){
 const results=await Promise.allSettled([api(base),api(`/courses/${props.course.id}/curriculum`)])
 if(results[0].status==='rejected')throw results[0].reason
 data.value=results[0].value
 curriculum.value=results[1].status==='fulfilled'?results[1].value:null
 unitError.value=results[1].status==='rejected'?'單元資料載入失敗，仍可查看成績；請重新載入以使用完整單元篩選。':''
 if(!data.value.items.some(x=>x.id===selected.value))selected.value=data.value.items[0]?.id || ''
}
async function run(action){busy.value=true;error.value='';notice.value='';try{await action()}catch(e){error.value=e.message}finally{busy.value=false}}
function choose(event){if(Object.keys(drafts.value).length && !confirm('此項目有尚未儲存的得分，確定放棄並切換？')){event.target.value=selected.value;return}drafts.value={};draftVersions.value={};selected.value=event.target.value}
function edit(item=null){if(item?.sourceAssignmentId||item?.sourceReportId)return;if(form.value && !confirm('確定放棄目前項目編輯？'))return;form.value=item?{id:item.id,title:item.title,category:item.category,maxScore:item.maxScore,expectedVersion:item.version,...(item.scheduledAt?{scheduledAt:item.scheduledAt}:{})}:{title:'',category:'offline_assignment',maxScore:100}}
async function saveItem(){await run(async()=>{await api(`${base}/items${form.value.id?'/'+form.value.id:''}`,{method:form.value.id?'PUT':'POST',body:form.value});form.value=null;await load();notice.value='評分項目已儲存，等比平均已重新計算。'})}
async function saveScore(student,publish=false){await run(async()=>{const score=value(student.id);await api(`${base}/items/${selected.value}/score`,{method:'PUT',body:{publish,studentId:student.id,score:score===''?null:score,expectedVersion:draftVersions.value[student.id]??entry(student.id)?.version??0}});delete drafts.value[student.id];delete draftVersions.value[student.id];await load();notice.value='得分已儲存，平均分數已更新。'})}
const adjustment=ref(0),adjustStudent=ref('')
function adjust(){return run(async()=>{await api(base+'/adjustment',{method:'PUT',body:{studentId:adjustStudent.value||null,adjustment:adjustment.value}});await load()})}
function publish(){if(confirm('發布所有有效學員的課程總成績？總分採已發布項目與調分。'))return run(async()=>{await api(base+'/publish-finals',{method:'POST'});await load()})}
function finalFor(id){return data.value.finals?.find(f=>f.studentId===id)}
function estimated(student){const avg=data.value.averages.find(a=>a.studentId===student.id)?.average;return avg==null?'—':(avg+data.value.classAdjustment+(finalFor(student.id)?.adjustment||0)).toFixed(2)}
function activityUrl(row){if(row.sourceReportId)return `/courses/${props.course.id}/reports/${row.sourceReportId}`;if(!row.sourceAssignmentId)return null;const activity=curriculum.value?.activities?.find(activity=>activity.id===row.sourceAssignmentId&&['quiz','assignment'].includes(activity.kind));return `/courses/${props.course.id}/${activity?.kind==='quiz'?'quizzes':'assignments'}/${row.sourceAssignmentId}`}
function exportGrades(){
 if(!data.value?.staff)return
 const header=['學號','姓名',...filtered.value.flatMap(item=>[`${item.title}（滿分 ${item.maxScore}）`,'發布狀態']),'個人調分','暫算總分','已發布總分']
 const records=data.value.students.map(student=>[student.studentNumber||'',student.displayName,...filtered.value.flatMap(item=>{const score=scoreFor(item.id,student.id);return [score?.score??'',scoreStatus(score,true)]}),finalFor(student.id)?.adjustment||0,estimated(student),finalFor(student.id)?.publishedTotal??''])
 const cell=value=>'"'+String(value).replace(/^[=+@\-]/,"'$&").replaceAll('"','""')+'"'
 const blob=new Blob(['\ufeff'+[header,...records].map(row=>row.map(cell).join(',')).join('\r\n')],{type:'text/csv;charset=utf-8'})
 const url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download='course-grades.csv';link.click();URL.revokeObjectURL(url)
}
function guard(){return !dirty.value || confirm('評分管理有尚未儲存的修改，確定離開？')}
onBeforeRouteLeave(guard)
onBeforeRouteUpdate((to,from)=>to.fullPath===from.fullPath || guard())
function unload(event){if(dirty.value){event.preventDefault();event.returnValue=''}}
window.addEventListener('beforeunload',unload)
onBeforeUnmount(()=>window.removeEventListener('beforeunload',unload))
onMounted(()=>{if(!practice.value)run(load)})
</script>

<template>
 <p v-if="practice" class="surface form-surface">學生視角預覽沒有個人成績。</p>
 <section v-else class="surface form-surface gradebook-panel">
  <div class="section-title"><div><h2>{{data?.staff?'課程成績':'我的成績'}}</h2><p class="muted">{{data?.staff?'先看全班摘要，再依單元與活動查看成績明細。':'查看自己的已發布成績，依單元與類型篩選。'}}</p></div><button class="button secondary" :disabled="busy||dirty" @click="run(load)">重新載入</button></div>
  <p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="notice" class="success-box" role="status">{{notice}}</p><p v-if="unitError" class="submission-notice" role="status">{{unitError}}</p><p v-if="busy&&!data" role="status">成績載入中…</p>
  <template v-if="data">
   <div class="gradebook-summary"><div v-if="data.staff"><span>全班等比平均（百分制）</span><strong>{{percent(data.classAverage)}}</strong><small>{{data.gradedStudentCount}} 位同學已有得分 · 全課程，不受篩選影響</small></div><div v-else><span>已發布課程總成績</span><strong>{{data.finals?.[0]?.publishedTotal==null?'尚未發布':percent(data.finals[0].publishedTotal)}}</strong></div><div><span>評分項目</span><strong>{{data.items.length}} 項</strong><small>{{filtered.length}} 項符合目前篩選</small></div></div>
   <div class="grade-toolbar"><div v-if="data.staff" class="grade-view-switch" role="group" aria-label="成績檢視"><button data-view="overview" class="button secondary" :class="{active:view==='overview'}" :aria-pressed="view==='overview'" @click="view='overview'">成績總覽</button><button data-view="details" class="button secondary" :class="{active:view==='details'}" :aria-pressed="view==='details'" @click="view='details'">成績明細</button></div><button v-if="data.staff" class="button secondary" :disabled="busy" @click="exportGrades">匯出篩選成績 CSV</button></div>
   <div class="grade-filters"><label>單元<select v-model="unitFilter" aria-label="成績單元"><option value="">全部單元／課程</option><option v-for="unit in curriculum?.units||[]" :key="unit.id" :value="unit.id">{{unit.name}}</option><option v-if="rows.some(row=>row.unitKey==='unassigned')" value="unassigned">未歸類</option><option v-if="rows.some(row=>row.unitKey==='course')" value="course">課程層級</option><option v-if="rows.some(row=>row.unitKey==='unknown')" value="unknown">單元資料未取得</option></select></label><label>類型<select v-model="typeFilter" aria-label="成績類型"><option value="">全部類型</option><option v-for="(label,key) in categories" :key="key" :value="key">{{label}}</option></select></label><label v-if="data.staff">活動<select v-model="activityFilter" aria-label="成績活動"><option value="">全部活動</option><option v-for="row in activityOptions" :key="row.id" :value="row.id">{{row.title}}</option></select></label></div>
   <template v-if="data.staff">
    <div v-if="view==='overview'" class="table-scroll"><table class="data-table grade-overview" data-testid="grade-overview"><caption class="grade-caption">全班成績摘要 · 目前篩選範圍，百分制</caption><thead><tr><th scope="col">學號／姓名</th><th scope="col">作業平均</th><th scope="col">測驗平均</th><th scope="col">平時平均</th><th scope="col">正式考試摘要</th></tr></thead><tbody><tr v-for="student in summaries" :key="student.id"><th scope="row">{{student.studentNumber||'未填學號'}} {{student.displayName}}</th><td>{{percent(student.assignment)}}</td><td>{{percent(student.quiz)}}</td><td>{{percent(student.regular)}}</td><td>{{percent(student.formal)}}</td></tr><tr v-if="!summaries.length"><td colspan="5" class="empty-inline">尚無有效學生。</td></tr></tbody></table></div>
    <template v-else>
     <div class="table-scroll"><table class="data-table" data-testid="grade-details"><caption class="grade-caption">全班成績明細 · {{detailRows.length}} 筆，每列為一位學生的一項活動</caption><thead><tr><th scope="col">學號／姓名</th><th scope="col">單元</th><th scope="col">活動</th><th scope="col">類型</th><th scope="col">繳交狀態</th><th scope="col">滿分</th><th scope="col">得分</th><th scope="col">評分狀態</th></tr></thead><tbody><tr v-for="row in visibleDetailRows" :key="`${row.student.id}:${row.item.id}`"><th scope="row">{{row.student.studentNumber||'未填學號'}} {{row.student.displayName}}</th><td>{{row.item.unitName}}</td><td><RouterLink v-if="activityUrl(row.item)" :to="activityUrl(row.item)">{{row.item.title}}</RouterLink><span v-else>{{row.item.title}}</span></td><td>{{categories[row.item.category]}}</td><td><RouterLink v-if="activityUrl(row.item)" :to="activityUrl(row.item)">前往原評量查看</RouterLink><span v-else>不適用（手動登分）</span></td><td>{{row.item.maxScore}}</td><td>{{row.score?.score??'—'}}</td><td>{{scoreStatus(row.score,true)}}</td></tr><tr v-if="!detailRows.length"><td colspan="8" class="empty-inline">沒有符合篩選的成績明細。</td></tr></tbody></table></div>
     <div class="grade-pagination"><label>每頁筆數<select v-model.number="pageSize" aria-label="每頁成績筆數"><option :value="25">25 筆</option><option :value="50">50 筆</option></select></label><span role="status">第 {{detailPage}}／{{detailPageCount}} 頁 · 共 {{detailRows.length}} 筆</span><button class="button secondary" aria-label="上一頁成績" :disabled="detailPage<=1" @click="detailPage--">上一頁</button><button class="button secondary" aria-label="下一頁成績" :disabled="detailPage>=detailPageCount" @click="detailPage++">下一頁</button></div>
    </template>
    <p v-if="!filtered.length" class="empty-inline">沒有符合篩選的評分項目。</p>
    <p class="field-hint">作業平均含 PDF、線上與線下作業；測驗平均含小考；平時平均含上述項目及其他平時評量，不含期中／期末。各項先換算百分制，再取等比平均；未登錄不計入，0 分計入。摘要可含未發布草稿，無成績以「—」顯示。</p>
    <p class="field-hint">正式考試摘要包含紙本期中／期末，與 <RouterLink :to="`/courses/${course.id}?section=assessments`">正式考試全班登分</RouterLink> 使用同一筆成績。既有線上正式考試保留於該頁收合區。</p>
    <details class="grade-management"><summary>評分管理與課程總成績</summary><div class="grade-management-content">
     <div class="section-title"><div><h3>課程總成績</h3><p class="field-hint">以下均為全課程資料，不受上方篩選影響。暫算總分採已登錄平均與調分；發布總分只採已發布項目與調分。</p></div><button v-if="writable" class="button primary" :disabled="busy" @click="edit()">新增評分項目</button></div>
     <div class="table-scroll"><table class="data-table"><thead><tr><th>學號／姓名</th><th>個人調分</th><th>暫算總分</th><th>已發布總分</th></tr></thead><tbody><tr v-for="student in data.students" :key="student.id"><th>{{student.studentNumber}} {{student.displayName}}</th><td>{{finalFor(student.id)?.adjustment||0}}</td><td>{{estimated(student)}}</td><td>{{finalFor(student.id)?.publishedTotal??'尚未發布'}}</td></tr></tbody></table></div>
     <p>全班調分 {{data.classAdjustment}} 分。</p><form v-if="data.canPublishFinal&&writable" class="grade-adjustment" @submit.prevent="adjust"><label>調整對象<select v-model="adjustStudent"><option value="">全班</option><option v-for="student in data.students" :key="student.id" :value="student.id">{{student.studentNumber}} {{student.displayName}}</option></select></label><label>調分值（取代原調分）<input v-model.number="adjustment" type="number" step=".01" min="-99999" max="99999" required/></label><button class="button secondary" :disabled="busy">儲存調分</button><button type="button" class="button primary" :disabled="busy||dirty" @click="publish">發布課程總成績</button></form>
     <form v-if="form&&writable" class="question-editor" @submit.prevent="saveItem"><h3>{{form.id?'編輯評分項目':'新增評分項目'}}</h3><fieldset :disabled="busy"><div class="form-grid"><label>項目名稱<input :readonly="!!(form.sourceAssignmentId||form.sourceReportId)" v-model="form.title" required maxlength="200"/></label><label>評量類型<select :disabled="!!(form.sourceAssignmentId||form.sourceReportId)" v-model="form.category"><option v-for="(label,key) in categories" :key="key" :value="key">{{label}}</option></select></label><label>滿分<input :readonly="!!(form.sourceAssignmentId||form.sourceReportId)" v-model.number="form.maxScore" type="number" min="0.01" max="999999" step="0.01" required/></label></div><p class="field-hint">各項成績換算百分制後取等比平均；修改滿分會重新計算平均。連動項目的設定與給分請前往原評量。</p><button class="button primary">儲存項目</button> <button type="button" class="button secondary" @click="form=null">取消</button></fieldset></form>
     <template v-if="data.items.length"><h3>評分項目</h3><div class="table-scroll"><table class="data-table"><thead><tr><th>項目</th><th>類型</th><th>滿分</th><th>全班平均（百分制）</th><th v-if="writable">操作</th></tr></thead><tbody><tr v-for="row in data.items" :key="row.id"><td>{{row.title}}</td><td>{{categories[row.category]}}</td><td>{{row.maxScore}}</td><td>{{percent(data.itemAverages.find(average=>average.itemId===row.id)?.average)}}</td><td v-if="writable"><button v-if="!row.sourceAssignmentId&&!row.sourceReportId" class="text-button" :disabled="busy" @click="edit(row)">編輯項目</button><RouterLink v-else :to="activityUrl(row)">前往原評量</RouterLink></td></tr></tbody></table></div>
      <label>選擇評分項目<select aria-label="選擇評分項目" :value="selected" :disabled="busy" @change="choose"><option v-for="row in data.items" :key="row.id" :value="row.id">{{row.title}}（滿分 {{row.maxScore}}）</option></select></label><div v-if="linked" class="submission-notice">此項目由原評量連動。<RouterLink :to="activityUrl(item)">前往評閱與給分 →</RouterLink></div><p v-else class="field-hint">空白表示未登錄；若需計為零分，請輸入 0。每位同學分別儲存，其他未儲存輸入會保留。<RouterLink v-if="paperExam" :to="`/courses/${course.id}?section=assessments`">紙本考試須至正式考試頁全班一次發布 →</RouterLink></p>
      <div class="table-scroll"><table class="data-table"><thead><tr><th>學號</th><th>姓名</th><th>得分</th><th>個人等比平均（百分制）</th><th v-if="writable&&!linked">操作</th></tr></thead><tbody><tr v-for="student in data.students" :key="student.id"><td>{{student.studentNumber||'未填學號'}}</td><td>{{student.displayName}}</td><td><input v-if="writable&&!linked" type="number" min="0" :max="item?.maxScore" step="0.01" :aria-label="`${student.displayName} 得分`" :value="value(student.id)" :disabled="busy" @input="editScore(student.id,$event.target.value)"/><span v-else>{{entry(student.id)?.score??'未登錄'}}</span></td><td>{{percent(data.averages.find(average=>average.studentId===student.id)?.average)}}</td><td v-if="writable&&!linked"><button class="button secondary" :disabled="busy||!(student.id in drafts)" @click="saveScore(student)">儲存得分草稿</button> <button v-if="!paperExam" class="button secondary" :disabled="busy" @click="saveScore(student,true)">發布得分</button></td></tr></tbody></table></div>
     </template><p v-else class="empty-inline">尚無評分項目。</p>
    </div></details>
   </template>
   <template v-else>
    <div class="table-scroll"><table class="data-table my-grades" data-testid="my-grades"><caption class="grade-caption">我的成績</caption><thead><tr><th scope="col">單元</th><th scope="col">項目</th><th scope="col">類型</th><th scope="col">滿分</th><th scope="col">得分</th><th scope="col">狀態</th></tr></thead><tbody><tr v-for="row in filtered" :key="row.id"><td data-label="單元">{{row.unitName}}</td><th scope="row" data-label="項目"><RouterLink v-if="activityUrl(row)" :to="activityUrl(row)">{{row.title}}</RouterLink><span v-else>{{row.title}}</span></th><td data-label="類型">{{categories[row.category]}}</td><td data-label="滿分">{{row.maxScore}}</td><td data-label="得分">{{scoreFor(row.id)?.score??'—'}}</td><td data-label="狀態">{{scoreStatus(scoreFor(row.id),false)}}</td></tr><tr v-if="!filtered.length"><td colspan="6" class="empty-inline">{{data.items.length?'沒有符合篩選的成績項目。':'尚無評分項目。'}}</td></tr></tbody></table></div>
    <p class="field-hint">僅顯示已發布得分；0 分為實際得分。「未發布」表示尚無可見分數，不代表未繳交。各項與課程總成績分別發布。</p><p class="field-hint">紙本期中／期末與此表使用同一筆成績；既有線上考試成績請至 <RouterLink :to="`/courses/${course.id}?section=assessments`">正式考試</RouterLink> 的線上考試區查看。</p>
   </template>
  </template>
 </section>
</template>

<style scoped>
@media(max-width:650px){.my-grades{display:block;min-width:0;width:100%}.my-grades caption{display:block}.my-grades thead{position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0}.my-grades tbody{display:grid;gap:12px}.my-grades tbody tr{display:grid;gap:10px;border:1px solid var(--line);border-radius:10px;padding:16px}.my-grades tbody td,.my-grades tbody th{display:grid;grid-template-columns:58px minmax(0,1fr);gap:12px;border:0;padding:0;min-width:0;white-space:normal;overflow-wrap:anywhere}.my-grades tbody td::before,.my-grades tbody th::before{content:attr(data-label);font-size:.85rem;font-weight:400;color:var(--muted)}.my-grades td[data-label="得分"]{font-size:1.1rem;font-weight:700;color:var(--accent)}.my-grades tbody td[colspan]{display:block}.my-grades tbody td[colspan]::before{content:none}.my-grades a{min-width:0}}
.grade-pagination{display:flex;align-items:center;justify-content:flex-end;flex-wrap:wrap;gap:12px;margin-top:14px}.grade-pagination label{display:flex;align-items:center;gap:8px}.grade-pagination select{width:auto}
.grade-toolbar,.grade-view-switch,.grade-adjustment{display:flex;align-items:center;flex-wrap:wrap;gap:10px}.grade-toolbar{justify-content:space-between;margin-bottom:18px}.grade-view-switch .active{background:var(--accent);color:#fff;border-color:var(--accent)}.grade-filters{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:14px;margin-bottom:20px}.grade-filters label{min-width:0}.grade-filters select{width:100%;min-width:0}.grade-caption{text-align:left;font-weight:600;margin-bottom:12px;color:var(--muted)}.grade-overview{width:100%;min-width:620px}.grade-item-unit,.grade-cell-status{display:block;color:var(--muted);font-size:.78rem;font-weight:400;white-space:nowrap}.grade-cell-status{margin-top:4px}.grade-management{margin-top:26px;border-top:1px solid var(--line);padding-top:18px}.grade-management>summary{cursor:pointer;font-weight:600;padding:6px 0}.grade-management-content{display:grid;gap:18px;padding-top:20px}.grade-adjustment{align-items:end}.grade-adjustment label{min-width:160px;flex:1}.grade-management-content h3{margin:0}.gradebook-panel{min-width:0}.gradebook-panel .table-scroll{max-width:100%}@media(max-width:650px){.grade-toolbar{align-items:stretch}.grade-toolbar>button{width:100%}.grade-filters{grid-template-columns:1fr}.grade-adjustment{align-items:stretch;flex-direction:column}}
</style>
