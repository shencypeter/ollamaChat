import {reactive} from 'vue'
import {api} from '../api'
export const notifications=reactive({materials:0,assignments:0,questions:0,total:0,courses:[]})
let generation=0,request=0
export function resetNotifications(){generation++;request++;Object.assign(notifications,{materials:0,assignments:0,questions:0,total:0,courses:[]})}
export async function refreshNotifications(){
 const token=++request,epoch=generation
 try{const data=await api('/notifications');if(token===request&&epoch===generation)Object.assign(notifications,data)}catch{/* Keep navigation usable when a background refresh fails. */}
}
export function courseNotifications(id){return notifications.courses.find(c=>c.courseId===id)||{materials:0,assignments:0,questions:0,total:0}}
export async function markNotificationsRead(kind,ids){
 if(!ids.length)return
 const epoch=generation
 try{
  for(let i=0;i<ids.length;i+=200){
   if(epoch!==generation)return
   await api('/notifications/read',{method:'POST',body:{kind,ids:ids.slice(i,i+200)}})
  }
 }catch{return}
 if(epoch===generation)await refreshNotifications()
}
