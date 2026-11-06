<script setup>
import { ref, computed, watch } from 'vue'
import { api } from '../api'
import AssessmentResult from './AssessmentResult.vue'
import { formatDate } from '../utils/assignments'
import { feedbackIsStale } from '../utils/formative'
const props = defineProps({ assignmentId: String, question: Object, answer: Object, editorText: String, version: Number, eligible: Boolean })
const emit = defineEmits(['feedback'])
const latest = ref(null), loading = ref(false), asking = ref(false), error = ref('')
const unsaved = computed(() => (props.editorText || '') !== (props.answer?.text || ''))
const stale = computed(() => feedbackIsStale(latest.value, props.answer?.currentRevisionId, props.editorText || '', props.answer?.text || ''))
const canAsk = computed(() => props.eligible && props.question.stageCode && props.answer?.currentRevisionId && props.answer.text.trim() && !unsaved.value && !asking.value)
const path = computed(() => `/assignments/${props.assignmentId}/questions/${props.question.id}/ai-feedback`)
watch(latest, value => emit('feedback', value))
let generation = 0
async function load() { const id = ++generation; loading.value = true; error.value = ''; try { const values = await api(path.value); if (id === generation && !asking.value) latest.value = values[0] || null } catch (e) { if (id === generation) error.value = '暫時無法載入 AI 回饋；仍可儲存或提交作答。' } finally { if (id === generation) loading.value = false } }
watch(() => props.answer?.currentRevisionId, load, { immediate: true })
async function ask() {
  if (!canAsk.value) return
  generation++; asking.value = true; error.value = ''
  try { latest.value = await api(path.value, { method: 'POST', body: { expectedVersion: props.version, answerRevisionId: props.answer.currentRevisionId } }) }
  catch (e) { error.value = e.status === 409 ? e.message : 'AI 回饋暫時無法完成，可能逾時或連線中斷。已儲存草稿仍保留，可直接提交給教師／助教。' }
  finally { asking.value = false; loading.value = false }
}
</script>
<template><section class="formative-feedback" aria-label="AI 形成性練習回饋"><h3>AI 形成性回饋 · 私人練習</h3><p class="field-hint">不是正式成績，AI 可能出錯。回饋只供你本人閱讀，教師／助教不會自動取得；正式評量由教師／助教執行。</p><button type="button" class="button secondary" :disabled="!canAsk" @click="ask">{{ asking ? 'AI 正在提供回饋…' : '請 AI 提供回饋' }}</button><p v-if="unsaved" class="submission-notice">此題有尚未儲存的修改。請先按「儲存草稿」，再要求 AI 評閱相同版本。</p><p v-else-if="!answer?.currentRevisionId" class="muted">請先儲存此題草稿，再要求 AI 回饋。</p><p v-if="!question.stageCode" class="muted">本題尚未設定分析階段，暫不提供 AI 回饋；仍可正常提交。</p><p v-if="asking" role="status">正在評閱已儲存版本；你仍可修改、儲存或直接提交。這不會改動正式成績。</p><p v-else-if="loading" role="status">載入私人 AI 回饋中…</p><p v-if="error" class="error-box" role="alert">{{ error }}<button type="button" class="text-button" @click="load">重新載入回饋</button></p><template v-if="latest"><p class="formative-version" :class="{ stale }" role="status">{{ stale ? '已過期：此回饋不適用於目前草稿' : latest.status === 'completed' ? '此回饋對應目前已儲存版本' : '尚無完成回饋：此請求對應目前已儲存版本' }} · 答案版本 {{ latest.revisionNo }}</p><p class="muted">{{ formatDate(latest.createdAt) }} · {{ latest.model }} · {{ latest.profile }} · {{ latest.promptVersion }}<br />版本識別：{{ latest.answerRevisionId }}</p><p v-if="latest.status === 'failed'" class="error-box" role="alert">{{ latest.error }}</p><p v-else-if="latest.status === 'pending'" class="submission-notice">此回饋尚未完成。可重新請求或直接提交，不影響草稿。</p><div v-else-if="latest.assessment" :class="{ 'outdated-feedback': stale }"><p class="field-hint">以下 1–4 分僅為 AI 練習參考，不是作業正式分數。</p><AssessmentResult :evaluation="latest.assessment.evaluations[0]" /></div></template></section></template>
