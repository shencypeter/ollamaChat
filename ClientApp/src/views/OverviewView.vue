<script setup>
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { ArrowRight, BookOpen, CheckCheck, ClipboardCheck, Clock3, MessageCircle, RefreshCw } from '@lucide/vue'
import { auth } from '../composables/auth'
import { experience } from '../composables/experience'
import { api } from '../api'
import { givenName } from '../utils/names'
import { formatDate } from '../utils/assignments'
import { loadDashboard } from '../utils/dashboard'
import {courseNotifications} from '../composables/notifications'
import PortalActivity from '../components/PortalActivity.vue'
import PortalPanel from '../components/PortalPanel.vue'

const teaching = computed(() => !!auth.user?.canTeach && !experience.student)
const greeting = computed(() => `${givenName(auth.user?.displayName || '')}，歡迎回來`)
const data = ref(null), loading = ref(true)
const workPanel = ref(null), discussionPanel = ref(null)
const panelScope = computed(() => teaching.value ? 'teaching' : 'student')
const labels = { todo: '尚未提交', overdue: '逾期未交', upcoming: '尚未開放' }
const statuses = { draft: '草稿', active: '進行中', archived: '已封存' }
const pendingCount = computed(() => data.value?.pending.reduce((sum, item) => sum + item.count, 0) || 0)
const progress = computed(() => new Map((data.value?.learningCourses || []).map(course => [course.id, course])))
const courseRows = computed(() => {
  const rows = new Map((data.value?.courses || []).map(course => [course.id, course]))
  if (!teaching.value) for (const course of data.value?.learningCourses || []) if (!rows.has(course.id)) rows.set(course.id, { ...course, termCode: course.term })
  return [...rows.values()]
})
let generation = 0
async function load() {
  const current = ++generation
  loading.value = true
  data.value = null
  const result = await loadDashboard(api, teaching.value)
  if (current === generation) { data.value = result; loading.value = false }
}
watch(teaching, load, { immediate: true })
onBeforeUnmount(() => { generation++ })
</script>

