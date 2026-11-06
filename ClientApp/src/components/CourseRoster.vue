<script setup>
import {onMounted,ref} from 'vue'
import {api} from '../api'
import {auth,roleLabels} from '../composables/auth'
const props=defineProps({course:Object}),emit=defineEmits(['changed']),members=ref([]),accounts=ref([]),error=ref(''),notice=ref(''),busy=ref(false),form=ref({userId:'',courseRole:'student'})
const roles={teacher:'授課教師',ta:'課程助教',student:'學生'}
async function load(){try{members.value=await api(`/courses/${props.course.id}/members`);if(props.course.isOwner)accounts.value=await api(`/courses/${props.course.id}/eligible-accounts`)}catch(e){error.value=e.message}}
async function enroll(){busy.value=true;error.value='';notice.value='';try{await api(`/courses/${props.course.id}/members`,{method:'POST',body:form.value});form.value.userId='';await load();emit('changed');notice.value='本課程學員與角色已更新。'}catch(e){error.value=e.message}finally{busy.value=false}}
onMounted(load)
</script>
<template><section class="surface form-surface"><h2>課程學員與教學成員</h2><p class="muted">此名單僅屬於「{{course.title}}」。課程角色與平台角色分開管理。</p><p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="notice" class="success-box" role="status">{{notice}}</p><form v-if="course.isOwner && course.status!=='archived'" @submit.prevent="enroll"><div class="form-grid"><label>加入學員／教學成員<select aria-label="加入課程帳號" v-model="form.userId" required><option value="">請選擇帳號</option><option v-for="u in accounts" :key="u.id" :value="u.id">{{u.displayName}} · {{u.email}}</option></select></label><label>課程角色<select aria-label="加入課程角色" v-model="form.courseRole"><option v-for="(label,key) in roles" :key="key" :value="key">{{label}}</option></select></label></div><button class="button primary" :disabled="busy">加入／更新課程學員</button><p class="field-hint">沒有帳號時，請聯絡平台管理員建立。</p></form><p v-else class="field-hint">由課程擁有者安排課程學員與角色。</p><div class="table-scroll"><table class="data-table"><thead><tr><th>姓名</th><th>電子郵件</th><th>課程角色</th></tr></thead><tbody><tr v-for="m in members" :key="m.id"><td>{{m.displayName}}</td><td>{{m.email}}</td><td>{{roles[m.courseRole]}}</td></tr></tbody></table></div></section></template>
