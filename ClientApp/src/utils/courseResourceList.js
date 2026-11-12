export function searchResources(resources,query=''){
 const value=query.trim().normalize('NFKC').toLocaleLowerCase('zh-Hant')
 if(!value)return resources
 return resources.filter(item=>[item.title,item.fileName,item.summary].filter(Boolean).join(' ').normalize('NFKC').toLocaleLowerCase('zh-Hant').includes(value))
}

export function groupResourcesByUnit(resources,units){
 const sections=units.map(unit=>({key:unit.id,name:unit.name,description:unit.description||'',legacy:false,items:resources.filter(item=>item.unitId===unit.id)}))
 const legacy=resources.filter(item=>!item.unitId || !units.some(unit=>unit.id===item.unitId))
 if(legacy.length)sections.push({key:'legacy',name:'未歸類教材',description:'既有教材尚未安排單元',legacy:true,items:legacy})
 return sections
}