<template>
  <div class="personal-home">
    <section class="page-heading dashboard-heading">
      <div><p class="eyebrow">{{ teaching ? 'MY TEACHING' : 'MY LEARNING' }}</p><h1>我的首頁</h1><p class="muted">{{ greeting }}。{{ teaching ? '從待批改與課程討論開始今天的教學。' : '查看待辦、回到課程，掌握自己的學習進度。' }}</p></div>
      <button class="button secondary" :disabled="loading" @click="load"><RefreshCw :size="16" />重新整理</button>
    </section>
    <p v-if="loading" class="surface dashboard-loading" role="status">正在彙整{{ teaching ? '教學' : '學習' }}工作…</p>
    <template v-if="data">
      <div v-if="data.errors.length" class="error-box" role="alert"><strong>部分資料未能載入，以下只顯示成功取得的內容。</strong><ul><li v-for="message in data.errors" :key="message">{{ message }}</li></ul></div>
      <div class="dashboard-summary">
        <RouterLink :to="teaching ? '/courses' : '/student/courses'" class="surface summary-link"><BookOpen :size="21" /><span>{{ teaching ? '我的授課' : '我的修課' }}<strong>{{ data.unavailable.courses ? '待確認' : `${courseRows.length} 門` }}</strong></span><ArrowRight :size="17" /></RouterLink>
        <a :href="teaching ? '#grading-queue' : '#learning-tasks'" class="surface summary-link" @click="workPanel?.expand()"><ClipboardCheck :size="21" /><span>{{ teaching ? '待批改繳交' : '學習待辦' }}<strong>{{ teaching ? `${pendingCount} 份${data.unavailable.grading || data.unavailable.courses ? '（部分）' : ''}` : data.unavailable.tasks ? '待確認' : `${data.taskTotal} 項` }}</strong></span><ArrowRight :size="17" /></a>
        <a v-if="teaching" href="#discussion-queue" class="surface summary-link" @click="discussionPanel?.expand()"><MessageCircle :size="21" /><span>尚無回覆討論<strong>{{ data.discussions.length }} 串{{ data.unavailable.discussions || data.unavailable.courses ? '（部分）' : '' }}</strong></span><ArrowRight :size="17" /></a>
        <RouterLink v-else to="/learning-history" class="surface summary-link"><CheckCheck :size="21" /><span>學習歷程<strong>查看提交與成績</strong></span><ArrowRight :size="17" /></RouterLink>
      </div>
      <PortalActivity :teaching="teaching" :courses="courseRows" />
      <div class="dashboard-columns">
        <PortalPanel v-if="teaching" id="grading-queue" ref="workPanel" panel-key="grading" :scope="panelScope" title="待批改" :icon="ClipboardCheck" :count="`${pendingCount} 份${data.unavailable.grading||data.unavailable.courses?'（部分）':''}`" description="進行中課程的未評分繳交；已有評分草稿不重複列入。">
          <p v-if="data.restricted.length" class="field-hint">{{ data.restricted.join('、') }}：助教批改權限受限，未讀取或計入繳交資料。</p>
          <ul v-if="data.pending.length" class="dashboard-list"><li v-for="item in data.pending" :key="item.id"><RouterLink :to="item.url"><span><strong>{{ item.title }}</strong><small>{{ item.courseTitle }}</small></span><span class="queue-count">{{ item.count }} 份待批改 <ArrowRight :size="15" /></span></RouterLink></li></ul>
          <p v-else class="dashboard-empty">{{ data.unavailable.grading || data.unavailable.courses ? '未能確認完整待批改清單，請重新整理。' : '目前可檢視的評量沒有待批改繳交。' }}</p>
          <p class="field-hint">涵蓋一般線上評量與 PDF 作業；正式考試與案例請進入各課程查看。</p>
        </PortalPanel>
        <PortalPanel v-else id="learning-tasks" ref="workPanel" panel-key="tasks" :scope="panelScope" title="學習待辦" :icon="ClipboardCheck" :count="data.unavailable.tasks?'待確認':`${data.taskTotal} 項`" description="依最近截止排序，顯示最多 6 項。">
          <template #actions><RouterLink to="/learning-history">全部待辦 <ArrowRight :size="14" /></RouterLink></template>
          <ul v-if="data.tasks.length" class="dashboard-list"><li v-for="item in data.tasks" :key="item.id"><RouterLink :to="item.url"><span><strong>{{ item.title }}</strong><small>{{ progress.get(item.courseId)?.title }} · 截止 {{ formatDate(item.dueAt) }}</small></span><span class="status-badge" :class="{ 'overdue-label': item.state === 'overdue' }">{{ labels[item.state] }}</span></RouterLink></li></ul>
          <p v-else class="dashboard-empty">{{ data.unavailable.tasks ? '待辦暫時無法載入，請重新整理。' : '目前沒有待交評量。' }}</p>
          <p class="field-hint">包含線上測驗與 PDF 作業，不含封存課程；尚未開放與逾期項目會標示狀態。</p>
        </PortalPanel>
        <PortalPanel v-if="teaching" id="discussion-queue" ref="discussionPanel" panel-key="discussions" :scope="panelScope" title="尚無回覆的討論" :icon="MessageCircle" :count="`${data.discussions.length} 串${data.unavailable.discussions||data.unavailable.courses?'（部分）':''}`" description="列出最近 6 串仍開放、尚無任何回覆的討論。">
          <ul v-if="data.discussions.length" class="dashboard-list"><li v-for="item in data.discussions.slice(0,6)" :key="item.id"><RouterLink :to="item.url"><span><strong>{{ item.title }}</strong><small>{{ item.courseTitle }} · {{ item.author }}</small></span><ArrowRight :size="16" /></RouterLink></li></ul>
          <p v-else class="dashboard-empty">{{ data.unavailable.discussions || data.unavailable.courses ? '未能確認完整討論清單，請重新整理。' : '目前沒有尚無回覆的開放討論。' }}</p>
        </PortalPanel>
        <PortalPanel v-else panel-key="grades" :scope="panelScope" title="近期已公布成績" :icon="CheckCheck" :count="data.unavailable.grades?'待確認':`${data.grades.length} 筆`" description="從最近 12 筆學習活動中彙整已公布成績。">
          <template #actions><RouterLink to="/learning-history">完整歷程 <ArrowRight :size="14" /></RouterLink></template>
          <ul v-if="data.grades.length" class="dashboard-list"><li v-for="item in data.grades" :key="`${item.kind}:${item.id}`"><RouterLink :to="item.url"><span><strong>{{ item.title }}</strong><small>{{ progress.get(item.courseId)?.title }} · {{ formatDate(item.occurredAt) }}</small></span><span class="grade-result">{{ item.score }}<small> / {{ item.maxScore }}</small></span></RouterLink></li></ul>
          <p v-else class="dashboard-empty">{{ data.unavailable.grades ? '近期成績暫時無法載入。' : '最近活動中尚無已公布成績；可從完整歷程查看較早紀錄。' }}</p>
        </PortalPanel>
      </div>
      <PortalPanel panel-key="courses" :scope="panelScope" :title="teaching?'我的授課':'我的修課'" :icon="BookOpen" :count="data.unavailable.courses?'待確認':`${courseRows.length} 門`" :description="teaching?'選擇課程，進入單元與教學工作區。':'提交進度依已開放評量計算，不代表完課或學分。'">
        <template #actions><RouterLink :to="teaching ? '/courses' : '/student/courses'">所有課程 <ArrowRight :size="14" /></RouterLink></template>
        <div v-if="courseRows.length" class="dashboard-course-grid"><RouterLink v-for="course in courseRows" :key="course.id" :to="`/courses/${course.id}`" class="dashboard-course"><span class="eyebrow">{{ course.code }} · {{ course.termCode }}</span><h3>{{ course.title }}</h3><span class="muted">{{ statuses[course.status] }}{{ course.courseRole === 'ta' ? ' · 課程助教' : '' }}</span><span v-if="courseNotifications(course.id).total" class="status-badge">{{courseNotifications(course.id).total}} 則未讀課程通知</span><template v-if="!teaching && progress.has(course.id)"><p>已提交 {{ progress.get(course.id).submitted }} / {{ progress.get(course.id).required }} 份</p><progress v-if="progress.get(course.id).required" :value="progress.get(course.id).submitted" :max="progress.get(course.id).required" :aria-label="`${course.title} 評量提交進度`" /><p class="field-hint">{{ progress.get(course.id).pending }} 份待公布成績</p></template><span v-else-if="!teaching" class="field-hint">修課進度暫時無法取得。</span><span class="course-enter">進入課程 <ArrowRight :size="15" /></span></RouterLink></div>
        <p v-else class="dashboard-empty">{{ data.unavailable.courses ? '課程清單暫時無法載入。' : teaching ? '目前沒有授課課程，可從所有課程建立課程或請擁有者加入教學團隊。' : '目前沒有正式修課；加入課程且開放後即可在此查看。' }}</p>
      </PortalPanel>
      <PortalPanel v-if="teaching" panel-key="recent" :scope="panelScope" title="最近繳交" :icon="Clock3" :count="`${Math.min(data.recent.length,6)} 份${data.unavailable.grading||data.unavailable.courses?'（部分）':''}`" description="顯示可檢視評量最近 6 份繳交，點選進入批改工作區。">
        <ul v-if="data.recent.length" class="dashboard-list"><li v-for="item in data.recent.slice(0,6)" :key="item.id"><RouterLink :to="item.url"><span><strong>{{ item.studentName }} · {{ item.title }}</strong><small>{{ item.courseTitle }} · {{ formatDate(item.submittedAt) }}</small></span><ArrowRight :size="16" /></RouterLink></li></ul>
        <p v-else class="dashboard-empty">{{ data.unavailable.grading || data.unavailable.courses ? '最近繳交資料未完整載入。' : '目前可檢視的評量沒有繳交紀錄。' }}</p>
      </PortalPanel>
      <p v-if="experience.student" class="field-hint dashboard-scope">學生體驗只呈現本人正式修課紀錄，練習不計入；授課課程的學生預覽不會產生個人訊息。</p>
    </template>
  </div>
