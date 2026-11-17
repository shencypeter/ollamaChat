<script setup>
import {computed} from 'vue'
import {annotationPhaseAlignment,phaseNames} from '../utils/caseAnnotations'
const props=defineProps({content:{type:String,required:true},stage:String,annotations:{type:Array,default:()=>[]},classifications:{type:Array,default:()=>[]},displayMode:{type:String,default:'underline'}})
const alignment=computed(()=>annotationPhaseAlignment(props.content,props.annotations,props.classifications,props.stage))
const phaseLabel=phase=>`${phase} ${phaseNames[phase]}`
</script>
<template>
 <aside v-if="alignment" class="phase-alignment-notice" data-phase-alignment aria-label="階段核對（依 AI 分類標註）">
  <strong>階段核對（依 AI 分類標註）</strong>
  <p class="alignment-source">系統依分類標註整理，非 AI 回覆原文。</p>
  <p>目前階段：{{phaseLabel(alignment.phase)}}。</p>
  <p v-if="alignment.currentCodes.length">標註中屬於目前階段的分類：{{alignment.currentCodes.join('、')}}（{{displayMode==='badges'?'編碼徽章':'分類色底線'}}）。目前階段相符不代表臨床正確。</p>
  <p v-if="alignment.otherCodes.length">其他階段分類：{{alignment.otherPhases.map(phaseLabel).join('、')}}（{{alignment.otherCodes.join('、')}}，{{displayMode==='badges'?'編碼徽章':'彩色底線'}}）。<template v-if="!alignment.currentCodes.length">已標註內容歸其他階段，請核對是否放錯階段；若它是主要作答，可移至相應階段。未標記內容不代表缺漏，必要背景引用可以保留。</template><template v-else>請檢視是否切合本階段問題；必要的跨階段背景可以保留，不因跨階段分類就判為錯誤。</template></p>
  <p v-if="alignment.misplacedCodes.length">AI 另標示階段可能不符：{{alignment.misplacedCodes.join('、')}}；仍需核對原文與教學要求。</p>
  <p v-if="alignment.unsupported">部分敘述標為有待查證，請核對案例依據。</p>
  <p class="alignment-caveat">以上依 AI 分類辨識，不代表臨床正確，未驗證臨床內容；未標記內容也不代表錯誤。</p>
 </aside>
</template>
<style scoped>
.phase-alignment-notice{margin:14px 0;padding:12px 14px;border:1px solid var(--line);border-left:3px solid var(--accent);border-radius:8px;background:color-mix(in srgb,var(--surface) 75%,transparent);font-size:.85rem;line-height:1.75;overflow-wrap:anywhere}.phase-alignment-notice p{margin:6px 0}.alignment-source,.alignment-caveat{color:var(--muted);font-size:.8rem}
</style>
