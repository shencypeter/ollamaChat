// Offsets are JavaScript UTF-16 indices: start inclusive, end exclusive.
// Never locate a replacement span by searching its text (phrases can repeat).
const phases=['A','B','C','D']
export const phaseNames={A:'病前功能',B:'疾病病程',C:'功能評估結果',D:'心理社會條件'}
const stagePhases={premorbid:'A',illnessCourse:'B',functionalAssessment:'C',psychosocial:'D'}
export function phaseForStage(stage){return Object.hasOwn(stagePhases,stage)?stagePhases[stage]:null}
function splitsSurrogate(text,index){
 if(index<=0||index>=text.length)return false
 const before=text.charCodeAt(index-1),after=text.charCodeAt(index)
 return before>=0xd800&&before<=0xdbff&&after>=0xdc00&&after<=0xdfff
}
export function classificationCatalog(classifications){
 return new Map((Array.isArray(classifications)?classifications:[]).filter(c=>c&&typeof c.code==='string'&&typeof c.label==='string'&&phases.includes(c.phase)).map(c=>[c.code,c]))
}
export function validateAnnotations(content,annotations,classifications){
 if(typeof content!=='string'||!Array.isArray(annotations))return []
 const catalog=classificationCatalog(classifications)
 return annotations.filter(a=>a&&typeof a.annotationId==='string'&&a.annotationId.length>0&&Number.isInteger(a.start)&&Number.isInteger(a.end)&&a.start>=0&&a.end>a.start&&a.end<=content.length&&!splitsSurrogate(content,a.start)&&!splitsSurrogate(content,a.end)&&typeof a.text==='string'&&content.slice(a.start,a.end)===a.text&&Array.isArray(a.codes)&&a.codes.length>0&&a.codes.every(code=>catalog.has(code))).map(a=>({...a,codes:[...new Set(a.codes)],explanation:typeof a.explanation==='string'?a.explanation:'',status:['unverified','unsupported','misplaced'].includes(a.status)?a.status:'unverified'}))
}
export function annotationSegments(content,annotations,classifications){
 if(typeof content!=='string'||!content.length)return []
 const valid=validateAnnotations(content,annotations,classifications),catalog=classificationCatalog(classifications)
 const boundaries=[...new Set([0,content.length,...valid.flatMap(a=>[a.start,a.end])])].sort((a,b)=>a-b)
 return boundaries.slice(0,-1).map((start,index)=>{
  const end=boundaries[index+1],covering=valid.filter(a=>a.start<=start&&a.end>=end),codes=[...new Set(covering.flatMap(a=>a.codes))]
  return {start,end,text:content.slice(start,end),annotations:covering,codes,phases:phases.filter(phase=>codes.some(code=>catalog.get(code).phase===phase))}
 })
}
// This compares AI classifications with the selected stage, never clinical truth.
// Reuse the exact-original validation so a different revision cannot inherit a check.
export function annotationPhaseAlignment(content,annotations,classifications,stage){
 const phase=phaseForStage(stage),valid=validateAnnotations(content,annotations,classifications)
 if(!phase||!valid.length)return null
 const catalog=classificationCatalog(classifications),codes=[...new Set(valid.flatMap(a=>a.codes))]
 const currentCodes=codes.filter(code=>catalog.get(code).phase===phase),otherCodes=codes.filter(code=>catalog.get(code).phase!==phase)
 return {phase,currentCodes,otherCodes,otherPhases:phases.filter(p=>otherCodes.some(code=>catalog.get(code).phase===p)),misplacedCodes:[...new Set(valid.filter(a=>a.status==='misplaced').flatMap(a=>a.codes))],unsupported:valid.some(a=>a.status==='unsupported')}
}
