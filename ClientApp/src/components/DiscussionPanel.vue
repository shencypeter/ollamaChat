<script setup>
import {ref,onMounted} from 'vue'
import {api} from '../api'
const props=defineProps({assignmentId:String,staff:Boolean,practice:Boolean}),items=ref([]),question=ref(''),error=ref(''),busy=ref(false)
const path=`/assignments/${props.assignmentId}/discussions`
async function load(){try{items.value=await api(path)}catch(e){error.value=e.message}}
async function save(item){busy.value=true;error.value='';try{await api(item?`${path}/${item.id}`:path,{method:item?'PUT':'POST',body:item?{response:item.response,isPublic:item.isPublic}:{question:question.value}});question.value='';await load()}catch(e){error.value=e.message}finally{busy.value=false}}
onMounted(load)
</script>
<template><section class="surface form-surface"><h2>作業提問與答覆</h2><p class="muted">問題預設私人；教師／助教可選擇公開問題與答覆給同課程學生。</p><p v-if="error" class="error-box" role="alert">{{error}}</p><form v-if="!staff && !practice" @submit.prevent="save()"><label>提出問題<textarea v-model="question" required maxlength="8000" rows="3"></textarea></label><button class="button primary" :disabled="busy">送出私人問題</button></form><article v-for="item in items" :key="item.id" class="question-editor"><strong>{{item.author}} · {{item.isPublic?'公開':'私人'}}</strong><p style="white-space:pre-wrap">{{item.question}}</p><template v-if="staff"><label>答覆<textarea v-model="item.response" maxlength="16000" rows="3"></textarea></label><label><input type="checkbox" v-model="item.isPublic" />公開此問題與答覆</label><button class="button secondary" :disabled="busy" @click="save(item)">儲存答覆與可見性</button></template><p v-else style="white-space:pre-wrap">{{item.response || '等待教師／助教答覆'}}</p></article><p v-if="!items.length" class="muted">目前沒有可見的提問。</p></section></template>
