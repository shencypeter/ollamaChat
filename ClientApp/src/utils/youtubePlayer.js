let pending
export function loadYouTubeApi(){
 if(window.YT?.Player)return Promise.resolve(window.YT)
 if(pending)return pending
 pending=new Promise((resolve,reject)=>{
  const script=document.createElement('script'),prior=window.onYouTubeIframeAPIReady
  let timeout
  const fail=()=>{clearTimeout(timeout);script.remove();if(window.onYouTubeIframeAPIReady===ready)window.onYouTubeIframeAPIReady=prior;reject(new Error('無法載入 YouTube 播放器，請檢查連線後重新載入。'))}
  const ready=()=>{clearTimeout(timeout);try{prior?.()}finally{if(window.YT?.Player)resolve(window.YT);else fail()}}
  window.onYouTubeIframeAPIReady=ready;script.src='https://www.youtube.com/iframe_api';script.async=true;script.onerror=fail
  timeout=setTimeout(fail,15000);document.head.appendChild(script)
 }).catch(error=>{pending=null;throw error})
 return pending
}
