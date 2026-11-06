<script setup>
import { computed, nextTick, onMounted, onBeforeUnmount, reactive, ref } from 'vue'
import { Sparkles, FilePenLine, GitCompareArrows, MessagesSquare, ArrowUpRight, Send, ShieldCheck, Activity, Trash2, Check, LoaderCircle, ChevronDown } from '@lucide/vue'
import { api } from '../api'
import { givenName } from '../utils/names'
import { experience } from '../composables/experience'
import { auth } from '../composables/auth'
import OllamaRequestViewer from '../components/OllamaRequestViewer.vue'
import AssessmentResult from '../components/AssessmentResult.vue'
const tab = ref('review'), stage = ref('premorbid'), busy = ref(false), error = ref(''), config = ref(null), health = ref(null), checking = ref(false)
const forms = reactive({}), results = reactive({}), messages = ref([]), chatInput = ref(''), metrics = ref(null), memory = ref('Window'), rubricOpen = ref(false)
const inspection = ref(null)
const canInspect = computed(() => auth.user?.canTeach && !experience.student)
const conversation = ref(null)
const controller = new AbortController()
const stages = computed(() => config.value?.stages.filter(s => s.key !== 'generalChat') || [])
const formKey = computed(() => `${tab.value}:${stage.value}`)
const form = computed(() => forms[formKey.value] ||= { message: '', beforeMessage: '', caseFacts: '' })
const result = computed(() => results[formKey.value])
const stageLabel = computed(() => stages.value.find(s => s.key === stage.value)?.label || '病前功能')
const rubric = ['分析不足，有重大誤解，或幾乎未提供相關證據。', '僅完成部分分析，有重大遺漏，或提出缺乏證據支持的結論。', '分析大致正確，但有少量遺漏，或部分論點的支持證據不足。', '分析完整，能準確引用案例證據支持論點，且臨床推理適切。']
onMounted(async () => {
  try { config.value = await api('/ai/configuration', { signal: controller.signal }); messages.value = config.value.messages; memory.value = config.value.memoryStrategy } catch(e) { if(e.name !== 'AbortError') error.value = e.message }
})
onBeforeUnmount(() => controller.abort())
async function assess() {
  if (busy.value) return
  const key = formKey.value, payload = { ...form.value, stage: stage.value, compare: tab.value === 'compare' }
  if (!payload.message.trim() || (payload.compare && !payload.beforeMessage.trim())) { error.value = '請填寫本次作答；比較時需同時提供兩個版本。'; return }
  busy.value = true; error.value = ''; inspection.value = null
  try { const response = await api('/ai/send', { method: 'POST', body: payload, signal: controller.signal }); results[key] = response; metrics.value = response.metrics; inspection.value = response.requestInspection || null } catch(e) { if (e.name !== 'AbortError') { error.value = e.message; inspection.value = e.requestInspection || null } } finally { busy.value = false }
}
async function sendChat() {
  const text = chatInput.value.trim(); if (!text || busy.value) return
  busy.value = true; error.value = ''; inspection.value = null
  try {
    const response = await api('/ai/send', { method: 'POST', body: { message: text, stage: 'generalChat', compare: false }, signal: controller.signal })
    messages.value.push({ role: 'user', content: text, timestamp: new Date().toISOString() }, { ...response.message, renderedContent: response.renderedContent }); chatInput.value = ''; metrics.value = response.metrics; inspection.value = response.requestInspection || null
    await nextTick(); conversation.value?.scrollTo({ top: conversation.value.scrollHeight, behavior: 'smooth' })
  } catch(e) { if (e.name !== 'AbortError') { error.value = e.message; inspection.value = e.requestInspection || null } } finally { busy.value = false }
}
async function changeMemory() { error.value = ''; try { await api('/ai/memory', { method: 'POST', body: { strategy: memory.value } }); config.value.memoryStrategy = memory.value } catch(e) { error.value = e.message; memory.value = config.value.memoryStrategy } }
async function clearChat() { if (!window.confirm('清除這個帳號目前的一般對話？')) return; try { await api('/ai/clear', { method: 'POST' }); messages.value = []; inspection.value = null } catch(e) { error.value = e.message } }
async function checkHealth() { checking.value = true; error.value = ''; try { health.value = await api('/ai/health') } catch(e) { health.value = { online: false }; error.value = e.message } finally { checking.value = false } }
function ns(value) { return `${((value || 0) / 1e9).toFixed(2)} 秒` }
</script>
<template>
  <section class="page-heading"><div><p class="eyebrow">AI TEACHING PORTAL</p><h1>把思考說清楚，讓回饋更有依據。</h1><p class="muted">案例分析、版本比較與 AI 對話，都在這裡。</p></div><button class="button secondary" :disabled="checking" @click="checkHealth"><Activity :size="16" /><span v-if="checking">檢查中…</span><span v-else>{{ health ? health.online ? '模型連線正常' : '模型無法連線' : '檢查模型連線' }}</span></button></section>
  <div class="portal-layout"><section class="portal-primary"><div class="portal-card"><header class="portal-card-header"><span class="tool-icon small violet"><Sparkles :size="20" /></span><div><strong>AI 助教工作室</strong><small>獨立練習 · 不計入正式成績</small></div><span class="model-pill">{{ config?.model || '連線中' }}</span></header>
    <div class="portal-tabs" role="tablist" aria-label="AI 工作模式"><button v-for="item in [{key:'review',label:'助教評閱',icon:FilePenLine},{key:'compare',label:'版本比較',icon:GitCompareArrows},{key:'chat',label:'一般對話',icon:MessagesSquare}]" :id="`tab-${item.key}`" :key="item.key" role="tab" :aria-selected="tab === item.key" :aria-controls="`panel-${item.key}`" :disabled="busy" :class="{ active: tab === item.key }" @click="tab = item.key"><component :is="item.icon" :size="17" />{{ item.label }}</button></div>
    <div v-if="tab !== 'chat'" :id="`panel-${tab}`" role="tabpanel" :aria-labelledby="`tab-${tab}`" class="assessment-workspace"><form @submit.prevent="assess"><fieldset :disabled="busy || !config"><div class="form-section-heading"><div><h2>{{ tab === 'compare' ? '看見修改前後的差異' : '提交你的案例分析' }}</h2><p>{{ tab === 'compare' ? '兩個版本分別評分，改善與退步都會據實呈現。' : '先辨識證據，再檢查推理，最後依規準評分。' }}</p></div><label class="stage-picker">分析階段<select v-model="stage"><option v-for="s in stages" :key="s.key" :value="s.key">{{ s.label }}</option></select></label></div>
      <label class="field-label">案例參考資料 <span class="optional">選填</span><textarea v-model="form.caseFacts" rows="4" maxlength="24000" placeholder="貼上本次階段的原始案例資料，作為核對證據與遺漏的依據。"></textarea></label><p class="field-hint"><ShieldCheck :size="14" />留空時，事實性轉述不會被當作已核實證據，評分將標示為暫定。</p>
      <div :class="{ 'compare-inputs': tab === 'compare' }"><label v-if="tab === 'compare'" class="field-label"><span class="version-label">01 <b>修改前版本</b></span><textarea v-model="form.beforeMessage" required rows="8" maxlength="8000" placeholder="貼上修改前的學生作答…"></textarea></label><label class="field-label"><span v-if="tab === 'compare'" class="version-label">02 <b>修改後版本</b></span><span v-else>學生作答 <span class="required-marker">*</span></span><textarea v-model="form.message" required rows="8" maxlength="8000" :placeholder="`貼上${stageLabel}的${tab === 'compare' ? '修改後' : '目前'}作答…`"></textarea><span class="char-count">{{ form.message.length }} / 8,000</span></label></div>
      <div class="form-bottom"><span><ShieldCheck :size="15" />只評閱本次輸入，不帶入先前作答。</span><button class="button primary" type="submit" :disabled="busy"><LoaderCircle v-if="busy" class="spin" :size="17" /><Sparkles v-else :size="17" />{{ busy ? 'AI 正在評閱…' : tab === 'compare' ? '比較兩個版本' : '開始評閱' }}</button></div></fieldset></form></div>
    <div v-else id="panel-chat" role="tabpanel" aria-labelledby="tab-chat" class="chat-workspace"><div class="chat-toolbar"><span><MessagesSquare :size="16" />與 AI 的學習對話</span><label>記憶<select v-model="memory" :disabled="busy || !config" @change="changeMemory"><option value="None">無記憶</option><option value="Window">滑動視窗</option><option value="Buffer">完整緩衝</option><option value="Summary">摘要記憶</option></select></label><button class="icon-button" aria-label="清除一般對話" :disabled="busy || !messages.length" @click="clearChat"><Trash2 :size="16" /></button></div>
      <div ref="conversation" class="conversation" aria-live="polite"><div v-if="!messages.length" class="chat-empty"><span class="tool-icon violet"><MessagesSquare :size="28" /></span><h3>從一個好問題開始</h3><p>這是一般 AI 對話，不套用四階段評閱規準。<br>你的對話與其他帳號分開。</p></div><article v-for="(message, index) in messages" :key="index" class="conversation-message"><span class="message-avatar" :class="{ assistant: message.role === 'assistant' }"><Sparkles v-if="message.role === 'assistant'" :size="18" /><template v-else>{{ Array.from(givenName(auth.user.displayName)).slice(0,2).join('') }}</template></span><div><header><strong>{{ message.role === 'assistant' ? 'AI 助教' : givenName(auth.user.displayName) }}</strong><time>{{ new Date(message.timestamp).toLocaleTimeString('zh-TW', {hour:'2-digit',minute:'2-digit'}) }}</time></header><div v-if="message.role === 'assistant' && message.renderedContent" class="conversation-text markdown-body" v-html="message.renderedContent"></div><p v-else class="conversation-text">{{ message.content }}</p></div></article><div v-if="busy" class="pending-row"><LoaderCircle class="spin" :size="16" />AI 正在回應…</div></div>
      <form class="chat-composer" @submit.prevent="sendChat"><label class="sr-only" for="chat-message">傳送訊息</label><textarea id="chat-message" v-model="chatInput" @keydown.ctrl.enter.prevent="sendChat" @keydown.meta.enter.prevent="sendChat" :disabled="busy || !config" required maxlength="8000" rows="3" placeholder="輸入你的問題，開始一段有幫助的對話…"></textarea><div><span>Ctrl+Enter 傳送 · 不評分</span><button class="button primary" :disabled="busy || !chatInput.trim() || !config"><Send :size="16" />傳送訊息</button></div></form></div>
  </div><p v-if="error" class="error-box" role="alert">{{ error }}</p>
  <OllamaRequestViewer v-if="canInspect" :inspection="inspection" /><section v-if="tab !== 'chat'" class="results-section" aria-live="polite" :aria-busy="busy"><div class="section-title"><h2>評閱回饋</h2><span v-if="result" class="status-badge"><Check :size="13" />已完成本次評閱</span></div><div v-if="!result" class="feedback-placeholder"><span class="tool-icon neutral"><FilePenLine :size="24" /></span><h3>{{ busy ? '正在整理證據與推理…' : '你的回饋將顯示在這裡' }}</h3><p>每項結論都應有依據；分數只是回饋的起點。</p></div><template v-else><div class="evaluation-grid" :class="{ paired: result.assessment.evaluations.length === 2 }"><AssessmentResult v-for="evaluation in result.assessment.evaluations" :key="evaluation.version" :evaluation="evaluation" /></div><div v-if="result.assessment.comparisonFeedback" class="comparison-summary"><h3>版本比較摘要</h3><p>{{ result.assessment.comparisonFeedback }}</p></div></template></section>
  </section><aside class="portal-guidance"><section class="guidance-card"><div class="guidance-label"><ShieldCheck :size="17" />有依據的回饋</div><h3>證據先行，<br>分數在後。</h3><p>AI 會區分已支持、部分支持、缺乏支持與尚未核實的主張，再依分析品質評分。</p><div class="guidance-steps"><span><i>1</i>辨識案例中的相關事實</span><span><i>2</i>檢查論點與證據是否相稱</span><span><i>3</i>檢視遺漏、推論與矛盾</span><span><i>4</i>依規準給分並說明理由</span></div></section><section class="rubric-card"><button class="rubric-toggle" :aria-expanded="rubricOpen" @click="rubricOpen = !rubricOpen">1–4 分評分規準<ChevronDown :size="16" :class="{ rotated: rubricOpen }" /></button><ol v-if="rubricOpen" class="rubric-list"><li v-for="score in [4,3,2,1]" :key="score"><strong>{{ score }}</strong><p>{{ rubric[score-1] }}</p></li></ol><p v-else>查看各級分析品質的定義。</p></section><section v-if="metrics" class="telemetry-card"><span class="eyebrow">上一筆請求</span><dl><div><dt>模型處理</dt><dd>{{ ns(metrics.totalDurationNanoseconds) }}</dd></div><div><dt>往返時間</dt><dd>{{ (metrics.roundTripMilliseconds / 1000).toFixed(2) }} 秒</dd></div><div><dt>生成速度</dt><dd>{{ metrics.tokensPerSecond?.toFixed(1) }} tok/s</dd></div><div><dt>生成 Token</dt><dd>{{ metrics.responseTokens }}</dd></div></dl><details><summary>更多遙測</summary><dl><div><dt>模型載入</dt><dd>{{ ns(metrics.loadDurationNanoseconds) }}</dd></div><div><dt>提示詞評估</dt><dd>{{ ns(metrics.promptEvalDurationNanoseconds) }}</dd></div><div><dt>生成時間</dt><dd>{{ ns(metrics.evalDurationNanoseconds) }}</dd></div><div><dt>輸入 / 快取</dt><dd>{{ metrics.promptTokens }} / {{ metrics.cachedPromptTokens }}</dd></div></dl></details></section><p class="guidance-disclaimer">AI 可能誤判或產生不可靠的解釋。正式評量仍需由合格教師審閱。</p></aside></div>
</template>
