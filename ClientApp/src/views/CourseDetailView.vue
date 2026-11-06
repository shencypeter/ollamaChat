<script setup>
import {computed,onMounted,ref,watch} from 'vue'
import {useRoute} from 'vue-router'
import {setCourseExperience} from '../composables/experience'
import {auth} from '../composables/auth'
import {api} from '../api'
import CourseTools from '../components/CourseTools.vue'
import CourseRoster from '../components/CourseRoster.vue'
import AssessmentsView from './AssessmentsView.vue'
import GradebookPanel from '../components/GradebookPanel.vue'
const route=useRoute(),course=ref(null),cases=ref([]),error=ref(''),loading=ref(true),tools=ref(null)
const sections=[{key:'resources',label:'課程資源／講義'},{key:'students',label:'課程學員／小組'},{key:'assessments',label:'測驗與 PDF 作業'},{key:'grades',label:'測驗／作業成績'},{key:'cases',label:'案例學習'}]
const section=computed(()=>sections.some(s=>s.key===route.query.section)?route.query.section:'resources')
const staff=computed(()=>['teacher','ta'].includes(course.value?.courseRole))
async function load(){try{course.value=await api(`/courses/${route.params.courseId}`);setCourseExperience(course.value.canPractice);cases.value=(await api('/studio/cases')).filter(c=>c.courseId===course.value.id)}catch(e){error.value=e.message}finally{loading.value=false}}
async function changed(){try{course.value=await api(`/courses/${route.params.courseId}`);await tools.value?.reload()}catch(e){error.value=e.message}}
watch(()=>route.fullPath,()=>{if(course.value)setCourseExperience(course.value.canPractice)})
onMounted(load)
</script>
<template><RouterLink to="/courses" class="back-link">← 返回課程管理</RouterLink><p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="loading" class="empty-inline">載入課程中…</p><template v-if="course"><section class="page-heading"><div><p class="eyebrow">{{course.code}} · {{course.termCode}}</p><h1>{{course.title}}</h1><p class="muted">本課程的資源、學員與評量集中於此。{{{teacher:'授課教師',ta:'課程助教',student:'學生'}[course.courseRole]}}</p></div><span class="status-badge">{{{draft:'草稿',active:'進行中',archived:'已封存'}[course.status]}}</span></section><div class="course-detail-strip"><span>{{course.studentCount}} 位學員</span><span>{{course.resourceCount}} 份資源</span><span>{{course.quizCount}} 份測驗</span><span>{{course.reportCount}} 份 PDF 作業</span></div><nav class="course-navigation" aria-label="課程功能"><RouterLink v-for="s in sections" :key="s.key" :to="{path:route.path,query:{section:s.key}}" :class="{active:section===s.key}" :aria-current="section===s.key?'page':undefined">{{s.label}}</RouterLink></nav><CourseTools v-if="section==='resources'" :course="course" :staff="staff" section="resources" @changed="changed" /><template v-if="section==='students'"><CourseRoster v-if="staff" :course="course" @changed="changed" /><CourseTools ref="tools" :course="course" :staff="staff" :show-classmates="!staff" section="students" /></template><AssessmentsView v-if="section==='assessments'" embedded :bound-course="course" /><GradebookPanel v-if="section==='grades'" :course="course" /><section v-if="section==='cases'" class="surface form-surface"><div class="section-title"><h2>本課程案例學習</h2><RouterLink v-if="staff" :to="{path:'/admin/cases',query:{course:course.id}}" class="button secondary">維護本課案例</RouterLink></div><p class="muted">只顯示「{{course.title}}」的已發布案例。</p><article v-for="c in cases" :key="c.id" class="question-editor"><h3>{{c.title}}</h3><p>案例版本 {{c.versionNo}}</p><RouterLink v-if="!staff" :to="{path:`/student/cases/${c.id}`,query:{course:course.id}}" class="button primary">進入案例作答</RouterLink><RouterLink v-else :to="{path:'/teaching',query:{course:course.id}}" class="button secondary">查看本課學習歷程</RouterLink></article><p v-if="!cases.length" class="empty-inline">本課程尚無已發布案例。</p></section></template></template>
