import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach,afterEach} from 'vitest'
import YouTubeReplay from './YouTubeReplay.vue'
import {loadYouTubeApi} from '../utils/youtubePlayer'
vi.mock('../utils/youtubePlayer',()=>({loadYouTubeApi:vi.fn()}))
let player,options,wrapper
beforeEach(()=>{
 vi.useFakeTimers();player={getCurrentTime:vi.fn(()=>0),getPlaybackRate:vi.fn(()=>1),getPlayerState:vi.fn(()=>1),getIframe:()=>document.createElement('iframe'),destroy:vi.fn()}
 loadYouTubeApi.mockResolvedValue({Player:vi.fn(function(_host,settings){options=settings;return player})})
})
afterEach(()=>{wrapper?.unmount();vi.useRealTimers();vi.clearAllMocks()})
it('uses the fixed embed origin and reports playback changes and seeks',async()=>{
 wrapper=mount(YouTubeReplay,{props:{videoId:'M7lc1UVf-VE',title:'合成回放'}});await flushPromises();expect(options.host).toBe('https://www.youtube-nocookie.com');expect(options.playerVars.origin).toBe(window.location.origin);expect(options.playerVars.autoplay).toBe(0)
 options.events.onReady();expect(wrapper.emitted('ready')).toHaveLength(1);options.events.onStateChange({data:1});expect(wrapper.emitted('state')[0]).toEqual([1]);await vi.advanceTimersByTimeAsync(1000);player.getCurrentTime.mockReturnValue(100);await vi.advanceTimersByTimeAsync(1000);expect(wrapper.emitted('seek')).toHaveLength(1)
 options.events.onPlaybackRateChange();expect(wrapper.emitted('rate')).toHaveLength(1);wrapper.unmount();expect(player.destroy).toHaveBeenCalled()
})
it('shows embed failures instead of pretending playback earns credit',async()=>{
 wrapper=mount(YouTubeReplay,{props:{videoId:'M7lc1UVf-VE'}});await flushPromises();options.events.onError({data:150});await flushPromises();expect(wrapper.get('[role="alert"]').text()).toContain('目前不累積時間');expect(wrapper.emitted('error')).toHaveLength(1);expect(wrapper.get('a').attributes('rel')).toContain('noopener')
})
it('does not create a player after its view has been disposed',async()=>{
 let resolve;loadYouTubeApi.mockReturnValueOnce(new Promise(r=>resolve=r));wrapper=mount(YouTubeReplay,{props:{videoId:'M7lc1UVf-VE'}});wrapper.unmount();const ctor=vi.fn();resolve({Player:ctor});await flushPromises();expect(ctor).not.toHaveBeenCalled()
})
