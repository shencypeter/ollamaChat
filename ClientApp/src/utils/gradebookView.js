export const gradeCategories={offline_assignment:'線下作業',assignment:'PDF／線上作業',quiz:'測驗',midterm:'期中考試',final:'期末考試',other:'其他平時評量'}
const assignments=new Set(['offline_assignment','assignment'])
const formal=new Set(['midterm','final'])
export function formatGrade(value){
 if(value==null)return '—'
 const number=Number(value),absolute=Math.abs(number)
 const rounded=Math.sign(number)*Math.round((absolute+Number.EPSILON*Math.max(1,absolute))*100)/100
 return `${rounded.toFixed(2)} 分`
}
// Only persisted source IDs establish an activity's unit. Manual items stay at course level.
export function gradeItems(items,curriculum){
 return items.map(item=>{
  if(!item.sourceAssignmentId&&!item.sourceReportId)return {...item,unitKey:'course',unitName:'課程層級'}
  const activity=curriculum?.activities?.find(activity=>item.sourceReportId?activity.kind==='report'&&activity.id===item.sourceReportId:['assignment','quiz'].includes(activity.kind)&&activity.id===item.sourceAssignmentId)
  if(!activity)return {...item,unitKey:'unknown',unitName:'單元資料未取得'}
  if(!activity.unitId)return {...item,unitKey:'unassigned',unitName:'未歸類'}
  const unit=curriculum.units?.find(unit=>unit.id===activity.unitId)
  return {...item,unitKey:activity.unitId,unitName:unit?.name||'單元資料未取得'}
 })
}
export function filterGradeItems(items,{unit='',type='',activity=''}={}){return items.filter(item=>(!unit||item.unitKey===unit)&&(!type||item.category===type)&&(!activity||item.id===activity))}
// Each scored activity contributes one normalized percentage to the mean.
// The backend remains authoritative for course averages and published final totals.
export function normalizedGrade(items,scores,studentId){
 let total=0,count=0
 for(const item of items){const score=scores.find(score=>score.itemId===item.id&&score.studentId===studentId)?.score;if(score==null)continue;total+=Number(score)/Number(item.maxScore)*100;count++}
 return count?total/count:null
}
export function gradeSummary(items,scores,studentId){const mean=predicate=>normalizedGrade(items.filter(predicate),scores,studentId);return {assignment:mean(item=>assignments.has(item.category)),quiz:mean(item=>item.category==='quiz'),regular:mean(item=>!formal.has(item.category)),formal:mean(item=>formal.has(item.category))}}
export function scoreStatus(score,staff){
 if(!staff)return score?.score==null?'未發布':'已發布'
 if(score?.publishedScore!=null)return score.score!==score.publishedScore?'已發布（另有草稿）':'已發布'
 return score?.score==null?'未登錄':'草稿（未發布）'
}
