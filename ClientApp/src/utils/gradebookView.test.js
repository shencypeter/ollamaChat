import {describe,expect,it} from 'vitest'
import {gradeItems,filterGradeItems,normalizedGrade,scoreStatus,gradeSummary,formatGrade} from './gradebookView'

const items=[
 {id:'a',category:'assignment',sourceReportId:'r',maxScore:50,weight:1},
 {id:'q',category:'quiz',sourceAssignmentId:'q1',maxScore:100,weight:3},
 {id:'m',category:'midterm',maxScore:100,weight:2},
 {id:'o',category:'other',maxScore:100,weight:1}
]
const curriculum={units:[{id:'u',name:'實際單元'}],activities:[{id:'r',kind:'report',unitId:'u'},{id:'q1',kind:'quiz',unitId:null}]}
const scores=[{itemId:'a',studentId:'s',score:0,publishedScore:0},{itemId:'q',studentId:'s',score:80,publishedScore:null},{itemId:'m',studentId:'s',score:null}]

describe('gradebook presentation',()=>{
 it('uses actual source IDs and curriculum units without guessing manual-item units',()=>{
  const rows=gradeItems(items,curriculum)
  expect(rows.map(x=>x.unitName)).toEqual(['實際單元','未歸類','課程層級','課程層級'])
  expect(filterGradeItems(rows,{unit:'u'}).map(x=>x.id)).toEqual(['a'])
  expect(filterGradeItems(rows,{type:'quiz',activity:'q'}).map(x=>x.id)).toEqual(['q'])
  expect(filterGradeItems(rows,{unit:'u',type:'quiz'})).toEqual([])
  expect(gradeItems(items,null)[0].unitName).toBe('單元資料未取得')
 })
 it('keeps zero, omits null and averages normalized scores equally despite unequal legacy weights',()=>{
  expect(normalizedGrade(items,scores,'s')).toBe(40)
  expect(normalizedGrade(items,scores,'missing')).toBeNull()
  expect(normalizedGrade(items,[{itemId:'a',studentId:'s',score:40},{itemId:'q',studentId:'s',score:100}],'s')).toBe(90)
  expect(gradeSummary(items,scores,'s')).toEqual({assignment:0,quiz:80,regular:40,formal:null})
 })
 it('rounds display midpoint values away from zero, matching the backend decimal policy',()=>{
  expect(formatGrade(2.01/200*100)).toBe('1.01 分')
  expect(formatGrade(-1.005)).toBe('-1.01 分')
  expect(formatGrade(0)).toBe('0.00 分')
  expect(formatGrade(null)).toBe('—')
 })
 it('does not invent submission status and distinguishes unpublished, zero and a changed draft',()=>{
  expect(scoreStatus(undefined,false)).toBe('未發布')
  expect(scoreStatus(undefined,true)).toBe('未登錄')
  expect(scoreStatus(scores[0],false)).toBe('已發布')
  expect(scoreStatus(scores[1],true)).toBe('草稿（未發布）')
  expect(scoreStatus({score:75,publishedScore:70},true)).toBe('已發布（另有草稿）')
 })
})
