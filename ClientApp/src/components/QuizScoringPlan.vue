<script setup>
import {computed} from 'vue'
import {quizScoringPlan} from '../utils/quizScoringPlan'
const props=defineProps({maxScore:[Number,String],questions:Array})
const plan=computed(()=>quizScoringPlan(props.maxScore,props.questions))
</script>
<template>
<section class="question-editor" aria-label="測驗配分組成">
 <h3>這份測驗的 {{maxScore}} 分如何組成</h3>
 <p class="field-hint">這是本份測驗內的配分，與課程成績權重分開。修改逐題配分時即時更新。</p>
 <p v-if="plan.error" class="error-box" role="alert">{{plan.error}}</p>
 <template v-else><div class="table-scroll"><table class="data-table"><thead><tr><th>題型</th><th>題數</th><th>每題配分</th><th>小計</th><th>批改方式</th></tr></thead><tbody><tr v-for="row in plan.rows" :key="row.type"><td>{{row.label}}</td><td>{{row.count}}</td><td>{{row.each==null?'個別配分':row.each+' 分'}}</td><td>{{row.total}} 分</td><td>{{row.type==='essay'?'教師評分':'系統自動批改'}}</td></tr></tbody></table></div><p aria-live="polite"><strong>合計 {{plan.total}} 分</strong> · 客觀題 {{plan.objectiveTotal}} 分 ＋ 申論題 {{plan.essayTotal}} 分</p></template>
</section>
</template>
