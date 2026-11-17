<script setup>
import {computed,nextTick,onMounted,onBeforeUnmount,ref,useId,watch} from 'vue'
import {annotationSegments,classificationCatalog,phaseForStage,phaseNames} from '../utils/caseAnnotations'
const props=defineProps({content:{type:String,required:true},stage:String,annotations:{type:Array,default:()=>[]},classifications:{type:Array,default:()=>[]}})
const displayMode=defineModel('displayMode',{type:String,default:'underline'})
const badgeMode=computed(()=>displayMode.value==='badges')
const currentPhase=computed(()=>phaseForStage(props.stage))
const statusLabels={unverified:'未驗證：僅辨識分類，未確認臨床正確性',unsupported:'有待查證：此敘述的臨床依據尚未獲支持',misplaced:'階段可能不符：請配合 AI 回饋檢視分類位置'}
const root=ref(null),active=ref(null),selectedCode=ref(''),position=ref({}),tooltipId=`annotation-${useId()}`
const catalog=computed(()=>classificationCatalog(props.classifications))
const segments=computed(()=>annotationSegments(props.content,props.annotations,props.classifications))
const differentCodes=(a,b)=>a?.codes.length&&b?.codes.length&&(a.codes.length!==b.codes.length||a.codes.some(code=>!b.codes.includes(code)))
const underlineInsets=computed(()=>new Map(segments.value.map((segment,index)=>[segment.start,[differentCodes(segments.value[index-1],segment)?3:0,differentCodes(segment,segments.value[index+1])?3:0]])))
const parentCode=code=>code.match(/^([A-D]\d+)/)?.[1]
const mentionedCodes=computed(()=>new Set(segments.value.flatMap(segment=>segment.codes).flatMap(code=>{
 const parent=parentCode(code);return parent&&catalog.value.has(parent)?[code,parent]:[code]
})))
const matchesCode=(segment,code)=>segment.codes.some(value=>value===code||parentCode(value)===code)
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
function underlinePhases(segment){return segment.phases}
function highlightPhase(segment){return segment.phases.includes(currentPhase.value)?currentPhase.value:''}
function underline(segment){
 // Stacked, two-pixel strokes keep every phase visible when spans overlap.
 const muted=selectedCode.value&&!matchesCode(segment,selectedCode.value),color=phase=>muted?`color-mix(in srgb,var(--phase-${phase}) 35%,transparent)`:`var(--phase-${phase})`
 const strokes=underlinePhases(segment)
 const [left,right]=underlineInsets.value.get(segment.start)||[0,0]
 return {backgroundImage:strokes.map(phase=>`linear-gradient(${color(phase)},${color(phase)})`).join(',')||'none',backgroundSize:`calc(100% - ${left+right}px) 2px`,backgroundPosition:strokes.map((_,i)=>`${left}px calc(100% - ${i*3}px)`).join(','),paddingBottom:`${strokes.length*3+1}px`}
}
watch(()=>[props.content,props.annotations,props.classifications,props.stage],()=>{dismiss();selectedCode.value='';focused=null})
watch(displayMode,()=>{dismiss();focused=null})
onMounted(()=>{document.addEventListener('pointerdown',outside);document.addEventListener('keydown',keydown);window.addEventListener('resize',dismiss);window.addEventListener('scroll',scroll,true)})
onBeforeUnmount(()=>{cancelClose();document.removeEventListener('pointerdown',outside);document.removeEventListener('keydown',keydown);window.removeEventListener('resize',dismiss);window.removeEventListener('scroll',scroll,true)})
</script>

