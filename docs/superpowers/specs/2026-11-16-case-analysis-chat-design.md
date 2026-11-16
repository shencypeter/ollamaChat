# 課程個案分析五模式聊天

本次已由使用者明確要求直接實作，無需再等待方案確認。保留Vue／ASP.NET Core／Npgsql與三模型profile，不移植舊站、不新增自動評分。

## 盤點

`codex/pre-vue-ollama`的`66b67953e070e2c45244b7c176566703c444f1fd`保留四階段無評分提示與LangChain.Core0.17.0（現版相同）。既有Buffer仍受SessionStore40則截斷，Window只保留近期、Summary會加system與額外模型呼叫，皆不可直接當Raw完整記憶。現有OllamaClient.ChatAsync會加全域system及diagnostics，需明確獨立的透明ConversationAsync，但共用HTTP transport。

## 決策

- 課程新增明確case_analysis_enabled設定，預設false；教師可開啟課程內個案分析入口。若沒有個案分析課，以可重入Development示範工具建立，沿用所選既有教師／課程成員，不新增真人答案。
- Raw採每user/course/random-session各自LangChain ConversationBufferMemory，完整順序user/assistant；不經HTTP Session、DB、快取、Summary、診斷或教師統計。新頁／重新開始取新ID，20分鐘閒置釋放，限制每人3及全站200個暫時context。清除取消推論，晚回覆不可復活。
- 四階段各自DB active Session，歷史關閉Session可查。保存原始user、成功assistant及requestId、attempt token、模型／prompt版本；失敗user仍在，重試不新增user／成功round-trip。推論不持有DB交易鎖；短交易租約防止同時送出、舊回覆覆寫與重複統計。
- Prompt來源用舊四階段教學內容，修正校名、舊無上下文限制及通過／解鎖措辭，另版本`legacy-66b6795-chat-v1`；現有有分數Assessment／Feedback服務不改。
- Raw與Stage使用同一模型與既有sampling。Transparent provider不自加任何system／schema，Stage明確傳server prompt，Raw只能人／AI訊息。兩者不寫一般body log／diagnostics。服務端模型template／日誌不受應用保證。
- 上下文以明確保守UTF8預算＋role開銷預留輸出tokens。超限413、保留完整原始history，不偷偷摘要／截斷；Stage可開始新session，舊記錄保留。此非精確tokenizer或臨床驗證。
- 真正學生只看本人；教師／有statistics能力TA看本課完整名冊／分階段統計與歷程。教師試聊以practice namespace隔離且不列正式統計；既有唯讀學生預覽不開放寫入。封存課程仍可讀、不可聊天。

## API／驗收

課內`case-analysis`section重用導覽；設定、configuration、stage history／messages／retry／restart、raw create／messages／delete、staff records均在`/api/courses/{course}/case-analysis`。登入、CSRF、課程／TA權限、速率及長度限制由後端執行。Mock檢查原始request角色／三輪上下文／三profile不變；DB檢查跨人／課／階段、失敗及去重統計；Vue／Chrome檢查清除、刷新、離開提醒與教師只讀。Migration037在既有資料升級及空schema驗證後套用唯一7173環境。
