const encode=value=>encodeURIComponent(String(value))

export function unitLocation(courseId,unitId){
 return `/courses/${encode(courseId)}/units/${encode(unitId)}`
}

export function activityLocation(courseId,item){
 const segments={material:'materials',quiz:'quizzes',assignment:'assignments',report:'reports'}
 const segment=segments[item.kind]
 if(!segment)throw new Error('不支援的單元活動類型。')
 return `/courses/${encode(courseId)}/${segment}/${encode(item.id)}`
}

export function activityReturnLocation(courseId,unitId){
 return unitId?unitLocation(courseId,unitId):{path:`/courses/${encode(courseId)}`,query:{legacy:'activities'}}
}
