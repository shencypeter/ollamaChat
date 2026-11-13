<script setup>
import {computed,onBeforeUnmount,ref,watch} from 'vue'
import {api} from '../api'
const props=defineProps({course:{type:Object,required:true}})
const data=ref(null),error=ref(''),loading=ref(false)
const staff=computed(()=>['teacher','ta'].includes(props.course.courseRole))
const permitted=computed(()=>!(props.course.courseRole==='student'&&props.course.canPractice)&&!(props.course.courseRole==='ta'&&props.course.taRestrictions?.includes('statistics')))
let generation=0
async function load(){const current=++generation;data.value=null;error.value='';if(!permitted.value)return;loading.value=true;try{const result=await api(staff.value?`/courses/${props.course.id}/statistics`:'/learning-history/courses');if(current===generation)data.value=staff.value?result:result.find(row=>row.id===props.course.id)}catch(e){if(current===generation)error.value=e.message}finally{if(current===generation)loading.value=false}}
watch(()=>[props.course.id,props.course.courseRole,props.course.canPractice,props.course.taRestrictions],load,{immediate:true})
onBeforeUnmount(()=>generation++)
</script>
<template><section v-if="permitted" class="surface course-progress"><header><h2>{{staff?'本課學習概況':'我的課程進度'}}</h2><RouterLink :to="`/courses/${course.id}?section=history`">查看{{staff?'學習統計':'完整進度'}} →</RouterLink></header><p v-if="loading" role="status">正在載入本課進度…</p><p v-else-if="error" role="alert">{{error}} <button class="button secondary" @click="load">重試</button></p><div v-else-if="data" class="progress-values"><template v-if="staff"><span><strong>{{data.enrolled}}</strong>有效修課學生</span><span><strong>{{data.submitters}}</strong>人已正式提交評量</span><span><strong>{{data.submissions}}</strong>份正式提交</span></template><template v-else><span><strong>{{data.submitted}} / {{data.required}}</strong>已提交評量</span><span><strong>{{data.pending}}</strong>{{data.pending}} 份待公布成績</span><progress v-if="data.required" :value="data.submitted" :max="data.required" aria-label="本課評量提交進度"/></template></div><p v-else class="field-hint">尚無本課學習紀錄。</p><p class="field-hint">{{staff?'統計不包含教師學生視角的練習資料。':'進度依已開放的評量計算，不代表完課或學分。'}}</p></section></template>
<style scoped>
.course-progress{padding:20px 24px;margin-bottom:22px}.course-progress header{display:flex;justify-content:space-between;align-items:center;gap:12px}.course-progress h2{font-size:17px}.course-progress header a{font-size:12px;color:var(--accent)}.progress-values{display:flex;gap:30px;align-items:center;flex-wrap:wrap;margin:18px 0}.progress-values span{font-size:12px;color:var(--muted)}.progress-values strong{display:block;font-size:24px;color:var(--text);margin-bottom:6px}.progress-values progress{width:min(100%,280px);accent-color:var(--accent)}.course-progress>.field-hint{margin-top:12px;line-height:1.7}@media(max-width:650px){.course-progress{padding:18px}.course-progress header{flex-wrap:wrap}.progress-values{gap:20px}}
</style>
