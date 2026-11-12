<script setup>
import {onMounted,onBeforeUnmount,ref,computed} from 'vue'
import {useRoute} from 'vue-router'
import {api} from '../api'
import {Eye} from '@lucide/vue'
import {setCourseExperience} from '../composables/experience'
import {newRequestId} from '../utils/requestId'
import ShareResource from '../components/ShareResource.vue'
import ResourceInteractions from '../components/ResourceInteractions.vue'
import FilePreview from '../components/FilePreview.vue'
import MaterialSummary from '../components/MaterialSummary.vue'
import ResourceTranscript from '../components/ResourceTranscript.vue'
import YouTubeReplay from '../components/YouTubeReplay.vue'
const route=useRoute(),meta=ref(null),canEditTranscript=ref(false),url=ref(''),video=ref(null),youtube=ref(null),error=ref(''),active=ref(false),busy=ref(false),loaded=ref(false),seconds=ref(0),currentRate=ref(1),contentSeconds=ref(0)
const untimed=ref(false),teachingPreview=ref(false),demoActualMs=ref(0),demoContentMs=ref(0),demoRunning=ref(false)
const viewRequestId=newRequestId()
const displaySeconds=computed(()=>teachingPreview.value?Math.floor(demoActualMs.value/1000):seconds.value)
const displayContentSeconds=computed(()=>teachingPreview.value?Math.floor(demoContentMs.value/1000):contentSeconds.value)
let demoSample=null,demoTimer=null
function demoTick(close=false){
 if(!teachingPreview.value)return
 const now=performance.now(),pos=position(),speed=rate()
 if(demoSample){
  const elapsed=now-demoSample.at,movement=pos-demoSample.position
  if(elapsed>=0&&elapsed<=25000&&speed===demoSample.rate&&movement>=0&&movement<=elapsed/1000*speed+1){
   const actual=Math.min(elapsed,movement/speed*1000)
   demoActualMs.value+=actual;demoContentMs.value+=actual*speed
  }
 }
 currentRate.value=speed
 demoRunning.value=!close&&loaded.value&&document.visibilityState==='visible'&&!!playing()
 demoSample=demoRunning.value?{at:now,position:pos,rate:speed}:null
}
async function downloadOriginal(){try{const blob=await api(`/courses/${route.params.courseId}/resources/${route.params.resourceId}/download`,{method:'POST',download:true});const href=URL.createObjectURL(blob),a=document.createElement('a');a.href=href;a.download=meta.value.fileName;a.click();setTimeout(()=>URL.revokeObjectURL(href),1000)}catch(e){error.value=e.message}}
const base=`/courses/${route.params.courseId}/resources/${route.params.resourceId}/learning`
let session=null,sessionRate=1,timer=null,lastInput=Date.now(),sending=false,disposed=false,pendingBeat=Promise.resolve()
function activity(event){if(event.isTrusted)lastInput=Date.now()}
function position(){return meta.value?.youTubeVideoId?youtube.value?.getCurrentTime()||0:meta.value?.mediaType.startsWith('video/')?video.value?.currentTime||0:null}
function rate(){return meta.value?.youTubeVideoId?youtube.value?.getPlaybackRate()||1:video.value?.playbackRate||1}
function playing(){return meta.value?.youTubeVideoId?youtube.value?.isPlaying()&&rate()>=.25&&rate()<=4:video.value&&!video.value.paused&&!video.value.seeking&&!video.value.ended&&rate()>=.25&&rate()<=4}
async function start(){
 if(teachingPreview.value){demoTick();return}
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
async function youtubeState(state){if(teachingPreview.value){demoTick(state!==1);return}if(state===1){await pendingBeat;if(meta.value?.canTrack)start()}else await beat(true)}
async function youtubeRestart(){if(teachingPreview.value){demoSample=null;demoTick();return}currentRate.value=rate();await beat(true);if(meta.value?.canTrack&&playing())start()}
function hidden(){if(document.visibilityState!=='visible'){youtube.value?.pause?.();video.value?.pause();demoTick(true);beat(true)}}
function mediaError(message){demoSample=null;demoRunning.value=false;loaded.value=false;error.value=typeof message==='string'?message:'無法開啟教材，請返回課程檢查原檔。';beat(true)}
onMounted(async()=>{try{try{meta.value=await api(base)}catch(e){if(e.status!==415)throw e;const resources=await api(`/courses/${route.params.courseId}/resources`);const item=resources.find(r=>r.id===route.params.resourceId);if(!item)throw new Error('找不到教材。');untimed.value=true;meta.value={...item,title:item.title||item.fileName,mediaType:'',canTrack:false,isPractice:false}}if(disposed)return;if(!meta.value.mediaType?.startsWith('video/'))void api(`/courses/${route.params.courseId}/resources/${route.params.resourceId}/views`,{method:'POST',body:{id:viewRequestId}}).catch(()=>{});if(meta.value.isPractice)meta.value.canTrack=false;setCourseExperience(meta.value.canTrack?null:false);seconds.value=meta.value.accumulatedSeconds;contentSeconds.value=meta.value.contentSeconds||0;if(meta.value.youTubeVideoId){const course=await api(`/courses/${route.params.courseId}`);teachingPreview.value=['teacher','ta'].includes(course.courseRole)&&!meta.value.isPractice;canEditTranscript.value=['teacher','ta'].includes(course.courseRole)&&course.status!=='archived'&&!course.taRestrictions?.includes('materials')&&!meta.value.isPractice}if(!untimed.value&&!meta.value.youTubeVideoId){const blob=await api(`${base}/content`,{download:true});if(!disposed)url.value=URL.createObjectURL(blob)}}catch(e){error.value=e.message}
 demoTimer=setInterval(()=>demoTick(),1000);timer=setInterval(()=>beat(),10000);document.addEventListener('visibilitychange',hidden);for(const name of ['pointerdown','keydown','wheel'])window.addEventListener(name,activity,{passive:true})})
onBeforeUnmount(()=>{disposed=true;clearInterval(demoTimer);demoSample=null;clearInterval(timer);document.removeEventListener('visibilitychange',hidden);for(const name of ['pointerdown','keydown','wheel'])window.removeEventListener(name,activity);beat(true);if(url.value)URL.revokeObjectURL(url.value)})
</script>
<template><RouterLink :to="`/courses/${route.params.courseId}?section=resources`" class="back-link">← 返回課程教材</RouterLink><section v-if="meta" class="page-heading"><div><p class="eyebrow">LEARNING MATERIAL</p><h1>{{meta.title}}</h1><p class="muted">{{untimed?'課程教材與互動':teachingPreview?'教師／助教計時示範・不記錄或計入學習時間':meta.isPractice?'學生視角預覽・唯讀':meta.canTrack?'本人正式教材學習紀錄':'唯讀教材・不記錄學習時間'}}</p></div><ShareResource :course-id="route.params.courseId" :resource-id="route.params.resourceId" :title="meta.title" /></section><p v-if="error" class="error-box" role="alert">{{error}}</p>
<div v-if="meta&&!untimed" class="material-learning" :class="{'youtube-learning':meta.youTubeVideoId}">
<section v-if="meta&&(url||meta.youTubeVideoId)" class="surface form-surface material-viewer"><YouTubeReplay v-if="meta.youTubeVideoId" ref="youtube" :video-id="meta.youTubeVideoId" :title="meta.title" @ready="loaded=true" @state="youtubeState" @rate="youtubeRestart" @seek="youtubeRestart" @error="mediaError" /><video v-else-if="meta.mediaType.startsWith('video/')" ref="video" :src="url" controls preload="metadata" @loadeddata="loaded=true" @pause="beat(true)" @seeking="beat(true)" @ended="beat(true)" @ratechange="youtubeRestart" @play="youtubeState(1)" @error="mediaError"></video><object v-else-if="meta.mediaType==='application/pdf'" :data="url" type="application/pdf" aria-label="教材 PDF 閱讀器" @load="loaded=true" @error="mediaError"><p>此瀏覽器不支援內嵌 PDF，請使用支援的瀏覽器閱讀；下載離線閱讀無法計時。</p></object><img v-else :src="url" :alt="meta.title" @load="loaded=true" @error="mediaError" /></section>
<section v-if="meta&&!untimed" class="surface form-surface material-timer"><h2>{{teachingPreview?'計時示範':'學習計時'}}</h2><p v-if="teachingPreview" class="submission-notice" role="note"><strong>您是本課程的授課教師／助教：計時僅供示範。</strong><br />示範數字會隨播放變化，但不會儲存或計入學習時間、成績與測驗門檻；重新整理後歸零。</p><div class="timer-values"><p v-if="meta.isPractice" class="submission-notice" role="note">學生視角預覽不累積學習時間。</p><p>{{teachingPreview?'示範實際觀看時間：':'累積實際閱讀／觀看時間：'}}<strong>{{Math.floor(displaySeconds/60)}} 分 {{displaySeconds%60}} 秒</strong></p><p v-if="meta.mediaType.startsWith('video/')">{{teachingPreview?'示範影片內容時間：':'累積影片內容時間：'}}<strong>{{Math.floor(displayContentSeconds/60)}} 分 {{displayContentSeconds%60}} 秒</strong></p><p v-if="meta.mediaType.startsWith('video/')">目前播放速度：{{currentRate}} 倍 · {{teachingPreview?'倍速換算僅供示範':'門檻以影片內容時間計算'}}</p><p role="status">{{teachingPreview?(demoRunning?'示範計時中（不計入紀錄）':'示範計時已暫停'):active?'計時中':'計時已暫停'}}</p><div v-if="meta.youTubeVideoId" class="timer-visibility-note"><strong><Eye :size="17" aria-hidden="true" />前景播放才計時</strong><p>只有本教材分頁可見、影片正在播放時，才會累計{{teachingPreview?'示範時間':'學習時間'}}。切換至其他分頁或將視窗最小化後，播放器會暫停並停止計時；回到本頁後請自行播放。</p></div><p v-if="teachingPreview" class="field-hint">暫停或緩衝時停止，快轉跳過不計入示範數字。</p><p v-else class="field-hint">YouTube 回放正常播放時自動開始計時；其他教材請按開始。每 10 秒保存一次；切到背景、閒置或另開教材時停止。講義每分鐘至少與此頁互動一次，可按「繼續閱讀」；PDF 內部操作可能無法偵測。影片門檻依有效播放的內容時間累計，例如2倍速觀看10分鐘計20分鐘；實際時間與播放速度另列供參考；快轉略過不計，重新觀看可累積。此為活動紀錄，不代表已理解教材。</p><template v-if="meta.canTrack"><button class="button primary" :disabled="busy||!loaded||active" @click="start">{{busy?'開始中…':'開始／繼續計時'}}</button><button class="button secondary" :disabled="!active" @click="beat(true)">暫停計時</button><button v-if="!meta.mediaType.startsWith('video/')" class="button secondary" @click="lastInput=Date.now()">繼續閱讀</button></template><p v-else-if="!teachingPreview" class="muted">本課目前只供閱讀，不累積學習時間。</p></div></section>
</div>
<section v-if="meta&&untimed" class="surface form-surface"><p>此格式提供預覽或下載，不累積學習時間。</p><FilePreview :endpoint="`/courses/${route.params.courseId}/resources/${route.params.resourceId}/preview`" :file-name="meta.fileName" :content-type="meta.previewType" /><button class="button secondary" @click="downloadOriginal">下載檔案</button></section>
<ResourceInteractions v-if="meta" :course-id="route.params.courseId" :resource-id="route.params.resourceId" />
<MaterialSummary v-if="meta" :course-id="route.params.courseId" :resource-id="route.params.resourceId" />
<ResourceTranscript v-if="meta?.youTubeVideoId" :course-id="route.params.courseId" :resource-id="route.params.resourceId" :editable="canEditTranscript" />
</template>
<style scoped>.material-viewer video,.material-viewer img{display:block;max-width:100%;max-height:75vh;margin:auto}.material-viewer object{display:block;width:100%;height:75vh;min-height:300px}.form-surface .button{margin:4px}.material-learning{display:flex;flex-direction:column;min-width:0}.material-timer{order:-1;min-width:0}.timer-visibility-note{margin:16px 0;padding:12px 14px;background:var(--surface-soft);border:1px solid var(--line);border-left:3px solid var(--accent);border-radius:8px}.timer-visibility-note strong{display:flex;align-items:center;gap:7px;font-size:13px;color:var(--accent)}.timer-visibility-note strong svg{flex-shrink:0}.timer-visibility-note p{margin:7px 0 0;font-size:12px;line-height:1.8}.material-timer .submission-notice{display:block;line-height:1.8}.material-learning>.surface.form-surface+.surface.form-surface{margin-top:0}.youtube-learning{gap:24px;margin-bottom:24px}.youtube-learning>.surface{margin:0;min-width:0}.youtube-learning .material-timer{order:0}.youtube-learning .material-viewer{padding:20px}.youtube-learning .material-timer h2{margin-top:0}.youtube-learning .material-timer .field-hint{line-height:1.7}.youtube-learning .material-timer p{overflow-wrap:anywhere}@media(min-width:1280px){.youtube-learning{display:grid;grid-template-columns:minmax(0,1fr) 300px;align-items:start}}@media(max-width:760px){.youtube-learning .material-viewer{padding:12px}}
</style>
