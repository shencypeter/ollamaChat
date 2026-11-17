<script setup>
import { computed, nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { X } from '@lucide/vue'

const props = defineProps({
  open: Boolean,
  metrics: { type: Object, default: null },
  title: { type: String, default: 'AI 回覆效能' },
  modelName: { type: String, default: '' },
  promptVersion: { type: String, default: '' },
  canInspectRequests: Boolean,
  initialTab: { type: String, default: 'statistics' },
  requestInspection: { type: Object, default: null },
})
const emit = defineEmits(['close'])
const dialog = ref(null), closeButton = ref(null), titleId = useId()
const activeTab = ref('statistics'), copyStatus = ref('')
const requests = computed(() => props.canInspectRequests ? props.requestInspection?.requests || [] : [])
const requestJson = computed(() => requests.value.length ? JSON.stringify(requests.value.length===1 ? requests.value[0].payload : requests.value.map(request=>request.payload), null, 2) : '')
const pretty = value => JSON.stringify(value, null, 2)
async function copyRequest() {
  copyStatus.value = ''
  try { await navigator.clipboard.writeText(requestJson.value); copyStatus.value = '已複製 JSON' }
  catch { copyStatus.value = '無法自動複製，請選取下方 JSON 手動複製。' }
}
function chooseTab(value, event) {
  activeTab.value = value
  if (event) nextTick(() => dialog.value?.querySelector(`[data-tab="${value === 'request' ? 'request' : 'statistics'}"]`)?.focus())
}
function tabKey(event) {
  if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
  event.preventDefault()
  chooseTab(event.key === 'Home' ? 'statistics' : event.key === 'End' ? 'request' : activeTab.value === 'request' ? 'statistics' : 'request', event)
}
watch(() => props.canInspectRequests, value => { if (!value) { activeTab.value = 'statistics'; copyStatus.value = '' } })
let previousFocus = null, closeRequested = true
const finite = value => typeof value === 'number' && Number.isFinite(value) && value >= 0
const number = value => finite(value) ? String(value) : '未提供'
const seconds = (value, divisor) => finite(value) ? `${(value / divisor).toFixed(2)} 秒` : '未提供'
const rows = computed(() => {
  const m = props.metrics || {}
  const available = Array.isArray(m.availableFields) ? new Set(m.availableFields) : null
  const value = key => !available || available.has(key) ? m[key] : undefined
  return [
    ['輸入 Token', number(value('promptTokens'))], ['快取輸入 Token', number(value('cachedPromptTokens'))], ['輸出 Token', number(value('responseTokens'))],
    ['生成速度', finite(value('tokensPerSecond')) ? `${value('tokensPerSecond').toFixed(2)} tok/s` : '未提供'],
    ['往返時間', seconds(value('roundTripMilliseconds'), 1000)], ['模型處理時間', seconds(value('totalDurationNanoseconds'), 1e9)],
    ['模型載入時間', seconds(value('loadDurationNanoseconds'), 1e9)], ['輸入處理時間', seconds(value('promptEvalDurationNanoseconds'), 1e9)],
    ['生成時間', seconds(value('evalDurationNanoseconds'), 1e9)],
  ]
})
function restoreFocus() {
  if (previousFocus?.isConnected) previousFocus.focus()
  previousFocus = null
}
function hide() {
  if (dialog.value?.open) {
    if (typeof dialog.value.close === 'function') dialog.value.close()
    else dialog.value.removeAttribute('open')
  }
  restoreFocus()
}
function dismiss() {
  if (closeRequested) return
  closeRequested = true
  hide()
  emit('close')
}
function keyboard(event) {
  if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); dismiss() }
  if (event.key === 'Tab') {
    const focusable = [...dialog.value.querySelectorAll('button:not(:disabled),[tabindex="0"]')].filter(el => el.tabIndex >= 0 && !el.closest('[hidden]'))
    const first = focusable[0], last = focusable.at(-1)
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
  }
}
watch(() => props.open, async value => {
  if (!value) { closeRequested = true; hide(); return }
  await nextTick()
  if (!props.open || !dialog.value) return
  activeTab.value = props.canInspectRequests && props.initialTab === 'request' ? 'request' : 'statistics'; copyStatus.value = ''
  closeRequested = false
  previousFocus = document.activeElement
  if (!dialog.value.open) {
    try {
      if (typeof dialog.value.showModal === 'function') dialog.value.showModal()
      else dialog.value.setAttribute('open', '')
    } catch { dialog.value.setAttribute('open', '') }
  }
  closeButton.value?.focus()
}, { immediate: true })
onBeforeUnmount(() => { closeRequested = true; hide() })
</script>

