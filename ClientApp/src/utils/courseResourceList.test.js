import {describe,it,expect} from 'vitest'
import {groupResourcesByUnit,searchResources} from './courseResourceList'

describe('course resource list',()=>{
 it('groups resources by curriculum order and preserves a legacy unclassified section',()=>{
  const units=[{id:'u2',name:'第二單元',description:'進階'},{id:'u1',name:'第一單元',description:'基礎'}]
  const resources=[{id:'a',unitId:'u1'},{id:'b',unitId:null},{id:'c',unitId:'u2'}]
  expect(groupResourcesByUnit(resources,units)).toEqual([
   {key:'u2',name:'第二單元',description:'進階',legacy:false,items:[resources[2]]},
   {key:'u1',name:'第一單元',description:'基礎',legacy:false,items:[resources[0]]},
   {key:'legacy',name:'未歸類教材',description:'既有教材尚未安排單元',legacy:true,items:[resources[1]]}
  ])
 })
 it('searches only titles filenames and summaries without legacy tags',()=>{
  const resources=[
   {id:'a',title:'百分比講義',fileName:'math.pdf',summary:'估算與驗算',tags:['舊標籤']},
   {id:'b',title:'科學實驗',fileName:'lab.png',summary:'控制變因',tags:['百分比']}
  ]
  expect(searchResources(resources,'驗算').map(x=>x.id)).toEqual(['a'])
  expect(searchResources(resources,'LAB').map(x=>x.id)).toEqual(['b'])
  expect(searchResources(resources,'舊標籤')).toEqual([])
 })
})
