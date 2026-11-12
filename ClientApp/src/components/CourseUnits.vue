<script setup>
import {computed,onMounted,ref} from 'vue'
import {api} from '../api'
import {ArrowDown,ArrowUp,BookOpen,ClipboardList,FileText,Pencil,Plus,Trash2} from '@lucide/vue'
const props=defineProps({course:{type:Object,required:true},manage:Boolean})
const emit=defineEmits(['changed'])
const data=ref(null),error=ref(''),notice=ref(''),busy=ref(false),form=ref(null)
const labels={material:'教材',quiz:'線上測驗',assignment:'線上作業',report:'PDF 作業',exam:'舊版考試'}
const icons={material:BookOpen,quiz:ClipboardList,assignment:FileText,report:FileText,exam:ClipboardList}
const units=computed(()=>data.value?.units||[])
const activities=computed(()=>data.value?.activities||[])
const grouped=computed(()=>units.value.map(unit=>({...unit,activities:activities.value.filter(a=>a.unitId===unit.id)})))
const unassigned=computed(()=>activities.value.filter(a=>!a.unitId))
function link(item){if(item.kind==='material')return `/courses/${props.course.id}/materials/${item.id}`;if(item.kind==='quiz')return `/courses/${props.course.id}/quizzes/${item.id}`;if(item.kind==='assignment')return `/courses/${props.course.id}/assignments/${item.id}`;if(item.kind==='report')return `/courses/${props.course.id}/reports/${item.id}`;return {path:`/courses/${props.course.id}`,query:{section:'assessments'}}}
function reviewLabel(item){return item.kind==='report'?'批改 PDF':item.kind==='quiz'||item.kind==='assignment'?'批改作答':''}
async function load(){busy.value=true;error.value='';try{data.value=await api(`/courses/${props.course.id}/curriculum`)}catch(e){error.value=e.message}finally{busy.value=false}}
async function run(action,message){busy.value=true;error.value='';notice.value='';try{await action();await load();notice.value=message;emit('changed')}catch(e){error.value=e.message}finally{busy.value=false}}
function add(){form.value={name:'',description:'',expectedVersion:0}}
function edit(unit){form.value={id:unit.id,name:unit.name,description:unit.description,expectedVersion:unit.version}}
async function save(){const current=form.value;await run(()=>api(`/courses/${props.course.id}/units${current.id?'/'+current.id:''}`,{method:current.id?'PUT':'POST',body:current}),current.id?'單元已更新。':'單元已建立。');form.value=null}
async function moveOrder(index,direction){const ids=units.value.map(x=>x.id),target=index+direction;[ids[index],ids[target]]=[ids[target],ids[index]];await run(()=>api(`/courses/${props.course.id}/units/reorder`,{method:'PUT',body:{unitIds:ids}}),'單元順序已更新。')}
async function remove(unit){if(!confirm(`刪除「${unit.name}」？只有空單元可以刪除。`))return;await run(()=>api(`/courses/${props.course.id}/units/${unit.id}`,{method:'DELETE'}),'空單元已刪除。')}
async function assign(item,event){const value=event.target.value||null;await run(()=>api(`/courses/${props.course.id}/units/activities/${item.kind}/${item.id}`,{method:'PUT',body:{unitId:value}}),'教學活動所屬單元已更新。')}
onMounted(load)
</script>

