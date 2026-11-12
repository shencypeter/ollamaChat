<script setup>
import { computed, ref, watch } from 'vue'
import { ChevronLeft, ChevronRight, FileX2, RefreshCw } from '@lucide/vue'
import TeachingPanel from './TeachingPanel.vue'
import { formatDate } from '../utils/assignments'

const props = defineProps({
  assignment: { type: Object, required: true },
  roster: { type: Array, required: true },
  busy: Boolean
})
const emit = defineEmits(['refresh', 'changed'])
const selectedStudentId = ref('')

function compare(a, b) {
  return (a.studentNumber || '').localeCompare(b.studentNumber || '', 'zh-Hant', { numeric: true })
    || a.studentName.localeCompare(b.studentName, 'zh-Hant')
    || a.studentId.localeCompare(b.studentId)
}
const submitted = computed(() => props.roster.filter(row => row.id).sort(compare))
const missing = computed(() => props.roster.filter(row => !row.id).sort(compare))
const ordered = computed(() => [...submitted.value, ...missing.value])
const selected = computed(() => ordered.value.find(row => row.studentId === selectedStudentId.value) || null)
const selectedIndex = computed(() => ordered.value.findIndex(row => row.studentId === selectedStudentId.value))

watch(() => props.roster, () => {
  if (!ordered.value.some(row => row.studentId === selectedStudentId.value)) {
    selectedStudentId.value = ordered.value[0]?.studentId || ''
  }
}, { immediate: true, deep: true })

function turn(offset) {
  const next = ordered.value[selectedIndex.value + offset]
  if (next) selectedStudentId.value = next.studentId
}
</script>

<template>
  <section class="surface review-workspace">
    <header class="review-heading">
      <div>
        <p class="eyebrow">CLASS REVIEW</p>
        <h2>全班交卷與批改</h2>
        <p class="muted">已交卷 {{ submitted.length }} 人 · 未交卷 {{ missing.length }} 人</p>
      </div>
      <button type="button" class="button secondary" :disabled="busy" @click="emit('refresh')">
        <RefreshCw :size="15" />重新整理
      </button>
    </header>

    <div v-if="ordered.length" class="review-layout">
      <aside class="review-sidebar" aria-label="全班交卷學生">
        <section>
          <h3>已交卷 <span>{{ submitted.length }}</span></h3>
          <button v-for="row in submitted" :key="row.studentId" type="button" class="review-student"
            :class="{ active: row.studentId === selectedStudentId }" :aria-pressed="row.studentId === selectedStudentId"
            @click="selectedStudentId = row.studentId">
            <strong>{{ row.studentNumber || '未填學號' }} · {{ row.studentName }}</strong>
            <span>{{ row.total == null ? '待批改' : `${row.total} 分` }} · {{ formatDate(row.submittedAt) }}</span>
          </button>
        </section>
        <section class="missing-students">
          <h3>未交卷 <span>{{ missing.length }}</span></h3>
          <button v-for="row in missing" :key="row.studentId" type="button" class="review-student"
            :class="{ active: row.studentId === selectedStudentId }" :aria-pressed="row.studentId === selectedStudentId"
            @click="selectedStudentId = row.studentId">
            <strong>{{ row.studentNumber || '未填學號' }} · {{ row.studentName }}</strong>
            <span>尚無正式交卷</span>
          </button>
        </section>
      </aside>

      <main v-if="selected" class="review-paper">
        <nav class="review-navigation" aria-label="切換學生考卷">
          <button type="button" class="button secondary" :disabled="selectedIndex <= 0" @click="turn(-1)"><ChevronLeft :size="15" />上一位</button>
          <span>{{ selectedIndex + 1 }} / {{ ordered.length }}</span>
          <button type="button" class="button secondary" :disabled="selectedIndex >= ordered.length - 1" @click="turn(1)">下一位<ChevronRight :size="15" /></button>
        </nav>
        <header class="paper-student">
          <div>
            <p class="eyebrow">{{ selected.id ? 'SUBMITTED WORK' : 'BLANK PAPER' }}</p>
            <h2>{{ selected.studentNumber || '未填學號' }} · {{ selected.studentName }}</h2>
          </div>
          <span class="status-badge">{{ selected.id ? '已交卷' : '未交卷 · 唯讀空白卷' }}</span>
        </header>

        <TeachingPanel v-if="selected.id" :key="selected.id" :assignment="assignment" :submission-id="selected.id" :staff="true" @changed="emit('changed')" />
        <section v-else class="blank-paper" aria-label="唯讀空白卷">
          <p class="submission-notice"><FileX2 :size="17" />此學生尚未正式交卷。以下僅顯示題目，不會建立作答，也不能評分。</p>
          <article v-for="question in assignment.questions" :key="question.id" class="blank-question">
            <p class="eyebrow">{{ question.sectionTitle || '題目' }} · {{ question.sortOrder }}</p>
            <h3>{{ question.promptText }}</h3>
            <div class="blank-answer">（未作答）</div>
          </article>
        </section>
      </main>
    </div>
    <p v-else class="empty-inline">本課程目前沒有有效學生。</p>
  </section>
