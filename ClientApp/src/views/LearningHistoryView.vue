<script setup>
import {computed,onMounted,ref} from 'vue'
import {api} from '../api'
import {setCourseExperience} from '../composables/experience'
import {formatDate} from '../utils/assignments'
const courses=ref([]),items=ref([]),tasks=ref([]),total=ref(0),page=ref(1),busy=ref(false),error=ref('')
const props=defineProps({boundCourse:String})
const course=ref(props.boundCourse||''),term=ref(''),kind=ref(''),from=ref(''),until=ref('')
const labels={quiz:'線上測驗',report:'PDF 作業',case:'案例提交',offline:'線下成績',material:'教材學習'}
const states={todo:'尚未提交',overdue:'逾期未交',upcoming:'尚未開放',closed:'已關閉未交',pending:'已提交，待公布成績',graded:'已公布成績',submitted:'已正式提交',studied:'已有有效學習時間'}
const terms=computed(()=>[...new Set(courses.value.map(c=>c.term))])
const visibleCourses=computed(()=>courses.value.filter(c=>(!term.value||c.term===term.value)&&(!course.value||c.id===course.value)))
const count=computed(()=>visibleCourses.value.reduce((s,c)=>({required:s.required+c.required,submitted:s.submitted+c.submitted,pending:s.pending+c.pending}),{required:0,submitted:0,pending:0}))
function name(id){return courses.value.find(c=>c.id===id)?.title||''}
let generation=0
async function load(reset=false){if(reset)page.value=1;const request=++generation;busy.value=true;error.value=''
 try{const q=new URLSearchParams({page:page.value,pageSize:20});if(course.value)q.set('course',course.value);if(term.value)q.set('term',term.value);if(kind.value)q.set('kind',kind.value)
 if(from.value)q.set('from',new Date(`${from.value}T00:00:00`).toISOString());if(until.value){const end=new Date(`${until.value}T00:00:00`);end.setDate(end.getDate()+1);q.set('until',end.toISOString())}
 const taskQ=new URLSearchParams({todo:true,pageSize:10});if(course.value)taskQ.set('course',course.value);if(term.value)taskQ.set('term',term.value)
 const [history,todo]=await Promise.all([api(`/learning-history?${q}`),api(`/learning-history?${taskQ}`)])
 if(request===generation){items.value=history.items;total.value=history.total;tasks.value=todo.items}
 }catch(e){if(request===generation)error.value=e.message}finally{if(request===generation)busy.value=false}}
