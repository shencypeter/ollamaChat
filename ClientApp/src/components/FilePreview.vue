<script setup>
import {ref,watch,onBeforeUnmount} from 'vue'
import {api} from '../api'
import {FileText} from '@lucide/vue'
const emit=defineEmits(['expanded-change'])
const props=defineProps({endpoint:String,fileName:String,contentType:String,compact:Boolean,active:{type:Boolean,default:true}})
const url=ref(''),busy=ref(false),error=ref(''),expanded=ref(false)
watch(expanded,value=>emit('expanded-change',value))
let generation=0
function clear(){generation++;if(url.value)URL.revokeObjectURL(url.value);url.value='';error.value='';busy.value=false;expanded.value=false}
async function load(){
 if(url.value || busy.value)return
 const request=++generation;busy.value=true;error.value=''
 try{const blob=await api(props.endpoint,{download:true});if(request!==generation)return
  if(blob.type!==props.contentType)throw new Error('檔案格式不符，請下載後開啟。')
  url.value=URL.createObjectURL(blob)
 }catch(e){if(request===generation)error.value=e.message}finally{if(request===generation)busy.value=false}
}
watch(()=>[props.endpoint,props.contentType],()=>{clear();if(!props.compact&&props.contentType?.startsWith('image/'))load()},{immediate:true})
watch(()=>props.active,v=>{if(!v)clear()})
onBeforeUnmount(clear)
</script>
<template>
<div v-if="contentType" class="file-preview">
 <p v-if="busy" role="status">載入預覽中…</p>
 <p v-if="error" role="alert">{{error}} <button class="text-button" @click="load">重試預覽</button></p>
 <button v-if="compact&&contentType.startsWith('image/')&&!expanded" class="button secondary" @click="expanded=true;load()">預覽圖片</button>
 <template v-if="contentType.startsWith('image/') && url && (!compact||expanded)">
  <button type="button" class="file-thumbnail" :aria-label="`${expanded?'縮小':'放大'}圖片：${fileName}`" :aria-expanded="expanded" @click="expanded=!expanded">
   <img :src="url" :alt="fileName" :class="{expanded}" @error="error='無法顯示圖片，請下載後開啟。'" />
  </button>
 </template>
 <template v-if="contentType==='application/pdf'">
  <button type="button" :class="expanded?'button secondary':'pdf-document-placeholder'" :disabled="busy" :aria-label="expanded?'收合 PDF 預覽':'預覽 PDF'" :aria-expanded="expanded" @click="expanded=!expanded;if(expanded)load()">
   <span v-if="!expanded" class="pdf-document-glyph" aria-hidden="true"><FileText :size="22" :stroke-width="1.4" /><strong>PDF</strong></span>
   <span>{{expanded?'收合 PDF 預覽':'預覽 PDF'}}</span>
  </button>
  <div v-if="expanded && url">
   <object :data="url" type="application/pdf" class="file-pdf" :aria-label="`PDF 預覽：${fileName}`"><p>瀏覽器無法內嵌 PDF，請使用下載按鈕。</p></object>
   <p class="field-hint">若預覽空白或此瀏覽器不支援 PDF，請使用下載按鈕開啟原檔。</p>
  </div>
 </template>
</div>
</template>
<style scoped>
.file-preview{margin:.8rem 0;min-width:0}
.pdf-document-placeholder{width:100%;min-height:30px;display:flex;flex-direction:row;align-items:center;justify-content:center;gap:12px;border:1px solid var(--line);border-radius:8px;background:var(--surface);color:var(--text);font-size:13px}
.pdf-document-placeholder:hover{background:var(--accent-soft);border-color:var(--accent)}
.pdf-document-glyph{position:relative;display:inline-flex;color:var(--accent);padding:0 8px 6px}
.pdf-document-glyph strong{position:absolute;bottom:4px;left:4px;padding:3px 8px;border-radius:4px;background:#b42332;color:#fff;font-size:12px;font-weight:700;letter-spacing:1px}
.file-thumbnail{display:block;max-width:100%;border:1px solid var(--border-color,#8885);border-radius:8px;padding:4px;background:transparent;cursor:zoom-in}
.file-thumbnail img{display:block;width:180px;height:120px;object-fit:contain;max-width:100%}
.file-thumbnail img.expanded{width:auto;height:auto;max-width:100%;max-height:70vh;object-fit:contain;cursor:zoom-out}
.file-pdf{display:block;width:100%;height:65vh;min-height:300px;margin-top:.7rem;border:1px solid #8885;border-radius:8px;background:white}
</style>
