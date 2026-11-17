import {phaseForStage,phaseNames} from './caseAnnotations'

// Public workflow illustrations, not live model progress or generated responses.
// Never include student answers, private prompts or request snapshots here.
export function caseChatRequestGuide(stage,classifications=[]){
 if(stage==='raw')return [
  '已送出本次訊息，等候 AI 助教回應。',
  '整理本次聊天前文，提供這一輪對話的上下文。',
  '等候模型產生本次回覆，準備顯示在對話區。',
  '本模式不加入應用程式教學提示詞，也不進行階段分類。',
  '回覆收到後會保留於本次聊天暫存，不列入正式學習統計。'
 ]
 const phase=phaseForStage(stage)
 if(!phase)return ['已送出本次訊息，等候 AI 助教回應。']
 const catalog=(Array.isArray(classifications)?classifications:[]).filter(c=>c?.phase===phase&&typeof c.code==='string'&&typeof c.label==='string')
 return [...new Set([
  '已接收本次 '+phase+' '+phaseNames[phase]+' 作答，準備交由 AI 助教閱讀。',
  '閱讀作答內容，整理段落、事件與關鍵資訊。',
  '對照分類目錄，核對各段原文所屬的分類與編碼。',
  '整理逐字引用的原文片段，準備分類標註資料。',
  '準備原文的分類底線與編碼徽章，供畫面著色呈現。',
  '核對資訊與原文依據，不捏造缺漏或臨床事實。',
  '彙整回饋、疑點與修改建議，不給分、不代寫作答。',
  '等候完整回覆；內容抵達後會自動顯示在作答旁。',
  ...catalog.map(c=>'對照 '+c.code+' — '+c.label+'，核對相關敘述與標註依據。')
 ])]
}
