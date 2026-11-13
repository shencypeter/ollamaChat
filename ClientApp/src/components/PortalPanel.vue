<script setup>
import {computed,onBeforeUnmount,onMounted,ref,useSlots,watch} from 'vue'
import {ChevronDown,ChevronLeft,ChevronRight} from '@lucide/vue'
import {auth} from '../composables/auth'
const props=defineProps({id:String,panelKey:{type:String,required:true},scope:{type:String,required:true},title:{type:String,required:true},description:String,count:[String,Number],icon:[Object,Function],heading:{type:String,default:'h2'},items:{type:Array,default:null},remote:Boolean,remotePage:{type:Number,default:1},remoteSize:{type:Number,default:5},hasMore:Boolean,busy:Boolean})
const emit=defineEmits(['page-change'])
const slots=useSlots(),expanded=ref(true)
const pageSize=ref(props.remote?props.remoteSize:5),localPage=ref(1)
const page=computed(()=>props.remote?props.remotePage:localPage.value)
const pageCount=computed(()=>Math.max(1,Math.ceil((props.items?.length||0)/pageSize.value)))
const more=computed(()=>props.remote?props.hasMore:page.value<pageCount.value)
const visibleItems=computed(()=>props.remote?props.items:props.items?.slice((page.value-1)*pageSize.value,page.value*pageSize.value))
watch(()=>props.remoteSize,size=>{if(props.remote)pageSize.value=size})
watch(()=>props.items,()=>{localPage.value=1})
function move(next){if(props.remote)emit('page-change',{page:next,pageSize:pageSize.value});else localPage.value=next}
function resize(){move(1)}
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
  <summary :title="expanded?'收合區塊':'展開區塊'"><component :is="heading" class="panel-title"><component :is="icon" v-if="icon" :size="20" aria-hidden="true"/>{{title}}</component><span v-if="count!==undefined&&count!==null" class="panel-count">{{count}}</span><label v-if="items!==null" class="panel-size" @click.stop @keydown.stop>每頁<select v-model.number="pageSize" :aria-label="`${title} 每頁筆數`" :disabled="busy" @change="resize"><option v-for="size in [5,10,20,50]" :key="size" :value="size">{{size}}</option></select></label><ChevronDown class="panel-chevron" :size="19" aria-hidden="true"/></summary>
  <div class="panel-content"><header v-if="description||slots.actions" class="panel-intro"><p v-if="description" class="muted">{{description}}</p><div v-if="slots.actions" class="panel-actions"><slot name="actions"/></div></header><slot :items="visibleItems"/><nav v-if="items!==null&&(page>1||more)" class="panel-pages" :aria-label="`${title} 分頁`"><button class="icon-button" :aria-label="`${title} 上一頁`" :disabled="busy||page===1" @click="move(page-1)"><ChevronLeft :size="17"/></button><span>第 {{page}} 頁{{remote?'':` / ${pageCount}`}}</span><button class="icon-button" :aria-label="`${title} 下一頁`" :disabled="busy||!more" @click="move(page+1)"><ChevronRight :size="17"/></button></nav></div>
 </details>
</template>
<style scoped>
.portal-panel{padding:0;min-width:0;scroll-margin-top:24px}.portal-panel>summary{display:flex;align-items:center;gap:12px;padding:22px 24px;list-style:none;cursor:pointer;border-radius:inherit;user-select:none}.portal-panel>summary::-webkit-details-marker{display:none}.portal-panel>summary::marker{content:''}.portal-panel>summary:hover{background:var(--surface-soft)}.portal-panel>summary:focus-visible{outline:2px solid var(--accent);outline-offset:3px}.panel-title{display:flex;align-items:center;gap:8px;font-size:18px;font-weight:650;line-height:1.6;min-width:0;overflow-wrap:anywhere;margin:0}.panel-title svg{color:var(--accent);flex-shrink:0}.panel-count{font-size:11px;line-height:1.5;color:var(--muted);margin-left:auto;flex-shrink:0;padding:4px 8px;background:var(--surface-soft);border-radius:6px;max-width:40%;overflow-wrap:anywhere}.panel-chevron{flex-shrink:0;color:var(--muted);margin-left:auto;transform:rotate(-90deg)}.panel-count+.panel-chevron{margin-left:0}.portal-panel[open]>summary .panel-chevron{transform:rotate(0)}.panel-content{padding:0 24px 24px}.panel-intro{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;margin-bottom:14px}.panel-intro>.muted{font-size:12px;line-height:1.7;margin:0}.panel-actions{font-size:12px;flex-shrink:0}.panel-actions :deep(a){display:inline-flex;align-items:center;gap:4px;color:var(--accent);line-height:1.7;text-decoration:none}.panel-actions :deep(a:hover){text-decoration:underline}@media(max-width:650px){.portal-panel>summary{padding:18px;gap:9px}.panel-title{font-size:16px}.panel-content{padding:0 18px 18px}.panel-intro{flex-wrap:wrap;gap:8px}.panel-count{font-size:10px}}
</style>
<style scoped>
.portal-panel>summary{flex-wrap:wrap}.panel-size{display:flex;align-items:center;gap:6px;white-space:nowrap;font-size:11px;color:var(--muted);margin-left:auto}.panel-size select{width:58px;min-height:30px;padding:5px 6px;font-size:12px}.panel-count+.panel-size{margin-left:0}.panel-size+.panel-chevron{margin-left:0}.panel-pages{display:flex;align-items:center;justify-content:center;gap:14px;margin-top:16px;font-size:12px;color:var(--muted)}@media(max-width:650px){.panel-size{font-size:10px}.panel-count{max-width:35%}}
</style>
