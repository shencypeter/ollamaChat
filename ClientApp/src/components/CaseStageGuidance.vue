<script setup>
import {computed} from 'vue'
import guidance from '../../../Configuration/CaseAnalysis/stage-guidance.json'
const props=defineProps({stage:String,teacher:Boolean})
const current=computed(()=>guidance.stages.find(item=>item.key===props.stage))
const visibleStages=computed(()=>props.teacher?guidance.stages:current.value?[current.value]:[])
</script>

<template>
 <details class="stage-guidance" data-stage-guidance :data-guidance-audience="teacher?'teacher':'student'">
  <summary>{{teacher?'教師階段說明':'本階段撰寫指引'}}<span v-if="current">{{current.phase}} {{current.title}}</span></summary>
  <p class="guidance-source">{{guidance.sourceNote}}</p>
  <div v-if="teacher" class="guide-column-headings" aria-hidden="true"><span>階段</span><span>核心問題與撰寫指引</span></div>
  <template v-for="item in visibleStages" :key="item.key">
   <details v-if="teacher" class="phase-guide" :data-guide-phase="item.phase" :open="item.key===stage">
    <summary class="phase-summary"><span class="phase-title">{{item.phase}} {{item.title}}<small>{{item.englishTitle}}</small></span><span class="phase-core-question">{{item.coreQuestion}}</span></summary>
    <div class="phase-guide-content"><p>{{item.description}}</p><ul><li v-for="topic in item.topics" :key="topic">{{topic}}</li></ul>
    <p class="writing-focus"><strong>撰寫重點：</strong>{{item.writingFocus}}</p></div>
   </details>
   <section v-else class="phase-guide" :data-guide-phase="item.phase" :aria-label="`${item.phase} ${item.title}撰寫指引`">
    <h4>{{item.phase}} {{item.title}}<span>{{item.englishTitle}}</span></h4>
    <p>{{item.description}}</p><ul><li v-for="topic in item.topics" :key="topic">{{topic}}</li></ul>
    <p class="writing-focus"><strong>撰寫重點：</strong>{{item.writingFocus}}</p>
   </section>
  </template>
  <table v-if="!teacher" class="phase-comparison"><caption>四階段的核心差異</caption><thead><tr><th scope="col">階段</th><th scope="col">學生需要回答的核心問題</th></tr></thead><tbody><tr v-for="item in guidance.stages" :key="item.key"><th scope="row">{{item.phase}} {{item.title}}</th><td>{{item.coreQuestion}}</td></tr></tbody></table>
 </details>
</template>

<style scoped>
.phase-guide-content{margin-inline-start:calc(36% + 24px);min-width:0}@media(max-width:650px){.phase-guide-content{margin-inline-start:16px}}
.guide-column-headings,.phase-guide>.phase-summary{display:grid;grid-template-columns:minmax(0,36%) minmax(0,1fr);column-gap:24px;align-items:start}.guide-column-headings{margin-top:16px;padding:12px 0;background:var(--surface-header);font-size:.8rem;font-weight:600;color:var(--muted)}.guide-column-headings>span:first-child{padding-left:16px}.phase-guide>.phase-summary{list-style:none}.phase-summary::-webkit-details-marker{display:none}.phase-summary::marker{content:''}.stage-guidance .phase-summary>.phase-title,.stage-guidance .phase-summary>.phase-core-question{display:block;margin:0;font-size:.9rem;color:var(--text)}.phase-summary>.phase-title{position:relative;padding-left:16px;font-weight:600}.phase-title::before{content:'▸';position:absolute;left:0;top:0;display:inline-block}.phase-guide[open]>.phase-summary>.phase-title::before{transform:rotate(90deg)}.phase-title small{display:block;font-size:.8rem;font-weight:400;color:var(--muted);line-height:1.7}.phase-summary>.phase-core-question{font-weight:400}@media(max-width:650px){.guide-column-headings{display:none}.phase-guide>.phase-summary{grid-template-columns:minmax(0,1fr);row-gap:4px}.phase-summary>.phase-core-question{padding-left:16px}}
.stage-guidance{margin:18px 0;padding:14px;border:1px solid var(--line);border-radius:10px;min-width:0;overflow-wrap:anywhere;background:var(--surface)}.stage-guidance summary{cursor:pointer;min-height:44px;line-height:1.6;padding:8px 0;box-sizing:border-box}.stage-guidance summary:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.stage-guidance summary span,.phase-guide h4 span{display:inline-block;margin-left:10px;font-size:.8rem;color:var(--muted);font-weight:400}.guidance-source{font-size:.8rem;line-height:1.8;color:var(--muted);padding:10px 12px;border-left:3px solid var(--accent);background:var(--accent-soft)}.phase-guide{border-top:1px solid var(--line);padding:6px 0;font-size:.9rem;line-height:1.8}.phase-guide h4{font-size:1rem;margin:10px 0}.phase-guide p{margin:10px 0}.phase-guide ul{padding-left:24px;margin:10px 0}.phase-guide li+li{margin-top:4px}.writing-focus{padding:10px 12px;background:var(--accent-soft);border-radius:6px}.phase-comparison{width:100%;border-collapse:collapse;table-layout:fixed;font-size:.85rem;line-height:1.7;margin-top:16px}.phase-comparison caption{text-align:left;font-size:.95rem;font-weight:600;padding:8px 0}.phase-comparison th,.phase-comparison td{text-align:left;vertical-align:top;border-bottom:1px solid var(--line);padding:10px 8px;overflow-wrap:anywhere}.phase-comparison th:first-child{width:36%}.phase-comparison thead{background:var(--accent-soft)}
</style>
