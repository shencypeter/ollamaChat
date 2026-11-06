<script setup>
import {onMounted,onBeforeUnmount,ref} from 'vue'
import {useRoute} from 'vue-router'
import {api} from '../api'
import {setCourseExperience} from '../composables/experience'
import ResourceTranscript from '../components/ResourceTranscript.vue'
import YouTubeReplay from '../components/YouTubeReplay.vue'
const route=useRoute(),meta=ref(null),canEditTranscript=ref(false),url=ref(''),video=ref(null),youtube=ref(null),error=ref(''),active=ref(false),busy=ref(false),loaded=ref(false),seconds=ref(0),currentRate=ref(1),contentSeconds=ref(0)
const base=`/courses/${route.params.courseId}/resources/${route.params.resourceId}/learning`
let session=null,sessionRate=1,timer=null,lastInput=Date.now(),sending=false,disposed=false,pendingBeat=Promise.resolve()
function activity(event){if(event.isTrusted)lastInput=Date.now()}
function position(){return meta.value?.youTubeVideoId?youtube.value?.getCurrentTime()||0:meta.value?.mediaType.startsWith('video/')?video.value?.currentTime||0:null}
function rate(){return meta.value?.youTubeVideoId?youtube.value?.getPlaybackRate()||1:video.value?.playbackRate||1}
function playing(){return meta.value?.youTubeVideoId?youtube.value?.isPlaying()&&rate()>=.25&&rate()<=4:video.value&&!video.value.paused&&!video.value.seeking&&!video.value.ended&&rate()>=.25&&rate()<=4}
async function start(){
 if(busy.value||!loaded.value||session||disposed||document.visibilityState!=='visible')return
 if(meta.value.youTubeVideoId&&!playing())return
 busy.value=true;error.value=''
 try{await pendingBeat;if(disposed||session)return;const startRate=rate();const result=await api(`${base}/sessions`,{method:'POST',body:{position:position(),active:true,playbackRate:startRate}});sessionRate=startRate;currentRate.value=sessionRate;session=result.id;seconds.value=result.accumulatedSeconds;contentSeconds.value=result.contentSeconds||0;lastInput=Date.now();active.value=true}
 catch(e){error.value=e.message}finally{busy.value=false;if(session&&meta.value?.mediaType.startsWith('video/')){if(!playing()||document.visibilityState!=='visible')beat(true);else if(rate()!==sessionRate)youtubeRestart()}}
}
async function sendBeat(stop,sample){
 if(!session)return;sending=true;const id=session
 const eligible=!stop&&document.visibilityState==='visible'&&loaded.value&&(meta.value?.mediaType.startsWith('video/')?playing():Date.now()-lastInput<60000)
 try{const result=await api(`${base}/sessions/${id}`,{method:'POST',body:{position:sample,playbackRate:sessionRate,active:!!eligible||!!(stop&&active.value&&loaded.value&&meta.value?.mediaType.startsWith('video/')),close:stop}});if(!disposed){seconds.value=result.accumulatedSeconds;contentSeconds.value=result.contentSeconds||0;}if(!eligible){session=null;active.value=false}}
 catch(e){session=null;active.value=false;if(!disposed)error.value=e.message}finally{sending=false}
}
function beat(stop=false){if(!stop&&sending)return pendingBeat;const sample=position();pendingBeat=pendingBeat.then(()=>sendBeat(stop,sample));return pendingBeat}
async function youtubeState(state){if(state===1){await pendingBeat;if(meta.value?.canTrack)start()}else await beat(true)}
async function youtubeRestart(){currentRate.value=rate();await beat(true);if(meta.value?.canTrack&&playing())start()}
function hidden(){if(document.visibilityState!=='visible')beat(true)}
function mediaError(message){loaded.value=false;error.value=typeof message==='string'?message:'無法開啟教材，請返回課程檢查原檔。';beat(true)}
onMounted(async()=>{try{meta.value=await api(base);if(disposed)return;setCourseExperience(meta.value.canTrack?null:false);seconds.value=meta.value.accumulatedSeconds;contentSeconds.value=meta.value.contentSeconds||0;if(meta.value.youTubeVideoId){const course=await api(`/courses/${route.params.courseId}`);canEditTranscript.value=['teacher','ta'].includes(course.courseRole)&&course.status!=='archived'&&!meta.value.isPractice}if(!meta.value.youTubeVideoId){const blob=await api(`${base}/content`,{download:true});if(!disposed)url.value=URL.createObjectURL(blob)}}catch(e){error.value=e.message}
 timer=setInterval(()=>beat(),10000);document.addEventListener('visibilitychange',hidden);for(const name of ['pointerdown','keydown','wheel'])window.addEventListener(name,activity,{passive:true})})