</template>

<style scoped>
.review-workspace{padding:0;overflow:hidden;min-width:0}.review-heading{display:flex;align-items:center;justify-content:space-between;gap:18px;padding:22px 24px;border-bottom:1px solid var(--line)}.review-layout{display:grid;grid-template-columns:minmax(260px,330px) minmax(0,1fr);min-height:640px}.review-sidebar{border-right:1px solid var(--line);background:var(--surface-soft);min-width:0;max-height:850px;overflow:auto}.review-sidebar section>h3{display:flex;justify-content:space-between;padding:14px 17px;margin:0;border-bottom:1px solid var(--line);font-size:12px}.review-sidebar h3 span{color:var(--muted);font-size:10px}.missing-students{border-top:3px solid var(--line)}.review-student{display:grid;width:100%;gap:6px;padding:15px 18px;text-align:left;background:transparent;color:var(--text);border-bottom:1px solid var(--line)}.review-student:hover{background:var(--accent-soft)}.review-student.active{background:var(--surface);box-shadow:inset 3px 0 var(--accent)}.review-student strong{font-size:12px;line-height:1.5}.review-student span{color:var(--muted);font-size:10px}.review-paper{min-width:0;padding:22px 26px}.review-navigation{display:grid;grid-template-columns:1fr auto 1fr;align-items:center;gap:12px;margin-bottom:22px}.review-navigation>span{text-align:center;color:var(--muted);font-size:10px}.review-navigation .button:first-child{justify-self:start}.review-navigation .button:last-child{justify-self:end}.paper-student{display:flex;justify-content:space-between;align-items:flex-start;gap:18px;padding-bottom:18px;border-bottom:1px solid var(--line)}.paper-student h2{font-size:20px}.blank-paper{padding-top:20px}.blank-question{padding:20px 0;border-bottom:1px solid var(--line)}.blank-question h3{font-size:15px;line-height:1.75}.blank-answer{min-height:100px;margin-top:14px;padding:16px;border:1px dashed var(--line);border-radius:8px;background:var(--surface-soft);color:var(--muted)}.review-paper :deep(.teaching-panel){margin:20px 0 0;box-shadow:none;border:0;padding:0}@media(max-width:900px){.review-layout{grid-template-columns:minmax(230px,285px) minmax(0,1fr)}.review-paper{padding:18px}}@media(max-width:680px){.review-heading{align-items:flex-start;flex-wrap:wrap;padding:18px}.review-layout{display:block;min-height:0}.review-sidebar{max-height:360px;border-right:0;border-bottom:1px solid var(--line)}.review-paper{padding:18px 14px}.paper-student{display:block}.paper-student .status-badge{display:inline-flex;margin-top:10px}.review-navigation .button{padding:8px 10px;font-size:10px}}
</style>