function changeTerm(){if(course.value&&!visibleCourses.value.some(c=>c.id===course.value))course.value='';load(true)}
async function move(delta){page.value+=delta;await load()}
onMounted(async()=>{setCourseExperience(false);try{courses.value=(await api('/learning-history/courses')).filter(c=>!props.boundCourse||c.id===props.boundCourse);await load()}catch(e){error.value=e.message}})
</script>
<template>
<section class="page-heading"><div><p class="eyebrow">{{boundCourse?'COURSE PROGRESS':'MY LEARNING'}}</p><h1>{{boundCourse?'課程學習進度':'我的學習歷程'}}</h1><p class="muted">{{boundCourse?'查看本課教材、評量提交與已發布成績。':'跨課程、跨學期查看正式修課、學習紀錄與已發布成績。'}}</p></div><button class="button secondary" :disabled="busy" @click="load()">重新載入</button></section>
<p class="field-hint">目前涵蓋仍有有效學生資格的課程，包含封存課程；不含教學練習、私人草稿或尚未匯入的校務紀錄。</p>
<p v-if="error" class="error-box" role="alert">{{error}}</p>
<section class="surface form-surface"><form class="history-filters" @submit.prevent="load(true)">
<label v-if="!boundCourse">學期<select aria-label="學期" v-model="term" @change="changeTerm"><option value="">所有學期</option><option v-for="t in terms" :key="t">{{t}}</option></select></label>
<label v-if="!boundCourse">課程<select aria-label="課程" v-model="course" @change="load(true)"><option value="">所有修課</option><option v-for="c in courses.filter(c=>!term||c.term===term)" :key="c.id" :value="c.id">{{c.code}} · {{c.title}}</option></select></label>
<label>活動類型<select aria-label="活動類型" v-model="kind" @change="load(true)"><option value="">所有類型</option><option v-for="(label,key) in labels" :key="key" :value="key">{{label}}</option></select></label>
<label>紀錄起日<input v-model="from" type="date" /></label><label>紀錄迄日<input v-model="until" type="date" :min="from||undefined" /></label><button class="button primary" :disabled="busy">查詢</button>
</form><p class="field-hint">日期依裝置時區篩選提交／成績公布事件；未有事件日期的待辦不列入日期查詢。</p></section>
<section class="gradebook-summary" aria-label="修課概況"><div><span>修課數</span><strong>{{visibleCourses.length}}</strong></div><div><span>已交／已開放評量</span><strong>{{count.submitted}} / {{count.required}}</strong></div><div><span>待公布成績</span><strong>{{count.pending}}</strong></div></section>
<section class="surface form-surface"><h2>我的修課與評量進度</h2><p class="field-hint">提交進度包含已開放測驗與 PDF 作業，不代表完課或學分；總成績顯示教師已發布的結果。重新發布時，各評分項目換算百分制後等比平均。</p>
<div class="history-courses"><article v-for="c in visibleCourses" :key="c.id" class="question-editor"><p class="eyebrow">{{c.term}} · {{c.code}}</p><h3><RouterLink :to="`/courses/${c.id}`">{{c.title}}</RouterLink></h3><p>{{c.status==='archived'?'課程已封存':'修課中'}} · 平台加入 {{formatDate(c.joinedAt)}}</p><p>{{c.required?`評量已提交 ${c.submitted} / ${c.required}`:'無應交評量'}}</p><progress v-if="c.required" :value="c.submitted" :max="c.required" :aria-label="`${c.title} 評量提交進度`"></progress><p>課內已發布總成績：<strong>{{c.average==null?'尚無成績':c.average.toFixed(2)}}</strong></p></article></div><p v-if="!visibleCourses.length" class="muted">目前沒有符合條件的修課紀錄。</p></section>
<section class="surface form-surface"><h2>學習待辦</h2><p class="field-hint">依所選課程／學期列出最多 10 項，優先顯示最近截止項目；不包含封存課程。</p><ul class="history-tasks"><li v-for="t in tasks" :key="t.id"><RouterLink :to="t.url">{{t.title}}</RouterLink> · {{name(t.courseId)}}<span class="status-badge">{{states[t.state]}}</span><span>截止 {{formatDate(t.dueAt)}}</span></li></ul><p v-if="!tasks.length" class="muted">目前沒有待交評量。</p></section>
<section class="surface form-surface" :aria-busy="busy"><h2>活動與成績時間軸</h2><p role="status" v-if="busy">載入歷程中…</p><p v-else class="muted">共 {{total}} 筆；同一測驗以最新正式提交版本呈現。</p>
<article v-for="item in items" :key="`${item.kind}:${item.id}`" class="question-editor history-event"><p class="eyebrow">{{labels[item.kind]}} · {{name(item.courseId)}}</p><h3><RouterLink :to="item.url">{{item.title}}</RouterLink></h3><p><span class="status-badge">{{states[item.state]}}</span><span v-if="item.revision"> · 提交版本 {{item.revision}}</span></p><time v-if="item.occurredAt">{{formatDate(item.occurredAt)}}</time><p v-else>尚無提交紀錄 · 截止 {{formatDate(item.dueAt)}}</p><p v-if="item.effectiveSeconds!=null">累積有效學習時間：{{Math.floor(item.effectiveSeconds/60)}} 分 {{item.effectiveSeconds%60}} 秒</p><p v-if="item.score!=null">得分：<strong>{{item.score}} / {{item.maxScore}}</strong></p><p v-if="item.feedback" class="history-feedback">教師評語：{{item.feedback}}</p></article>
<p v-if="!busy&&!items.length" class="muted">沒有符合條件的活動紀錄。</p><div class="staff-actions"><button class="button secondary" :disabled="busy||page===1" @click="move(-1)">上一頁</button><span>第 {{page}} 頁</span><button class="button secondary" :disabled="busy||page*20>=total" @click="move(1)">下一頁</button></div></section>
</template>
<style scoped>
.history-filters{display:flex;gap:12px;align-items:end;flex-wrap:wrap}.history-filters label{flex:1 1 150px;min-width:0}.history-courses{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,260px),1fr));gap:16px}.history-courses article{margin:0}.history-courses progress{width:100%;accent-color:var(--accent)}.history-tasks{padding-left:20px}.history-tasks li{margin:16px 0}.history-tasks span{margin-left:10px}.history-event h3,.history-feedback{overflow-wrap:anywhere}.history-feedback{white-space:pre-wrap}.history-event time{font-size:13px;color:var(--muted)}
</style>
