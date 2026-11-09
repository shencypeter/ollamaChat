<script setup>
import {onMounted,ref} from 'vue'
import {api} from '../api'
const props=defineProps({courseId:String,resourceId:String}),data=ref(null),error=ref('')
onMounted(async()=>{try{data.value=await api(`/courses/${props.courseId}/resources/${props.resourceId}/metadata`)}catch(e){error.value=e.message}})
</script>
<template><section class="surface form-surface"><h2>教師摘要</h2><p v-if="error" class="error-box" role="alert">{{error}}</p><template v-if="data"><p class="muted">授課日期：{{data.teachingDate||'未指定授課日期'}}</p><p class="material-summary">{{data.summary||'尚未提供教師摘要。'}}</p></template></section></template>
<style scoped>.material-summary{white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.8}</style>
