export function filterMaterialMetadata(resources,{from='',to='',undated=false,sort='uploaded_desc'}={}){
 if(from&&to&&from>to || undated&&(from||to))return []
 return resources.filter(r=>(!undated||!r.teachingDate)&&(!from||(r.teachingDate&&r.teachingDate>=from))&&(!to||(r.teachingDate&&r.teachingDate<=to))).sort((a,b)=>{
  if(sort!=='uploaded_desc'){
   if(!a.teachingDate&&b.teachingDate)return 1
   if(a.teachingDate&&!b.teachingDate)return -1
   const dates=(a.teachingDate||'').localeCompare(b.teachingDate||'')
   if(dates)return sort==='teaching_asc'?dates:-dates
  }
  return (b.createdAt||'').localeCompare(a.createdAt||'')||b.id.localeCompare(a.id)
 })
}
export function groupMaterials(resources,grouped){
 if(!grouped)return [{key:'all',label:'',items:resources}]
 const result=new Map()
 for(const item of resources){const key=item.teachingDate||'undated';if(!result.has(key))result.set(key,{key,label:item.teachingDate||'未指定授課日期',items:[]});result.get(key).items.push(item)}
 return [...result.values()]
}
