<script setup>
import {computed,nextTick,onBeforeUnmount,onMounted,ref,useId,watch} from 'vue'
import {classificationCatalog,phaseForStage,phaseNames} from '../utils/caseAnnotations'
import {categoryDefinition} from '../utils/categoryDefinitions'

const props=defineProps({stage:String,classifications:{type:Array,default:()=>[]},visible:{type:Boolean,default:true}})
const root=ref(null),tooltip=ref(null),activeCode=ref(null),position=ref({}),tooltipId=`writing-definition-${useId()}`
let trigger=null,closeTimer=null,restoringFocus=false
const currentPhase=computed(()=>phaseForStage(props.stage))
const groups=computed(()=>{
 const catalog=[...classificationCatalog(props.classifications).values()].filter(item=>item.enabled!==false)
 return Object.entries(phaseNames).map(([phase,label])=>({phase,label,codes:catalog.filter(item=>item.phase===phase)})).filter(group=>group.codes.length)
})
const defaultPhase=computed(()=>groups.value.some(group=>group.phase===currentPhase.value)?currentPhase.value:groups.value[0]?.phase)
const definition=computed(()=>groups.value.flatMap(group=>group.codes).find(code=>code.code===activeCode.value))
function cancelClose(){clearTimeout(closeTimer)}
function close(){cancelClose();activeCode.value=null;trigger=null}
function place(){
 if(!trigger||!definition.value)return
 const rect=trigger.getBoundingClientRect(),code=activeCode.value,width=Math.min(340,window.innerWidth-32)
 position.value={left:`${Math.max(16,Math.min(rect.left,window.innerWidth-width-16))}px`,top:'16px',width:`${width}px`,maxHeight:`${Math.max(1,window.innerHeight-32)}px`}
 void nextTick(()=>{
  if(activeCode.value!==code)return
  const height=tooltip.value?.getBoundingClientRect().height||0,below=rect.bottom+8,top=below+height<=window.innerHeight-16?below:rect.top-height-8
  position.value={...position.value,top:`${Math.max(16,Math.min(top,window.innerHeight-height-16))}px`}
 })
}
function show(code,event){if(!props.visible||restoringFocus)return;cancelClose();activeCode.value=code;trigger=event.currentTarget;place()}
async function inspect(code,event){show(code,event);await nextTick();if(props.visible&&activeCode.value===code)tooltip.value?.focus({preventScroll:true})}
function hasFocus(){return trigger===document.activeElement||tooltip.value?.contains(document.activeElement)}
function leave(){cancelClose();if(!hasFocus())closeTimer=setTimeout(()=>{if(!hasFocus())close()},150)}
function focusOutside(event){if(event.target!==trigger&&!tooltip.value?.contains(event.target))close()}
function pointerOutside(event){if(!root.value?.contains(event.target)&&!tooltip.value?.contains(event.target))close()}
function keydown(event){if(event.key==='Escape'&&activeCode.value){const target=tooltip.value?.contains(document.activeElement)?trigger:null;close();if(target?.isConnected){restoringFocus=true;target.focus({preventScroll:true});restoringFocus=false}}}
function scroll(event){if(!tooltip.value?.contains(event.target))place()}
watch(()=>[props.stage,props.classifications,props.visible],close,{flush:'sync'})
onMounted(()=>{document.addEventListener('pointerdown',pointerOutside);document.addEventListener('focusin',focusOutside);document.addEventListener('keydown',keydown);window.addEventListener('resize',place);window.addEventListener('scroll',scroll,true)})
onBeforeUnmount(()=>{close();document.removeEventListener('pointerdown',pointerOutside);document.removeEventListener('focusin',focusOutside);document.removeEventListener('keydown',keydown);window.removeEventListener('resize',place);window.removeEventListener('scroll',scroll,true)})
</script>

<template>
 <section ref="root" class="writing-category-reference" aria-label="分類定義參考" data-writing-category-reference>
  <h3>分類定義參考</h3>
  <p class="field-hint">滑鼠移入分類標題查看定義；也可聚焦或點選。可展開其他階段查閱。</p>
  <details v-for="group in groups" :key="group.phase" :open="group.phase===defaultPhase" :data-reference-phase="group.phase">
   <summary>{{group.phase}} — {{group.label}}<span v-if="group.phase===currentPhase" class="current-phase">目前階段</span></summary>
   <div class="definition-titles"><button v-for="code in group.codes" :key="code.code" type="button" :data-reference-code="code.code" :aria-describedby="activeCode===code.code?tooltipId:undefined" @mouseenter="show(code.code,$event)" @mouseleave="leave" @focus="show(code.code,$event)" @click="inspect(code.code,$event)">{{code.code}} — {{code.label}}</button></div>
  </details>
  <p v-if="!groups.length" class="field-hint">此課程尚未提供分類定義。</p>
  <Teleport to="body"><div v-if="definition&&visible" ref="tooltip" :id="tooltipId" role="tooltip" tabindex="0" class="writing-definition-tip" data-writing-definition-tip :style="position" @mouseenter="cancelClose" @mouseleave="leave"><strong>{{definition.code}} — {{definition.label}}</strong><p>{{categoryDefinition(definition)}}</p></div></Teleport>
 </section>
</template>

<style scoped>
.writing-category-reference{margin-top:24px;padding:16px;border:1px solid var(--line);border-radius:10px;background:var(--surface);min-width:0}.writing-category-reference h3{margin:0;font-size:1rem}.writing-category-reference>.field-hint{margin:8px 0 12px}.writing-category-reference details+details{border-top:1px solid var(--line);margin-top:6px}.writing-category-reference summary{cursor:pointer;min-height:44px;line-height:1.6;padding:10px 0;box-sizing:border-box;overflow-wrap:anywhere}.writing-category-reference summary:focus-visible,.definition-titles button:focus-visible,.writing-definition-tip:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.current-phase{font-size:.75rem;margin-left:8px;color:var(--accent);white-space:nowrap}.definition-titles{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:12px}.definition-titles button{min-width:0;max-width:100%;min-height:44px;border:1px solid var(--line);border-radius:6px;padding:8px 10px;font:inherit;font-size:.85rem;line-height:1.6;white-space:normal;overflow-wrap:anywhere;text-align:left;background:transparent;color:var(--text);cursor:help}.definition-titles button:hover{border-color:var(--accent)}.writing-definition-tip{position:fixed;z-index:2000;box-sizing:border-box;overflow:auto;overflow-wrap:anywhere;padding:14px;border:1px solid var(--line);border-radius:10px;background:var(--surface);color:var(--text);box-shadow:0 6px 24px #0003;font-size:.85rem;line-height:1.8}.writing-definition-tip p{margin:8px 0 0}
</style>
