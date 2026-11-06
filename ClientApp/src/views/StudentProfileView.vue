<script setup>
import {onMounted,ref} from 'vue'
import {useRouter} from 'vue-router'
import {api} from '../api'
import {auth} from '../composables/auth'
const router=useRouter(),form=ref({displayName:'',studentNumber:'',departmentYear:''}),error=ref(''),busy=ref(false)
onMounted(async()=>{try{form.value=await api('/studio/profile')}catch(e){error.value=e.message}})
async function save(){busy.value=true;error.value='';try{await api('/studio/profile',{method:'PUT',body:form.value});auth.user.displayName=form.value.displayName;auth.user.studentNumber=form.value.studentNumber;auth.profileComplete=true;await router.replace('/student/cases')}catch(e){error.value=e.message}finally{busy.value=false}}
</script>
<template><section class="page-heading"><div><p class="eyebrow">S2 · STUDENT PROFILE</p><h1>完成基本資料</h1><p class="muted">開始案例學習前，請確認姓名與學號。系級可選填。</p></div></section><section class="surface form-surface studio-profile"><form class="stack-form" @submit.prevent="save"><fieldset :disabled="busy"><label>姓名<input v-model="form.displayName" required maxlength="100" autocomplete="name" /></label><label>學號<input v-model="form.studentNumber" required maxlength="40" /></label><label>系級（選填）<input v-model="form.departmentYear" maxlength="100" /></label><p v-if="error" class="error-box" role="alert">{{error}}</p><button class="button primary">{{busy?'儲存中…':'儲存並開始案例學習'}}</button></fieldset></form></section></template>