</template>

<style scoped>
.personal-home{display:grid;gap:22px}.dashboard-heading{margin-bottom:0}.dashboard-heading .muted{margin-top:8px}.dashboard-summary{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px}.summary-link{display:flex;align-items:center;gap:13px;padding:20px;color:var(--text);text-decoration:none}.summary-link>svg:first-child{color:var(--accent);flex-shrink:0}.summary-link>svg:last-child{margin-left:auto;flex-shrink:0}.summary-link span{font-size:12px;color:var(--muted)}.summary-link strong{display:block;color:var(--text);font-size:20px;margin-top:7px}.dashboard-columns{align-items:start;display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:22px}.dashboard-list{list-style:none;margin:0;padding:0}.dashboard-list li+li{border-top:1px solid var(--line)}.dashboard-list a{display:flex;align-items:center;justify-content:space-between;gap:14px;padding:15px 0;color:var(--text);text-decoration:none}.dashboard-list a:hover strong{color:var(--accent)}.dashboard-list a>span:first-child{min-width:0}.dashboard-list strong{display:block;font-size:14px;font-weight:600;overflow-wrap:anywhere}.dashboard-list small{display:block;font-size:11px;line-height:1.7;color:var(--muted);margin-top:5px}.dashboard-list svg{flex-shrink:0}.queue-count{display:flex;align-items:center;gap:5px;font-size:12px;white-space:nowrap;color:var(--accent)}.grade-result{white-space:nowrap;font-size:20px;font-weight:650;color:var(--accent)}.grade-result small{display:inline;font-size:12px;font-weight:400}.overdue-label{color:var(--danger)}.dashboard-empty,.dashboard-loading{padding:24px 0;color:var(--muted);font-size:13px;line-height:1.8}.dashboard-loading{padding:26px}.dashboard-course-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.dashboard-course{display:flex;flex-direction:column;align-items:flex-start;gap:9px;min-width:0;padding:19px;border:1px solid var(--line);border-radius:10px;color:var(--text);text-decoration:none}.dashboard-course:hover,.summary-link:hover{border-color:var(--accent)}.dashboard-course h3{font-size:16px;line-height:1.6;overflow-wrap:anywhere}.dashboard-course .muted,.dashboard-course p{font-size:12px}.dashboard-course progress{width:100%;height:7px;accent-color:var(--accent)}.course-enter{display:flex;align-items:center;gap:6px;color:var(--accent);font-size:12px;padding-top:10px;margin-top:auto}.dashboard-scope{line-height:1.7}.error-box ul{padding-left:20px;margin:10px 0 0}.error-box li{overflow-wrap:anywhere}
@media(max-width:1050px){.dashboard-course-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.dashboard-columns{grid-template-columns:1fr}}
@media(max-width:650px){.dashboard-summary{grid-template-columns:1fr;gap:10px}.summary-link{padding:16px}.summary-link strong{font-size:18px}.dashboard-course-grid{grid-template-columns:1fr}.dashboard-list a{flex-wrap:wrap}.dashboard-heading{gap:16px}.personal-home{gap:16px}.queue-count{white-space:normal}}
</style>
