<script setup>
import { computed, nextTick, onMounted, ref } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
import { api } from '../api'
import { markNotificationsRead } from '../composables/notifications'
import { setCourseExperience } from '../composables/experience'
import ReportModeFields from '../components/ReportModeFields.vue'
import AssignmentEditor from '../components/AssignmentEditor.vue'
import GradebookPanel from '../components/GradebookPanel.vue'
import ExamManager from '../components/ExamManager.vue'
import { formatDate, toUtc } from '../utils/assignments'

const props = defineProps({ boundCourse: { type: Object, default: null }, embedded: Boolean, reviewOnly: Boolean })
const route = useRoute(), router = useRouter()
const courses = ref([]), selected = ref(''), course = ref(null), data = ref(null), error = ref(''), busy = ref(false)
const quizEditor = ref(false), reportEditor = ref(false), weight = ref(1)
const form = ref({ submissionMode: 'individual', groupIds: [], evaluationMode: 'off', dimensions: ['工作參與', '任務完成', '合作與溝通', '整體貢獻'], notifyStudents: true, title: '', instructions: '', maxScore: 100, weight: 1, opensAt: '', dueAt: '' })
const grades = computed(() => !props.embedded && route.meta.assessmentGrades)
const staff = computed(() => data.value?.staff)

async function load() {
  busy.value = true; error.value = ''
  try {
    courses.value = props.boundCourse ? [props.boundCourse] : await api('/courses')
    selected.value = props.boundCourse?.id || route.query.course || courses.value[0]?.id || ''
    course.value = courses.value.find(item => item.id === selected.value)
    if (!course.value && selected.value) throw new Error('找不到你參與的課程。')
    setCourseExperience(course.value?.canPractice ?? null)
    if (course.value) {
      data.value = await api(`/courses/${selected.value}/assessments`)
      if (!grades.value && !data.value.staff && !course.value.canPractice) {
        await nextTick()
        await Promise.all([
          markNotificationsRead('quiz', data.value.quizzes.filter(item => item.status === 'published' && item.notifyStudents).map(item => item.id)),
          markNotificationsRead('report', data.value.reports.filter(item => item.status === 'published' && item.notifyStudents).map(item => item.id))
        ])
      }
    }
  } catch (e) { error.value = e.message } finally { busy.value = false }
}
async function change() {
  if ((quizEditor.value || reportEditor.value) && !confirm('確定放棄未儲存的評量設定？')) { selected.value = course.value?.id || ''; return }
  quizEditor.value = false; reportEditor.value = false
  await router.replace({ path: route.path, query: { course: selected.value } }); await load()
}
async function createQuiz(input) {
  busy.value = true; error.value = ''
  try { const created = await api(`/courses/${selected.value}/quizzes`, { method: 'POST', body: { assignment: input, weight: weight.value } }); quizEditor.value = false; await router.push(`/courses/${selected.value}/quizzes/${created.id}`) }
  catch (e) { error.value = e.message } finally { busy.value = false }
}
async function createReport() {
  busy.value = true; error.value = ''
  try { const created = await api(`/courses/${selected.value}/reports`, { method: 'POST', body: { ...form.value, opensAt: toUtc(form.value.opensAt), dueAt: toUtc(form.value.dueAt) } }); reportEditor.value = false; await router.push(`/courses/${selected.value}/reports/${created.id}`) }
  catch (e) { error.value = e.message } finally { busy.value = false }
}
function guard() { return !(quizEditor.value || reportEditor.value) || confirm('確定放棄未儲存的評量設定？') }
onBeforeRouteLeave(guard); onBeforeRouteUpdate(guard); onMounted(load)
</script>

