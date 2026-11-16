<script setup>
import {onMounted,ref,computed} from 'vue'
import {api} from '../api'
const props=defineProps({modelValue:{type:Array,default:()=>[]},courseId:String})
const emit=defineEmits(['update:modelValue'])
const resources=ref([]),error=ref('')
const visibleResources=computed(()=>resources.value.filter(r=>r.learningType?.startsWith('video/')||props.modelValue.some(x=>x.resourceId===r.id)))
onMounted(async()=>{try{resources.value=await api(`/courses/${props.courseId}/resources`)}catch(e){error.value=e.message}})
function toggle(id,checked){emit('update:modelValue',checked?[...props.modelValue,{resourceId:id,minimumSeconds:60}]:props.modelValue.filter(x=>x.resourceId!==id))}
function setTime(id,value){emit('update:modelValue',props.modelValue.map(x=>x.resourceId===id?{...x,minimumSeconds:Number(value)}:x))}
</script>
<template><fieldset class="question-editor"><legend>測驗前教材學習門檻（選填）</legend><p class="field-hint">勾選影片並設定最低有效觀看秒數，全部達標才可開始測驗。不勾選則不設門檻。僅支援 MP4／WebM 及 YouTube 回放，以有效播放的內容時間計算（2倍速實看10分鐘計20分鐘）；下載及快轉跳過不計，發布後設定鎖定。PDF、圖片、Office 與其他附件不計時。</p><p v-if="error" class="error-box" role="alert">{{error}}</p><div v-for="r in visibleResources" :key="r.id" class="requirement-row"><label class="choice-label"><input type="checkbox" :checked="modelValue.some(x=>x.resourceId===r.id)" @change="toggle(r.id,$event.target.checked)" /><span class="choice-text">{{r.title||r.fileName}}</span></label><template v-if="modelValue.some(x=>x.resourceId===r.id)"><label v-if="r.learningType?.startsWith('video/')">最低有效秒數<input type="number" min="1" max="86400" step="1" required :value="modelValue.find(x=>x.resourceId===r.id).minimumSeconds" @input="setTime(r.id,$event.target.value)" /></label><p v-else class="field-hint">此舊教材門檻以點閱確認；原要求 {{modelValue.find(x=>x.resourceId===r.id).minimumSeconds}} 秒及已達標紀錄保留。可保留或取消，不能新增靜態計時要求。</p></template></div><p v-if="!visibleResources.length" class="muted">本課尚無可計時影片，請先至課程教材上傳。</p></fieldset></template>
<style scoped>.requirement-row{display:flex;align-items:center;gap:16px;flex-wrap:wrap;margin:12px 0}.requirement-row>label:first-child{flex:1;min-width:180px}.requirement-row input[type=number]{max-width:150px}</style>
