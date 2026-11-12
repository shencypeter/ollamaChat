<script setup>
import {computed,onBeforeUnmount,onMounted,ref} from 'vue'
import {api} from '../api'
import {materialFormat} from '../utils/materialFormat'
const props=defineProps({item:{type:Object,required:true},endpoint:{type:String,required:true}})
const root=ref(null),url=ref(''),failed=ref(false),loading=ref(false)
const image=computed(()=>props.item.previewType?.startsWith('image/'))
const format=computed(()=>materialFormat(props.item))
const label=computed(()=>format.value.label==='YouTube'?'YouTube 影片':format.value.label==='圖片'?'圖片文件':`${format.value.label} 文件`)
let observer=null,disposed=false
async function load(){
 if(!image.value||url.value||failed.value||loading.value)return
 loading.value=true
 try{const blob=await api(props.endpoint,{download:true});if(disposed)return;if(blob.type!==props.item.previewType)throw new Error('檔案格式不符');url.value=URL.createObjectURL(blob)}
 catch{if(!disposed)failed.value=true}finally{loading.value=false}
}
onMounted(()=>{
 if(!image.value)return
 if(typeof IntersectionObserver==='undefined')load()
 else{observer=new IntersectionObserver(entries=>{if(entries.some(entry=>entry.isIntersecting)){observer?.disconnect();observer=null;load()}},{rootMargin:'160px'});observer.observe(root.value)}
})
onBeforeUnmount(()=>{disposed=true;observer?.disconnect();if(url.value)URL.revokeObjectURL(url.value)})
</script>

<template>
<div ref="root" class="resource-visual" :class="{loading}">
 <img v-if="image&&url&&!failed" :src="url" :alt="item.title||item.fileName" loading="lazy" @error="failed=true" />
 <component v-else :is="format.icon" :size="30" :stroke-width="1.5" :aria-label="label" role="img" />
 <span v-if="!image||failed" class="resource-format-label">{{format.label}}</span>
</div>
</template>

<style scoped>
.resource-visual{width:88px;height:64px;flex:0 0 88px;border:1px solid var(--line);border-radius:9px;background:var(--surface-muted,#f6f7fb);display:flex;align-items:center;justify-content:center;position:relative;overflow:hidden;color:var(--accent)}
.resource-visual img{display:block;width:100%;height:100%;object-fit:cover}
.resource-format-label{position:absolute;right:5px;bottom:4px;padding:2px 5px;border-radius:4px;background:color-mix(in srgb,var(--surface) 88%,transparent);color:var(--text);font-size:10px;font-weight:700;line-height:1}
.resource-visual.loading{opacity:.72}
</style>
