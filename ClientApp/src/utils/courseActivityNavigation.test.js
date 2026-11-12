import {describe,expect,it} from 'vitest'
import {activityLocation,activityReturnLocation,unitLocation} from './courseActivityNavigation'

describe('course activity navigation',()=>{
 it('builds canonical unit and activity locations',()=>{
  expect(unitLocation('course a','unit/1')).toBe('/courses/course%20a/units/unit%2F1')
  expect(activityLocation('c',{kind:'material',id:'m'})).toBe('/courses/c/materials/m')
  expect(activityLocation('c',{kind:'quiz',id:'q'})).toBe('/courses/c/quizzes/q')
  expect(activityLocation('c',{kind:'assignment',id:'a'})).toBe('/courses/c/assignments/a')
  expect(activityLocation('c',{kind:'report',id:'r'})).toBe('/courses/c/reports/r')
 })
 it('returns to the persisted unit or the legacy course area',()=>{
  expect(activityReturnLocation('c','u')).toBe('/courses/c/units/u')
  expect(activityReturnLocation('c',null)).toEqual({path:'/courses/c',query:{legacy:'activities'}})
  expect(activityReturnLocation('c','')).toEqual({path:'/courses/c',query:{legacy:'activities'}})
 })
})