<template><section class="course-units">
  <p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="notice" class="success-box" role="status">{{notice}}</p>
  <div v-if="manage&&data?.canManage" class="section-title"><div><h2>單元管理</h2><p>單元不綁固定週次；可依主題、階段或課程進度安排。</p></div><button class="button primary" :disabled="busy||!!form" @click="add"><Plus :size="16"/>新增單元</button></div>
  <form v-if="form" class="surface form-surface" @submit.prevent="save"><fieldset :disabled="busy"><h3>{{form.id?'編輯單元':'新增單元'}}</h3><label>單元名稱<input v-model="form.name" required maxlength="160"/></label><label>單元說明<textarea v-model="form.description" maxlength="8000" rows="4"/></label><button class="button primary">儲存單元</button> <button type="button" class="button secondary" @click="form=null">取消</button></fieldset></form>
  <p v-if="busy&&!data" role="status">載入課程單元中…</p>
  <template v-if="data"><article v-for="(unit,index) in grouped" :key="unit.id" class="surface unit-card">
    <header><div><p class="eyebrow">UNIT {{index+1}}</p><h2>{{unit.name}}</h2><p v-if="unit.description" class="muted">{{unit.description}}</p></div><div v-if="manage&&data.canManage" class="unit-actions"><button class="icon-button" :aria-label="`${unit.name} 上移`" :disabled="busy||index===0" @click="moveOrder(index,-1)"><ArrowUp :size="17"/></button><button class="icon-button" :aria-label="`${unit.name} 下移`" :disabled="busy||index===units.length-1" @click="moveOrder(index,1)"><ArrowDown :size="17"/></button><button class="icon-button" :aria-label="`編輯 ${unit.name}`" :disabled="busy" @click="edit(unit)"><Pencil :size="16"/></button><button class="icon-button unit-delete" :aria-label="`刪除 ${unit.name}`" :disabled="busy||unit.activities.length>0" @click="remove(unit)"><Trash2 :size="16"/></button></div></header>
    <ol class="unit-activities"><li v-for="item in unit.activities" :key="`${item.kind}:${item.id}`"><component :is="icons[item.kind]" :size="18"/><div><small>{{labels[item.kind]}} · {{item.status==='draft'?'草稿':item.status==='closed'?'已關閉':'可使用'}}</small><RouterLink :to="link(item)">{{item.title}}</RouterLink><RouterLink v-if="manage&&data.canManage&&reviewLabel(item)" :to="link(item)" class="activity-review-link">{{reviewLabel(item)}} →</RouterLink></div><label v-if="manage&&data.canManage">所屬單元<select :aria-label="`${item.title} 所屬單元`" :value="item.unitId||''" :disabled="busy" @change="assign(item,$event)"><option value="">未歸類</option><option v-for="choice in units" :key="choice.id" :value="choice.id">{{choice.name}}</option></select></label></li></ol>
    <p v-if="!unit.activities.length" class="empty-inline">此單元尚無教學活動。</p>
  </article>
  <article v-if="unassigned.length" class="surface unit-card unassigned"><header><div><p class="eyebrow">COMPATIBILITY</p><h2>未歸類教學活動</h2><p class="muted">保留相容匯入與舊資料；教師可將內容移入正式單元。</p></div></header><ol class="unit-activities"><li v-for="item in unassigned" :key="`${item.kind}:${item.id}`"><component :is="icons[item.kind]" :size="18"/><div><small>{{labels[item.kind]}}</small><RouterLink :to="link(item)">{{item.title}}</RouterLink></div><label v-if="manage&&data.canManage">所屬單元<select :aria-label="`${item.title} 所屬單元`" value="" :disabled="busy" @change="assign(item,$event)"><option value="">未歸類</option><option v-for="choice in units" :key="choice.id" :value="choice.id">{{choice.name}}</option></select></label></li></ol></article>
  <p v-if="!units.length&&!unassigned.length" class="surface empty-inline">尚未建立課程單元。{{data.canManage?'請新增第一個單元，再安排教材與評量。':''}}</p></template>
</section></template>

<style scoped>
.course-units{display:grid;gap:18px}.unit-card{padding:22px}.unit-card>header{display:flex;justify-content:space-between;align-items:flex-start;gap:18px;padding-bottom:16px;border-bottom:1px solid var(--line)}.unit-card h2{font-size:19px}.unit-card header .muted{white-space:pre-wrap;margin-top:5px}.unit-actions{display:flex;gap:3px}.unit-delete{color:var(--danger)}.unit-activities{list-style:none;padding:0;margin:0}.unit-activities li{display:grid;grid-template-columns:auto minmax(0,1fr) minmax(150px,240px);align-items:center;gap:12px;padding:15px 0;border-bottom:1px solid var(--line)}.unit-activities li:last-child{border-bottom:0}.unit-activities div{display:grid;min-width:0}.unit-activities small{color:var(--muted);font-size:10px}.unit-activities a{font-weight:600;overflow-wrap:anywhere}.unit-activities .activity-review-link{width:max-content;margin-top:5px;color:var(--accent);font-size:11px;font-weight:700}.unit-activities label{font-size:10px}.unit-activities select{margin-top:3px;padding:7px 9px}.unassigned{border-style:dashed}@media(max-width:650px){.unit-card>header{flex-direction:column}.unit-activities li{grid-template-columns:auto minmax(0,1fr)}.unit-activities label{grid-column:1/-1}.unit-actions{align-self:flex-end}}
</style>
