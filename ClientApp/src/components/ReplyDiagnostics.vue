<script setup>
import {computed,nextTick,onBeforeUnmount,onMounted,ref,useId,watch} from 'vue'
import {Gauge,CodeXml} from '@lucide/vue'
const props=defineProps({modelName:{type:String,default:''},canInspectRequests:Boolean})
const emit=defineEmits(['performance','request'])
const actions=computed(()=>[{key:'performance',label:'查看回覆效能',title:'回覆效能',description:'Token 用量、生成速度與往返時間',icon:Gauge},...(props.canInspectRequests?[{key:'request-inspection',label:'查看 Ollama 請求',title:'Ollama 請求',description:'查看本輪請求封包與診斷資料',icon:CodeXml}]:[])])
const active=ref(null),tooltip=ref(null),position=ref({}),tooltipId=`reply-diagnostic-${useId()}`
let anchor=null,leaveTimer=null
function cancelLeave(){clearTimeout(leaveTimer);leaveTimer=null}
function hide(){cancelLeave();active.value=null;anchor=null}
function place(event){
 if(!anchor||!active.value)return
 if(event?.target instanceof Node&&tooltip.value?.contains(event.target))return
 const rect=anchor.getBoundingClientRect(),width=Math.min(288,window.innerWidth-32),height=tooltip.value?.getBoundingClientRect().height||100
 const fitsLeft=rect.left-width-10>=16,above=rect.top-26,below=window.innerHeight-rect.bottom-26
 const maxHeight=fitsLeft?window.innerHeight-32:Math.max(above,below),shownHeight=Math.min(height,maxHeight)
 const left=fitsLeft?rect.left-width-10:Math.max(16,Math.min(rect.right-width,window.innerWidth-width-16))
 const top=fitsLeft?Math.max(16,Math.min(rect.top+(rect.height-shownHeight)/2,window.innerHeight-shownHeight-16)):above>=below?rect.top-shownHeight-10:rect.bottom+10
 position.value={left:`${left}px`,top:`${top}px`,width:`${width}px`,maxHeight:`${Math.max(0,maxHeight)}px`}
}
async function show(action,event){cancelLeave();active.value=action;anchor=event.currentTarget;place();await nextTick();place()}
async function resize(){place();await nextTick();place()}
function leave(){if(document.activeElement===anchor)return;cancelLeave();leaveTimer=setTimeout(hide,120)}
function open(action){hide();emit(action.key==='performance'?'performance':'request')}
function keydown(event){if(event.key==='Escape')hide()}
watch(()=>[props.modelName,props.canInspectRequests],hide)
onMounted(()=>{document.addEventListener('keydown',keydown);window.addEventListener('resize',resize);window.addEventListener('scroll',place,true)})
onBeforeUnmount(()=>{hide();document.removeEventListener('keydown',keydown);window.removeEventListener('resize',resize);window.removeEventListener('scroll',place,true)})
</script>

<template>
 <div class="reply-diagnostics" role="group" aria-label="回覆診斷" :data-reply-model="modelName||undefined">
  <button v-for="action in actions" :key="action.key" type="button" class="diagnostic-icon" :data-action="action.key" :aria-label="action.label" :aria-describedby="active?.key===action.key?tooltipId:undefined" @mouseenter="show(action,$event)" @mouseleave="leave" @focus="show(action,$event)" @blur="hide" @click="open(action)"><component :is="action.icon" :size="18" aria-hidden="true"/></button>
  <div v-if="active" ref="tooltip" :id="tooltipId" role="tooltip" class="diagnostic-tooltip" :style="position" @mouseenter="cancelLeave" @mouseleave="leave"><strong>{{active.title}}</strong><span>{{active.description}}</span><small>模型：<code>{{modelName||'此回覆未記錄模型'}}</code></small></div>
 </div>
</template>

<style scoped>
.reply-diagnostics{display:flex;flex-direction:column;gap:6px;padding-left:8px;border-left:1px solid color-mix(in srgb,#438b88 30%,var(--line))}
.diagnostic-icon{display:inline-flex;align-items:center;justify-content:center;width:36px;min-height:36px;padding:0;border:1px solid var(--line);border-radius:8px;background:color-mix(in srgb,var(--surface) 85%,transparent);color:var(--muted);cursor:pointer}
.diagnostic-icon:hover,.diagnostic-icon:focus-visible{color:var(--accent);border-color:var(--accent);background:var(--accent-soft)}.diagnostic-icon:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
.diagnostic-tooltip{position:fixed;z-index:2000;display:grid;gap:7px;overflow:auto;box-sizing:border-box;padding:12px 14px;border:1px solid var(--line);border-top:3px solid #438b88;border-radius:9px;background:var(--surface);color:var(--text);box-shadow:0 5px 18px #0003;font-size:.8rem;line-height:1.6;overflow-wrap:anywhere;white-space:normal}.diagnostic-tooltip span,.diagnostic-tooltip small{color:var(--muted)}.diagnostic-tooltip small{border-top:1px solid var(--line);padding-top:7px}.diagnostic-tooltip code{font-size:inherit;white-space:normal;word-break:break-word}
@media(max-width:600px){.diagnostic-icon{width:40px;min-height:44px}}
</style>
