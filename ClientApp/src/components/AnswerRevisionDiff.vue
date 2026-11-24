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
const columns = computed(() => [
  { key: 'left', label: '基準版本', revision: left.value, segments: difference.value.segments.filter(segment => segment.type !== 'add') },
  { key: 'right', label: '比較版本', revision: right.value, segments: difference.value.segments.filter(segment => segment.type !== 'remove') },
])
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
  const controls = [...dialog.value.querySelectorAll('button, select, [tabindex="0"]')].filter(element => !element.disabled)
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
      <p class="muted">選擇兩份作答，左右閱讀完整版本；標色處是內容差異，也可以交換比較方向。</p>
      <div class="answer-diff-selectors">
        <label>基準版本<select v-model="leftId" aria-label="基準版本"><option v-for="revision in revisions" :key="revision.id" :value="String(revision.id)">{{ revision.label }}</option></select></label>
        <label>比較版本<select v-model="rightId" aria-label="比較版本"><option v-for="revision in revisions" :key="revision.id" :value="String(revision.id)">{{ revision.label }}</option></select></label>
      </div>
      <template v-if="opened && left && right">
        <div class="answer-diff-legend" aria-label="差異圖例"><span class="removed">基準版本刪除 {{ count('remove') }} 字</span><span class="added">比較版本新增 {{ count('add') }} 字</span><span>未標記：相同文字</span></div>
        <p v-if="difference.unchanged" role="status">兩個版本內容相同。</p>
        <p v-if="difference.fallback" role="status">差異範圍較大，已保留相同的開頭與結尾，其餘差異以整段標色；兩欄皆保留完整文字。</p>
        <p :id="`${titleId}-scroll-hint`" class="comparison-narrow-note muted">左右滑動查看兩份完整作答。</p>
        <div class="answer-diff-scroll" data-diff-scroll tabindex="0" role="region" aria-label="左右並排作答比較" :aria-describedby="`${titleId}-scroll-hint`">
          <div class="answer-diff-columns" data-diff-output>
            <article v-for="column in columns" :key="column.key" class="answer-diff-version" :aria-labelledby="`${titleId}-${column.key}`">
              <header class="version-heading"><span class="muted">{{column.label}}</span><h3 :id="`${titleId}-${column.key}`">{{column.revision.label}}</h3></header>
              <p class="answer-diff-text" :data-diff-side="column.key"><template v-for="(segment,index) in column.segments" :key="index"><span v-if="segment.type==='equal'">{{segment.text}}</span><mark v-else :data-diff-change="segment.type">{{segment.text}}</mark></template></p>
            </article>
          </div>
        </div>
      </template>
    </section>
  </dialog>
</template>

<style scoped>
.answer-diff-trigger{display:inline-flex;align-items:center;gap:6px}
.answer-diff-dialog{--diff-removed-bg:#fee2e2;--diff-removed-text:#852020;--diff-added-bg:#dcfce7;--diff-added-text:#14532d;width:min(1280px,calc(100vw - 24px));max-width:calc(100vw - 24px);max-height:calc(100dvh - 32px);padding:0;border:1px solid var(--border,#d7dce4);border-radius:16px;color:var(--text,#172033);background:var(--surface,#fff);box-sizing:border-box;overflow:auto}.answer-diff-dialog::backdrop{background:rgb(15 23 42 / .55)}
.answer-diff-panel{padding:20px;min-width:0;overflow-wrap:anywhere}header{display:flex;justify-content:space-between;align-items:center;gap:12px}h2{font-size:1.2rem;margin:0}header button{flex:none;min-width:44px;min-height:44px;padding:8px}
.answer-diff-selectors{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:14px}.answer-diff-selectors label{display:flex;flex-direction:column;gap:6px;min-width:0}.answer-diff-selectors select{width:100%;max-width:100%;min-width:0;min-height:44px;box-sizing:border-box}
.answer-diff-legend{display:flex;flex-wrap:wrap;gap:12px;font-size:.9rem;margin:16px 0}.removed,.answer-diff-text mark[data-diff-change=remove]{background:var(--diff-removed-bg);color:var(--diff-removed-text);text-decoration:none}.added,.answer-diff-text mark[data-diff-change=add]{background:var(--diff-added-bg);color:var(--diff-added-text);text-decoration:none}
.answer-diff-scroll{max-width:100%;overflow-x:auto;overscroll-behavior-x:contain;border-radius:10px}.answer-diff-scroll:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.answer-diff-columns{display:grid;grid-template-columns:repeat(2,minmax(18rem,1fr));gap:16px;padding:2px;align-items:stretch}.answer-diff-version{min-width:0;border:1px solid var(--line,#d7dce4);border-radius:10px;padding:16px;background:var(--surface,#fff)}.version-heading{display:block;border-bottom:1px solid var(--line,#d7dce4);padding-bottom:12px}.version-heading h3{margin:6px 0 0;font-size:1rem;font-weight:600}.answer-diff-text{white-space:pre-wrap;overflow-wrap:anywhere;word-break:break-word;tab-size:4;line-height:1.9;min-height:160px;margin:14px 0 0;font-family:inherit;unicode-bidi:plaintext}.answer-diff-text mark{white-space:pre-wrap;border-radius:2px;padding:0}.comparison-narrow-note{display:none;font-size:.85rem}
:global(:root[data-theme=dark] .answer-diff-dialog){--diff-removed-bg:#50272e;--diff-removed-text:#ffd0d6;--diff-added-bg:#193d31;--diff-added-text:#b4eed0}
@media(max-width:650px){.answer-diff-panel{padding:16px}.answer-diff-selectors{grid-template-columns:minmax(0,1fr)}.comparison-narrow-note{display:block}}
</style>
