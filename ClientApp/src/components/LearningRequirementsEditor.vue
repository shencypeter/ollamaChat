<script setup>
import {onMounted,ref} from 'vue'
import {api} from '../api'
const props=defineProps({modelValue:{type:Array,default:()=>[]},courseId:String})
const emit=defineEmits(['update:modelValue'])
const resources=ref([]),error=ref('')
onMounted(async()=>{try{resources.value=await api(`/courses/${props.courseId}/resources`)}catch(e){error.value=e.message}})
function toggle(id,checked){emit('update:modelValue',checked?[...props.modelValue,{resourceId:id,minimumSeconds:60}]:props.modelValue.filter(x=>x.resourceId!==id))}
function setTime(id,value){emit('update:modelValue',props.modelValue.map(x=>x.resourceId===id?{...x,minimumSeconds:Number(value)}:x))}
</script>
<template><fieldset class="question-editor"><legend>測驗前教材學習門檻（選填）</legend><p class="field-hint">勾選教材並設定最低有效閱讀／觀看秒數，全部達標才可開始測驗。不勾選則不設門檻。支援 PDF、圖片及 MP4／WebM；下載不計入時間，發布後設定鎖定。</p><p v-if="error" class="error-box" role="alert">{{error}}</p><div v-for="r in resources.filter(r=>r.learningType)" :key="r.id" class="requirement-row"><label class="choice-label"><input type="checkbox" :checked="modelValue.some(x=>x.resourceId===r.id)" @change="toggle(r.id,$event.target.checked)" />{{r.title||r.fileName}}</label><label v-if="modelValue.some(x=>x.resourceId===r.id)">最低有效秒數<input type="number" min="1" max="86400" step="1" required :value="modelValue.find(x=>x.resourceId===r.id).minimumSeconds" @input="setTime(r.id,$event.target.value)" /></label></div><p v-if="!resources.some(r=>r.learningType)" class="muted">本課尚無可追蹤教材，請先至課程資源上傳。</p></fieldset></template>
<style scoped>.requirement-row{display:flex;align-items:center;gap:16px;flex-wrap:wrap;margin:12px 0}.requirement-row>label:first-child{flex:1;min-width:180px}.requirement-row input[type=number]{max-width:150px}</style>