<template>
  <dialog ref="dialog" class="chat-performance-dialog" :aria-labelledby="titleId" aria-modal="true" @cancel.prevent="dismiss" @close="dismiss" @keydown="keyboard" @click="event => event.target === dialog && dismiss()">
    <section class="chat-performance-panel">
      <header><h2 :id="titleId">{{ title }}</h2><button ref="closeButton" type="button" class="button secondary" aria-label="關閉效能資訊" @click="dismiss"><X :size="20" aria-hidden="true" /></button></header>
      <div v-if="canInspectRequests" class="inspection-tabs" role="tablist" aria-label="回覆資訊" @keydown="tabKey">
        <button :id="`${titleId}-statistics-tab`" type="button" role="tab" data-tab="statistics" :aria-selected="activeTab==='statistics'" :aria-controls="`${titleId}-statistics`" :tabindex="activeTab==='statistics'?0:-1" @click="chooseTab('statistics')">統計</button>
        <button :id="`${titleId}-request-tab`" type="button" role="tab" data-tab="request" :aria-selected="activeTab==='request'" :aria-controls="`${titleId}-request`" :tabindex="activeTab==='request'?0:-1" @click="chooseTab('request')">Ollama 請求</button>
      </div>
      <div :id="`${titleId}-statistics`" :role="canInspectRequests?'tabpanel':undefined" :aria-labelledby="canInspectRequests?`${titleId}-statistics-tab`:undefined" :hidden="activeTab!=='statistics'">
      <p v-if="modelName" class="muted">模型：{{ modelName }}</p>
      <p v-if="promptVersion" class="muted">提示版本：{{ promptVersion }}</p>
      <p v-if="!metrics" role="status">此回覆未提供效能資料；較早的紀錄可能尚未記錄這些數據。</p>
      <template v-else>
        <dl><div v-for="[label, value] in rows" :key="label"><dt>{{ label }}</dt><dd>{{ value }}</dd></div></dl>
        <p v-if="metrics.providerCalls>1" class="chat-performance-note" data-provider-calls>模型呼叫 {{metrics.providerCalls}} 次，包含一次評語重整；Token 與耗時為合計，正式回饋輪次仍為一次。</p>
        <p v-if="metrics.doneReason" class="muted">結束原因：{{ metrics.doneReason }}</p>
        <p class="chat-performance-note">往返時間為應用程式到模型服務的本次請求時間，不包含完整頁面與資料庫流程；模型耗時依本次回傳資料呈現。未提供的數值不推算為 0。</p>
      </template>
      </div>
      <div v-if="canInspectRequests" :id="`${titleId}-request`" role="tabpanel" :aria-labelledby="`${titleId}-request-tab`" :hidden="activeTab!=='request'">
        <p class="chat-performance-note">僅顯示本頁新回覆實際送出的 Ollama 請求；只保存在本頁記憶體，重新整理或離開後即清除。歷史回覆不會依目前設定重建。</p>
        <p v-if="!requests.length" role="status">未擷取此回覆的 Ollama 請求。較早的回覆、重新整理後的歷史紀錄或未提供擷取資料的回覆，無法檢視原始請求。</p>
        <template v-else>
          <p v-if="requestInspection.promptVersion" class="muted">提示版本：{{ requestInspection.promptVersion }}</p>
          <article v-for="(request,index) in requests" :key="index" class="request-detail">
            <h3>請求 {{ index+1 }}<span v-if="request.purpose"> · {{ request.purpose }}</span></h3>
            <p class="muted">端點：{{ request.url }}</p><p>實際模型：{{ request.payload?.model || '未提供' }}</p>
            <h4>訊息與提示詞</h4>
            <template v-if="typeof request.payload?.prompt==='string'"><p class="chat-performance-note">完整 prompt（含模型角色標記）；Raw 不會因此增加應用程式 System Prompt。</p><pre tabindex="0" data-generated-prompt :aria-label="`請求 ${index+1} 完整 prompt`">{{request.payload.prompt}}</pre></template>
            <section v-for="(message,messageIndex) in request.payload?.messages||[]" :key="messageIndex" class="request-message"><h5>{{ messageIndex+1 }} · {{ message.role }}</h5><pre tabindex="0" :aria-label="`請求 ${index+1} 訊息 ${messageIndex+1}`">{{ typeof message.content==='string'?message.content:pretty(message.content) }}</pre></section>
            <template v-if="request.payload?.format!==undefined"><h4>回覆格式（format）</h4><pre tabindex="0" aria-label="回覆格式">{{ pretty(request.payload.format) }}</pre></template>
            <template v-if="request.payload?.options!==undefined"><h4>模型選項（options）</h4><pre tabindex="0" aria-label="模型選項">{{ pretty(request.payload.options) }}</pre></template>
          </article>
          <div class="request-json-heading"><h3>{{ requests.length===1?'完整請求 JSON（HTTP body）':'各請求 payload JSON 陣列' }}</h3><button type="button" class="button secondary" data-action="copy-request" @click="copyRequest">{{ requests.length===1?'複製 JSON':'複製 payload 陣列' }}</button></div><p v-if="requests.length>1" class="chat-performance-note">依上方請求順序列出各次 HTTP body；此陣列用於檢視，單次呼叫請使用其中一個 payload。</p><p v-if="copyStatus" role="status">{{ copyStatus }}</p>
          <pre tabindex="0" data-request-json :aria-label="requests.length===1?'完整 Ollama 請求 JSON HTTP body':'各請求 payload JSON 陣列'">{{ requestJson }}</pre>
        </template>
      </div>
    </section>
  </dialog>
