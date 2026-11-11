<script setup>
import InteractionProfile from '../components/InteractionProfile.vue'
import {onMounted,ref} from 'vue'
import {api} from '../api'
const form=ref({displayName:'',studentNumber:'',departmentYear:''}),error=ref('')
onMounted(async()=>{try{form.value=await api('/studio/profile')}catch(e){error.value=e.message}})
</script>
<template><section class="surface form-surface"><h1>學員檔案</h1><p>學籍資料由校方維護。如需更正，請聯絡課程管理人員。</p><dl><dt>姓名</dt><dd>{{form.displayName}}</dd><dt>學號</dt><dd>{{form.studentNumber||'尚未登錄'}}</dd><dt>學系</dt><dd>{{form.department||'尚未登錄'}}</dd><dt>年級</dt><dd>{{form.gradeLevel||'尚未登錄'}}</dd><template v-if="form.departmentYear"><dt>既有系級紀錄</dt><dd>{{form.departmentYear}}</dd></template></dl><p v-if="error" role="alert">{{error}}</p></section><InteractionProfile/></template>
