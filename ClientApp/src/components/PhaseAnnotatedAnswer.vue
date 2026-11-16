<script setup>
import {computed,nextTick,onMounted,onBeforeUnmount,ref,useId,watch} from 'vue'
import {annotationSegments,classificationCatalog,phaseForStage,phaseNames} from '../utils/caseAnnotations'
const props=defineProps({content:{type:String,required:true},stage:String,annotations:{type:Array,default:()=>[]},classifications:{type:Array,default:()=>[]}})
const currentPhase=computed(()=>phaseForStage(props.stage))
const statusLabels={unverified:'未驗證：僅辨識分類，未確認臨床正確性',unsupported:'有待查證：此敘述的臨床依據尚未獲支持',misplaced:'階段可能不符：請配合 AI 回饋檢視分類位置'}
const root=ref(null),active=ref(null),selectedCode=ref(''),position=ref({}),tooltipId=`annotation-${useId()}`
const catalog=computed(()=>classificationCatalog(props.classifications))
const segments=computed(()=>annotationSegments(props.content,props.annotations,props.classifications))
const hasAnnotations=computed(()=>segments.value.some(s=>s.codes.length))
const activeSegment=computed(()=>segments.value.find(s=>s.start===active.value))
const groups=computed(()=>Object.entries(phaseNames).map(([phase,label])=>({phase,label,codes:[...catalog.value.values()].filter(c=>c.phase===phase)})))
let pinned=false,focused=null,closeTimer=null,triggerElement=null
function cancelClose(){clearTimeout(closeTimer);closeTimer=null}
function dismiss(){cancelClose();active.value=null;pinned=false;triggerElement=null}
function show(segment,event){
 cancelClose();if(pinned&&active.value!==segment.start)return
 active.value=segment.start;triggerElement=event.currentTarget;positionTooltip(segment.start)
}
function positionTooltip(start){
 if(!triggerElement)return
 const rect=triggerElement.getBoundingClientRect(),width=Math.min(360,window.innerWidth-32)
 position.value={left:`${Math.max(16,Math.min(rect.left,window.innerWidth-width-16))}px`,top:'16px',width:`${width}px`,maxHeight:`${window.innerHeight-32}px`}
 void nextTick(()=>{
  if(active.value!==start)return
  const height=root.value?.querySelector('[role="tooltip"]')?.getBoundingClientRect().height||0
  const below=rect.bottom+8,above=rect.top-height-8
  position.value={...position.value,top:`${below+height<=window.innerHeight-16?below:Math.max(16,above)}px`}
 })
}
function focus(segment,event){pinned=false;focused=segment.start;show(segment,event)}
function blur(){focused=null;if(!pinned)dismiss()}
function leave(){cancelClose();if(!pinned&&focused!==active.value)closeTimer=setTimeout(dismiss,150)}
function toggle(segment,event){if(pinned&&active.value===segment.start){dismiss();return}pinned=false;show(segment,event);pinned=true}
function keydown(event){if(event.key==='Escape')dismiss()}
function outside(event){if(!(event.target instanceof Element)||!root.value?.contains(event.target)||!event.target.closest('[data-annotation-segment],[role="tooltip"]'))dismiss()}
function scroll(event){
 if(event.target instanceof Node&&root.value?.querySelector('[role="tooltip"]')?.contains(event.target))return
 if(active.value!==null)positionTooltip(active.value)
}
function emphasize(code){selectedCode.value=selectedCode.value===code?'':code;dismiss()}
function underlinePhases(segment){return segment.phases.filter(phase=>phase!==currentPhase.value)}
function highlightPhase(segment){return segment.phases.includes(currentPhase.value)?currentPhase.value:''}
function underline(segment){
 // Stacked, two-pixel strokes keep every phase visible when spans overlap.
 const muted=selectedCode.value&&!segment.codes.includes(selectedCode.value),color=phase=>muted?`color-mix(in srgb,var(--phase-${phase}) 35%,transparent)`:`var(--phase-${phase})`
 const strokes=underlinePhases(segment),highlight=highlightPhase(segment)
 return {backgroundImage:strokes.map(phase=>`linear-gradient(${color(phase)},${color(phase)})`).join(',')||'none',backgroundPosition:strokes.map((_,i)=>`0 calc(100% - ${i*3}px)`).join(','),'--annotation-highlight':highlight?`var(--current-phase-highlight${muted?'-muted':''})`:'transparent',color:highlight?'var(--current-phase-text)':undefined,paddingBottom:`${strokes.length*3+1}px`}
}
watch(()=>[props.content,props.annotations,props.classifications,props.stage],()=>{dismiss();selectedCode.value='';focused=null})
onMounted(()=>{document.addEventListener('pointerdown',outside);document.addEventListener('keydown',keydown);window.addEventListener('resize',dismiss);window.addEventListener('scroll',scroll,true)})
onBeforeUnmount(()=>{cancelClose();document.removeEventListener('pointerdown',outside);document.removeEventListener('keydown',keydown);window.removeEventListener('resize',dismiss);window.removeEventListener('scroll',scroll,true)})
</script>

