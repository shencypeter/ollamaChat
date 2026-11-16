import {it,expect} from 'vitest'
import {courseSections} from './courseNavigation'
it('only exposes the course case-analysis section when enabled for that course',()=>{
 for(const courseRole of ['teacher','ta','student']){
  expect(courseSections({courseRole}).some(s=>s.key==='case-analysis')).toBe(false)
  expect(courseSections({courseRole,caseAnalysisEnabled:true}).filter(s=>s.key==='case-analysis')).toMatchObject([{label:'個案分析 AI 助教'}])
 }
})
