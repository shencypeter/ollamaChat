<script setup>
import {computed,onBeforeUnmount,onMounted,ref,useSlots,watch} from 'vue'
import {ChevronDown} from '@lucide/vue'
import {auth} from '../composables/auth'
const props=defineProps({id:String,panelKey:{type:String,required:true},scope:{type:String,required:true},title:{type:String,required:true},description:String,count:[String,Number],icon:[Object,Function],heading:{type:String,default:'h2'}})
const slots=useSlots(),expanded=ref(true)
const storageKey=computed(()=>auth.user?.id?`portal:panels:v1:${auth.user.id}:${props.scope}:${props.panelKey}`:null)
watch(storageKey,key=>{try{expanded.value=!key||localStorage.getItem(key)!=='closed'}catch{expanded.value=true}},{immediate:true})
function remember(){if(storageKey.value)try{localStorage.setItem(storageKey.value,expanded.value?'open':'closed')}catch{/* Browser storage is optional. */}}
function toggle(event){if(expanded.value===event.target.open)return;expanded.value=event.target.open;remember()}
function expand(){expanded.value=true;remember()}
function revealAnchor(){if(props.id&&window.location.hash===`#${props.id}`)expand()}
onMounted(()=>{revealAnchor();window.addEventListener('hashchange',revealAnchor)})
onBeforeUnmount(()=>window.removeEventListener('hashchange',revealAnchor))
defineExpose({expand})
</script>
<template>
 <details :id="id" class="surface portal-panel" :open="expanded" @toggle="toggle">
  <summary :title="expanded?'收合區塊':'展開區塊'"><component :is="heading" class="panel-title"><component :is="icon" v-if="icon" :size="20" aria-hidden="true"/>{{title}}</component><span v-if="count!==undefined&&count!==null" class="panel-count">{{count}}</span><ChevronDown class="panel-chevron" :size="19" aria-hidden="true"/></summary>
  <div class="panel-content"><header v-if="description||slots.actions" class="panel-intro"><p v-if="description" class="muted">{{description}}</p><div v-if="slots.actions" class="panel-actions"><slot name="actions"/></div></header><slot/></div>
 </details>
</template>
<style scoped>
.portal-panel{padding:0;min-width:0;scroll-margin-top:24px}.portal-panel>summary{display:flex;align-items:center;gap:12px;padding:22px 24px;list-style:none;cursor:pointer;border-radius:inherit;user-select:none}.portal-panel>summary::-webkit-details-marker{display:none}.portal-panel>summary::marker{content:''}.portal-panel>summary:hover{background:var(--surface-soft)}.portal-panel>summary:focus-visible{outline:2px solid var(--accent);outline-offset:3px}.panel-title{display:flex;align-items:center;gap:8px;font-size:18px;font-weight:650;line-height:1.6;min-width:0;overflow-wrap:anywhere;margin:0}.panel-title svg{color:var(--accent);flex-shrink:0}.panel-count{font-size:11px;line-height:1.5;color:var(--muted);margin-left:auto;flex-shrink:0;padding:4px 8px;background:var(--surface-soft);border-radius:6px;max-width:40%;overflow-wrap:anywhere}.panel-chevron{flex-shrink:0;color:var(--muted);margin-left:auto;transform:rotate(-90deg)}.panel-count+.panel-chevron{margin-left:0}.portal-panel[open]>summary .panel-chevron{transform:rotate(0)}.panel-content{padding:0 24px 24px}.panel-intro{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;margin-bottom:14px}.panel-intro>.muted{font-size:12px;line-height:1.7;margin:0}.panel-actions{font-size:12px;flex-shrink:0}.panel-actions :deep(a){display:inline-flex;align-items:center;gap:4px;color:var(--accent);line-height:1.7;text-decoration:none}.panel-actions :deep(a:hover){text-decoration:underline}@media(max-width:650px){.portal-panel>summary{padding:18px;gap:9px}.panel-title{font-size:16px}.panel-content{padding:0 18px 18px}.panel-intro{flex-wrap:wrap;gap:8px}.panel-count{font-size:10px}}
</style>
