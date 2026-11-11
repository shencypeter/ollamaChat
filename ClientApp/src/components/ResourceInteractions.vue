<script setup>
import {ref,onMounted} from 'vue'
import {ThumbsUp,MessageCircle} from '@lucide/vue'
import {api} from '../api'
import {newRequestId} from '../utils/requestId'
const props=defineProps({courseId:String,resourceId:String}),data=ref(null),error=ref(''),busy=ref(false),text=ref(''),page=ref(1)
const base=`/courses/${props.courseId}/resources/${props.resourceId}`
let requestId=newRequestId(),sentText=null
async function load(){data.value=await api(`${base}/interactions?page=${page.value}`)}
async function run(action){busy.value=true;error.value='';try{await action()}catch(e){error.value=e.message}finally{busy.value=false}}
async function like(){await run(async()=>{await api(`${base}/like`,{method:'PUT',body:{liked:!data.value.liked}});await load()})}
async function send(){await run(async()=>{if(sentText!==text.value){requestId=newRequestId();sentText=text.value}await api(`${base}/comments`,{method:'POST',body:{id:requestId,text:text.value}});text.value='';sentText=null;requestId=newRequestId();page.value=1;await load()})}
async function turn(delta){await run(async()=>{page.value+=delta;await load()})}
onMounted(()=>run(load))
</script>
<template><section class="surface form-surface interaction-panel"><h2>教材互動</h2><p v-if="error" class="error-box" role="alert">{{error}}</p><template v-if="data"><div class="studio-actions"><button class="button secondary" :aria-pressed="data.liked" :disabled="busy||!data.writable" @click="like"><ThumbsUp :size="17" />{{data.liked?'已按讚':'讚'}} {{data.likes}}</button><span><MessageCircle :size="17" /> 留言 {{data.total}}</span></div><form v-if="data.writable" @submit.prevent="send"><label>留言<textarea :disabled="busy" v-model="text" required maxlength="4000" rows="3"></textarea></label><button class="button primary" :disabled="busy||!text.trim()">送出留言</button></form><p v-else class="muted">目前為唯讀，無法按讚或留言。</p><article v-for="item in data.items" :key="item.id" class="question-editor"><strong>{{item.author}}</strong><small> · {{new Date(item.createdAt).toLocaleString()}}</small><p class="interaction-text">{{item.text}}</p></article><p v-if="!data.total">目前沒有留言。</p><nav v-if="data.total>20" class="studio-actions" aria-label="留言分頁"><button class="button secondary" :disabled="busy||page<=1" @click="turn(-1)">上一頁</button><span>第 {{data.page}} 頁</span><button class="button secondary" :disabled="busy||page*20>=data.total" @click="turn(1)">下一頁</button></nav></template></section></template>
