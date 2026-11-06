> 2026-10-29 最新更新：新案例站台的本人學生體驗可儲存草稿與取得文字 AI 回饋，不給 AI 分數、不能正式提交。新測驗及 PDF 作業也不能代交；原作業形成性 API 仍停用。現行以 [案例站台](case-studio.md) 為準，本文舊流程保留歷史。

> 2026-10-29 政策更新：作業 AI 評分與私人回饋全部暫停，相關 API 回傳 403；獨立 AI 入口保留。本文中 AI 作業流程為先前實作紀錄，現行範圍見 [課程工具與考試](course-tools.md)。

# 教師／助教的學生體驗模式

更新：2026-10-29。依使用者選擇提供學生視角練習：使用本人隔離草稿與 AI 回饋；依後續明確要求，不能提交，正式提交須由學生本人操作。

## 操作與功能分布

教師或助教登入後，頁首提供「切換學生視角」；切換後顯示學生體驗提示，頁首改為「返回教學視角」。偏好依帳號保存在本機，重新載入可延續；不改登入 Cookie、帳號角色或課程名單。每個分頁依自己送出的 view header 操作，不以共用伺服器角色開關影響其他分頁。

| 功能 | 教學視角 | 學生體驗模式 | 正式學生 |
| --- | --- | --- | --- |
| 可見課程 | 本人的有效課程成員資格 | 同樣須具有效資格 | 本人的有效資格 |
| 未發布作業 | 課程教師／助教可見 | 不可見 | 不可見 |
| 建立／編輯／發布作業 | 原有課程授權 | 不提供 | 不提供 |
| 儲存、AI 回饋、提交 | 教學端不代交 | 可存草稿與 AI 回饋；提交按鈕可見但停用 | 僅本人正式作答，可提交 |
| 正式提交名單與人工評分 | 原有課程授權 | 不提供 | 只讀自己的已發布成績 |
| 原始 Ollama 請求／回應檢視 | 教師／助教可讀自己操作 | 不提供 | 不提供 |
| 平台帳號與課程名單管理 | 原有教授授權 | 不提供 | 不提供 |

體驗模式同樣尊重發布、開放、截止、關閉及封存政策，不會因為教師身分看到學生看不到的草稿。切換時先走正常離頁流程，未儲存作答若取消離開，不會切換模式。

一般 AI 對話在本人帳號內另以 practice namespace 保存記憶及設定，切換不刪掉原本教學對話，也不把教學對話帶入學生體驗。正式學生使用原有 namespace，不受影響。

## 後端邊界

- 前端只宣告 `X-Portal-View: student`；後端先驗證已登入且實際平台角色為 Professor／TeachingAssistant，否則 403。這不是瀏覽器指定身分、學生識別碼或新權限。
- `LearnerExperience` 是 request-scoped 能力。作業服務在此模式將有效課程成員投影為 student，仍保留實際 actor ID，且不略過成員資格。
- 體驗模式阻擋教授專用管理端點與課程名單；作業服務的 staff-only 操作也因有效視角為 student 而拒絕。
- 形成性端點允許正式 Student，或已驗證的 staff practice；資料仍以本人、題目、答案 revision 及 practice namespace 查詢。一般教學視角不能藉此讀取正式學生的私人 AI 回饋。
- staff 的原始 Ollama 檢視在 practice 模式不擷取也不回傳；不是只在 Vue 隱藏。

## 持久化與正式資料隔離

`Database/005_student_experience.sql` 新增 `submissions.is_practice`，既有紀錄預設 false。個人作答唯一鍵加入此旗標，因此同一人、同一作業的兩個 namespace 不相撞；practice 必須是個人作答。

沿用現有 submissions、逐題 revisions、提交 snapshots、evaluation_runs 與 audit，沒有虛構學生帳號或複製正式課程。

- 草稿／提交／AI 回饋查詢都限制本次 namespace 與本人。
- 正式提交名單、提交數量及教學端 Review 排除 practice。
- 快照、評分與發布路徑驗證 namespace。教學視角拿 practice snapshot ID 評分會 404；學生體驗視角則沒有 staff 評分權限。
- 學生體驗的 submit=true 在後端拒絕，不建立新提交或成績；歷史練習快照仍保留隔離，不改寫舊資料。
- 教師與助教各自的練習互相隔離，正式學生不能用 view header 開啟 staff practice。

