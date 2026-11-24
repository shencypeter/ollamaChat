<script setup>
import {computed,nextTick,onMounted,onBeforeUnmount,ref,useId,watch} from 'vue'
import {annotationSegments,annotationBadgeRuns,classificationCatalog,phaseForStage,phaseNames} from '../utils/caseAnnotations'
import {categoryDefinition} from '../utils/categoryDefinitions'
const props=defineProps({content:{type:String,required:true},stage:String,annotations:{type:Array,default:()=>[]},classifications:{type:Array,default:()=>[]},legendTarget:{type:[String,Object],default:null}})
const displayMode=defineModel('displayMode',{type:String,default:'underline'})
const displayModes=[{key:'underline',label:'原文'},{key:'badges',label:'編碼徽章'}]
const activeMode=computed(()=>displayModes.some(mode=>mode.key===displayMode.value)?displayMode.value:'underline')
const badgeMode=computed(()=>activeMode.value==='badges')
const currentPhase=computed(()=>phaseForStage(props.stage))
const statusLabels={unverified:'未驗證：僅辨識分類，未確認臨床正確性',unsupported:'有待查證：此敘述的臨床依據尚未獲支持',misplaced:'階段可能不符：請配合 AI 回饋檢視分類位置'}
const root=ref(null),active=ref(null),selectedCodes=ref(new Set()),preview=ref(null),pinned=ref(false),position=ref({}),tooltipId=`annotation-${useId()}`
const definitionCode=ref(null),definitionPosition=ref({}),definitionId=`${tooltipId}-definition`
let definitionTrigger=null
const catalog=computed(()=>classificationCatalog(props.classifications))
const segments=computed(()=>annotationSegments(props.content,props.annotations,props.classifications))
const badgeRuns=computed(()=>new Map(annotationBadgeRuns(segments.value).map(run=>[run.start,run])))
const differentCodes=(a,b)=>a?.codes.length&&b?.codes.length&&(a.codes.length!==b.codes.length||a.codes.some(code=>!b.codes.includes(code)))
const underlineInsets=computed(()=>new Map(segments.value.map((segment,index)=>[segment.start,[differentCodes(segments.value[index-1],segment)?3:0,differentCodes(segment,segments.value[index+1])?3:0]])))
const parentCode=code=>code.match(/^([A-D]\d+)/)?.[1]
const actualCodes=computed(()=>[...new Set(segments.value.flatMap(segment=>segment.codes))])
const mentionedCodes=computed(()=>new Set(actualCodes.value.flatMap(code=>{
 const parent=parentCode(code);return parent&&catalog.value.has(parent)?[code,parent]:[code]
})))
const filterCodes=code=>actualCodes.value.filter(value=>value===code||parentCode(value)===code||catalog.value.get(value)?.phase===code)
const selectionState=code=>{const codes=filterCodes(code),count=codes.filter(value=>selectedCodes.value.has(value)).length;return count===0?false:count===codes.length?true:'mixed'}
const hasAnnotations=computed(()=>segments.value.some(s=>s.codes.length))
const activeSegment=computed(()=>badgeMode.value?badgeRuns.value.get(active.value):undefined)
// Group presentation only: preserve the original annotations and each evidence range.
const legendEntries=computed(()=>{
 const entries=new Map()
 for(const annotation of activeSegment.value?.annotations||[]){
  const key=JSON.stringify([[...annotation.codes].sort(),annotation.status,annotation.explanation])
  if(!entries.has(key))entries.set(key,{codes:annotation.codes,status:annotation.status,explanation:annotation.explanation,quotes:[]})
  const entry=entries.get(key)
  if(!entry.quotes.some(quote=>quote.start===annotation.start&&quote.end===annotation.end&&quote.text===annotation.text))entry.quotes.push({start:annotation.start,end:annotation.end,text:annotation.text})
 }
 return [...entries.values()]
})
const allGroups=computed(()=>Object.entries(phaseNames).map(([phase,label])=>({phase,label,codes:[...catalog.value.values()].filter(c=>c.phase===phase)})))
const groups=computed(()=>allGroups.value.map(group=>({...group,codes:group.codes.filter(code=>mentionedCodes.value.has(code.code))})).filter(group=>group.codes.length))
const selectedCategoryPhase=ref('')
const activeCategoryPhase=computed(()=>groups.value.find(group=>group.phase===selectedCategoryPhase.value)?.phase||groups.value.find(group=>group.phase===currentPhase.value)?.phase||groups.value[0]?.phase)
const categoryTabId=phase=>`${tooltipId}-category-${phase}`
function chooseCategoryPhase(phase){if(groups.value.some(group=>group.phase===phase)){dismiss();selectedCategoryPhase.value=phase}}
function navigateCategories(event){
 if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return
 const tabs=[...event.currentTarget.querySelectorAll('[role="tab"]')],index=tabs.indexOf(event.target.closest('[role="tab"]'));if(index<0)return
 event.preventDefault()
 const next=event.key==='Home'?0:event.key==='End'?tabs.length-1:(index+(event.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length
 chooseCategoryPhase(tabs[next].dataset.categoryTab);tabs[next].focus({preventScroll:true});tabs[next].scrollIntoView?.({block:'nearest',inline:'nearest'})
}
const unmentioned=computed(()=>allGroups.value.flatMap(group=>group.codes.filter(code=>!mentionedCodes.value.has(code.code))))
const unmentionedSections=computed(()=>{
 const missing=allGroups.value.map(group=>({...group,codes:group.codes.filter(code=>!mentionedCodes.value.has(code.code))}))
 if(!currentPhase.value)return [{key:'all',label:'未提及',groups:missing.filter(group=>group.codes.length)}]
 return [
  {key:'current',label:'本階段未提及',groups:missing.filter(group=>group.phase===currentPhase.value)},
  {key:'other',label:'其他階段未提及',groups:missing.filter(group=>group.phase!==currentPhase.value&&group.codes.length)}
 ].filter(section=>section.groups.length)
})
const definition=computed(()=>catalog.value.get(definitionCode.value))
let focused=null,keyboardFocused=null,pointerFocusedElement=null,hoveredStart=null,closeTimer=null,triggerElement=null,restoringFocus=false
function cancelClose(){clearTimeout(closeTimer);closeTimer=null}
function dismiss(){cancelClose();active.value=null;preview.value=null;hoveredStart=null;pinned.value=false;triggerElement=null;hideDefinition(true)}
function hideDefinition(force=false){if(!force&&document.activeElement===definitionTrigger)return;definitionCode.value=null;definitionTrigger=null}
function showDefinition(code,event){
 dismiss();definitionCode.value=code;definitionTrigger=event.currentTarget
 positionDefinition()
}
function positionDefinition(){
 if(!definitionTrigger||!definitionCode.value)return
 const code=definitionCode.value
 const rect=definitionTrigger.getBoundingClientRect(),width=Math.min(360,window.innerWidth-32)
 definitionPosition.value={left:`${Math.max(16,Math.min(rect.left,window.innerWidth-width-16))}px`,top:'16px',width:`${width}px`,maxHeight:`${window.innerHeight-32}px`}
 void nextTick(()=>{
  if(definitionCode.value!==code)return
  const height=root.value?.querySelector('[data-category-definition-tip]')?.getBoundingClientRect().height||0
  const top=rect.bottom+8+height<=window.innerHeight-16?rect.bottom+8:rect.top-height-8
  definitionPosition.value={...definitionPosition.value,top:`${Math.max(16,Math.min(top,window.innerHeight-height-16))}px`}
 })
}
function definitionHasFocus(){return document.activeElement===definitionTrigger||root.value?.querySelector('[data-category-definition-tip]')?.contains(document.activeElement)}
function leaveDefinition(){cancelClose();if(!definitionHasFocus())closeTimer=setTimeout(()=>{if(!definitionHasFocus())hideDefinition(true)},150)}
function blurDefinition(event){if(!root.value?.querySelector('[data-category-definition-tip]')?.contains(event.relatedTarget))hideDefinition(true)}
function show(segment,event){
 hideDefinition(true)
 if(event.type==='mouseenter')hoveredStart=segment.start
 if(badgeMode.value)preview.value=segment.start
 cancelClose();if(pinned.value&&active.value!==segment.start)return
 active.value=segment.start;triggerElement=event.currentTarget;positionTooltip(segment.start)
}
function positionTooltip(start){
 if(!triggerElement)return
 const columns=window.innerWidth>=1200?3:window.innerWidth>=700?2:1,entries=legendEntries.value.length||1
 const rect=triggerElement.getBoundingClientRect(),width=Math.min(pinned.value?Math.min(columns,entries)*320+32:260,1020,window.innerWidth-32)
 position.value={left:`${Math.max(16,Math.min(rect.left,window.innerWidth-width-16))}px`,top:'16px',width:`${width}px`,maxHeight:`${window.innerHeight-32}px`}
 void nextTick(()=>{
  if(active.value!==start)return
  const height=root.value?.querySelector('[role="tooltip"],[data-tag-legend]')?.getBoundingClientRect().height||0
  const below=rect.bottom+8,above=rect.top-height-8
  const desired=below+height<=window.innerHeight-16?below:above
  position.value={...position.value,top:`${Math.max(16,Math.min(desired,window.innerHeight-height-16))}px`}
 })
}
function focus(segment,event){if(restoringFocus)return;pinned.value=false;focused=segment.start;keyboardFocused=pointerFocusedElement===event.currentTarget?null:segment.start;pointerFocusedElement=null;show(segment,event)}
function blur(){focused=null;keyboardFocused=null;preview.value=hoveredStart;if(!pinned.value)dismiss()}
function leave(){hoveredStart=null;preview.value=keyboardFocused;cancelClose();if(!pinned.value&&focused!==active.value)closeTimer=setTimeout(dismiss,150)}
async function toggle(segment,event){
 if(pinned.value&&active.value===segment.start){closeLegend();return}
 pinned.value=false;show(segment,event);pinned.value=true;positionTooltip(segment.start)
 await nextTick();if(pinned.value&&active.value===segment.start)root.value?.querySelector('[data-close-tag-legend]')?.focus({preventScroll:true})
}
function closeLegend(){const trigger=triggerElement;dismiss();if(trigger?.isConnected){restoringFocus=true;trigger.focus({preventScroll:true});restoringFocus=false}}
function resize(){positionDefinition();if(active.value!==null)positionTooltip(active.value)}
function focusOutside(event){if(definitionCode.value&&event.target!==definitionTrigger&&!root.value?.querySelector('[data-category-definition-tip]')?.contains(event.target))hideDefinition(true);if(pinned.value&&event.target!==triggerElement&&!root.value?.querySelector('[data-tag-legend]')?.contains(event.target))dismiss()}
function keydown(event){if(event.key==='Escape'){if(pinned.value){event.preventDefault();event.stopPropagation();closeLegend()}else dismiss()}}
function outside(event){
 if(event.target instanceof Element&&root.value?.querySelector('[data-tag-legend],[data-category-definition-tip]')?.contains(event.target))return
 keyboardFocused=null
 pointerFocusedElement=event.target instanceof Element?event.target.closest('[data-annotation-segment],[data-annotation-reference]'):null
 if(!(event.target instanceof Element)||!root.value?.contains(event.target)||!event.target.closest('[data-annotation-segment],[data-annotation-reference],[role="tooltip"]'))dismiss()
}
function scroll(event){
 if(event.target instanceof Node&&root.value?.querySelector('[role="tooltip"],[data-tag-legend]')?.contains(event.target))return
 positionDefinition()
 if(active.value!==null)positionTooltip(active.value)
}
function emphasize(code){
 const codes=filterCodes(code),remove=selectionState(code)===true,next=new Set(selectedCodes.value)
 for(const value of codes){if(remove)next.delete(value);else next.add(value)}
 selectedCodes.value=next;dismiss()
 if(next.size)displayMode.value='badges'
}
function selectAll(){selectedCodes.value=new Set(actualCodes.value);dismiss();if(selectedCodes.value.size)displayMode.value='badges'}
function clearSelection(){selectedCodes.value=new Set();dismiss()}
function hideAnnotations(){clearSelection();displayMode.value='underline';root.value?.querySelector('[data-annotation-mode="underline"]')?.focus({preventScroll:true})}
function navigateDisplayModes(event){
 if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return
 const tabs=[...event.currentTarget.querySelectorAll('[role="tab"]')],index=tabs.indexOf(event.target.closest('[role="tab"]'));if(index<0)return
 event.preventDefault()
 const next=event.key==='Home'?0:event.key==='End'?tabs.length-1:(index+(event.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length
 displayMode.value=tabs[next].dataset.annotationMode;tabs[next].focus()
}
function underlinePhases(segment){
 if(!badgeMode.value)return []
 const local=badgeMode.value&&badgeRuns.value.get(preview.value)?.members.some(member=>member.start===segment.start)
 return segment.phases.filter(phase=>local||segment.codes.some(code=>selectedCodes.value.has(code)&&catalog.value.get(code)?.phase===phase))
}
function highlightPhase(segment){return segment.phases.includes(currentPhase.value)?currentPhase.value:''}
function underline(segment){
 const strokes=underlinePhases(segment)
 if(!strokes.length)return {}
 // Stacked, two-pixel strokes keep every phase visible when spans overlap.
 const color=phase=>`var(--phase-${phase})`
 const [left,right]=underlineInsets.value.get(segment.start)||[0,0]
 return {backgroundImage:strokes.map(phase=>`linear-gradient(${color(phase)},${color(phase)})`).join(',')||'none',backgroundSize:`calc(100% - ${left+right}px) 2px`,backgroundPosition:strokes.map((_,i)=>`${left}px calc(100% - ${i*3}px)`).join(','),paddingBottom:`${strokes.length*3+1}px`}
}
watch(()=>[props.content,props.annotations,props.classifications,props.stage],()=>{dismiss();selectedCodes.value=new Set();selectedCategoryPhase.value='';focused=null;keyboardFocused=null;pointerFocusedElement=null})
watch(displayMode,value=>{if(value!=='badges')selectedCodes.value=new Set();dismiss();focused=null;keyboardFocused=null;pointerFocusedElement=null},{flush:'sync'})
onMounted(()=>{document.addEventListener('pointerdown',outside);document.addEventListener('focusin',focusOutside);document.addEventListener('keydown',keydown);window.addEventListener('resize',resize);window.addEventListener('scroll',scroll,true)})
onBeforeUnmount(()=>{cancelClose();document.removeEventListener('pointerdown',outside);document.removeEventListener('focusin',focusOutside);document.removeEventListener('keydown',keydown);window.removeEventListener('resize',resize);window.removeEventListener('scroll',scroll,true)})
</script>

<template>
 <div ref="root" class="phase-annotated-answer">
  <div v-if="hasAnnotations||$slots.actions" class="annotation-display" role="group" aria-label="作答標註與操作">
   <nav v-if="hasAnnotations" class="annotation-tabs" role="tablist" aria-label="標註呈現方式" @keydown="navigateDisplayModes"><button v-for="mode in displayModes" :key="mode.key" type="button" role="tab" class="annotation-tab" :id="`${tooltipId}-${mode.key}`" :aria-controls="`${tooltipId}-panel`" :data-annotation-mode="mode.key" :aria-selected="mode.key===activeMode" :tabindex="mode.key===activeMode?0:-1" @click="displayMode=mode.key">{{mode.label}}<span v-if="mode.key==='badges'" class="annotation-tab-example annotation-reference-code" data-annotation-example aria-hidden="true" title="分類徽章範例" :style="{'--reference-color':`var(--category-${currentPhase||'A'})`}">{{currentPhase||'A'}}</span></button></nav>
   <div v-if="$slots.actions" class="annotation-actions"><slot name="actions"/></div>
  </div>
  <div :role="hasAnnotations?'tabpanel':undefined" :id="hasAnnotations?`${tooltipId}-panel`:undefined" :aria-labelledby="hasAnnotations?`${tooltipId}-${activeMode}`:undefined" :tabindex="hasAnnotations?0:undefined" class="annotation-panel">
  <p class="message-content annotated-text" data-annotated-text>
   <template v-for="segment in segments" :key="segment.start">
    <span v-if="badgeMode&&segment.codes.length" class="annotation-segment annotation-reference-text" data-annotation-segment :data-phases="segment.phases.join(' ')" :data-current-phase="highlightPhase(segment)" :data-underline-phases="underlinePhases(segment).join(' ')" :style="underline(segment)">{{segment.text}}</span><template v-else>{{segment.text}}</template><sup v-if="badgeMode&&badgeRuns.has(segment.start)" class="annotation-reference"><button type="button" data-annotation-reference :data-codes="segment.codes.join(' ')" :aria-label="'查看分類：'+segment.codes.map(code=>code+' '+catalog.get(code)?.label).join('、')+(badgeRuns.get(segment.start).members.length>1?'，合併 '+badgeRuns.get(segment.start).members.length+' 個連續片段':'')" :aria-describedby="!pinned&&active===segment.start?tooltipId:undefined" :aria-expanded="pinned&&active===segment.start" aria-haspopup="dialog" :aria-controls="pinned&&active===segment.start?`${tooltipId}-legend`:undefined" @mouseenter="show(badgeRuns.get(segment.start),$event)" @mouseleave="leave" @focus="focus(badgeRuns.get(segment.start),$event)" @blur="blur" @click="toggle(badgeRuns.get(segment.start),$event)" @keydown.esc.stop="dismiss"><template v-for="(code,index) in segment.codes" :key="code"><template v-if="index">{{' · '}}</template><span class="annotation-reference-code" :data-reference-phase="catalog.get(code).phase" :style="{'--reference-color':`var(--category-${catalog.get(code).phase})`}">{{code}}</span></template></button></sup>
   </template>
  </p>
  <section v-show="badgeMode&&hasAnnotations" class="category-sidebar" aria-label="作答分類標註">
   <h3>作答分類標註</h3>
  <template v-if="hasAnnotations">
   <p class="annotation-caveat"><template v-if="currentPhase">目前階段：{{currentPhase}} {{phaseNames[currentPhase]}}。</template><template v-if="!badgeMode">原文以純文字呈現。</template>分類辨識不代表臨床正確，未標記內容也不代表錯誤。</p>
   <Teleport :to="legendTarget" :disabled="!legendTarget"><div v-show="badgeMode" class="phase-category-explorer" :class="{'legend-in-rail':legendTarget}">
   <section class="annotation-legend" aria-label="分類圖例">
    <p class="legend-hint">點選下方分類，為相符原文加上底線；可複選，再次點選可取消。外框表示已辨識，填色與 ✓ 表示已選取，虛線表示部分選取。滑入或聚焦分類可看定義；未提及項目僅供查閱。文中徽章滑入時暫時顯示片段底線，點選開啟說明。</p>
    <div class="annotation-filter-actions"><button type="button" data-annotation-filter="show-all" @click="selectAll">顯示全部標註</button><button type="button" data-annotation-filter="hide-all" @click="hideAnnotations">隱藏全部標註</button></div>
    <nav class="phase-category-tabs" role="tablist" aria-label="分類階段" @keydown="navigateCategories"><button v-for="group in groups" :key="group.phase" :id="categoryTabId(group.phase)" type="button" role="tab" class="phase-category-tab" :data-category-tab="group.phase" :aria-label="`${group.phase} — ${group.label}${currentPhase===group.phase?'，目前作答階段':''}`" :aria-selected="activeCategoryPhase===group.phase" :aria-controls="`${categoryTabId(group.phase)}-panel`" :tabindex="activeCategoryPhase===group.phase?0:-1" @click="chooseCategoryPhase(group.phase)"><span class="category-phase-badge" :data-category-phase-badge="group.phase" :data-current-stage-badge="currentPhase===group.phase?group.phase:undefined" :style="{'--phase-badge-color':`var(--category-${group.phase})`,'--phase-badge-outline':`var(--phase-${group.phase})`}" aria-hidden="true" :title="currentPhase===group.phase?'目前作答階段':undefined">{{group.phase}}</span>{{' — '+group.label}}</button></nav>
    <section v-for="group in groups" v-show="activeCategoryPhase===group.phase" :key="group.phase" :id="`${categoryTabId(group.phase)}-panel`" class="phase-group" role="tabpanel" :aria-labelledby="categoryTabId(group.phase)">
     <div class="legend-codes" :style="{'--category-color':`var(--category-${group.phase})`,'--category-outline-color':`var(--phase-${group.phase})`}"><button type="button" :data-legend-phase="group.phase" class="category-mentioned" :aria-pressed="selectionState(group.phase)" @click="emphasize(group.phase)">{{group.phase}} 全類</button><button v-for="code in group.codes" :key="code.code" type="button" class="category-mentioned" :data-legend-code="code.code" data-mentioned="true" :aria-label="`${code.code} — ${code.label}，作答已辨識此分類`" :aria-describedby="definitionCode===code.code?definitionId:undefined" :aria-pressed="selectionState(code.code)" @mouseenter="showDefinition(code.code,$event)" @mouseleave="leaveDefinition" @focus="showDefinition(code.code,$event)" @blur="blurDefinition" @click="emphasize(code.code)">{{code.code}} — {{code.label}}</button></div>
    </section>
    <div v-if="unmentioned.length" class="unmentioned-categories" data-unmentioned-categories>
     <section v-for="section in unmentionedSections" :key="section.key" :data-unmentioned-scope="section.key" :aria-label="section.label">
      <h4>{{section.label}}</h4>
      <p v-for="group in section.groups" :key="group.phase" :data-unmentioned-phase="group.phase"><strong>{{group.phase}} {{group.label}}：</strong><template v-if="group.codes.length"><template v-for="(code,index) in group.codes" :key="code.code"><template v-if="index">、</template><button type="button" class="unmentioned-category" :data-unmentioned-code="code.code" :aria-label="`${code.code} ${code.label}，查看分類定義`" :aria-describedby="definitionCode===code.code?definitionId:undefined" @mouseenter="showDefinition(code.code,$event)" @mouseleave="leaveDefinition" @focus="showDefinition(code.code,$event)" @blur="blurDefinition" @click="showDefinition(code.code,$event)">{{code.code}} {{code.label}}</button></template></template><span v-else>無</span></p>
     </section>
    </div>
   </section>
   <p v-if="selectedCodes.size" class="selected-classification" role="status">已選取 {{[...selectedCodes].join('、')}} <button type="button" @click="clearSelection">取消全部選取</button></p>
   </div></Teleport>
  </template>
  <p v-else class="muted category-empty">本輪尚無可顯示的 AI 分類標註。</p>
  </section>
  </div>
  <div v-if="definition" :id="definitionId" role="tooltip" data-category-definition-tip tabindex="0" class="annotation-tooltip category-definition-tip" :style="definitionPosition" @mouseenter="cancelClose" @mouseleave="leaveDefinition" @focus="cancelClose" @blur="blurDefinition"><strong>{{definition.code}} — {{definition.label}}</strong><p>{{categoryDefinition(definition)}}</p></div>
  <div v-if="activeSegment&&!pinned" :id="tooltipId" role="tooltip" class="annotation-tooltip annotation-hint" :style="position" @mouseenter="cancelClose" @mouseleave="leave">點選開啟分類圖例</div>
  <section v-if="activeSegment&&pinned" :id="`${tooltipId}-legend`" role="dialog" aria-modal="false" aria-label="分類圖例" data-tag-legend class="annotation-tooltip tag-legend" :style="position">
   <header class="tag-legend-header"><strong>分類圖例</strong><button type="button" aria-label="關閉分類圖例" data-close-tag-legend @click="closeLegend">×</button></header>
   <p v-if="badgeMode&&activeSegment.members?.length>1" class="recognition-status">已合併 {{activeSegment.members.length}} 個連續片段；相同分類、狀態與說明合併呈現，保留各段原文。</p>
   <div class="legend-entry-stack" data-legend-entries><div v-for="(annotation,index) in legendEntries" :key="index" class="tooltip-annotation">
    <strong v-for="code in annotation.codes" :key="code" class="tooltip-code">{{code}} — {{catalog.get(code)?.label}}<span v-if="currentPhase" class="tooltip-alignment">{{catalog.get(code)?.phase===currentPhase?'目前階段相符不代表臨床正確':`屬於 ${catalog.get(code)?.phase} ${phaseNames[catalog.get(code)?.phase]}，請核對與目前 ${currentPhase} 階段的關聯`}}</span></strong>
    <blockquote v-for="quote in annotation.quotes" :key="`${quote.start}:${quote.end}`">{{quote.text}}</blockquote><p v-if="annotation.explanation">{{annotation.explanation}}</p><p class="recognition-status">{{statusLabels[annotation.status]}}</p>
   </div></div>
  </section>
 </div>
</template>

<style scoped>
.category-phase-badge{display:inline-flex;align-items:center;justify-content:center;flex:none;min-width:24px;height:24px;padding:2px 5px;border-radius:5px;background:var(--phase-badge-color);color:#fff;font-size:.8rem;font-weight:700;line-height:1}.category-phase-badge[data-current-stage-badge]{outline:1px solid var(--phase-badge-outline);outline-offset:2px}
.annotation-actions{display:flex;justify-content:flex-end;min-width:0;margin-inline-start:auto}
.category-sidebar{min-width:0;margin-top:18px}.category-sidebar>h3{font-size:1rem;margin:0 0 10px}.category-empty{font-size:.85rem;line-height:1.7;margin:12px 0 0}
.annotation-display{display:flex;align-items:center;flex-wrap:wrap;gap:12px;margin:8px 0}.annotation-tabs{display:flex;gap:0;border-bottom:1px solid var(--line);max-width:100%}.annotation-tab{font:inherit;font-size:.85rem;min-height:44px;padding:10px 14px;border:0;border-bottom:3px solid transparent;border-radius:0;background:transparent;color:var(--muted);cursor:pointer}.annotation-tab-example{display:inline-block;margin-left:7px;font-size:.75rem;line-height:1.2;font-weight:600;vertical-align:baseline}.annotation-tab[aria-selected=true]{border-bottom-color:var(--accent);color:var(--text);font-weight:600}.annotation-tab:hover{background:var(--accent-soft)}.annotation-tab:focus-visible,.annotation-panel:focus-visible{outline:2px solid var(--accent);outline-offset:-3px}.annotation-reference button:focus-visible{outline:2px solid var(--accent);outline-offset:3px}.annotation-reference{vertical-align:super;font-size:.75em;line-height:0;margin:0 2px}.annotation-reference button{font:inherit;line-height:1.4;min-height:24px;max-width:100%;overflow-wrap:anywhere;display:inline-flex;flex-wrap:wrap;align-items:center;gap:2px;padding:0;border:0;border-radius:4px;background:transparent;color:var(--text);cursor:pointer}.annotation-reference-code{padding:2px 5px;border:1px solid var(--reference-color);border-radius:4px;background:var(--reference-color);color:#fff}.annotation-segment.annotation-reference-text{cursor:text}

.phase-annotated-answer,.phase-category-explorer{--phase-A:var(--code-line-A);--phase-B:var(--code-line-B);--phase-C:var(--code-line-C);--phase-D:var(--code-line-D);--category-A:var(--code-A);--category-B:var(--code-B);--category-C:var(--code-C);--category-D:var(--code-D);min-width:0}
.annotated-text{white-space:pre-wrap;overflow-wrap:anywhere;margin:8px 0;line-height:2.2}
.annotation-segment{cursor:pointer;background-color:transparent;background-size:100% 2px;background-repeat:no-repeat;-webkit-box-decoration-break:clone;box-decoration-break:clone;border-radius:2px}
.annotation-segment:focus-visible{outline:2px solid var(--accent);outline-offset:3px}
.unmentioned-categories{font-size:.8rem;line-height:2;color:var(--muted);overflow-wrap:anywhere;margin:14px 0 0}.unmentioned-categories section+section{margin-top:10px}.unmentioned-categories h4{margin:0;font:inherit;font-weight:600;color:var(--text)}.unmentioned-categories p{margin:4px 0 0}.unmentioned-categories strong{font-weight:600}.unmentioned-category{display:inline;border:0;border-radius:2px;padding:0;background:transparent;color:inherit;font:inherit;line-height:inherit;text-align:left;cursor:help;text-decoration:underline dotted;text-underline-offset:3px}.unmentioned-category:hover{color:var(--text)}.unmentioned-category:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.category-definition-tip:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
.annotation-filter-actions{display:flex;flex-wrap:wrap;gap:6px}.legend-codes button[aria-pressed=mixed]{border-style:dashed;border-width:2px}
.annotation-caveat,.legend-hint,.selected-classification{font-size:.8rem;line-height:1.65;margin:12px 0 8px;color:var(--muted)}
.annotation-legend{border-top:1px solid var(--line);padding-top:10px;font-size:.8rem}
.phase-group{margin:8px 0}.legend-codes{display:flex;flex-wrap:wrap;align-items:flex-start;gap:6px;padding:8px 0}.legend-codes button,.selected-classification button,.annotation-filter-actions button{background:var(--surface);color:inherit;border:1px solid var(--line);border-radius:6px;padding:8px 10px;cursor:pointer;min-height:44px;font:inherit;text-align:left}.legend-codes button{flex:0 1 auto;min-width:0;max-width:100%;white-space:normal;overflow-wrap:anywhere}.legend-codes button:focus-visible,.annotation-filter-actions button:focus-visible,.selected-classification button:focus-visible{outline:2px solid var(--accent);outline-offset:3px}
.annotation-tooltip{position:fixed;z-index:2000;background:var(--surface);color:var(--text);border:1px solid var(--line);border-radius:10px;box-shadow:0 6px 24px #0003;padding:14px;box-sizing:border-box;overflow:auto;overflow-wrap:anywhere;font-size:.85rem;line-height:1.65;white-space:normal}.tooltip-code{display:block}.tooltip-alignment{display:block;font-size:.8rem;font-weight:400}.tag-legend-header{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:12px}.tag-legend-header button{width:36px;min-height:36px;background:var(--surface);border:1px solid var(--line);border-radius:6px;color:var(--text);font-size:1.25rem;cursor:pointer}.tag-legend-header button:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.legend-entry-stack{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,250px),1fr));gap:12px;margin-top:12px;align-items:start}.tooltip-annotation{min-width:0;border:1px solid var(--line);border-radius:7px;padding:12px}.tooltip-annotation blockquote{margin:10px 0;padding:7px 10px;border-left:3px solid var(--accent);white-space:pre-wrap;color:var(--muted)}.annotation-tooltip p{margin:8px 0 0}.recognition-status{font-size:.8rem;font-weight:600}
.annotation-filter-actions button,.selected-classification button,.annotation-display :deep(.button.secondary){background:var(--action);border-color:var(--action);color:#fff;font-weight:600}
.legend-codes button.category-mentioned{position:relative;padding-right:32px;background:transparent;border-color:var(--category-outline-color);color:var(--category-outline-color);font-weight:600}
.annotation-filter-actions button:hover,.selected-classification button:hover,.annotation-display :deep(.button.secondary:hover){background:var(--action-hover);border-color:var(--action-hover);color:#fff}
.legend-codes button.category-mentioned[aria-pressed=true]{background:var(--category-color);border-color:var(--category-color);color:#fff}
.legend-codes button.category-mentioned[aria-pressed=true]::after{content:'✓';position:absolute;right:10px;top:50%;transform:translateY(-50%);font-weight:700}
.legend-codes button.category-mentioned[aria-pressed=mixed]{border-color:var(--category-outline-color)}
@media(max-width:600px){.annotation-tooltip{font-size:.9rem}}
.legend-in-rail .annotation-legend{border-top:0;padding-top:0}.legend-in-rail .legend-codes{display:flex}.legend-in-rail .selected-classification button{display:block;margin-top:8px;width:100%}.legend-in-rail .legend-hint{margin-top:10px}.phase-category-explorer{min-width:0}
.phase-category-tabs{display:flex;gap:0;max-width:100%;overflow-x:auto;border-bottom:1px solid var(--line);margin-top:12px;scrollbar-width:thin}.phase-category-tab{display:inline-flex;align-items:center;gap:8px;flex:0 0 auto;min-height:44px;padding:10px 14px;border:0;border-bottom:3px solid transparent;border-radius:0;background:transparent;color:var(--muted);font:inherit;white-space:nowrap;cursor:pointer}.phase-category-tab[aria-selected=true]{border-bottom-color:var(--accent);background:var(--accent-soft);color:var(--text);font-weight:700}.phase-category-tab:hover{color:var(--text)}.phase-category-tab:focus-visible{outline:2px solid var(--accent);outline-offset:-2px}
</style>
