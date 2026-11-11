<script setup>
import {ref,computed} from 'vue'
import {Share2,Copy} from '@lucide/vue'
const props=defineProps({courseId:String,resourceId:String,title:String}),notice=ref('')
const url=computed(()=>new URL(`/courses/${props.courseId}/materials/${props.resourceId}`,window.location.origin).href)
async function copy(){try{await navigator.clipboard.writeText(url.value);notice.value='連結已複製。'}catch{notice.value='無法自動複製，請選取下方連結。'}}
async function share(){notice.value='';if(navigator.share){try{await navigator.share({title:props.title,url:url.value});notice.value='已開啟分享。'}catch(e){if(e.name!=='AbortError')await copy()}}else await copy()}
</script>
<template><div class="resource-share"><button type="button" class="button secondary" @click="share"><Share2 :size="16" />分享連結</button><button type="button" class="button secondary" @click="copy"><Copy :size="16" aria-hidden="true" />複製連結</button><p v-if="notice" role="status">{{notice}}</p><input v-if="notice.startsWith('無法')" aria-label="教材分享網址" :value="url" readonly @focus="$event.target.select()" /></div></template>
