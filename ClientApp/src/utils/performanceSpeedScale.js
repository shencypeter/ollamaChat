import {ref} from 'vue'

// Shared only in the current page's memory so slower replies remain comparable.
export const speedScaleMaximum=ref(200)

export function observePerformanceSpeed(speed){
  if(typeof speed!=='number'||!Number.isFinite(speed)||speed<0)return
  const rounded=Math.ceil(speed/0.8/50)*50
  speedScaleMaximum.value=Math.max(speedScaleMaximum.value,Number.isFinite(rounded)?rounded:Number.MAX_VALUE)
}
