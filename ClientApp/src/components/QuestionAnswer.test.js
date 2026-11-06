import {mount} from '@vue/test-utils'
import {describe,it,expect} from 'vitest'
import QuestionAnswer from './QuestionAnswer.vue'
describe('question answers',()=>{
 it('preserves the essay textarea',async()=>{const w=mount(QuestionAnswer,{props:{question:{questionType:'essay',sortOrder:1},modelValue:'申論'}});await w.get('textarea').setValue('修改申論');expect(w.emitted('update:modelValue')[0]).toEqual(['修改申論'])})
 it('single choice replaces the previous answer',async()=>{const w=mount(QuestionAnswer,{props:{question:{id:'q',questionType:'single',options:['A','B']},modelValue:'[0]'}});await w.findAll('input')[1].setValue();expect(w.emitted('update:modelValue')[0]).toEqual(['[1]'])})
 it('multiple choice adds and removes selected options',async()=>{const w=mount(QuestionAnswer,{props:{question:{id:'q',questionType:'multiple',options:['A','B']},modelValue:'[0]'}});await w.findAll('input')[1].setValue(true);expect(w.emitted('update:modelValue')[0]).toEqual(['[0,1]']);await w.findAll('input')[0].setValue(false);expect(w.emitted('update:modelValue')[1]).toEqual(['[]'])})
})
