import {it,expect} from 'vitest'
import {safeMaterialFileName} from './materialFormat'
it('shows the original basename and removes invisible controls while preserving Chinese names',()=>{
 expect(safeMaterialFileName('C:\\fakepath\\實驗\u202e報告\n.pdf')).toBe('實驗報告.pdf')
 expect(safeMaterialFileName('../作業.docx')).toBe('作業.docx')
 expect(safeMaterialFileName('\n')).toBe('未命名檔案')
})
