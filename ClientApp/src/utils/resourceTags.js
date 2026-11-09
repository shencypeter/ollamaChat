export function normalizeResourceTag(value){
 let tag=String(value).normalize('NFKC').trim().replace(/\s+/g,' ')
 if(tag.length>30 || /[\x00-\x1f\x7f]/.test(tag))throw new Error('每個標籤最多 30 字，且不能含控制字元。')
 const match=tag.match(/^(?:第\s*0*(\d{1,2})\s*[週周]|week\s*0*(\d{1,2}))$/i)
 if(match && Number(match[1]||match[2])>0)tag=`第${Number(match[1]||match[2])}週`
 return tag
}
export function parseResourceTags(text){
 const result=[]
 for(const value of text.split(/[,，;；\n]+/)){
  const tag=normalizeResourceTag(value)
  if(tag && !result.some(t=>t.toLowerCase()===tag.toLowerCase()))result.push(tag)
 }
 if(result.length>10)throw new Error('每份教材最多 10 個標籤。')
 return result
}
export function resourceTagChoices(resources){
 const tags=new Map()
 for(const resource of resources)for(const tag of resource.tags||[]){
  const key=tag.toLowerCase(),old=tags.get(key)
  if(old)old.count++;else tags.set(key,{name:tag,count:1})
 }
 const week=value=>/^第\d+週$/.test(value)?Number(value.match(/\d+/)[0]):Infinity
 return [...tags.values()].sort((a,b)=>(week(a.name)-week(b.name))||a.name.localeCompare(b.name,'zh-Hant',{numeric:true}))
}
export function filterResources(resources,{tag='',query='',untagged=false}={}){
 const q=query.normalize('NFKC').trim().toLowerCase(),selected=tag.toLowerCase()
 return resources.filter(r=>(!untagged||!(r.tags||[]).length)&&(!selected||(r.tags||[]).some(t=>t.toLowerCase()===selected))&&(!q||[r.title,r.fileName,r.summary,...r.tags||[]].filter(Boolean).join(' ').normalize('NFKC').toLowerCase().includes(q)))
}