<template>
  <section v-if="!embedded" class="page-heading"><div><p class="eyebrow">FORMAL EXAMS &amp; GRADES</p><h1>{{ grades ? '課程成績' : '正式考試' }}</h1><p class="muted">此區適合期中考、期末考等獨立正式考試；一般作業、隨堂與課後測驗請從課程單元進入作答及批改。</p></div></section>
  <p v-if="error" class="error-box" role="alert">{{ error }}</p>
  <RouterLink v-if="!embedded && course" :to="`/courses/${course.id}?section=${grades ? 'grades' : 'assessments'}`" class="back-link">← 返回 {{ course.title }}</RouterLink>
  <label v-if="!embedded && courses.length">課程<select aria-label="評量課程" v-model="selected" @change="change"><option v-for="item in courses" :key="item.id" :value="item.id">{{ item.code }} · {{ item.title }}</option></select></label>
  <nav v-if="!embedded" class="course-navigation" aria-label="正式考試與成績功能"><RouterLink :to="{ path: '/assessments', query: { course: selected } }">正式考試</RouterLink><RouterLink :to="{ path: '/assessment-grades', query: { course: selected } }">課程成績</RouterLink></nav>
  <GradebookPanel v-if="grades && course" :key="course.id" :course="course" />

  <template v-if="!grades && data && course">
    <section class="surface form-surface formal-exam-intro"><h2>期中／期末正式考試</h2><p class="muted">獨立、一次提交的正式考試集中於此；一般單元活動仍使用原本的作答、提交與成績資料。</p></section>
    <ExamManager :course="course" :staff="staff" />
    <section class="surface form-surface unit-assessment-note"><h2>一般單元作業與測驗</h2><p>一般作業、PDF 報告及隨堂／課後測驗已收在課程單元中，教師可在單元活動旁直接進入批改。</p><RouterLink :to="`/courses/${course.id}`" class="button secondary">前往課程內容</RouterLink></section>

    <details v-if="false && staff" class="surface form-surface legacy-assessments" :open="reviewOnly">
      <summary>{{ reviewOnly ? '相容入口：選擇要批改的舊評量' : '建立／整理一般單元評量' }}</summary>
      <p class="field-hint">新建後請回到單元管理指定所屬單元；既有深層連結與資料仍保留。</p>
      <button type="button" class="button secondary" :disabled="busy || quizEditor || reportEditor" @click="load">重新整理單元評量</button>
      <div v-if="!reviewOnly && !course.taRestrictions?.includes('assessments') && course.status !== 'archived'" class="studio-actions"><button class="button primary" @click="quizEditor = !quizEditor; reportEditor = false">建立線上／課後測驗</button><button class="button secondary" @click="reportEditor = !reportEditor; quizEditor = false">建立 PDF 報告作業</button></div>
      <section v-if="quizEditor" class="question-editor"><label>成績權重<input v-model.number="weight" type="number" min="0.001" max="999999" step="0.001" /></label><AssignmentEditor quiz :course-id="course.id" :busy="busy" @save="createQuiz" @cancel="quizEditor = false" /></section>
      <section v-if="reportEditor" class="question-editor"><h2>建立 PDF 報告作業</h2><form @submit.prevent="createReport"><fieldset :disabled="busy"><ReportModeFields :form="form" :course="selected"/><label>作業名稱<input v-model="form.title" required maxlength="200" /></label><label>作業說明<textarea v-model="form.instructions" maxlength="24000" rows="5"></textarea></label><div class="form-grid"><label>滿分<input v-model.number="form.maxScore" type="number" min="0.01" max="999999" step="0.01" required /></label><label>成績權重<input v-model.number="form.weight" type="number" min="0.001" max="999999" step="0.001" required /></label><label>開放時間<input v-model="form.opensAt" type="datetime-local" /></label><label>截止時間<input v-model="form.dueAt" type="datetime-local" /></label></div><label><input type="checkbox" v-model="form.notifyStudents"/>發布時通知學生</label><button class="button primary">儲存作業草稿</button></fieldset></form></section>
      <section class="question-editor"><h2>線上／課後測驗</h2><RouterLink v-for="item in data.quizzes.filter(item => !reviewOnly || item.status !== 'draft')" :key="item.id" :to="`/courses/${course.id}/quizzes/${item.id}`" class="studio-case-card surface"><h3>{{ item.title }}</h3><p>{{ item.released ? '成績已發布' : item.myStatus === 'submitted' ? '已交卷・批改中' : item.status === 'draft' ? '草稿' : item.status === 'closed' || Date.parse(item.dueAt) <= Date.now() ? '已截止' : Date.parse(item.opensAt) > Date.now() ? '已發布・尚未開放' : '開放作答' }} · 權重 {{ item.weight }} · 截止 {{ formatDate(item.dueAt) }}</p></RouterLink><p v-if="!data.quizzes.length" class="empty-inline">目前沒有可見的線上測驗。</p></section>
      <section class="question-editor"><h2>PDF 報告作業</h2><RouterLink v-for="item in data.reports.filter(item => !reviewOnly || item.status !== 'draft')" :key="item.id" :to="`/courses/${course.id}/reports/${item.id}`" class="studio-case-card surface"><h3>{{ item.title }}</h3><p>{{ { draft: '草稿', published: '已發布', closed: '已關閉' }[item.status] }} · 權重 {{ item.weight }} · 截止 {{ formatDate(item.dueAt) }}</p></RouterLink><p v-if="!data.reports.length" class="empty-inline">目前沒有可見的 PDF 作業。</p></section>
    </details>
  </template>
  <p v-if="!busy && !courses.length" class="surface form-surface">尚未加入課程，請聯絡管理者。</p>
</template>

<style scoped>
.formal-exam-intro,.unit-assessment-note{margin-bottom:18px}.legacy-assessments>summary{cursor:pointer;font-weight:700}.legacy-assessments[open]>summary{margin-bottom:16px}
</style>
