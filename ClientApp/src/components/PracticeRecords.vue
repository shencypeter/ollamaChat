<script setup>
import {ref,onMounted,computed,onBeforeUnmount} from 'vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
import {api} from '../api'
import FilePreview from './FilePreview.vue'
import QuizAnswerReview from './QuizAnswerReview.vue'
const props=defineProps({course:Object}),data=ref(null),error=ref(''),notice=ref(''),busy=ref(false),drafts=ref({})
const base=`/courses/${props.course.id}/practice`
const dirty=computed(()=>Object.keys(drafts.value).length>0)
async function load(){data.value=await api(base)}
async function save(row){busy.value=true;error.value='';try{const draft=drafts.value[row.id];await api(`${base}/${row.kind}/${row.id}/grade`,{method:'PUT',body:{score:row.quiz?row.quiz.objectiveScore+Object.values(draft.essayScores||{}).reduce((a,b)=>a+Number(b||0),0):draft.score===''?null:Number(draft.score),essayScores:draft.essayScores||{},feedback:draft.feedback,expectedVersion:draft.expectedVersion,expectedGradeId:draft.expectedGradeId}});delete drafts.value[row.id];await load();notice.value='練習成績已儲存，切換學生視角即可查看；正式成績不受影響。'}catch(e){error.value=e.message}finally{busy.value=false}}
function edit(row,key,value){drafts.value[row.id]={...(drafts.value[row.id]||{score:row.score??'',feedback:row.feedback,expectedVersion:row.version,expectedGradeId:row.expectedGradeId,essayScores:row.essayScores||{}}),[key]:value}}
function editEssay(row,id,value){edit(row,'essayScores',{...(drafts.value[row.id]?.essayScores||row.essayScores||{}),[id]:Number(value)})}
function guard(){return !dirty.value||confirm('練習評分有未儲存內容，確定離開？')}
onBeforeRouteLeave(guard);onBeforeRouteUpdate(guard)
function unload(event){if(dirty.value){event.preventDefault();event.returnValue=''}}
window.addEventListener('beforeunload',unload);onBeforeUnmount(()=>window.removeEventListener('beforeunload',unload))
function display(answer){if(!answer.questionType||answer.questionType==='essay')return answer.text;try{return JSON.parse(answer.text).map(i=>(answer.questionType==='true_false'?['是','非']:answer.options)[i]).join('、')}catch{return answer.text}}
onMounted(async()=>{try{await load()}catch(e){error.value=e.message}})
</script>
<template>
<section class="surface form-surface">
 <h2>{{data?.canGrade?'我的練習提交與人工評分':'我的練習成績'}}</h2>
 <p class="submission-notice">僅限本人學生體驗資料，不是正式成績，不納入正式學生名單、班級平均或統計。</p>
 <p v-if="error" role="alert" class="error-box">{{error}}</p><p v-if="notice" role="status">{{notice}}</p>
 <template v-if="data"><p>我的練習等比平均：{{data.average==null?'待評分':data.average.toFixed(2)+' 分'}}</p>
 <p v-if="!data.records.length">尚無練習提交。請切換學生視角，完成已發布的測驗或 PDF 作業。</p>
 <article v-for="row in data.records" :key="row.id" class="question-editor">
  <h3>{{row.title}} · {{row.kind==='quiz'?'測驗練習':'PDF 作業練習'}}</h3>
  <p>{{new Date(row.submittedAt).toLocaleString()}} · 得分 {{row.score??'待評分'}} / {{row.maxScore}}</p>
  <details v-if="row.answers"><summary>檢視本次練習作答</summary><div v-for="(a,i) in row.answers" :key="i"><strong>{{a.prompt}}</strong><p v-if="data.canGrade && a.gradingGuidance" class="studio-prose submission-notice">教師評分參考：{{a.gradingGuidance}}</p><QuizAnswerReview :question="a" :text="a.text" :show-key="data.canGrade" /></div></details>
  <FilePreview v-if="row.kind==='report'" :endpoint="`${base}/reports/${row.id}/preview`" :file-name="row.fileName" content-type="application/pdf" />
  <p v-if="row.quiz">客觀題自動得分：{{row.quiz.objectiveScore}}；{{row.quiz.pendingEssays?'申論題待評分':'已完成評分'}}。</p><form v-if="data.canGrade && course.status!=='archived' && (!row.quiz || row.quiz.marks.some(m=>m.questionType==='essay'))" @submit.prevent="save(row)">
   <label v-for="mark in (row.quiz?.marks||[]).filter(m=>m.questionType==='essay')" :key="mark.questionId">申論題練習得分／{{mark.points}}<input aria-label="練習得分" type="number" min="0" :max="mark.points" step="0.01" required :value="drafts[row.id]?.essayScores?.[mark.questionId]??row.essayScores?.[mark.questionId]??''" :disabled="busy" @input="editEssay(row,mark.questionId,$event.target.value)" /></label><label v-if="!row.quiz">練習得分<input type="number" min="0" :max="row.maxScore" step="0.01" required :value="drafts[row.id]?.score??row.score??''" :disabled="busy" @input="edit(row,'score',$event.target.value)" /></label>
   <label>練習評語<textarea :value="drafts[row.id]?.feedback??row.feedback" maxlength="16000" :disabled="busy" @input="edit(row,'feedback',$event.target.value)" /></label>
   <p v-if="row.quiz" aria-live="polite">即時暫算練習總分：{{row.quiz.objectiveScore+Object.values(drafts[row.id]?.essayScores||row.essayScores||{}).reduce((a,b)=>a+Number(b||0),0)}} / {{row.maxScore}}（不計入正式成績）</p><button class="button primary" :disabled="busy || !drafts[row.id]">儲存練習成績</button>
  </form><p v-else class="studio-prose">{{row.feedback || '尚無練習評語。'}}</p>
 </article></template>
</section>
</template>
