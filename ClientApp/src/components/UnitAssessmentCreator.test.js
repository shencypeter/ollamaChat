import {mount,flushPromises} from '@vue/test-utils'
import {defineComponent} from 'vue'
import {beforeEach,expect,it,vi} from 'vitest'
import UnitAssessmentCreator from './UnitAssessmentCreator.vue'
import {api} from '../api'

const push=vi.fn()
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRouter:()=>({push})}))
const AssignmentEditor=defineComponent({name:'AssignmentEditor',emits:['save','cancel'],template:'<button class="save-quiz" @click="$emit(\'save\',{title:\'概念檢核\',instructions:\'\',maxScore:100,unitId:null})">save</button>'})
const ReportModeFields=defineComponent({name:'ReportModeFields',template:'<div />'})
const course={id:'c',status:'active',taRestrictions:[]}

beforeEach(()=>{api.mockReset();push.mockReset();api.mockResolvedValue({id:'new-id'})})
function render(){return mount(UnitAssessmentCreator,{props:{course,unitId:'u'},global:{stubs:{AssignmentEditor,ReportModeFields}}})}

it('creates a quiz inside the current unit',async()=>{
 const view=render();await view.get('button[data-create="quiz"]').trigger('click');await view.get('.save-quiz').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/quizzes',{method:'POST',body:{assignment:expect.objectContaining({title:'概念檢核',unitId:'u'}),weight:1}})
 expect(push).toHaveBeenCalledWith('/courses/c/quizzes/new-id')
})

it('creates a PDF report inside the current unit',async()=>{
 const view=render();await view.get('button[data-create="report"]').trigger('click');await view.get('input[name="report-title"]').setValue('資料分析報告');await view.get('form').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/reports',{method:'POST',body:expect.objectContaining({title:'資料分析報告',unitId:'u'})})
 expect(push).toHaveBeenCalledWith('/courses/c/reports/new-id')
})