</template>

<style scoped>
.chat-performance-dialog{width:min(860px,calc(100vw - 24px));max-width:calc(100vw - 24px);max-height:calc(100dvh - 32px);padding:0;border:1px solid var(--border,#d7dce4);border-radius:16px;color:var(--text,#172033);background:var(--surface,#fff);box-sizing:border-box;overflow:auto}
.chat-performance-dialog::backdrop{background:rgb(15 23 42 / .55)}
.chat-performance-panel{padding:20px;min-width:0;overflow-wrap:anywhere}
header{display:flex;justify-content:space-between;align-items:center;gap:12px}h2{font-size:1.2rem;margin:0}header button{flex:none;min-width:44px;min-height:44px;padding:8px}
dl{margin:20px 0;display:grid;gap:0}dl>div{display:flex;justify-content:space-between;gap:16px;padding:10px 0;border-bottom:1px solid var(--border,#e2e8f0)}dt{color:var(--muted,#526176)}dd{margin:0;text-align:right;font-variant-numeric:tabular-nums}.chat-performance-note{font-size:.88rem;color:var(--muted,#526176);line-height:1.6}
.inspection-tabs{display:flex;gap:8px;border-bottom:1px solid var(--border,#d7dce4);margin:16px 0}.inspection-tabs button{min-height:44px;padding:8px 16px;border:0;border-bottom:3px solid transparent;background:transparent;color:inherit;cursor:pointer}.inspection-tabs button[aria-selected=true]{border-bottom-color:var(--accent,#5b5fc7);font-weight:700}.request-detail{border-top:1px solid var(--border,#d7dce4);margin-top:20px;padding-top:4px}.request-detail h3,.request-json-heading h3{font-size:1rem}.request-detail h4{font-size:.94rem;margin:18px 0 8px}.request-message h5{font-size:.88rem;margin:12px 0 6px}pre{white-space:pre-wrap;overflow-wrap:anywhere;word-break:break-word;overflow:auto;max-height:340px;background:var(--surface-alt,var(--surface,#f5f7fa));color:inherit;border:1px solid var(--border,#d7dce4);border-radius:8px;padding:12px;font-size:.85rem;line-height:1.6;box-sizing:border-box;max-width:100%;tab-size:2}pre:focus-visible,.inspection-tabs button:focus-visible{outline:2px solid var(--accent,#5b5fc7);outline-offset:2px}.request-json-heading{display:flex;gap:12px;align-items:center;justify-content:space-between;flex-wrap:wrap}.request-json-heading button{min-height:44px}
@media(max-width:440px){.chat-performance-panel{padding:16px}dl>div{gap:10px}dt,dd{font-size:.9rem}}
</style>
