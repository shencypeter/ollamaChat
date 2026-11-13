<script setup>
import {computed,onBeforeUnmount,onMounted,ref} from 'vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate,useRoute} from 'vue-router'
import {api} from '../api'
import {experience} from '../composables/experience'
import {formatDate,toLocalInput,toUtc} from '../utils/assignments'
const props=defineProps({course:{type:Object,required:true}})
const route=useRoute()
const base=`/courses/${props.course.id}/gradebook`
const data=ref(null),selected=ref(''),drafts=ref({}),form=ref(null),busy=ref(false),error=ref(''),notice=ref('')
const practice=computed(()=>props.course.canPractice&&experience.student)
const exams=computed(()=>(data.value?.items||[]).filter(item=>['midterm','final'].includes(item.category)&&!item.sourceAssignmentId&&!item.sourceReportId))
const item=computed(()=>exams.value.find(item=>item.id===selected.value))
const writable=computed(()=>data.value?.staff&&!props.course.taRestrictions?.includes('grading')&&props.course.status!=='archived')
const dirty=computed(()=>Object.keys(drafts.value).length>0||!!form.value)
const students=computed(()=>data.value?.students||[])
function entry(studentId,itemId=selected.value){return data.value?.scores.find(score=>score.itemId===itemId&&score.studentId===studentId)}
function value(id){return id in drafts.value?drafts.value[id]:entry(id)?.score??''}
function editScore(id,text){const next=text===''?'':Number(text);if(next===(entry(id)?.score??''))delete drafts.value[id];else drafts.value[id]=next}
const recorded=computed(()=>students.value.filter(student=>entry(student.id)?.score!=null).length)
const complete=computed(()=>students.value.length>0&&recorded.value===students.value.length)
function status(score){if(score?.publishedScore==null)return score?.score==null?'未登錄':'草稿・未發布';return score.score===score.publishedScore?'已發布':'已發布・另有草稿'}
function guard(){return !dirty.value||confirm('正式考試有尚未儲存的修改，確定放棄？')}
async function run(action){busy.value=true;error.value='';notice.value='';try{await action()}catch(e){error.value=e.message}finally{busy.value=false}}
async function load(){data.value=await api(base);if(!selected.value&&exams.value.some(exam=>exam.id===route.query.exam))selected.value=route.query.exam;if(!exams.value.some(exam=>exam.id===selected.value))selected.value=exams.value[0]?.id||''}
function reload(){if(guard())return run(async()=>{await load();drafts.value={};form.value=null})}
function choose(id){if(id===selected.value||!guard())return;selected.value=id;drafts.value={};form.value=null}
function edit(exam=null){if(!guard())return;drafts.value={};form.value=exam?{id:exam.id,title:exam.title,category:exam.category,maxScore:exam.maxScore,scheduledAt:toLocalInput(exam.scheduledAt),expectedVersion:exam.version}:{title:'',category:'midterm',maxScore:100,scheduledAt:'',expectedVersion:0}}
function saveItem(){return run(async()=>{const{id,...input}=form.value;const result=await api(`${base}/items${id?'/'+id:''}`,{method:id?'PUT':'POST',body:{...input,scheduledAt:toUtc(input.scheduledAt)}});form.value=null;if(result?.id)selected.value=result.id;await load();notice.value='考試設定已儲存。'})}
function versions(withScores){return {expectedItemVersion:item.value.version,scores:students.value.map(student=>({studentId:student.id,...(withScores?{score:value(student.id)===''?null:value(student.id)}:{}),expectedVersion:entry(student.id)?.version??0}))}}
function saveScores(){return run(async()=>{await api(`${base}/items/${selected.value}/scores`,{method:'PUT',body:versions(true)});drafts.value={};await load();notice.value='全班得分草稿已儲存，學生尚看不到新草稿。'})}
function publish(){if(!complete.value||dirty.value||!confirm(`發布「${item.value.title}」全班 ${students.value.length} 位學生的成績？`))return;return run(async()=>{await api(`${base}/items/${selected.value}/publish-scores`,{method:'POST',body:versions(false)});await load();notice.value='全班考試成績已一次發布。'})}
function unload(event){if(dirty.value){event.preventDefault();event.returnValue=''}}
onBeforeRouteLeave(guard);onBeforeRouteUpdate(to=>{if(!guard())return false;if(exams.value.some(exam=>exam.id===to.query.exam)){selected.value=to.query.exam;drafts.value={};form.value=null}})
onMounted(()=>{window.addEventListener('beforeunload',unload);if(!practice.value)run(load)})
onBeforeUnmount(()=>window.removeEventListener('beforeunload',unload))
</script>

