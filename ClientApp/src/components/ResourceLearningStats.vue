<script setup>
import {onMounted,ref} from 'vue'
import {UsersRound} from '@lucide/vue'
import {api} from '../api'
const props=defineProps({courseId:String,resourceId:String,title:String}),emit=defineEmits(['close'])
const data=ref(null),error=ref('')
function duration(seconds){return `${Math.floor(seconds/60)} 分 ${seconds%60} 秒`}
onMounted(async()=>{try{data.value=await api(`/courses/${props.courseId}/resources/${props.resourceId}/learning/statistics`)}catch(e){error.value=e.message}})
</script>
<template><section class="surface form-surface"><div class="section-title"><h2><UsersRound :size="20" aria-hidden="true" />{{title}} · 學生參與紀錄（正式學習時間）</h2><button type="button" class="button secondary" @click="emit('close')">關閉紀錄</button></div><p v-if="error" class="error-box" role="alert">{{error}}</p><template v-if="data"><p>本課 {{data.studentCount}} 位學生 · {{data.watchedCount}} 位已有紀錄 · 實際合計 {{duration(data.totalSeconds)}} · 內容合計 {{duration(data.totalContentSeconds||0)}}</p><p class="field-hint">僅統計本課有效學生的正式有效時間，未觀看者顯示 0；本人練習不列入，重看可累積。影片門檻採內容時間；內容時間為依速度換算的累積片段，非不重複觀看比例。</p><div class="table-scroll"><table class="data-table"><thead><tr><th>學號</th><th>姓名</th><th>實際觀看時間</th><th>影片內容時間</th><th>播放速度／實際時間／內容時間</th><th>最近觀看</th></tr></thead><tbody><tr v-for="student in data.students" :key="student.id"><td>{{student.studentNumber||'未填學號'}}</td><td>{{student.displayName}}</td><td>{{duration(student.accumulatedSeconds)}}</td><td>{{duration(student.contentSeconds||0)}}</td><td><div v-for="speed in student.playbackSpeeds||[]" :key="speed.playbackRate">{{speed.playbackRate}} 倍 · {{duration(speed.watchSeconds)}} ／ {{duration(speed.contentSeconds)}}</div><span v-if="!student.playbackSpeeds?.length">—</span></td><td>{{student.lastWatchedAt?new Date(student.lastWatchedAt).toLocaleString():'尚無紀錄'}}</td></tr></tbody></table></div></template><p v-else-if="!error">載入中…</p></section></template>
