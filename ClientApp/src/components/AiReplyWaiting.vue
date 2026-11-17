<script setup>
import {computed,onBeforeUnmount,onMounted,ref,useId,watch} from 'vue'
import {ChevronRight,LoaderCircle,Pause,Play} from '@lucide/vue'
import TutorAvatar from './TutorAvatar.vue'

const props=defineProps({hints:{type:Array,default:()=>[]},formal:Boolean})
const disclosureId=useId(),hintIndex=ref(0),visibleCount=ref(0),paused=ref(false),reducedMotion=ref(false)
const hints=computed(()=>{
 const values=[...new Set(props.hints.filter(h=>typeof h==='string'&&h.trim()).map(h=>h.trim()))]
 return values.length?values:['正在等待模型回覆本次訊息。']
})
const currentHint=computed(()=>hints.value[hintIndex.value%hints.value.length])
const characters=computed(()=>Array.from(currentHint.value))
const visibleText=computed(()=>characters.value.slice(0,visibleCount.value).join(''))
let timer=null,media=null,mounted=false
function stop(){if(timer!==null)clearTimeout(timer);timer=null}
function schedule(){
 if(!mounted||paused.value||reducedMotion.value)return
 timer=setTimeout(()=>{
  timer=null
  if(visibleCount.value<characters.value.length)visibleCount.value++
  else{hintIndex.value=(hintIndex.value+1)%hints.value.length;visibleCount.value=0}
  schedule()
 },visibleCount.value<characters.value.length?40:1600)
}
function reset(){stop();hintIndex.value=0;visibleCount.value=paused.value||reducedMotion.value?characters.value.length:0;schedule()}
function togglePause(){paused.value=!paused.value;stop();schedule()}
function next(){stop();hintIndex.value=(hintIndex.value+1)%hints.value.length;visibleCount.value=paused.value||reducedMotion.value?characters.value.length:0;schedule()}
function motionChanged(event){reducedMotion.value=event.matches}
watch(()=>props.hints,reset,{deep:true})
watch(reducedMotion,()=>{stop();if(reducedMotion.value)visibleCount.value=characters.value.length;schedule()})
onMounted(()=>{
 mounted=true;media=window.matchMedia?.('(prefers-reduced-motion: reduce)')||null
 reducedMotion.value=media?.matches===true;media?.addEventListener?.('change',motionChanged);reset()
})
onBeforeUnmount(()=>{mounted=false;stop();media?.removeEventListener?.('change',motionChanged)})
</script>

<template>
 <div class="reply-loading" data-reply-loading role="status" aria-live="polite" :aria-describedby="disclosureId">
  <div class="loading-author"><TutorAvatar/><strong>{{formal?'AI 助教回饋':'AI 助教'}}</strong></div>
  <div class="reply-loading-content">
   <div class="waiting-status"><LoaderCircle class="reply-spinner" :size="22" aria-hidden="true"/><strong>{{formal?'AI 助教處理中…':'AI 助教正在回覆…'}}</strong></div>
   <div class="request-summary" aria-live="off" :data-current-request-hint="currentHint">
    <p class="summary-label">{{formal?'作答處理流程':'對話處理流程'}}</p>
    <p class="request-typewriter" aria-hidden="true"><span data-request-hint-text>{{visibleText}}</span><span class="stream-caret" :class="{'caret-paused':paused}" aria-hidden="true"/></p>
    <p class="sr-only">{{currentHint}}</p>
   </div>
   <div class="hint-controls" aria-live="off"><span class="hint-count">提示 {{hintIndex+1}} / {{hints.length}}</span><div class="hint-buttons"><button v-if="!reducedMotion" type="button" data-action="pause-hints" :aria-label="paused?'繼續提示輪播':'暫停提示輪播'" :aria-pressed="paused" @click="togglePause"><Play v-if="paused" :size="14" aria-hidden="true"/><Pause v-else :size="14" aria-hidden="true"/>{{paused?'繼續提示':'暫停提示'}}</button><button type="button" data-action="next-hint" aria-label="下一則處理提示" @click="next">下一則<ChevronRight :size="15" aria-hidden="true"/></button></div></div>
   <p :id="disclosureId" class="waiting-disclosure">以下為處理流程示意，並非模型即時進度。</p>
  </div>
 </div>
</template>

<style scoped>
.reply-loading{margin:16px 0;width:100%;box-sizing:border-box;min-width:0}.loading-author{display:flex;align-items:center;gap:12px;margin-bottom:16px}.reply-loading-content{max-width:680px;margin:0 auto;min-width:0}.waiting-status{display:flex;align-items:center;gap:10px}.reply-spinner{flex:none;color:var(--accent);animation:reply-spin 1s linear infinite}.request-summary{margin-top:18px}.summary-label,.hint-count,.waiting-disclosure{font-size:.75rem;color:var(--muted)}.summary-label{margin:0 0 8px}.request-typewriter{margin:0;min-height:5.4em;line-height:1.8;font-size:.95rem;overflow-wrap:anywhere}.stream-caret{display:inline-block;vertical-align:-.12em;width:2px;height:1.1em;margin-left:3px;background:var(--accent);animation:caret-blink 1s steps(2,end) infinite}.caret-paused{animation:none}.hint-controls{display:flex;align-items:center;justify-content:space-between;gap:8px;flex-wrap:wrap;margin-top:12px}.hint-buttons{display:flex;gap:6px}.hint-buttons button{display:inline-flex;align-items:center;justify-content:center;gap:5px;font:inherit;font-size:.75rem;min-height:36px;padding:6px 9px;color:var(--text);background:var(--surface);border:1px solid var(--line);border-radius:6px;cursor:pointer}.hint-buttons button:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.waiting-disclosure{margin:12px 0 0;line-height:1.6}.sr-only{position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip-path:inset(50%);white-space:nowrap;border:0}@keyframes reply-spin{to{transform:rotate(360deg)}}@keyframes caret-blink{50%{opacity:0}}@media(prefers-reduced-motion:reduce){.reply-spinner,.stream-caret{animation:none}}@media(max-width:600px){.hint-buttons button{min-height:40px}}
</style>