<template>
 <div ref="root" class="phase-annotated-answer">
  <p class="message-content annotated-text" data-annotated-text><template v-for="segment in segments" :key="segment.start"><span v-if="segment.codes.length" role="button" tabindex="0" class="annotation-segment" :class="{'annotation-muted':selectedCode&&!segment.codes.includes(selectedCode),'annotation-emphasized':selectedCode&&segment.codes.includes(selectedCode)}" data-annotation-segment :data-phases="segment.phases.join(' ')" :data-current-phase="highlightPhase(segment)" :data-underline-phases="underlinePhases(segment).join(' ')" :style="underline(segment)" :aria-label="`${segment.text}，分類 ${segment.codes.join('、')}`" :aria-describedby="active===segment.start?tooltipId:undefined" :aria-expanded="active===segment.start" @mouseenter="show(segment,$event)" @mouseleave="leave" @focus="focus(segment,$event)" @blur="blur" @click="toggle(segment,$event)" @keydown.enter.prevent="toggle(segment,$event)" @keydown.space.prevent="toggle(segment,$event)" @keydown.esc.stop="dismiss">{{segment.text}}</span><template v-else>{{segment.text}}</template></template></p>
  <template v-if="hasAnnotations">
   <p class="annotation-caveat"><template v-if="currentPhase">目前階段：{{currentPhase}} {{phaseNames[currentPhase]}}。相符分類以背景色標示，其他階段的分類以彩色底線標示；</template><template v-else>底線表示 AI 辨識到的分類；</template>分類辨識不代表臨床正確，未標記內容也不代表錯誤。</p>
   <details class="annotation-legend">
    <summary>分類圖例 <span v-for="group in groups" :key="group.phase" class="phase-key" :class="[`phase-${group.phase}`,{'phase-current':currentPhase===group.phase}]">{{group.phase}} {{group.label}}</span></summary>
    <p class="legend-hint">展開分類後選取代碼，可強調原文中相符的標註；再次選取即可取消。</p>
    <details v-for="group in groups" :key="group.phase" class="phase-group">
     <summary class="phase-key" :class="[`phase-${group.phase}`,{'phase-current':currentPhase===group.phase}]">{{group.phase}} — {{group.label}}</summary>
     <div class="legend-codes"><button v-for="code in group.codes" :key="code.code" type="button" :data-legend-code="code.code" :aria-pressed="selectedCode===code.code" @click="emphasize(code.code)">{{code.code}} — {{code.label}}</button></div>
    </details>
   </details>
   <p v-if="selectedCode" class="selected-classification" role="status">已強調 {{selectedCode}} — {{catalog.get(selectedCode)?.label}} <button type="button" @click="emphasize(selectedCode)">顯示全部標註</button></p>
  </template>
  <div v-if="activeSegment" :id="tooltipId" role="tooltip" class="annotation-tooltip" :style="position" @mouseenter="cancelClose" @mouseleave="leave">
   <div v-for="(annotation,index) in activeSegment.annotations" :key="index" class="tooltip-annotation">
    <strong v-for="code in annotation.codes" :key="code" class="tooltip-code">{{code}} — {{catalog.get(code)?.label}}<span v-if="currentPhase" class="tooltip-alignment">{{catalog.get(code)?.phase===currentPhase?'目前階段相符不代表臨床正確':`屬於 ${catalog.get(code)?.phase} ${phaseNames[catalog.get(code)?.phase]}，請核對與目前 ${currentPhase} 階段的關聯`}}</span></strong>
    <p v-if="annotation.explanation">{{annotation.explanation}}</p><p class="recognition-status">{{statusLabels[annotation.status]}}</p>
   </div>
  </div>
 </div>