## 遷移與現有示範

005 已於 2026-10-29 交易提交；既有 001–004 不重寫。網站仍不自動遷移，部署其他環境前沿用 `--migrate`。

LEARN-101 的虛構案例作業仍保留原未發布草稿，沒有因本功能而發布。在教學視角確認並發布後，才能從學生視角完整體驗。

## 限制

- 不冒用其他學生，也不預覽其私人草稿或形成性回饋。
- 練習的截止與提交鎖定仍依作業政策；目前沒有「重設練習」或繞過發布／期限的功能。
- 第一次提交前可使用私人 AI 回饋；既有 P1 提交後修訂仍沒有私人修訂草稿流程。
- 學生體驗不產生可正式評分的成績。若需要示範教師回饋，應用獨立合成學生帳號測試正式流程。
- 未新增跨課程存取、課程名單或帳號角色授予功能。

## 驗證

後端含真實 PostgreSQL 的 namespace、草稿／AI／提交、正式名單／計數排除、禁止人工評分、教師間隔離，以及聊天 namespace／原始檢視抑制測試。Vue 測試涵蓋角色能力、依帳號保存偏好及 header；瀏覽器測試使用 loopback 合成模型，包含教師與助教操作及正式學生隔離，不向真實模型送測。

本機本輪缺少 npm，但有現有 Node 與 node_modules，因此直接執行等效工具，不安裝或更改全域環境：

```powershell
# 根目錄；透過程序環境供應 FORMATIVE_TEST_CONNECTION
dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/experience-final/ --verbosity quiet

# ClientApp；NODE 為既有 runtime 的 node.exe 絕對路徑
& $NODE node_modules/vitest/vitest.mjs run
& $NODE node_modules/vite/bin/vite.js build
# PORTAL_TEST_URL 指向隔離 API，OLLAMA_INSPECTION_STUB=1
& $NODE node_modules/@playwright/test/cli.js test student-experience.spec.js
& $NODE node_modules/@playwright/test/cli.js test assignments.spec.js teaching.spec.js portal.spec.js
```

2026-10-29 實際結果：後端 **115 項通過**（程序環境啟用真實 PostgreSQL 整合測試，沒有略過）；前端 **21 項通過**；Vite 生產建置及 .NET 發布成功。

Focused 合成模型 browser/API **3 項通過**（學生體驗、正式學生形成性回饋、staff 檢視）；原有 P0/P1/入口回歸 **6 項通過**。學生體驗測試另於手機深色模式執行通過，已檢視桌面與手機截圖。這些測試使用隔離 API 5107 與 loopback 11535，不向真實模型送出推論，也不代表模型評分品質驗收。

005 已交易提交，重跑 001–005 均正確略過。沒有修改真實帳號角色、課程名單或發布既有示範作業。測試期間一項舊測試將學生入口綁死在單一平台角色，已調整為驗證登入及明確 learner capability，並新增匿名／學生偽造模式拒絕與完整 practice 隔離測試。

## 2026-10-29 政策修正：體驗可看、不可提交

使用者明確改為只體驗草稿／AI，不能提交。提交按鈕保留可見但 disabled，並註明正式提交須由學生本人操作；後端亦拒絕 practice 的 submit=true。真正 Student 的提交不受影響。既有歷史已提交練習維持原狀，不自動重開或刪除。無新遷移。

本次實際驗證：後端 115 項（含已啟用 PostgreSQL）通過、前端 22 項通過、Vite 建置與發布成功；focused browser/API 2 項通過，驗證教師／助教被拒及真正學生正常提交，使用合成模型。前一輪 9 項 browser/API 為歷史結果，本輪未全部重跑。

## 2026-10-30 學生體驗提交更新

本人學生視角現可提交目前線上／課後測驗、PDF 作業及案例練習；教學視角可從獨立本人練習紀錄人工評分測驗／PDF，再於學生視角查看練習成績。正式成績／統計隔離。此政策取代先前禁止練習提交敘述；操作、011 遷移與驗證見 practice-assessments.md。
