import {it,expect} from 'vitest'
import {quizScoringPlan} from './quizScoringPlan'
it('shows a 25/15/20/40 composition and separates automatic from human grading',()=>{
 const questions=[...Array.from({length:5},()=>({questionType:'single',points:5})),...Array.from({length:3},()=>({questionType:'multiple',points:5})),...Array.from({length:4},()=>({questionType:'true_false',points:5})),{questionType:'essay',points:40}]
 const result=quizScoringPlan(100,questions);expect(result.rows.map(r=>r.total)).toEqual([25,15,20,40]);expect(result.objectiveTotal).toBe(60);expect(result.essayTotal).toBe(40)
})
it('distributes remaining cents consistently with the server',()=>{expect(quizScoringPlan(100,[{},{},{}]).questionPoints).toEqual([33.33,33.33,33.34]);expect(quizScoringPlan(100,[{points:40},{},{}]).questionPoints).toEqual([40,30,30])})
it('rejects totals and invalid precision instead of presenting an incorrect plan',()=>{expect(quizScoringPlan(100,[{points:90}]).error).toBeTruthy();expect(quizScoringPlan(100,[{points:101},{}]).error).toBeTruthy();expect(quizScoringPlan(100,[{points:0.001}]).error).toBeTruthy()})
