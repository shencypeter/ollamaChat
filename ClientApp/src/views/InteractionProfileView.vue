<script setup>
import {useRoute} from 'vue-router'
import {UserRound} from '@lucide/vue'
import InteractionProfile from '../components/InteractionProfile.vue'
const route=useRoute()
</script>
<template><section class="page-heading"><h1><UserRound :size="25" /> 學員檔案</h1></section><RouterLink v-if="route.query.course" :to="`/courses/${route.query.course}?section=students`" class="back-link">← 返回課程學員</RouterLink><InteractionProfile :key="route.fullPath" :student-id="route.params.studentId" :course-id="typeof route.query.course==='string'?route.query.course:undefined" /></template>
