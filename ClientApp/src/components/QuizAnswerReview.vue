<script setup>
import {computed} from 'vue'
const props=defineProps({question:Object,text:{type:String,default:''},showKey:Boolean,mark:Object,essayScore:Number})
const selected=computed(()=>{try{const v=JSON.parse(props.text);return Array.isArray(v)?v:[]}catch{return []}})
const options=computed(()=>props.question.questionType==='true_false'?['是','非']:props.question.options||[])
const key=computed(()=>props.showKey?props.question.standardAnswer||[]:[])
</script>
<template>
<div v-if="mark" class="quiz-mark" :class="{ evaluated:mark.autoScore!=null || essayScore!=null }"><strong>{{mark.questionType==='essay' ? (essayScore!=null?'教師已評分':'申論待教師評分') : mark.correct==null?'已自動批改，結果待發布':mark.correct?'答對':'答錯／未答'}}</strong><span v-if="mark.autoScore!=null || essayScore!=null">得分 {{mark.autoScore ?? essayScore}} / {{mark.points}}</span><span v-else>配分 {{mark.points}}</span></div>
<p v-if="!question.questionType || question.questionType==='essay'" class="studio-prose">{{text || '未作答'}}</p>
<template v-else>
 <p v-if="!selected.length" class="field-hint">學生未作答</p>
 <ul class="quiz-option-review">
  <li v-for="(option,index) in options" :key="index" :class="{wrong:showKey && selected.includes(index) && !key.includes(index),missing:showKey && !selected.includes(index) && key.includes(index),correct:showKey && selected.includes(index) && key.includes(index)}">
   <span>{{selected.includes(index)?'☑':'☐'}} {{option}} <small v-if="selected.includes(index)">（學生作答）</small></span>
   <strong v-if="showKey && selected.includes(index) && !key.includes(index)">誤選</strong>
   <strong v-if="showKey && !selected.includes(index) && key.includes(index)">漏選（正確選項）</strong>
   <strong v-if="showKey && selected.includes(index) && key.includes(index)">選對（正確答案）</strong>
  </li>
 </ul>
</template>
</template>
<style scoped>
.quiz-mark{display:flex;justify-content:space-between;gap:12px;flex-wrap:wrap;padding:12px;border:1px solid var(--line);border-radius:8px;background:var(--surface)}
.quiz-mark.evaluated{border-left:4px solid var(--accent)}
.quiz-option-review{list-style:none;padding:0;margin:10px 0;display:grid;gap:6px}
.quiz-option-review li{padding:9px 12px;border:1px solid var(--line);border-radius:6px;display:flex;gap:12px;justify-content:space-between;flex-wrap:wrap}
.wrong{border-color:var(--danger)!important;background:color-mix(in srgb,var(--danger) 9%,transparent)}
.missing{border-style:dashed!important;border-color:var(--accent)!important}
.correct strong{color:var(--green)}.wrong strong{color:var(--danger)}
</style>