<template>
 <section class="surface form-surface offline-exams">
  <div class="section-title"><div><h2>紙本期中／期末考</h2><p class="muted">考後依全班名冊登分，與課程成績簿使用同一筆評分項目。</p></div><div class="exam-actions"><button v-if="writable" data-action="create" class="button primary" :disabled="busy" @click="edit()">新增紙本考試</button><button v-if="!practice" class="button secondary" :disabled="busy" @click="reload">重新載入</button></div></div>
  <p v-if="practice" class="submission-notice">學生視角預覽沒有個人成績。</p>
  <p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="notice" class="success-box" role="status">{{notice}}</p>
  <p v-if="busy&&!data" role="status">考試成績載入中…</p>
  <form v-if="form&&writable" class="question-editor" @submit.prevent="saveItem"><h3>{{form.id?'編輯紙本考試':'新增紙本考試'}}</h3><fieldset :disabled="busy"><div class="form-grid"><label>考試名稱<input v-model="form.title" aria-label="考試名稱" required maxlength="200"/></label><label>考試類型<select v-model="form.category" aria-label="考試類型"><option value="midterm">期中考</option><option value="final">期末考</option></select></label><label>滿分<input v-model.number="form.maxScore" aria-label="考試滿分" type="number" required min="0.01" max="999999" step="0.01"/></label><label>考試時間（選填）<input v-model="form.scheduledAt" type="datetime-local" aria-label="考試時間"/></label></div><div class="exam-actions"><button class="button primary">儲存考試設定</button><button type="button" class="button secondary" @click="form=null">取消</button></div></fieldset></form>
  <template v-if="data">
   <p v-if="!exams.length" class="empty-inline">尚無紙本期中／期末考試。</p>
   <div v-else class="exam-list" role="group" aria-label="紙本考試清單"><button v-for="exam in exams" :key="exam.id" class="button secondary" :class="{active:exam.id===selected}" :aria-pressed="exam.id===selected" :disabled="busy" @click="choose(exam.id)">{{exam.title}} · {{exam.category==='midterm'?'期中考':'期末考'}} · 滿分 {{exam.maxScore}}</button></div>
   <template v-if="item">
    <div class="section-title"><div><h3>{{item.title}}</h3><p class="field-hint">考試時間：{{formatDate(item.scheduledAt)}} · 滿分 {{item.maxScore}}</p></div><button v-if="writable" class="text-button" :disabled="busy" @click="edit(item)">編輯考試設定</button></div>
    <template v-if="data.staff">
     <p class="field-hint">已登錄 {{recorded}}／{{students.length}} 位有效學生。空白為未登錄，不計入平均；0 分是實際得分。全班登分並儲存後才能一次發布，學生只看見已發布得分。</p>
     <div class="table-scroll"><table class="data-table"><thead><tr><th scope="col">學號</th><th scope="col">姓名</th><th scope="col">草稿得分</th><th scope="col">已發布得分</th><th scope="col">狀態</th></tr></thead><tbody><tr v-for="student in students" :key="student.id"><td>{{student.studentNumber||'未填學號'}}</td><th scope="row">{{student.displayName}}</th><td><input v-if="writable" type="number" min="0" :max="item.maxScore" step="0.01" :value="value(student.id)" :aria-label="`${student.displayName} 得分`" :disabled="busy||!!form" @input="editScore(student.id,$event.target.value)"/><span v-else>{{entry(student.id)?.score??'未登錄'}}</span></td><td>{{entry(student.id)?.publishedScore??'尚未發布'}}</td><td>{{student.id in drafts?'尚未儲存':status(entry(student.id))}}</td></tr><tr v-if="!students.length"><td colspan="5">尚無有效學生。</td></tr></tbody></table></div>
     <div v-if="writable" class="exam-actions"><button class="button secondary" data-action="save" :disabled="busy||!!form||!Object.keys(drafts).length" @click="saveScores">儲存全班得分草稿</button><button class="button primary" data-action="publish" :disabled="busy||dirty||!complete" @click="publish">全班一次發布成績</button></div>
     <p v-if="writable&&!complete" class="field-hint">仍有學生未登錄得分，請完成後儲存；未登錄不會自動當成 0 分。</p>
    </template>
    <p v-else class="submission-notice">我的得分：<strong>{{entry(students[0]?.id)?.publishedScore??'尚未發布'}}</strong>／{{item.maxScore}} · {{entry(students[0]?.id)?.publishedScore==null?'尚未發布':'已發布'}}</p>
   </template>
  </template>
 </section>
</template>

<style scoped>
.offline-exams{min-width:0}.exam-actions,.exam-list{display:flex;flex-wrap:wrap;gap:10px}.exam-list{margin:20px 0}.exam-list .active{border-color:var(--accent);color:var(--accent)}.exam-actions{margin-top:12px}.offline-exams input[type=number]{width:110px}.offline-exams fieldset{border:0;padding:0;min-width:0}.offline-exams .section-title{flex-wrap:wrap}.offline-exams .form-grid{margin:14px 0}.offline-exams .form-grid input{max-width:100%}@media(max-width:650px){.exam-list{display:grid}.exam-list .button{white-space:normal;text-align:left}.exam-actions>.button{flex:1}.offline-exams .form-grid{grid-template-columns:1fr}}
</style>