</template>

<style scoped>
.phase-annotated-answer{--phase-A:#1861b8;--phase-B:#167343;--phase-C:#853ac3;--phase-D:#a74e0b;--current-phase-highlight:#ffe066;--current-phase-highlight-muted:#e6dca7;--current-phase-text:#332a00;min-width:0}
:global(:root[data-theme=dark] .phase-annotated-answer){--phase-A:#86bbff;--phase-B:#78d7a3;--phase-C:#d4a2ff;--phase-D:#ffb66d}
.annotated-text{white-space:pre-wrap;overflow-wrap:anywhere;margin:8px 0;line-height:2.2}
.annotation-segment{cursor:pointer;background-color:var(--annotation-highlight,transparent);background-size:100% 2px;background-repeat:no-repeat;-webkit-box-decoration-break:clone;box-decoration-break:clone;border-radius:2px}
.annotation-segment[data-current-phase]:not([data-current-phase=""]){--phase-A:#1861b8;--phase-B:#167343;--phase-C:#853ac3;--phase-D:#a74e0b}
.annotation-segment:focus-visible{outline:2px solid var(--accent);outline-offset:3px}.annotation-emphasized{outline:1px dashed currentColor;outline-offset:2px}
.annotation-caveat,.legend-hint,.selected-classification{font-size:.8rem;line-height:1.65;margin:12px 0 8px;color:var(--muted)}
.annotation-legend{border-top:1px solid var(--line);padding-top:10px;font-size:.8rem}.annotation-legend summary{cursor:pointer;line-height:2}.annotation-legend>summary{min-height:44px}.phase-key{display:inline-block;margin-right:12px;text-decoration:underline;text-underline-offset:4px;text-decoration-thickness:2px}.phase-A{text-decoration-color:var(--phase-A)}.phase-B{text-decoration-color:var(--phase-B)}.phase-C{text-decoration-color:var(--phase-C)}.phase-D{text-decoration-color:var(--phase-D)}
.phase-group{margin:8px 0}.phase-group>summary{display:list-item;min-height:36px}.legend-codes{display:flex;flex-wrap:wrap;gap:6px;padding:8px 0}.legend-codes button,.selected-classification button{background:var(--surface);color:inherit;border:1px solid var(--line);border-radius:6px;padding:8px 10px;cursor:pointer;min-height:44px;font:inherit;text-align:left}.legend-codes button[aria-pressed=true]{border-color:var(--accent);outline:1px solid var(--accent)}.legend-codes button:focus-visible,summary:focus-visible,.selected-classification button:focus-visible{outline:2px solid var(--accent);outline-offset:3px}
.annotation-tooltip{position:fixed;z-index:2000;background:var(--surface);color:var(--text);border:1px solid var(--line);border-radius:10px;box-shadow:0 6px 24px #0003;padding:14px;box-sizing:border-box;overflow:auto;overflow-wrap:anywhere;font-size:.85rem;line-height:1.65;white-space:normal}.tooltip-code{display:block}.tooltip-alignment{display:block;font-size:.8rem;font-weight:400}.phase-current{background:var(--current-phase-highlight);color:var(--current-phase-text);text-decoration:none;border-radius:3px;padding:0 4px}.tooltip-annotation+.tooltip-annotation{border-top:1px solid var(--line);padding-top:10px;margin-top:10px}.annotation-tooltip p{margin:8px 0 0}.recognition-status{font-size:.8rem;font-weight:600}
@media(max-width:600px){.annotation-legend>summary{padding:4px 0}.phase-key{margin-right:8px}.annotation-tooltip{font-size:.9rem}.legend-codes{display:grid;grid-template-columns:minmax(0,1fr)}}
</style>
