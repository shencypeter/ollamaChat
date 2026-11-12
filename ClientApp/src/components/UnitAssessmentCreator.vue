<script setup>
import {ref} from 'vue'
import {useRouter} from 'vue-router'
import {api} from '../api'
import {toUtc} from '../utils/assignments'
import AssignmentEditor from './AssignmentEditor.vue'
import ReportModeFields from './ReportModeFields.vue'

const props=defineProps({course:{type:Object,required:true},unitId:{type:String,required:true}})
const router=useRouter(),mode=ref(''),busy=ref(false),error=ref(''),weight=ref(1)
const form=ref({submissionMode:'individual',groupIds:[],evaluationMode:'off',dimensions:['工作參與','任務完成','合作與溝通','整體貢獻'],notifyStudents:true,title:'',instructions:'',maxScore:100,weight:1,opensAt:'',dueAt:'',unitId:props.unitId})
async function createQuiz(input){busy.value=true;error.value='';try{const created=await api(`/courses/${props.course.id}/quizzes`,{method:'POST',body:{assignment:{...input,unitId:props.unitId},weight:weight.value}});await router.push(`/courses/${props.course.id}/quizzes/${created.id}`)}catch(e){error.value=e.message}finally{busy.value=false}}
async function createReport(){busy.value=true;error.value='';try{const created=await api(`/courses/${props.course.id}/reports`,{method:'POST',body:{...form.value,unitId:props.unitId,opensAt:toUtc(form.value.opensAt),dueAt:toUtc(form.value.dueAt)}});await router.push(`/courses/${props.course.id}/reports/${created.id}`)}catch(e){error.value=e.message}finally{busy.value=false}}
</script>
<template><section class="unit-assessment-creator"><p v-if="error" class="error-box" role="alert">{{error}}</p><div class="studio-actions"><button data-create="quiz" class="button secondary" :disabled="busy" @click="mode=mode==='quiz'?'':'quiz'">建立線上／課後測驗</button><button data-create="report" class="button secondary" :disabled="busy" @click="mode=mode==='report'?'':'report'">建立 PDF 作業</button></div><section v-if="mode==='quiz'" class="question-editor"><label>成績權重<input v-model.number="weight" type="number" min="0.001" max="999999" step="0.001"/></label><AssignmentEditor quiz :course-id="course.id" :busy="busy" @save="createQuiz" @cancel="mode=''"/></section><section v-if="mode==='report'" class="question-editor"><form @submit.prevent="createReport"><fieldset :disabled="busy"><ReportModeFields :form="form" :course="course.id"/><label>作業名稱<input name="report-title" v-model="form.title" required maxlength="200"/></label><label>作業說明<textarea v-model="form.instructions" maxlength="24000" rows="4"/></label><div class="form-grid"><label>滿分<input v-model.number="form.maxScore" type="number" min="0.01" step="0.01" required/></label><label>權重<input v-model.number="form.weight" type="number" min="0.001" step="0.001" required/></label><label>開放時間<input v-model="form.opensAt" type="datetime-local"/></label><label>截止時間<input v-model="form.dueAt" type="datetime-local"/></label></div><button class="button primary">儲存 PDF 作業草稿</button><button type="button" class="button secondary" @click="mode=''">取消</button></fieldset></form></section></section></template>
