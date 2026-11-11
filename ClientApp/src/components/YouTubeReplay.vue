<script setup>
import {onMounted,onBeforeUnmount,ref,watch} from 'vue'
import {loadYouTubeApi} from '../utils/youtubePlayer'
const props=defineProps({videoId:{type:String,required:true},title:String}),emit=defineEmits(['ready','state','rate','seek','error']),host=ref(null),error=ref('')
let player=null,poll=null,generation=0,last=null
function getCurrentTime(){return player?.getCurrentTime?.()||0}
function getPlaybackRate(){return player?.getPlaybackRate?.()||1}
function isPlaying(){return player?.getPlayerState?.()===1}
function cleanup(){clearInterval(poll);poll=null;last=null;try{player?.destroy()}catch{}player=null}
async function mountPlayer(){
 const token=++generation;cleanup();error.value=''
 try{
  const YT=await loadYouTubeApi();if(token!==generation)return
  const target=document.createElement('div');host.value.replaceChildren(target)
  player=new YT.Player(target,{host:'https://www.youtube-nocookie.com',videoId:props.videoId,width:'100%',height:'100%',playerVars:{origin:window.location.origin,enablejsapi:1,playsinline:1,autoplay:0,controls:1,rel:0},events:{
   onReady:()=>{if(token!==generation)return;const iframe=player.getIframe?.();if(iframe){iframe.title=props.title||'YouTube 課程回放';iframe.referrerPolicy='strict-origin-when-cross-origin'}emit('ready')},
   onStateChange:event=>{if(token!==generation)return;last=null;if(event.data===1&&document.visibilityState==='hidden')pause();emit('state',event.data)},
   onPlaybackRateChange:()=>{if(token!==generation)return;last=null;emit('rate')},
   onError:()=>{if(token!==generation)return;error.value='此影片無法在平台播放，可能未開放嵌入、已下架或受到觀看限制；目前不累積時間。';emit('error',error.value)}
  }})
  poll=setInterval(()=>{
   if(!isPlaying()||getPlaybackRate()<.25||getPlaybackRate()>4){last=null;return}
   const current={position:getCurrentTime(),at:Date.now()}
   if(last){const movement=current.position-last.position,elapsed=(current.at-last.at)/1000;if(movement<-.25||movement>elapsed*getPlaybackRate()+1)emit('seek')}
   last=current
  },1000)
 }catch(e){if(token!==generation)return;error.value=e.message;emit('error',e.message)}
}
onMounted(mountPlayer);watch(()=>props.videoId,mountPlayer);onBeforeUnmount(()=>{generation++;cleanup()})
function pause(){player?.pauseVideo?.()}
defineExpose({pause,getCurrentTime,getPlaybackRate,isPlaying})
</script>
<template><div><div ref="host" class="youtube-host"></div><p v-if="error" class="error-box" role="alert">{{error}}</p><a :href="`https://www.youtube.com/watch?v=${videoId}`" target="_blank" rel="noopener noreferrer">在 YouTube 開啟（站外觀看不計入平台時間）</a></div></template>
<style scoped>.youtube-host{width:100%;aspect-ratio:16/9;min-height:220px;background:#111}.youtube-host :deep(iframe){width:100%;height:100%;min-height:220px;border:0}a{display:inline-block;margin-top:12px;overflow-wrap:anywhere}</style>
