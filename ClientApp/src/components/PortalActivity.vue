<script setup>
import {computed,onBeforeUnmount,ref,watch} from 'vue'
import {Bell,CalendarDays,ChevronLeft,ChevronRight,RefreshCw} from '@lucide/vue'
import {api} from '../api'
import {formatDate} from '../utils/assignments'
import PortalPanel from './PortalPanel.vue'
const props=defineProps({teaching:Boolean,courses:{type:Array,default:()=>[]}})
const panelScope=computed(()=>props.teaching?'teaching':'student')
const courseId=ref(''),page=ref(1),feed=ref([]),agenda=ref([]),hasMore=ref(false),agendaMore=ref(false),busy=ref(false),feedError=ref(''),agendaError=ref('')
const labels={announcement:'課程公告',material:'教材',assignment:'作業',quiz:'測驗',report:'PDF 作業',grade:'已公布成績',discussion:'討論更新',exam:'正式考試',midterm:'期中考',final:'期末考'}
const days=computed(()=>{const groups=new Map();for(const item of agenda.value){const date=new Date(item.dueAt||item.opensAt);const key=Number.isNaN(date.getTime())?'未定時間':date.toLocaleDateString('zh-TW',{year:'numeric',month:'long',day:'numeric',weekday:'short'});if(!groups.has(key))groups.set(key,[]);groups.get(key).push(item)}return [...groups].map(([label,items])=>({label,items}))})
let generation=0
async function load(){
 const current=++generation;busy.value=true;feedError.value='';agendaError.value=''
 const params=new URLSearchParams({view:props.teaching?'teaching':'student'});if(courseId.value)params.set('courseId',courseId.value)
 const results=await Promise.allSettled([api(`/portal-feed?${params}&page=${page.value}&pageSize=12`),api(`/portal-feed/agenda?${params}`)])
 if(current!==generation)return
 const [messages,events]=results
 if(messages.status==='fulfilled'){feed.value=messages.value.items;hasMore.value=messages.value.hasMore}else{feed.value=[];hasMore.value=false;feedError.value=messages.reason.message}
 if(events.status==='fulfilled'){agenda.value=events.value.items;agendaMore.value=events.value.hasMore}else{agenda.value=[];agendaError.value=events.reason.message}
 busy.value=false
}
watch(()=>props.teaching,()=>{courseId.value='';page.value=1;load()},{immediate:true})
watch(courseId,()=>{page.value=1;load()})
function move(delta){page.value+=delta;load()}
onBeforeUnmount(()=>generation++)
</script>
<template>
 <section class="portal-activity" aria-label="跨課訊息與行程">
  <header class="activity-toolbar"><h2>課程動態與行程</h2><div><label>課程<select v-model="courseId" aria-label="訊息與行程課程"><option value="">全部課程</option><option v-for="course in courses" :key="course.id" :value="course.id">{{course.title}}</option></select></label><button class="icon-button" aria-label="重新載入訊息與行程" title="重新整理" :disabled="busy" @click="load"><RefreshCw :size="17"/></button></div></header>
  <div class="activity-columns">
   <PortalPanel panel-key="inbox" :scope="panelScope" heading="h3" title="跨課訊息" :icon="Bell" :count="feedError?'暫不可用':busy?'載入中':`本頁 ${feed.length} 則`" description="依最新動態排列，點選返回原課程內容。"><p v-if="feedError" class="error-box" role="alert">{{feedError}}</p><p v-else-if="busy" role="status">正在載入訊息…</p><ul v-else-if="feed.length" class="activity-list"><li v-for="item in feed" :key="`${item.kind}:${item.id}`"><RouterLink :to="item.url"><span class="activity-context">{{item.courseTitle}} · {{labels[item.kind]||item.kind}}</span><strong>{{item.title}}</strong><small>{{item.authorName ? item.authorName+' · ' : ''}}{{formatDate(item.occurredAt)}}</small></RouterLink></li></ul><p v-else class="empty-inline">目前沒有課程訊息。</p><nav v-if="!feedError&&(page>1||hasMore)" class="feed-pages" aria-label="訊息分頁"><button class="icon-button" aria-label="上一頁訊息" :disabled="busy||page===1" @click="move(-1)"><ChevronLeft :size="18"/></button><span>第 {{page}} 頁</span><button class="icon-button" aria-label="下一頁訊息" :disabled="busy||!hasMore" @click="move(1)"><ChevronRight :size="18"/></button></nav></PortalPanel>
<PortalPanel panel-key="agenda" :scope="panelScope" heading="h3" title="即將到來的行程" :icon="CalendarDays" :count="agendaError?'暫不可用':busy?'載入中':`${agenda.length}${agendaMore?'+':''} 項`" description="作業截止與正式考試時間，以各課程設定為準。"><p v-if="agendaError" class="error-box" role="alert">{{agendaError}}</p><p v-else-if="busy" role="status">正在載入行程…</p><div v-else-if="days.length" class="agenda-days"><section v-for="day in days" :key="day.label"><h4>{{day.label}}</h4><ul class="activity-list"><li v-for="item in day.items" :key="`${item.kind}:${item.id}`"><RouterLink :to="item.url"><span class="activity-context">{{item.courseTitle}} · {{labels[item.kind]||item.kind}}</span><strong>{{item.title}}</strong><small>{{formatDate(item.dueAt||item.opensAt)}}</small></RouterLink></li></ul></section></div><p v-else class="empty-inline">目前沒有已排定的行程。</p><p class="field-hint">未設定日期的考試不會列入行程；逾期未交請查看學習待辦。</p><p v-if="agendaMore" class="field-hint">目前顯示最近 100 項，請選擇課程查看其餘行程。</p></PortalPanel>
  </div>
 </section>
</template>
<style scoped>
.portal-activity{display:grid;gap:15px}.activity-toolbar,.activity-toolbar>div{display:flex;align-items:center;gap:12px;justify-content:space-between}.activity-toolbar h2{font-size:20px}.activity-toolbar label{white-space:nowrap;display:flex;gap:8px;align-items:center;font-size:12px}.activity-toolbar select{max-width:240px}.activity-columns{align-items:start;display:grid;grid-template-columns:minmax(0,1.2fr) minmax(0,1fr);gap:22px}.activity-list{margin:0;padding:0;list-style:none}.activity-list li+li{border-top:1px solid var(--line)}.activity-list a{display:grid;gap:6px;padding:13px 0;text-decoration:none;color:var(--text);overflow-wrap:anywhere}.activity-list a:hover strong{color:var(--accent)}.activity-context{color:var(--accent);font-size:11px}.activity-list strong{font-size:14px;line-height:1.6}.activity-list small{font-size:11px;color:var(--muted)}.agenda-days{max-height:480px;overflow:auto}.agenda-days h4{font-size:12px;padding:10px 0;border-bottom:1px solid var(--line)}.feed-pages{display:flex;justify-content:center;align-items:center;gap:14px;margin-top:15px;font-size:12px}.field-hint{margin-top:14px;line-height:1.7}@media(max-width:850px){.activity-columns{grid-template-columns:1fr}.activity-toolbar{align-items:flex-start;flex-wrap:wrap}.activity-toolbar select{max-width:190px}}
</style>