onBeforeUnmount(()=>{disposed=true;clearInterval(timer);document.removeEventListener('visibilitychange',hidden);for(const name of ['pointerdown','keydown','wheel'])window.removeEventListener(name,activity);beat(true);if(url.value)URL.revokeObjectURL(url.value)})
</script>
<template><RouterLink :to="`/courses/${route.params.courseId}?section=resources`" class="back-link">← 返回課程教材</RouterLink><section v-if="meta" class="page-heading"><div><p class="eyebrow">LEARNING MATERIAL</p><h1>{{meta.title}}</h1><p class="muted">{{meta.isPractice?'本人練習':meta.canTrack?'本人正式':'教學預覽'}}教材學習紀錄</p></div></section><p v-if="error" class="error-box" role="alert">{{error}}</p>
<section v-if="meta" class="surface form-surface"><p>累積實際閱讀／觀看時間：<strong>{{Math.floor(seconds/60)}} 分 {{seconds%60}} 秒</strong></p><p v-if="meta.mediaType.startsWith('video/')">累積影片內容時間：<strong>{{Math.floor(contentSeconds/60)}} 分 {{contentSeconds%60}} 秒</strong></p><p v-if="meta.mediaType.startsWith('video/')">目前播放速度：{{currentRate}} 倍 · 門檻以影片內容時間計算</p><p role="status">{{active?'計時中':'計時已暫停'}}</p><p class="field-hint">YouTube 回放正常播放時自動開始計時；其他教材請按開始。每 10 秒保存一次；切到背景、閒置或另開教材時停止。講義每分鐘至少與此頁互動一次，可按「繼續閱讀」；PDF 內部操作可能無法偵測。影片門檻依有效播放的內容時間累計，例如2倍速觀看10分鐘計20分鐘；實際時間與播放速度另列供參考；快轉略過不計，重新觀看可累積。此為活動紀錄，不代表已理解教材。</p><template v-if="meta.canTrack"><button class="button primary" :disabled="busy||!loaded||active" @click="start">{{busy?'開始中…':'開始／繼續計時'}}</button><button class="button secondary" :disabled="!active" @click="beat(true)">暫停計時</button><button v-if="!meta.mediaType.startsWith('video/')" class="button secondary" @click="lastInput=Date.now()">繼續閱讀</button></template><p v-else class="muted">本課目前只供閱讀，不累積學習時間。</p></section>
<section v-if="meta&&(url||meta.youTubeVideoId)" class="surface form-surface material-viewer"><YouTubeReplay v-if="meta.youTubeVideoId" ref="youtube" :video-id="meta.youTubeVideoId" :title="meta.title" @ready="loaded=true" @state="youtubeState" @rate="youtubeRestart" @seek="youtubeRestart" @error="mediaError" /><video v-else-if="meta.mediaType.startsWith('video/')" ref="video" :src="url" controls preload="metadata" @loadeddata="loaded=true" @pause="beat(true)" @seeking="beat(true)" @ended="beat(true)" @ratechange="youtubeRestart" @play="youtubeState(1)" @error="mediaError"></video><object v-else-if="meta.mediaType==='application/pdf'" :data="url" type="application/pdf" aria-label="教材 PDF 閱讀器" @load="loaded=true" @error="mediaError"><p>此瀏覽器不支援內嵌 PDF，請使用支援的瀏覽器閱讀；下載離線閱讀無法計時。</p></object><img v-else :src="url" :alt="meta.title" @load="loaded=true" @error="mediaError" /></section>
<ResourceTranscript v-if="meta?.youTubeVideoId" :course-id="route.params.courseId" :resource-id="route.params.resourceId" :editable="canEditTranscript" />
</template>
<style scoped>.material-viewer video,.material-viewer img{display:block;max-width:100%;max-height:75vh;margin:auto}.material-viewer object{display:block;width:100%;height:75vh;min-height:300px}.form-surface .button{margin:4px}</style>
