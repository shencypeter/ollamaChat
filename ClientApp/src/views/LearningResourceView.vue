<script setup>
import {onMounted,onBeforeUnmount,ref} from 'vue'
import {useRoute} from 'vue-router'
import {api} from '../api'
import {setCourseExperience} from '../composables/experience'
const route=useRoute(),meta=ref(null),url=ref(''),video=ref(null),error=ref(''),active=ref(false),busy=ref(false),loaded=ref(false),seconds=ref(0)
const base=`/courses/${route.params.courseId}/resources/${route.params.resourceId}/learning`
let session=null,timer=null,lastInput=Date.now(),sending=false,disposed=false
function activity(event){if(event.isTrusted)lastInput=Date.now()}
function position(){return meta.value?.mediaType.startsWith('video/')?video.value?.currentTime||0:null}
async function start(){if(busy.value||!loaded.value)return;busy.value=true;error.value='';try{const result=await api(`${base}/sessions`,{method:'POST',body:{position:position(),active:true}});session=result.id;seconds.value=result.accumulatedSeconds;lastInput=Date.now();active.value=true}catch(e){error.value=e.message}finally{busy.value=false}}
async function beat(stop=false){if(!session||sending)return;sending=true;const id=session
 const playing=meta.value?.mediaType.startsWith('video/')
 const eligible=!stop&&document.visibilityState==='visible'&&loaded.value&&(playing?video.value&&!video.value.paused&&!video.value.seeking&&!video.value.ended&&video.value.playbackRate===1:Date.now()-lastInput<60000)
 try{const result=await api(`${base}/sessions/${id}`,{method:'POST',body:{position:position(),active:!!eligible}});if(!disposed)seconds.value=result.accumulatedSeconds;if(!eligible){session=null;active.value=false}}
 catch(e){session=null;active.value=false;if(!disposed)error.value=e.message}finally{sending=false}}
function hidden(){if(document.visibilityState!=='visible')beat(true)}
function mediaError(){loaded.value=false;error.value='無法開啟教材，請返回課程下載原檔。';beat(true)}
onMounted(async()=>{try{meta.value=await api(base);setCourseExperience(meta.value.canTrack?null:false);seconds.value=meta.value.accumulatedSeconds;const blob=await api(`${base}/content`,{download:true});if(!disposed)url.value=URL.createObjectURL(blob)}catch(e){error.value=e.message}
 timer=setInterval(()=>beat(),10000);document.addEventListener('visibilitychange',hidden);for(const name of ['pointerdown','keydown','wheel'])window.addEventListener(name,activity,{passive:true})})
onBeforeUnmount(()=>{disposed=true;clearInterval(timer);document.removeEventListener('visibilitychange',hidden);for(const name of ['pointerdown','keydown','wheel'])window.removeEventListener(name,activity);beat(true);if(url.value)URL.revokeObjectURL(url.value)})
</script>
<template><RouterLink :to="`/courses/${route.params.courseId}?section=resources`" class="back-link">← 返回課程教材</RouterLink><section v-if="meta" class="page-heading"><div><p class="eyebrow">LEARNING MATERIAL</p><h1>{{meta.title}}</h1><p class="muted">{{meta.isPractice?'本人練習':'本人正式'}}教材學習紀錄</p></div></section><p v-if="error" class="error-box" role="alert">{{error}}</p>
<section v-if="meta" class="surface form-surface"><p>累積有效閱讀／觀看時間：<strong>{{Math.floor(seconds/60)}} 分 {{seconds%60}} 秒</strong></p><p role="status">{{active?'計時中':'計時已暫停'}}</p><p class="field-hint">每 10 秒保存一次；切到背景、閒置或另開教材時停止。講義每分鐘至少與此頁互動一次，可按「繼續閱讀」；PDF 內部操作可能無法偵測。影片僅以正常速度播放時計入，快轉略過不計，重新觀看可累積。此為活動紀錄，不代表已理解教材。</p><template v-if="meta.canTrack"><button class="button primary" :disabled="busy||!loaded||active" @click="start">{{busy?'開始中…':'開始／繼續計時'}}</button><button class="button secondary" :disabled="!active" @click="beat(true)">暫停計時</button><button v-if="!meta.mediaType.startsWith('video/')" class="button secondary" @click="lastInput=Date.now()">繼續閱讀</button></template><p v-else class="muted">本課目前只供閱讀，不累積學習時間。</p></section>
<section v-if="url&&meta" class="surface form-surface material-viewer"><video v-if="meta.mediaType.startsWith('video/')" ref="video" :src="url" controls preload="metadata" @loadeddata="loaded=true" @pause="beat(true)" @seeking="beat(true)" @ended="beat(true)" @ratechange="beat(true)" @error="mediaError"></video><object v-else-if="meta.mediaType==='application/pdf'" :data="url" type="application/pdf" aria-label="教材 PDF 閱讀器" @load="loaded=true" @error="mediaError"><p>此瀏覽器不支援內嵌 PDF，請使用支援的瀏覽器閱讀；下載離線閱讀無法計時。</p></object><img v-else :src="url" :alt="meta.title" @load="loaded=true" @error="mediaError" /></section>
</template>
<style scoped>.material-viewer video,.material-viewer img{display:block;max-width:100%;max-height:75vh;margin:auto}.material-viewer object{display:block;width:100%;height:75vh;min-height:300px}.form-surface .button{margin:4px}</style>
