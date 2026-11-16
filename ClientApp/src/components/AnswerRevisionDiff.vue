<script setup>
import { computed, nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { X, GitCompareArrows } from '@lucide/vue'
import { diffAnswers } from '../utils/answerDiff'

const props = defineProps({ revisions: { type: Array, default: () => [] } })
const dialog = ref(null), closeButton = ref(null), opened = ref(false), leftId = ref(''), rightId = ref('')
const titleId = useId()
let previousFocus = null
const find = id => props.revisions.find(revision => String(revision.id) === id)
const left = computed(() => find(leftId.value)), right = computed(() => find(rightId.value))
const difference = computed(() => diffAnswers(left.value?.content ?? '', right.value?.content ?? ''))
const count = type => difference.value.segments.filter(s => s.type === type).reduce((n, s) => n + Array.from(s.text).length, 0)
function ensureSelection() {
  if (!find(leftId.value)) leftId.value = String(props.revisions[0]?.id ?? '')
  if (!find(rightId.value)) rightId.value = String(props.revisions.at(-1)?.id ?? '')
}
watch(() => props.revisions, () => { ensureSelection(); if (props.revisions.length < 2) dismiss() })
async function show() {
  if (props.revisions.length < 2) return
  ensureSelection()
  previousFocus = document.activeElement
  opened.value = true
  await nextTick()
  if (!opened.value || !dialog.value) return
  try {
    if (typeof dialog.value.showModal === 'function') dialog.value.showModal()
    else dialog.value.setAttribute('open', '')
  } catch { dialog.value.setAttribute('open', '') }
  closeButton.value?.focus()
}
function dismiss() {
  opened.value = false
  if (dialog.value?.open) {
    if (typeof dialog.value.close === 'function') dialog.value.close()
    else dialog.value.removeAttribute('open')
  }
  if (previousFocus?.isConnected) previousFocus.focus()
  previousFocus = null
}
function keyboard(event) {
  if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); dismiss(); return }
  if (event.key !== 'Tab') return
  const controls = [...dialog.value.querySelectorAll('button, select')].filter(element => !element.disabled)
  const first = controls[0], last = controls.at(-1)
  if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
  else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
}
onBeforeUnmount(dismiss)
</script>

<template>
  <button class="button secondary answer-diff-trigger" type="button" :disabled="revisions.length < 2" @click="show"><GitCompareArrows :size="17" aria-hidden="true" /> 比較作答版本</button>
  <dialog ref="dialog" class="answer-diff-dialog" :aria-labelledby="titleId" aria-modal="true" @cancel.prevent="dismiss" @close="dismiss" @keydown="keyboard" @click="event => event.target === dialog && dismiss()">
    <section class="answer-diff-panel">
      <header><h2 :id="titleId">比較作答版本</h2><button ref="closeButton" type="button" class="button secondary" aria-label="關閉作答版本比較" @click="dismiss"><X :size="20" aria-hidden="true" /></button></header>
      <p class="muted">選擇任意兩次提問／作答，比較文字的新增與刪除；也可以交換版本的比較方向。</p>
      <div class="answer-diff-selectors">
        <label>較早版本<select v-model="leftId" aria-label="較早版本"><option v-for="revision in revisions" :key="revision.id" :value="String(revision.id)">{{ revision.label }}</option></select></label>
        <label>較新版本<select v-model="rightId" aria-label="較新版本"><option v-for="revision in revisions" :key="revision.id" :value="String(revision.id)">{{ revision.label }}</option></select></label>
      </div>
      <template v-if="opened && left && right">
        <p class="answer-diff-labels">{{ left.label }} → {{ right.label }}</p>
        <div class="answer-diff-legend" aria-label="差異圖例"><span class="removed">− 刪除 {{ count('remove') }} 字</span><span class="added">＋ 新增 {{ count('add') }} 字</span><span>未標記：相同文字</span></div>
        <p v-if="difference.unchanged" role="status">兩個版本內容相同。</p>
        <p v-if="difference.fallback" role="status">差異範圍較大，已保留相同的開頭與結尾，其餘區段以整段刪除／新增顯示；完整文字皆保留。</p>
        <div class="answer-diff-output" data-diff-output aria-label="作答文字差異"><template v-for="(segment, index) in difference.segments" :key="index"><del v-if="segment.type === 'remove'">{{ segment.text }}</del><ins v-else-if="segment.type === 'add'">{{ segment.text }}</ins><span v-else>{{ segment.text }}</span></template></div>
      </template>
    </section>
  </dialog>
</template>

<style scoped>
.answer-diff-trigger{display:inline-flex;align-items:center;gap:6px}
.answer-diff-dialog{width:min(920px,calc(100vw - 24px));max-width:calc(100vw - 24px);max-height:calc(100dvh - 32px);padding:0;border:1px solid var(--border,#d7dce4);border-radius:16px;color:var(--text,#172033);background:var(--surface,#fff);box-sizing:border-box;overflow:auto}.answer-diff-dialog::backdrop{background:rgb(15 23 42 / .55)}
.answer-diff-panel{padding:20px;min-width:0;overflow-wrap:anywhere}header{display:flex;justify-content:space-between;align-items:center;gap:12px}h2{font-size:1.2rem;margin:0}header button{flex:none;min-width:44px;min-height:44px;padding:8px}
.answer-diff-selectors{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:14px}.answer-diff-selectors label{display:flex;flex-direction:column;gap:6px;min-width:0}.answer-diff-selectors select{width:100%;max-width:100%;min-width:0;min-height:44px;box-sizing:border-box}.answer-diff-labels{font-weight:600}
.answer-diff-legend{display:flex;flex-wrap:wrap;gap:12px;font-size:.9rem;margin:16px 0}.removed,.answer-diff-output del{background:#fee2e2;color:#852020;text-decoration:line-through}.added,.answer-diff-output ins{background:#dcfce7;color:#14532d;text-decoration:underline}.answer-diff-output{white-space:pre-wrap;overflow-wrap:anywhere;word-break:break-word;tab-size:4;line-height:1.9;border:1px solid var(--border,#d7dce4);border-radius:10px;padding:14px;min-height:80px;font-family:inherit;unicode-bidi:plaintext}.answer-diff-output del,.answer-diff-output ins{white-space:pre-wrap}
@media(max-width:520px){.answer-diff-panel{padding:16px}.answer-diff-selectors{grid-template-columns:minmax(0,1fr)}.answer-diff-output{padding:10px}}
</style>
