import {mount} from '@vue/test-utils'
import {it,expect} from 'vitest'
import QuizAnswerReview from './QuizAnswerReview.vue'
const question={questionType:'multiple',options:['A','B','C'],standardAnswer:[0,2]}
it('shows which selected answer was wrong and which correct answer was missed',()=>{
 const w=mount(QuizAnswerReview,{props:{question,text:'[0,1]',showKey:true}})
 expect(w.find('.wrong').text()).toContain('B');expect(w.find('.wrong').text()).toContain('誤選');expect(w.find('.missing').text()).toContain('C');expect(w.find('.missing').text()).toContain('漏選');expect(w.find('.correct').text()).toContain('A')
})
it('does not reveal correctness when key visibility is disabled',()=>{
 const w=mount(QuizAnswerReview,{props:{question,text:'[1]',showKey:false}});expect(w.text()).not.toContain('誤選');expect(w.text()).not.toContain('正確選項');expect(w.find('.correct').exists()).toBe(false)
})
it('shows unanswered choices and renders essay text safely',()=>{
 expect(mount(QuizAnswerReview,{props:{question,text:'',showKey:true}}).text()).toContain('學生未作答')
 const w=mount(QuizAnswerReview,{props:{question:{questionType:'essay'},text:'<script>synthetic</script>'}});expect(w.text()).toContain('<script>synthetic</script>');expect(w.find('script').exists()).toBe(false)
})

it('presents per-question score and explicitly marked student choices',()=>{
 const w=mount(QuizAnswerReview,{props:{question,text:'[1]',showKey:true,mark:{questionType:'multiple',points:5,autoScore:0,correct:false}}})
 expect(w.text()).toContain('得分 0 / 5');expect(w.text()).toContain('答錯／未答');expect(w.find('.wrong').text()).toContain('學生作答');expect(w.findAll('.missing')).toHaveLength(2)
})
it('withholds hidden scores and displays zero essay grades correctly',()=>{
 const w=mount(QuizAnswerReview,{props:{question,text:'[1]',mark:{questionType:'multiple',points:5,autoScore:null,correct:null}}})
 expect(w.text()).toContain('結果待發布');expect(w.text()).not.toContain('得分');expect(w.text()).not.toContain('誤選')
 const essay=mount(QuizAnswerReview,{props:{question:{questionType:'essay'},text:'Synthetic',mark:{questionType:'essay',points:40},essayScore:0}})
 expect(essay.text()).toContain('教師已評分');expect(essay.text()).toContain('得分 0 / 40')
})
