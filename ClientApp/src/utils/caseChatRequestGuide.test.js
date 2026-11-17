import {expect,it} from 'vitest'
import {caseChatRequestGuide} from './caseChatRequestGuide'

it('illustrates the selected phase workflow and authorized catalog without using student answers or raw payloads',()=>{
 const guide=caseChatRequestGuide('illnessCourse',[{code:'B1',label:'受傷資訊',phase:'B'},{code:'C2',label:'Muscle Power（肌力與相關動作檢查）',phase:'C'}])
 expect(guide.join(' ')).toContain('B 疾病病程');expect(guide.join(' ')).toContain('B1 — 受傷資訊');expect(guide.join(' ')).not.toContain('C2 —')
 expect(guide.join(' ')).toContain('逐字引用');expect(guide.join(' ')).toContain('不捏造');expect(guide.join(' ')).toContain('不給分')
 expect(new Set(guide).size).toBe(guide.length)
})
it('keeps Raw Chat descriptions separate from stage prompt tasks',()=>{
 const raw=caseChatRequestGuide('raw',[{code:'B1',label:'受傷資訊',phase:'B'}]).join(' ')
 expect(raw).toContain('不加入應用程式教學提示詞');expect(raw).toContain('聊天前文');expect(raw).not.toContain('B1');expect(raw).not.toContain('逐字引用')
})
