import {mount,flushPromises} from '@vue/test-utils'
import {defineComponent,onMounted} from 'vue'
import {it,expect,vi,beforeEach,afterEach} from 'vitest'
import LearningResourceView from './LearningResourceView.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>({params:{courseId:'c',resourceId:'r'}})}))
vi.mock('../composables/experience',()=>({setCourseExperience:vi.fn()}))
let state,position,speed,wrapper
const Player=defineComponent({name:'YouTubeReplay',emits:['ready','state','rate','seek','error'],setup(_,ctx){ctx.expose({pause:()=>{state=2},getCurrentTime:()=>position,getPlaybackRate:()=>speed,isPlaying:()=>state===1});onMounted(()=>ctx.emit('ready'));return()=>null}})
async function open(role='teacher',practice=false){
 api.mockImplementation(async path=>path.endsWith('/learning')?{title:'合成回放',youTubeVideoId:'M7lc1UVf-VE',mediaType:'video/youtube',canTrack:practice||role==='student',isPractice:practice,accumulatedSeconds:120,contentSeconds:240}:path==='/courses/c'?{courseRole:role,status:'active'}:path.endsWith('/sessions')?{id:'session',accumulatedSeconds:120,contentSeconds:240}:{});
 wrapper=mount(LearningResourceView,{global:{stubs:{RouterLink:true,ShareResource:true,ResourceInteractions:true,MaterialSummary:true,ResourceTranscript:true,YouTubeReplay:Player}}});await flushPromises();return wrapper.findComponent(Player)
}
beforeEach(()=>{vi.useFakeTimers();api.mockReset();state=2;position=0;speed=1})
afterEach(()=>{wrapper?.unmount();vi.useRealTimers()})
it.each(['teacher','ta'])('runs a local %s demonstration without session writes, pauses and excludes seeks',async role=>{
 const player=await open(role);expect(wrapper.text()).toContain('計時僅供示範');expect(wrapper.text()).toContain('不會儲存或計入學習時間');
 state=1;player.vm.$emit('state',1);await flushPromises();
 for(let i=0;i<3;i++){position++;await vi.advanceTimersByTimeAsync(1000)}
 expect(wrapper.text()).toContain('示範實際觀看時間：0 分 3 秒');
 state=2;player.vm.$emit('state',2);await vi.advanceTimersByTimeAsync(2000);expect(wrapper.text()).toContain('示範實際觀看時間：0 分 3 秒');
 state=1;player.vm.$emit('state',1);await flushPromises();position+=120;await vi.advanceTimersByTimeAsync(1000);expect(wrapper.text()).toContain('示範實際觀看時間：0 分 3 秒');
 speed=2;player.vm.$emit('rate');await flushPromises();position+=2;await vi.advanceTimersByTimeAsync(1000);expect(wrapper.text()).toContain('示範實際觀看時間：0 分 4 秒');expect(wrapper.text()).toContain('示範影片內容時間：0 分 5 秒');
 expect(api.mock.calls.some(c=>c[0].includes('/sessions'))).toBe(false);
 wrapper.unmount();await open(role);expect(wrapper.text()).toContain('示範實際觀看時間：0 分 0 秒')
})
it('records formal sessions and keeps preview read-only',async()=>{
 for(const practice of [false,true]){
  const player=await open(practice?'teacher':'student',practice);expect(wrapper.text()).not.toContain('計時僅供示範');expect(wrapper.text()).toContain('累積實際閱讀／觀看時間：2 分 0 秒');
  if(practice)expect(wrapper.text()).toContain('學生視角預覽不累積學習時間');
  state=1;player.vm.$emit('state',1);await flushPromises();expect(api.mock.calls.some(c=>c[0].endsWith('/sessions')&&c[1]?.method==='POST')).toBe(!practice);wrapper.unmount();api.mockReset();state=2;
 }
})

it('pauses the embedded player on visibility loss and waits for the user to resume',async()=>{
 await open();state=1
 const visibility=vi.spyOn(document,'visibilityState','get').mockReturnValue('hidden')
 try{document.dispatchEvent(new Event('visibilitychange'));expect(state).toBe(2);visibility.mockReturnValue('visible');document.dispatchEvent(new Event('visibilitychange'));expect(state).toBe(2)}finally{visibility.mockRestore()}
})
