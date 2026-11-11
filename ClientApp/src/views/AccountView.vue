<script setup>
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ShieldCheck, KeyRound, UserRound } from '@lucide/vue'
import { auth, roleLabels } from '../composables/auth'
import { api, resetCsrf } from '../api'
defineProps({password:Boolean})
const router=useRouter(), busy=ref(false), error=ref(''), form=reactive({currentPassword:'',newPassword:''})
async function change(){busy.value=true;error.value='';try{await api('/auth/password',{method:'POST',body:form});form.currentPassword='';form.newPassword='';auth.user=null;resetCsrf();await router.replace('/login')}catch(e){error.value=e.message}finally{busy.value=false}}
</script>
<template><section class="page-heading"><div><p class="eyebrow">YOUR ACCOUNT</p><h1>{{password?'更改密碼':'個人資料'}}</h1><p class="muted">管理登入資訊，安心使用教學工作區。</p></div></section><div class="account-layout"><section v-if="!password" class="surface form-surface"><div class="section-title"><h2><UserRound :size="20" />基本資料</h2></div><dl class="profile-details"><div><dt>顯示名稱</dt><dd>{{ auth.user.displayName }}</dd></div><div><dt>電子郵件</dt><dd>{{ auth.user.email }}</dd></div><div><dt>平台管理權限</dt><dd><span class="role-pill">{{ roleLabels[auth.user.role] }}</span></dd></div></dl><RouterLink v-if="auth.user.hasStudentCourses" to="/student/profile" class="button secondary">學員檔案與教材互動紀錄</RouterLink><p class="field-hint">如需調整姓名或平台管理權限，請聯絡管理教授。</p></section><section v-else class="surface form-surface"><div class="section-title"><h2><KeyRound :size="20" />變更密碼</h2></div><form class="stack-form" @submit.prevent="change"><label>目前密碼<input v-model="form.currentPassword" type="password" autocomplete="current-password" required /></label><label>新密碼<input v-model="form.newPassword" type="password" autocomplete="new-password" required minlength="12" maxlength="128" placeholder="至少 12 個字元" /></label><p v-if="error" class="error-box" role="alert">{{ error }}</p><button class="button primary" :disabled="busy">{{ busy?'更新中…':'更新並重新登入' }}</button></form><p class="field-hint"><ShieldCheck :size="15" />變更後會登出目前工作階段。</p></section></div></template>
