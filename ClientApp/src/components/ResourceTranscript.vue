<script setup>
import {onMounted,onBeforeUnmount,ref} from 'vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
import {api} from '../api'
const props=defineProps({courseId:String,resourceId:String,editable:Boolean})
const data=ref(null),draft=ref(''),editing=ref(false),busy=ref(false),error=ref(''),notice=ref('')
const base=`/courses/${props.courseId}/resources/${props.resourceId}/transcript`
async function load(){try{data.value=await api(base)}catch(e){error.value=e.message}}
function edit(){draft.value=data.value.transcript;editing.value=true;notice.value='';error.value=''}
async function save(){busy.value=true;error.value='';try{data.value=await api(base,{method:'PUT',body:{transcript:draft.value,expectedVersion:data.value.transcriptVersion}});editing.value=false;notice.value='逐字稿已儲存。'}catch(e){error.value=e.message}finally{busy.value=false}}
async function cancel(){editing.value=false;error.value='';await load()}
function dirty(){return editing.value&&draft.value!==data.value?.transcript}
function guard(){return !dirty()||confirm('逐字稿有尚未儲存的修改，確定離開？')}
function unload(e){if(dirty()){e.preventDefault();e.returnValue=''}}
onBeforeRouteLeave(guard);onBeforeRouteUpdate(guard)
onMounted(()=>{load();window.addEventListener('beforeunload',unload)});onBeforeUnmount(()=>window.removeEventListener('beforeunload',unload))
</script>
<template><section class="surface form-surface"><h2>課程回放逐字稿</h2><p v-if="error" role="alert" class="error-box">{{error}}</p><p v-if="notice" role="status">{{notice}}</p><template v-if="data"><form v-if="editing&&editable" @submit.prevent="save"><label>逐字稿<textarea aria-label="編輯逐字稿" v-model="draft" maxlength="100000" rows="14" :disabled="busy" /></label><p class="field-hint">可貼上逐字稿或含時間標記的文字，最多 100,000 字；以純文字顯示。</p><button class="button primary" :disabled="busy||!dirty()">儲存逐字稿</button><button type="button" class="button secondary" :disabled="busy" @click="cancel">取消逐字稿編輯</button></form><template v-else><button v-if="editable" class="button secondary" @click="edit">{{data.transcript?'編輯逐字稿':'新增逐字稿'}}</button><div v-if="data.transcript" class="transcript-text">{{data.transcript}}</div><p v-else class="muted">尚未提供逐字稿。</p></template></template><p v-else-if="!error">載入中…</p></section></template>
<style scoped>.transcript-text{white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.9;margin-top:16px;max-height:65vh;overflow:auto}.button{margin:8px 8px 8px 0}textarea{width:100%;box-sizing:border-box}</style>
