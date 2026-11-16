<script setup>
import { computed, nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { X } from '@lucide/vue'

const props = defineProps({
  open: Boolean,
  metrics: { type: Object, default: null },
  title: { type: String, default: 'AI 回覆效能' },
  modelName: { type: String, default: '' },
  promptVersion: { type: String, default: '' },
})
const emit = defineEmits(['close'])
const dialog = ref(null), closeButton = ref(null), titleId = useId()
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
  if (event.key === 'Tab') { event.preventDefault(); closeButton.value?.focus() }
}
watch(() => props.open, async value => {
  if (!value) { closeRequested = true; hide(); return }
  await nextTick()
  if (!props.open || !dialog.value) return
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
      <p v-if="modelName" class="muted">模型：{{ modelName }}</p>
      <p v-if="promptVersion" class="muted">提示版本：{{ promptVersion }}</p>
      <p v-if="!metrics" role="status">此回覆未提供效能資料；較早的紀錄可能尚未記錄這些數據。</p>
      <template v-else>
        <dl><div v-for="[label, value] in rows" :key="label"><dt>{{ label }}</dt><dd>{{ value }}</dd></div></dl>
        <p v-if="metrics.doneReason" class="muted">結束原因：{{ metrics.doneReason }}</p>
        <p class="chat-performance-note">往返時間為應用程式到模型服務的本次請求時間，不包含完整頁面與資料庫流程；模型耗時依本次回傳資料呈現。未提供的數值不推算為 0。</p>
      </template>
    </section>
  </dialog>
</template>

<style scoped>
.chat-performance-dialog{width:min(580px,calc(100vw - 24px));max-width:calc(100vw - 24px);max-height:calc(100dvh - 32px);padding:0;border:1px solid var(--border,#d7dce4);border-radius:16px;color:var(--text,#172033);background:var(--surface,#fff);box-sizing:border-box;overflow:auto}
.chat-performance-dialog::backdrop{background:rgb(15 23 42 / .55)}
.chat-performance-panel{padding:20px;min-width:0;overflow-wrap:anywhere}
header{display:flex;justify-content:space-between;align-items:center;gap:12px}h2{font-size:1.2rem;margin:0}header button{flex:none;min-width:44px;min-height:44px;padding:8px}
dl{margin:20px 0;display:grid;gap:0}dl>div{display:flex;justify-content:space-between;gap:16px;padding:10px 0;border-bottom:1px solid var(--border,#e2e8f0)}dt{color:var(--muted,#526176)}dd{margin:0;text-align:right;font-variant-numeric:tabular-nums}.chat-performance-note{font-size:.88rem;color:var(--muted,#526176);line-height:1.6}
@media(max-width:440px){.chat-performance-panel{padding:16px}dl>div{gap:10px}dt,dd{font-size:.9rem}}
</style>
