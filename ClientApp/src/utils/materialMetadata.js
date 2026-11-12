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
