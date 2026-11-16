# AI 助教回饋輪次、版本比較與效能

更新：2026-11-16，已同步至唯一7173環境。

## 操作與行為

四階段介面採左側「回饋輪次」、右側「該次作答＋對應AI回饋」。仍可選工作階段；教師查閱維持唯讀、按學生／階段隔離。待回覆或失敗的提問也列入輪次，失敗沿原訊息重試。

學生可點「修改這次作答」載入選中原文，再按「提交並取得 AI 助教回饋」產生新輪次。原文與AI回覆不覆寫，系統保存明確修訂來源與修改次數；一般新提問不自動視為前一題修改。重試同一失敗回覆不算另一次修改。回饋後可手動按「下一階段」，沒有新增AI過關／自動跳階／成績。

切換輪次不重寫草稿；載入別版、切換對話／階段／教師查閱或離頁前會確認尚未提交內容。取消後保留草稿、修訂來源與原選擇。

## 效能彈出對話框

每筆assistant旁提供「查看回覆效能」，Raw及四階段均可用。彈窗顯示該筆模型／提示版本、輸入／快取／輸出Token、生成tok/s、模型處理／載入／輸入處理／生成耗時，及應用程式與模型服務間往返時間。ns換秒除1e9，ms換秒除1000；往返時間不是完整瀏覽器、授權或DB流程耗時。

舊回覆未保存的效能維持NULL並明示未提供，不回填或拿最新數字替代。新provider回覆逐項記錄availableFields；模型缺少欄位與真實量測0分開顯示（例如部分Ollama版本沒有快取Token欄位）。只保存數值、已知欄位名與白名單結束原因，不保存prompt／raw response／thinking或錯誤本文。

四階段效能與assistant一起持久化且不可改寫；Raw只在原暫存context保留效能，清除／過期一併釋放，不寫DB、不送回模型上下文，也不列正式學生統計。原Raw隔離、20m逾時與明確帳號清除保留。

## 任兩版學生原文比較

使用者已明確確認比對學生作答／提問。按「比較作答版本」，可從目前同一學生、課程及階段已授權的工作階段歷史任選兩版，也可反向比較。完整保留文字、空白、換行與Unicode code points；紅色刪除、綠色新增，無變更則明示相同。內容以Vue純文字輸出，不執行HTML。

演算法先修剪相同前後綴，再做有250,000 cell硬上限的LCS；超過預算明示改用整段刪增，完整內容仍可重建，不宣稱最小差異。比對不呼叫LLM、不建立成績，也不另外傳送／保存學生文字至外部服務。

## 資料庫／API

Migration **038_case_chat_feedback_rounds.sql**新增nullable performance_metrics與revises_message_id，舊資料保留NULL。revision parent只允許同課、同本人、同stage、同practice的已完成user作答；DB trigger與workflow雙重檢查，禁止把他人／AI回覆／未完成提問當來源。原始content、版本來源與效能metadata不可覆寫。

CaseChatInput新增可選revisesMessageId，冪等requestId也會核對來源，避免同ID換parent。CaseChatMessage增加metrics／revisesMessageId，CaseChatStatistics增加revisionCount；修改次數由明確parent重算，並非總提問數。RawMessage增加只在記憶體的metrics。既有沒有提供新欄位的呼叫仍相容。

## 驗證與部署

- Vue：60檔 **241/241**；Chrome **15/15**（8個聊天情境＋7個既有師生／評分流程），含1440／390、原生dialog、任兩版比對、round1/3選取、舊輪次修訂、新UUID／parent、同ID失敗重試、鍵盤與取消草稿、Raw重整／BFcache。
- .NET一般 **283通過／47條件式DB略過**；另以授權隔離schema啟用DB後完整 **330/330，0略過**。覆蓋來源授權、修改／往返計數、原文／metadata不可改寫、逐筆效能、缺值／0、Raw效能不入模型及原有功能。
- 036→037→038真實舊資料fixture含原文／回饋、名冊與已發布成績，升級及重跑指紋一致；舊metric／parent為NULL。完成即刪測試schema。
- 同一真實Ollama provider合成2Raw＋1Stage傳輸成功，Raw逐筆效能可用欄位確認；沒有DB聊天種子或內容輸出。三profile設定／outbound payload及System Prompt不改，本次只補回覆量測欄位存在性。
- Public038已交易套用；標準VS MSBuild含Vite通過。正常bin/Debug PID26108只綁7173，/courses200及index-s9KPMSBt.js確認；其他既有舊埠無listener，後續仍須重查。
- Code review修正未提交草稿被覆寫問題，並補模型缺省數值不應視為量測0；先重現失敗後回歸通過。未執行完整歷史E2E或模型教學品質評估；無獨立TypeScript檢查設定，Vue經Vite編譯。

## 主要檔案

`CaseAnalysisChat.vue`與對應測試／E2E、`ChatPerformanceDialog.vue`、`AnswerRevisionDiff.vue`、`utils/answerDiff.js`；Models/CaseAnalysisChatModels、RawChatModels、ChatModels；Services/CaseAnalysisChat兩部分、RawChatSessions、ChatPerformanceMetrics、OllamaClient；Database038與PortalSmoke升級／合成測試工具。既有未提交功能與文件均保留，未提交／推送Git。