<template>
 <div ref="root" class="phase-annotated-answer">
  <div v-if="hasAnnotations" class="annotation-display" role="group" aria-label="標註呈現方式"><button type="button" data-annotation-mode="underline" :aria-pressed="!badgeMode" @click="displayMode='underline'">底線</button><button type="button" data-annotation-mode="badges" :aria-pressed="badgeMode" @click="displayMode='badges'">編碼徽章</button></div>
  <p class="message-content annotated-text" data-annotated-text>
   <template v-for="segment in segments" :key="segment.start">
    <span v-if="segment.codes.length" :role="badgeMode?undefined:'button'" :tabindex="badgeMode?undefined:0" class="annotation-segment" :class="{'annotation-reference-text':badgeMode,'annotation-muted':selectedCode&&!matchesCode(segment,selectedCode),'annotation-emphasized':selectedCode&&matchesCode(segment,selectedCode)}" data-annotation-segment :data-phases="segment.phases.join(' ')" :data-current-phase="highlightPhase(segment)" :data-underline-phases="badgeMode?'':underlinePhases(segment).join(' ')" :style="badgeMode?{}:underline(segment)" :aria-label="badgeMode?undefined:segment.text+'，分類 '+segment.codes.join('、')" :aria-describedby="!badgeMode&&active===segment.start?tooltipId:undefined" :aria-expanded="badgeMode?undefined:active===segment.start" @mouseenter="!badgeMode&&show(segment,$event)" @mouseleave="!badgeMode&&leave()" @focus="focus(segment,$event)" @blur="blur" @click="!badgeMode&&toggle(segment,$event)" @keydown.enter.prevent="!badgeMode&&toggle(segment,$event)" @keydown.space.prevent="!badgeMode&&toggle(segment,$event)" @keydown.esc.stop="dismiss">{{segment.text}}</span><template v-else>{{segment.text}}</template><sup v-if="badgeMode&&segment.codes.length" class="annotation-reference"><button type="button" data-annotation-reference :data-codes="segment.codes.join(' ')" :aria-label="'查看分類：'+segment.codes.map(code=>code+' '+catalog.get(code)?.label).join('、')" :aria-describedby="active===segment.start?tooltipId:undefined" :aria-expanded="active===segment.start" @mouseenter="show(segment,$event)" @mouseleave="leave" @focus="focus(segment,$event)" @blur="blur" @click="toggle(segment,$event)" @keydown.esc.stop="dismiss">{{segment.codes.join(' · ')}}</button></sup>
   </template>
  </p>
  <template v-if="hasAnnotations">
   <p class="annotation-caveat"><template v-if="currentPhase">目前階段：{{currentPhase}} {{phaseNames[currentPhase]}}。原文{{badgeMode?'以編碼徽章標示':'統一以分類色底線標示'}}，目前階段標於下方分類標題；</template><template v-else>{{badgeMode?'編碼徽章':'底線'}}表示 AI 辨識到的分類；</template>分類辨識不代表臨床正確，未標記內容也不代表錯誤。</p>
   <section class="annotation-legend" aria-label="分類圖例">
    <p class="legend-hint">有色底白字表示作答已辨識到的分類（包含所屬大類），不代表內容正確。點選代碼可強調相符原文，再次選取即可取消。</p>
    <details v-for="group in groups" :key="group.phase" class="phase-group">
     <summary class="phase-key" :class="[`phase-${group.phase}`,{'phase-current':currentPhase===group.phase}]">{{group.phase}} — {{group.label}}</summary>
     <div class="legend-codes" :style="{'--category-color':`var(--category-${group.phase})`}"><button v-for="code in group.codes" :key="code.code" type="button" :class="{'category-mentioned':mentionedCodes.has(code.code)}" :data-legend-code="code.code" :data-mentioned="mentionedCodes.has(code.code)" :aria-label="`${code.code} — ${code.label}${mentionedCodes.has(code.code)?'，作答已辨識此分類':''}`" :aria-pressed="selectedCode===code.code" @click="emphasize(code.code)">{{code.code}} — {{code.label}}</button></div>
    </details>
   </section>
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
.annotation-display{display:flex;flex-wrap:wrap;gap:6px;margin:8px 0}.annotation-display button{font:inherit;font-size:.8rem;min-height:36px;padding:6px 12px;border:1px solid var(--line);border-radius:6px;background:var(--surface);color:var(--text);cursor:pointer}.annotation-display button[aria-pressed=true]{border-color:var(--accent);background:var(--accent-soft)}.annotation-display button:focus-visible,.annotation-reference button:focus-visible{outline:2px solid var(--accent);outline-offset:3px}.annotation-reference{vertical-align:super;font-size:.75em;line-height:0;margin:0 2px}.annotation-reference button{font:inherit;line-height:1.4;min-height:24px;max-width:100%;overflow-wrap:anywhere;padding:2px 5px;border:1px solid #167343;border-radius:4px;background:#167343;color:#fff;cursor:pointer}.annotation-segment.annotation-reference-text{cursor:text}

.phase-annotated-answer{--phase-A:#1861b8;--phase-B:#167343;--phase-C:#853ac3;--phase-D:#a74e0b;--category-A:#1861b8;--category-B:#167343;--category-C:#853ac3;--category-D:#a74e0b;--current-phase-highlight:#ffe066;--current-phase-highlight-muted:#e6dca7;--current-phase-text:#332a00;min-width:0}
:global(:root[data-theme=dark] .phase-annotated-answer){--phase-A:#86bbff;--phase-B:#78d7a3;--phase-C:#d4a2ff;--phase-D:#ffb66d;--current-phase-highlight:transparent;--current-phase-highlight-muted:transparent;--current-phase-text:#ffe066}
.annotated-text{white-space:pre-wrap;overflow-wrap:anywhere;margin:8px 0;line-height:2.2}
.annotation-segment{cursor:pointer;background-color:transparent;background-size:100% 2px;background-repeat:no-repeat;-webkit-box-decoration-break:clone;box-decoration-break:clone;border-radius:2px}
.annotation-segment:focus-visible{outline:2px solid var(--accent);outline-offset:3px}.annotation-emphasized{outline:1px dashed currentColor;outline-offset:2px}
.legend-codes button.category-mentioned{background:var(--category-color);border-color:var(--category-color);color:#fff}.legend-codes button.category-mentioned[aria-pressed=true]{outline:2px solid var(--text);outline-offset:2px}
.annotation-caveat,.legend-hint,.selected-classification{font-size:.8rem;line-height:1.65;margin:12px 0 8px;color:var(--muted)}
.annotation-legend{border-top:1px solid var(--line);padding-top:10px;font-size:.8rem}.annotation-legend summary{cursor:pointer;line-height:2}.phase-key{display:inline-block;margin-right:12px;text-decoration:underline;text-underline-offset:4px;text-decoration-thickness:2px}.phase-A{text-decoration-color:var(--phase-A)}.phase-B{text-decoration-color:var(--phase-B)}.phase-C{text-decoration-color:var(--phase-C)}.phase-D{text-decoration-color:var(--phase-D)}
.phase-group{margin:8px 0}.phase-group>summary{display:list-item;min-height:36px}.legend-codes{display:flex;flex-wrap:wrap;gap:6px;padding:8px 0}.legend-codes button,.selected-classification button{background:var(--surface);color:inherit;border:1px solid var(--line);border-radius:6px;padding:8px 10px;cursor:pointer;min-height:44px;font:inherit;text-align:left}.legend-codes button[aria-pressed=true]{border-color:var(--accent);outline:1px solid var(--accent)}.legend-codes button:focus-visible,summary:focus-visible,.selected-classification button:focus-visible{outline:2px solid var(--accent);outline-offset:3px}
.annotation-tooltip{position:fixed;z-index:2000;background:var(--surface);color:var(--text);border:1px solid var(--line);border-radius:10px;box-shadow:0 6px 24px #0003;padding:14px;box-sizing:border-box;overflow:auto;overflow-wrap:anywhere;font-size:.85rem;line-height:1.65;white-space:normal}.tooltip-code{display:block}.tooltip-alignment{display:block;font-size:.8rem;font-weight:400}.phase-current{background:var(--current-phase-highlight);color:var(--current-phase-text);text-decoration:none;border-radius:3px;padding:0 4px}.tooltip-annotation+.tooltip-annotation{border-top:1px solid var(--line);padding-top:10px;margin-top:10px}.annotation-tooltip p{margin:8px 0 0}.recognition-status{font-size:.8rem;font-weight:600}
@media(max-width:600px){.phase-key{margin-right:8px}.annotation-tooltip{font-size:.9rem}.legend-codes{display:grid;grid-template-columns:minmax(0,1fr)}}
</style>
