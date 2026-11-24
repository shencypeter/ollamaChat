import {expect,it} from 'vitest'
import {feedbackHistory} from './caseFeedbackHistory'

it('keeps pairing local to the owning session and uses saved reply model before request model',()=>{
 const sessions=[{id:'new',messages:[{id:'q2',role:'user',createdAt:'2026-11-23T02:00:00Z',modelName:'request-model'},{id:'a2',role:'assistant',replyTo:'q2',modelName:'actual-model'},{id:'wrong',role:'assistant',replyTo:'q1',modelName:'unrelated'}]},{id:'old',messages:[{id:'q1',role:'user',createdAt:'2026-11-23T01:00:00Z',modelName:'retired:7b'}]}]
 const before=structuredClone(sessions),entries=feedbackHistory(sessions)
 expect(entries.map(e=>[e.round.id,e.number,e.modelKey])).toEqual([['q1',1,'model:retired:7b'],['q2',2,'model:actual-model']]);expect(entries[0].feedback).toBeUndefined();expect(entries[1].feedback.id).toBe('a2');expect(sessions).toEqual(before)
})
it('preserves missing-date legacy order and appends a new undated attempt without renumbering prior attempts',()=>{
 const sessions=[{id:'active',messages:[{id:'dated',role:'user',createdAt:'2026-11-23T02:00:00Z'},{id:'new',role:'user'}]},{id:'legacy',messages:[{id:'unknown',role:'user',createdAt:'invalid'}]}]
 expect(feedbackHistory(sessions).map(e=>[e.round.id,e.number])).toEqual([['unknown',1],['dated',2],['new',3]])
})
it('uses server-assigned global numbers when only one history page is loaded',()=>{
 const sessions=[{id:'s',messages:[{id:'q12',role:'user',historyNumber:12,createdAt:'2026-11-23T00:00:00Z'},{id:'q11',role:'user',historyNumber:11,createdAt:'2026-11-23T00:00:00Z'}]}]
 expect(feedbackHistory(sessions).map(e=>[e.round.id,e.number])).toEqual([['q11',11],['q12',12]])
})
