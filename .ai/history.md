# 開發歷程與決策紀錄

本檔保存對話中已確認的專案歷史，供後續代理恢復脈絡。初始紀錄整理於 2026-10-22；未有明確日期的早期工作以階段標示，不推定精確完成時間。新工作以日期追加於本檔末尾。

## 早期階段：教學助教與評分實驗

- 起點為 ASP.NET Core MVC／Razor 概念驗證，連接內網 Ollama。逐步加入四階段臨床案例分析及修改前後比較。
- 使用者要求以繁體中文呈現 1–4 級規準，並對修改前後分別評分。
- Llama 曾將刻意不完善的答案評為 4 分；後續提示調整一度植入與測試答案幾乎相同、附預期分數的範例，形成測試污染。
- 決策：移除答案特定提示與答案鍵，保留證據、推論及無支持結論的區別。建立獨立合成評估資料，預期分數待專業人工確認。
- `rubric-evidence-v2` 加入證據發現、單次推論結構化輸出、JSON Schema、後端驗證、缺案例限制及開發專用去識別診斷。
- 當時模型為 `llama3.1:8b`，端點為 `http://IAN02:11434`。歷史原題在無答案鍵情況得到 2 分，但仍有內容品質問題；部分未見作答測試逾時，未證明評分可泛化。

## 2026-10-22：Vue／Web API 與資料庫基礎

- 依使用者要求改為 Vue 3 前端及 ASP.NET Core Web API，採類似 Teams 的工作區與淺／深色主題；既有表單移入 AI 入口。
- 使用者提供 PostgreSQL DDL 並授權套用。最初網路逾時，遠端調整防火牆後重試成功。
- 套用領域結構及獨立帳號認證遷移，交易提交並以校驗碼遷移紀錄確認可重跑。實際伺服器回報 PostgreSQL 18.6。
- 建立第一位教授初始化、後續帳號管理、課程與名單、Cookie 驗證、CSRF 與密碼雜湊。憑證留於本地忽略設定，不記於本檔。
- 顯示姓名按使用者要求移除中文第一個姓氏字元；資料庫完整姓名不變。
- 修正 API 防偽 Filter 對 MVC ViewFeatures 的依賴及發布時前端資源重複衝突。舊 MVC 檔案保留作參考。

## 2026-10-22：P0 個人作業交付

- 完成課程教師／助教建立、編輯、發布及關閉個人文字作業；學生儲存草稿、重新載入及正式提交；教師／助教檢閱已提交版本與歷史。
- 沿用既有資料表，沒有為 P0 另加遷移。保留學生草稿隱私、一次提交、提交鎖定、交易、版本衝突與時間限制。
- 未實作分組、附件、遲交例外、重交、人工正式成績及作業 AI 整合。
- 當次驗證：後端 72 項、前端 8 項、瀏覽器／真實 API 4 項通過；完整發布成功，桌面淺深色及行動版畫面已檢視。
- QA 帳號、課程及相依作答資料已依專用識別碼清理，保留真實資料。詳見 `docs/p0-assignments.md`。
- 使用者決策：停止擴充 P0，於 11/15 展示前試用系統，優先修正缺陷。POC 是邀請教授討論的具體素材，並非已確認的完整教學設計。

## 2026-10-22：改用 Breeze 與格式相容性修正

- 使用者要求改用 `http://192.168.1.195:11434` 的 `ycchen/breeze-7b-instruct-v1_0`。確認伺服器可達且安裝 `:latest` 標籤，更新後端與預覽設定。
- 使用者回報「AI 評閱結果不符合證據與評分格式」。直接重試並非每次都失敗，故視為間歇性契約問題，未宣稱所有失敗均由同一原因造成。
- 檢查安裝模型模板為舊式 `[INST]`；將評閱多個 system 訊息合併。原始作答 JSON 的中文編碼為跳脫序列，改成可讀中文；依是否提供案例約束旗標與空陣列，單份比較欄位限空字串。
- 提示版本更新為 `rubric-evidence-v3`，保留單次推論、嚴格解析及無答案鍵原則。一般對話記憶仍維持原行為。
- 當次後端 73 項測試通過，包含新增輸入與 Schema 回歸；發布成功，已更新本機預覽組件。
- 最新三次即時評閱皆通過格式驗證：獨立弱作答 2 分、獨立強作答 1 分、歷史原始作答在無案例與無答案鍵情況 1 分。此結果顯示模型仍不能可靠區分品質，不支持準確率改善主張。
- 未完成：Breeze 全四階段與修改前後比較的完整即時驗收、重複與改寫一致性、專業人工校準。本次未重跑前端及瀏覽器測試。
- 原始評估輸出位於忽略的 `tests/AssessmentEvaluation/results/`；最新三筆報告檔名為 `20261022T082437Z-report.json`。該資料夾可能不隨儲存庫移轉，以上結論因此一併保留於本檔。

## 2026-10-22：持久化規格與代理交接

- 依使用者要求，以繁體中文建立 `.ai/elearning.md`，整理已完成範圍、規準、技術與待教授確認事項。
- 進一步新增根目錄 `AGENTS.md` 作為代理入口，填入原本空白的 `.ai/agents.md`，新增本歷程檔。
- 文件職責：`elearning.md` 記錄產品規格；`agents.md` 保存當前工作規則與交接；`history.md` 保存決策與驗證的時間脈絡。
- 根入口要求每次工作讀取三份文件，並在實質變更後維護。這是支援遵循 AGENTS.md 的代理的專案指示；不代表任意工具均會自動載入隱藏目錄文件。
- 驗證：檢查入口連結、檔案存在與繁體中文內容；本次僅整理文件，沒有修改執行程式，也未重跑測試。

## 2026-10-22：保存各模型設定與有效負載

- 使用者要求保留 Qwen → Llama3 → Breeze 的相容性經驗，以便日後切回模型。
- 新增三個獨立啟動 profile，保存端點、模型、訊息布局與抽樣參數；預設仍為 Breeze。Llama／Qwen 保留多 system，Breeze 保留單 system 及輸入附近格式要求；共同沿用最新安全與證據契約，不復原答案污染。
- 評估工具支援 --profile 及 --export-payloads，以真實序列化路徑離線保存四階段、比較及案例存在性共 48 份合成請求。明確標示不是歷史原始封包或即時成功紀錄。
- 修正模型 metadata 查找，支援未寫 :latest 的名稱。Qwen 的目前契約未驗證，Llama 舊結果亦不能代表 v3。
- 驗證：後端 76 項通過；48 份離線匯出成功。本輪未做即時推論、未重跑前端及瀏覽器。模型切換說明及驗收流程見 docs/ollama/README.md。

## 2026-10-27：P1 人工評閱與教學流程交付

- 使用者貼上完整 P1 任務，明確擴充原先 P0 凍結範圍；11/15 展示含 P1，12/15 預定 AI 整合、12/31 第一階段交付。
- 盤點確認無 EF Core；延用 Npgsql／交易式 SQL 遷移，未引進平行 EF 歷史。此工具要求差異已明確說明，細節見 docs/p1-teaching.md。
- 003_teaching_workflows.sql 已提交並驗證重跑略過；新增可選修訂、滿分、課程規準版本、整份提交快照、人工成績草稿／發布。舊作業預設維持提交鎖定；舊提交回填快照，不重寫答案歷史。
- Vue 新增課程規準編輯、作業政策設定、整份提交比較、指定版本人工評分、回饋發布及學生已發布成績。新提交不繼承舊版成績，已使用規準的修改建立新版本。
- 權限及隔離在後端執行；並行提交與評分採版本檢查及交易，規準編輯與作業關聯有列鎖。所有成績修改新增紀錄，保留評閱者與前筆關聯。
- 最終測試：後端 88、前端 8、瀏覽器／真實 API 6 項通過（含原有 4 項 P0／入口回歸）；生產前端建置及發布成功。最終瀏覽器驗證對象為發布版 5097。
- 修正首次提交重複逐題版本、可辨識欄位標籤及瀏覽器登出競態；登入限流測試等待恢復，未修改保護規則。
- 真實 PostgreSQL 驗證學生隔離、跨課規準拒絕、修訂競爭、規準版本保留、評分草稿保密及發布後可見。檢視教師桌面與學生手機深色截圖。
- 合成示範 helper 支援 --demo，隨機帳密與識別碼只存本機忽略檔；建立／清理均實際驗證。最終 QA／示範紀錄及狀態檔已清理，保留真實資料。
- 預覽仍為 http://127.0.0.1:5097，內容位於 .codex-build/p0-publish（已覆以 P1 發布組件）；5107 測試服務已停止。不得依舊程序編號操作。
- PortalSmoke 建置有 NU1900 弱點資料查詢網路警告；沒有完成該次弱點清單查詢。無 AI 推論、正式環境部署或負載測試。
- 後續限制與精確 demo 步驟見 docs/p1-teaching.md；沒有提交或推送 Git。

## 2026-10-27：教授／助教唯讀 AI 請求檢視

- 使用者希望教授／助教看見實際送給 Ollama 的系統詞與請求，便於討論日後是否自行 tune；明確選擇先做唯讀，不提供編輯。
- 新增 scoped OllamaRequestInspection，在送出 HTTP 本文處擷取；教授／助教自己的成功或失敗回應可含 requestInspection，學生及匿名不擷取也不回傳。無全站歷史查詢、資料庫／日誌／對話歷史保存，重新載入即清除頁面狀態。
- Vue 新增「系統詞與實際請求（唯讀）」；分列 system、案例與作答／對話脈絡、options、完整 JSON／Schema；摘要記憶請求以 memory-summary 另列。明確說明 Ollama 還會套用模型模板及預設值。
- 不顯示 thinking；端點移除 userinfo 與 query。前端純文字呈現，沒有 prompt 編輯能力。舊開發去識別診斷未開啟或放寬。
- 實際驗證：後端 98 項、前端 10 項通過；新增 1 項瀏覽器／真實登入 API 整合測試通過。模型部分使用明確標示的 loopback 合成 Ollama HTTP fixture，驗證實際傳輸組裝、教授／助教可見、學生成功與失敗皆無欄位、錯誤格式時可檢視及重載後清除；沒有藉此宣稱真實模型評分改善。
- Vue 建置及發布成功，檢視截圖；已更新 5097 預覽並保留 Breeze 設定。測試 API 5107、合成模型 11535 已停止，合成 QA 帳號及狀態檔已清理。此次無資料庫結構變更。
- 本輪未重跑全部 P0／P1 瀏覽器套件；先前 P1 交付的 6 項結果保留其歷史範圍。PortalSmoke 仍有 NU1900 弱點資訊網路查詢警告，建置成功。

## 2026-10-27：唯讀檢視加入模型原始回應

- 使用者要求在展開唯讀 Ollama 檢視時一併顯示模型原始回應。每筆已擷取請求現與其 HTTP 狀態碼及 Ollama JSON 回應配對，教授／助教可直接比對實際請求、原始模型輸出與後端解析結果。
- 保留既有隱私邊界：`OllamaRequestInspection` 仍為 HTTP request scoped；學生成功與錯誤回應都沒有檢視欄位；內容不寫入資料庫、日誌或對話歷史。後端遞迴移除請求及回應內所有 `thinking` 欄位，非 JSON 回應不向前端暴露未清理本文。
- `OllamaClient` 只讀取回應本文一次，先將安全化副本交給檢視，再由同一本文反序列化既有回應 DTO；未改變各模型 profile、評閱 Schema、提示版本或抽樣參數。
- 驗證：後端 99 項、前端 10 項通過；Vue production build 成功。受限沙箱內的 vstest testhost 連線兩次逾時，改以相同已建置 DLL 在沙箱外執行後全部通過。本輪未啟動合成 Ollama fixture、未重跑瀏覽器整合測試，也未向真實模型發送推論。

## 2026-10-28：學生作業提交前私人 AI 形成性回饋

- 依使用者明確授權，將學生作業頁串接私人形成性回饋；不是正式評分，不改動／發布 grades。保留既有工作樹，未重設、提交或推送其他變更。
- 完整盤點 AGENTS、規格／交接／歷史、Ollama／P1 文件及目前 Vue／Npgsql 程式。發現 evaluation_runs 已綁答案版本但 rubric 必填；004_formative_feedback.sql 以最小附加遷移允許 rubric 空，新增 purpose、requestor、profile、hash 與索引，沒有假外鍵或平行評閱表。
- 學生逐題先儲存再請 AI；POST 僅接受整份 expectedVersion 及精確 answerRevisionId。伺服器組裝作業、參考資料、題目、配置階段及本人版本，直接 AssessAsync，不含一般對話記憶或其他答案。
- 推論前短交易建立 pending 後釋放鎖，允許同時修改／提交；成功只存嚴格解析的 Assessment，失敗存安全本地化訊息。版本 pointer／本地未存文字變動都會標過期。無／舊／失敗回饋只有提交確認，不會阻擋提交快照或人工評分。
- 平台 Student 角色、有效課程 student 及作答所有權共同限制；教授／助教無權讀取私人形成性端點。學生沒有 requestInspection、raw response 或 thinking。獨立入口的 memory、三種 profile 及 staff request／HTTP status／安全化原始 JSON 檢視保持原樣。
- 只適用首次正式提交前；P1 沒有私人提交後修訂草稿，本次不暗中改變。題目未設階段時停用，整份人工規準不套成單題正式計分。參考欄位仍是混合的作業說明與案例資料，教學指示本身不是案例證據。
- 資料保留：只存 parsed findings、練習 score、身份／版本／模型／抽樣／hash／時間與狀態；不存 raw model output、thinking、完整 payload 或端點。沒有新增自動清除／背景恢復工作；崩潰留下 pending 可重試且不阻擋提交。
- 實際測試：dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj（隔離 OutputPath／UseAppHost=false；程序環境 FORMATIVE_TEST_CONNECTION 啟用真實 PostgreSQL）108 項通過，其中 7 項形成性 DB 測試，不是略過。npm test 18 項通過；npm run build 與 publish 成功。
- Focused loopback synthetic Ollama browser/API 2 項通過（形成性流程及 staff 檢視）；原 P0／P1／入口 browser/API 6 項亦通過。模型全為合成 fixture／fake，沒有向真實模型送出測試，也不宣稱評分品質改善。
- 首輪 DB 測試清理的多命令參數化問題已修正；8 個遺留合成 fixture 課程經核對 email／UUID／成員／無真實資料後清理，後續 DB 測試正常清理。PortalSmoke 更新為先刪 evaluation_runs；本輪 QA 合成紀錄與狀態檔已清理。
- 004 已交易提交，重跑 001–004 均略過。已檢視學生回饋／失敗提示畫面。測試 API 5107 與 fixture 11535 已停止；5097 更新為新發布版本，仍用 Breeze，沒有測試端點配置寫入共用 profile。
- PortalSmoke 建置仍有 NU1900 弱點清單查詢網路警告；發布不含 local secrets 或 QA state。完整交付、限制與重跑方式見 docs/formative-feedback.md。

## 2026-10-28：加入教授可檢視的虛構案例作業

- 使用者接受四階段個案功能分析草案並要求加入系統。先查核資料庫，只有一門既有 LEARN-101（測試課）且尚無作業；核實擁有教授與有效 teacher 成員後，沿用 AssignmentWorkflow／規準服務建立。
- 作業「從案例證據分析個案功能與適應」ID c7b059d4-40bb-4a9d-861f-48ee866a4255，保留為 draft，沒有發布、截止日期、學生答案、AI 評閱或正式成績。
- 四題對應 premorbid、illnessCourse、functionalAssessment、psychosocial。人工規準四項等權重，各有四級描述，作業滿分 100，正式提交後預設鎖定；教授發布前可調整。
- 虛構案例、題目與規準保存於 docs/examples/clinical-case-assignment.json，不寫入系統提示或測試答案鍵。此為使用者需要的實際平台內容，不是 QA 暫存資料，測試清理不得刪除。
- 寫入後重新讀取並核對狀態、完整案例、題數、階段及規準關聯；再次執行得到 already-exists，沒有重複建立或覆寫。沒有新遷移、應用程式修改或模型呼叫；本次未重跑軟體測試。

## 2026-10-29：教師與助教的完整學生體驗模式

- 使用者要求教師／助教也能從學生入口操作，並明確選擇以自己身分完整儲存、AI 回饋與提交，但不計正式學生紀錄。
- 新增 request-scoped LearnerExperience 與 X-Portal-View: student；後端驗證已登入真實 Professor／TeachingAssistant，仍檢查有效課程資格，沒有更改 Cookie 角色、actor ID 或課程名單。真正學生或匿名偽造 header 被拒絕。
- 005_student_experience.sql 已交易提交，新增 submissions.is_practice、個人作答唯一鍵 namespace 與約束／索引；既有紀錄保留 false。正式名單、計數、Review 及人工評分排除 practice；實際 snapshot 與 eval 仍沿用原表，避免虛構學生帳號。
- 教師／助教學生模式各自保存本人草稿、答案版本、形成性回饋與不可改寫提交；其他教師及真學生無權讀取。只見已發布作業，沿用截止／關閉／封存政策，沒有重設練習或越過期限功能。
- Vue 頁首切換／返回按鈕與明確提示，依帳號本機保存偏好；切換經原本離頁 guard。學生模式隱藏並在伺服器拒絕教學管理功能，回到教學視角恢復原功能。
- 一般 AI 對話、摘要與設定用獨立 practice scope；學生模式不擷取／回傳 staff 原始請求／HTTP／JSON 診斷。正常教學模式及正式學生的既有行為保持。
- 驗證：後端 115 項通過（FORMATIVE_TEST_CONNECTION 啟用真實 PostgreSQL，未略過），前端 21 項通過；Vite production build、dotnet publish 成功。Focused synthetic browser/API 3 項通過，P0／P1／入口 6 項通過；學生視角另加助教提交與手機深色寬度檢查後再次通過，檢視桌面／手機截圖。
- 本輪環境沒有 npm，使用既有 Node 絕對路徑直接執行 vitest.mjs、vite.js 及 Playwright cli.js；未安裝／改寫全域環境，沒有向真實模型送測。完整命令與功能矩陣見 docs/student-experience.md。
- 005 重跑正確略過；合成 QA 與狀態檔已清理，5107／11535 測試程序已停止，5097 更新為新發布版，仍用 Breeze。LEARN-101 使用者建立的案例草稿不屬 QA，保留未發布。
- .ai 規格／交接及 README、形成性／P1／Ollama 文件同步更新。沒有提交或推送 Git。

## 2026-10-29：學生視角改為可看交作業、不可實際提交

- 使用者補充因並非該學生本人，老師／助教學生視角應能看流程與 AI 評閱，但不能實際提交。此要求取代同日先前允許 practice 提交的決策。
- Vue 保留提交按鈕但停用並說明；Save handler 另擋，後端 AssignmentWorkflow 對 Practice + Submit 回傳 403。自己的草稿與 AI 回饋保留；真正學生提交與原快照流程不變。既有練習快照不刪除或重開，無新遷移。
- 實測：後端 115 項通過（啟用真實 PostgreSQL）、前端 22 項通過，Vite build／publish 成功。Focused browser/API 2 項通過，分別涵蓋真正學生提交與教師／助教禁止提交、仍可草稿及 AI；全用合成模型，未向真實模型發測試。
- 本輪未重跑先前全部 9 項 browser/API。使用現有 Node CLI，未改裝 npm；QA 已清理，測試程序停止，5097 更新。未提交或推送 Git。

## 2026-10-29：計畫進度盤點

- 依使用者要求整理 docs/progress-2026-10-29.md，明確區分已實作功能、測試證據、待教授確認、未實作與里程碑。
- 核對目前程式及最新學生視角禁止提交政策；唯讀資料庫確認 001–005 已套用、LEARN-101 的四題示範仍為 draft，目前無正式學生提交及留存 evaluation_runs。
- 報告引用實際歷史驗證：最新 115 後端、22 前端及 focused 2 項；前輪 3+6 瀏覽器結果不冒稱最新全部重跑。AI 合成測試不能代表模型評分準確；仍待教授與真實課堂驗收。
- 本次僅文件與唯讀盤點，不修改應用、帳號、作業、成績或 schema，未重跑測試、未呼叫模型、未提交／推送 Git。

## 2026-10-29：作業題型、課程資源、小組、問答與獨立考試

- 使用者提出七項調整並確認：作業全部停用 AI、保留獨立 AI 入口；是非／單選／多選存標準答案且保留原申論 textarea；課程資源與下載計數；帳戶頁登出；教學端分配小組；提問預設私人、由教師／助教決定公開；獨立考試管理及成績／學號排序。取代先前作業形成性回饋與只規劃分組的現行效力，保留歷史。
- 新增 006 附加遷移，既有題目 essay；不更改 001–005，不刪原內容。API 與 Vue 加入四題型驗證、管理端私密答案鍵、課程檔案（20 MB）與原子下載計數、沿用分組資料表、學生提問／教學答覆與可見性、獨立考試草稿版本／時間／一次提交／人工成績及排序。教授可維護唯一選填學號，不假造既有學號。
- FormativeFeedbackController GET／POST 全部 403，作業頁移除 AI 元件與確認提示；舊回饋保留。獨立 AI 模組／profile／提示未改，沒有模型推論。帳戶頁沿用原 signOut 清除身份與 CSRF，再回登入頁切換真人帳號。
- 考試固定滿分 100、人工儲存立即給學生本人看，不自動對答案計分，不套用作業規準／修訂／發布成績政策；小組只分配名單不共交作業。公開提問包含姓名；計數是授權下載請求，非獨立人數。界線均寫入 docs/course-tools.md 與規格。
- 實測後端 125、前端 25 通過；PostgreSQL 測試啟用，0 略過。Vite build／publish 成功。最終 5097 focused browser/API 2 項通過，既有 P0／P1 browser/API 4 項在 5107 通過。桌面／手機截圖已檢視。未重跑獨立 AI 瀏覽器全套或模型品質驗收。
- 過程的 sandbox testhost 連線逾時改以可連線環境 vstest 成功；舊 apphost 鎖定改隔離輸出；登入 429 等待恢復後重跑，題型選單補 aria-label 後 UI 通過。這些失敗不算成功數，不放寬保護。
- 006 套用與重跑略過均成功。5097 更新至本次發布，沿用 .codex-build/p0-publish 與環境提供設定；未更改使用者開發程序。QA helper 新增資料清理與非 QA 記錄拒絕保護；合成帳號／課程／答案／資源／提問／考試已清理，state 移除，5107 停止。保留真實課程／未發布示範作業。
- 同步更新 .ai/agents.md、elearning.md、history.md 與原形成性／學生體驗文件政策註記；文件變更亦納入本紀錄。未提交或推送 Git。

## 2026-10-29：課程五入口與線下加權評分管理

- 使用者要求功能結構為課程 → 教學資源／講義、同學、作業管理 → 提問公開控制、考試管理 → 排行榜（名次／學號）、評分管理（線下作業／小考／期中等僅登分與平均）；並明確補充每項需要成績權重。依此調整現有應用，未僅整理文字架構。
- 課程改 query section 五入口，可直接連結及重新整理；預設資源。原小組歸入同學並新增受課程授權保護的學生名單（無郵件／成績）；作業內新增提問導覽，考試有獨立排行榜表格與名次／學號排序，保留原教師／助教排行榜權限。
- 007 附加遷移新增 course_grade_items／course_grade_scores。線下項目有類型、滿分與相對權重；個人按已登錄項目換算百分制加權平均，空白不計、0 分計入；全班平均取已有成績的有效學生個人平均再平均。完整 decimal 計算後以兩位小數四捨五入顯示；不混入線上作業／考試，無 AI／題目／學生作答。
- API 後端驗證有效課程角色、有效學生、分數範圍、版本衝突與封存唯讀；修改滿分不得低於已存分數。學生只看本人原始得分及匿名統計，staff practice 不讀真學生個別分數也不寫。清除保留空值 tombstone 與遞增版本，避免舊分頁重新覆寫。
- 前端逐人儲存保留其他未存輸入，切換項目／功能與離頁提醒。初次瀏覽器發現 Object.hasOwn 對新增草稿 key 的反應性不足，改可追蹤 key 檢查後通過；每筆草稿另保留原始 expectedVersion，實測他人先修改、刷新其他列後仍拒絕過期覆寫。
- 實際驗證後端 135（含已啟用 PostgreSQL，0 略過）、前端 25、最終 5097 focused browser/API 2 項通過，原作業／P1 browser/API 4 項在 5107 通過。Vite build、publish 成功，教師桌面／教師與學生手機已檢視。未呼叫模型，未重跑獨立 AI 完整瀏覽器或模型品質驗收。
- 007 已交易套用並驗證 001–007 重跑略過。5097 更新、原內容根目錄與環境憑證方式保留；新增 QA 清理順序與非 QA 作者／學生拒絕保護，合成帳號／課程／成績與 state 已清理，5107 停止。真實內容保留，未提交／推送 Git。
- 同步更新規格、交接、歷史與 docs/gradebook.md、course-tools.md；文件異動亦記於本條。未進行教授／真實班級驗收，不將團隊計算方式描述為教授核定政策。

## 2026-10-29：S／T／A 案例學習站台重構

- 使用者要求 S1 登入、S2 基本資料、S3 三區四階段作答與 S3a 逾時／重試；T1 學生清單、T2 個別歷程、T3 全班統計、T4 匯出；A1–A6 管理首頁、帳號、案例、標準答案／重點、回饋模板、參數／稽核。確認取代原主導覽、舊資料保留；目前所有教授兼任管理者；只文字 AI 回饋不給分，教師只看所屬課程正式提交與對應回饋，私稿維持私人。
- 008 新增獨立案例／素材提示與重點版本、私人答案版本、回饋／模板、基本資料與參數。原作業 AI API 仍 403，舊獨立 AI／課程長清單 UI 不再主導覽。原示範與真實資料未轉公開或刪除。
- 新 FeedbackAsync 有文字 schema 與嚴格 parser，無 score／thinking／raw response；沿用三 profile 的合併與 sampling 規則，不改 Configuration/Ollama。回饋只綁本人已存階段與原案例版本，不持有作答鎖等待模型；失敗不阻擋正式提交。模板／案例／答案版本可追溯，新案例不覆寫進行中原版本。
- T2／T4 後端只回正式各階段最終 revision 與對應回饋，不帶私人初稿／原始標準答案；統計只算正式、不做能力分數。CSV 有公式開頭及引號保護。A3／A4 新版本草稿與發布分開；A5 版本化風格；A6 啟停、逾時（仍受部署 timeout 上限）、次數及安全稽核。
- 補上回饋刷新只更新狀態、不重綁編輯版本與案例，使其他分頁更新時仍 409；OwnContext 綁原 attempt／案例，practice 不冒用也不提交。超過 310 秒的 pending 可恢復重試。
- 實際後端 147 通過（啟用 PostgreSQL、0 略過），前端新增文字元件後 28 通過。三 profile 文字封包、嚴格契約與 CSV 單元測試皆通過；新 S／T／A browser/API 2 項用 loopback synthetic 模型，涵蓋逾時／重試、正式提交、所有管理頁的維護及跨角色隔離。未做真實模型品質／教授驗收。
- 測試重跑時修正登入後等待跳頁的競態，並讓合成案例／逾時標記每次獨立，未改登入限流。舊 UI suites 保留歷史，不冒稱適用新路徑；Playwright 主設定只跑 studio*.spec.js。
- 008 套用與重跑略過成功。主站 5097 已更新，原內容根目錄與 Breeze 保留；後續使用者在同輪補充測驗／PDF 定位，最終清理、驗證與交付續見下一條。文件以 docs/case-studio.md、規格第十四節及目前 agents 頂部為準，保留舊決策時間脈絡。

## 2026-10-29：命題作答改定位測驗、PDF 作業與加權成績連動

- 使用者補充：原線上作業命題／作答是線上／課後測驗；作業是繳交 PDF 報告讓老師閱讀給分，均納入先前測驗／作業成績管理。保留 S／T／A 案例學習，補上測驗／PDF／成績入口；此要求取代先前完全不合併線上評量的 gradebook 現行政策。
- 009 新增 PDF 作業／提交與 grade item 的真實來源外鍵／唯一索引。測驗沿用申論／是非／單選／多選、提交版本與人工成績草稿／發布；新測驗一次提交、不做 AI 或客觀題自動計分。新建來源評量與 grade item 同交易，舊未連動資料不自動發布或匯入。
- 測驗成績發布才同交易同步 gradebook；PDF 教師閱讀後儲存分數同交易同步。更正更新原列、不重複計項，連動項目在成績管理只改權重，不手動覆寫來源分數。原線下小考／期中等可手動登分，平均公式及空白／0 分規則不變。
- PDF 限單份 20 MB，副檔名／檔頭檢查、bytea 儲存、附件下載與 nosniff，學生只能讀本人、教師／助教限所屬課程；繳交鎖定、伺服器期限與評分版本保護。逐人給分保留其他未存輸入與原版本，離頁取消保留編輯。PDF 與測驗均無 AI 評分。
- 實測學生測驗作答、評分草稿不計分／發布才連動、PDF 上傳／壞檔／重複／截止拒絕、同學隔離與老師下載位元組、人工給分更正、權重修改、不重複計分與多人未存編輯。10/20 權重 30 加 80/100 權重 70 得 71，已由真實 API／DB 驗證。
- 最終後端 147、前端 28 通過，PostgreSQL 測試實際啟用且 0 略過；Vite build／publish 成功。現行站台全 4 項 browser/API 在 5107 通過（案例 AI 全用 synthetic），PDF 多人未存回歸另 focused 通過；最終 5097 的測驗／PDF連動與新導覽 focused 2 項通過，未觸發真實 AI，並檢查未殘留 inspection-test 模型。已檢視教師桌面與學生手機。
- 009 交易套用，001–009 重跑略過。QA helper 加入 PDF／來源成績清理順序及非 QA 作者／學生／評閱者拒絕保護；合成帳號、課程、PDF、測驗／案例作答、回饋、模板／成績與 state 已清理，設定值回復測試前；5107／11535 停止，5097 留供試用。真實資料、原內容根目錄與模型 profile 保留。
- 同步新建 docs/case-studio.md、learning-assessments.md，更新 .ai 規格／交接／歷史、README 與原 gradebook／課程工具／形成性／Ollama 文件的現行政策註記。未提交或推送 Git；沒有真實班級／教授驗收、壓力測試或模型內容校準主張。

## 2026-10-29：恢復課程管理並集中課程關聯

- 使用者要求課程管理加回，資源、學員與相關測驗都連結課程；本次優先於先前隱藏原課程導覽的決策，但保留新 S／T／A 與測驗／PDF 作業定位。
- 恢復 /courses 與課程工作區，教授建立課程，資源／講義、學員／小組、測驗與 PDF、成績、案例集中於課程。嵌入評量頁固定 courseId；原捷徑與測驗／報告／案例／歷程補回所屬課程連結。A3 從課程進入時預選／篩選本課。
- 沿用既有 FK 與授權，不新增遷移或搬移舊資料。CourseSummary 新增實際資源／有效學生／線上測驗／PDF 數與擁有者旗標；學生／practice 不數草稿。完整名單讀取維持教師／助教，加入／角色更新只給擁有教授，學生無郵件或管理入口。
- 原加權成績、來源連動、PDF 私密與各模型設定保持；補強成績編輯在任一課程頁籤／課程切換的離頁保護。手機 inline 卡片斷裂改 block 後重檢；測試使用唯一課名修正重跑時舊 QA 同名選取競態。
- 實測後端 147（含真實啟用的 PostgreSQL，0 略過）、前端 28 通過；Vite build／publish 成功。新課程 browser/API 及既有測驗／PDF／成績、導覽回歸共 3 項最終 5097 通過，含雙課隔離、草稿計數、資源下載、課程內評量建立、A3 預選、案例往返、角色限制與頁籤取消離頁。檢視教師桌面、學生手機。
- 本輪沒有模型推論，未重跑完整 S3 合成模型套件或模型品質驗收，先前結果保留原時間範圍。合成 QA 與 state 已依所有權核對清理，5107 停止，5097 更新且保留原 p0-publish 內容根目錄、真實資料與 Breeze；未提交／推送 Git。
- 新建 docs/course-hub.md，同步更新規格／交接／歷史與 README、案例／評量／課程工具文件的現行政策註記；文件修改亦記於本條。未新增完整課程生命週期或名單匯入承諾。

## 2026-10-29：既有課程更名為個案分析課程

- 使用者因目前沒有改名介面，要求將「測試課」更名為「個案分析課程」。查詢確認唯一目標為 LEARN-101、115上，課程 ID 897f8770-8a65-49d7-9cac-904eb00dbe53。
- 以交易鎖定目標、核對原名稱後僅更新 courses.title；留下 actor 為 NULL 的系統維護 course.renamed 稽核，記錄原／新名稱及使用者要求維護來源，不冒用教授身分。
- 提交後重讀確認新名稱；交易內比較除 title 以外的完整課程欄位及學員、命題作業、資源、PDF 作業、案例、成績項目筆數均一致。原 draft 狀態保留，無新增課程、發布或資料搬移。
- 使用忽略目錄 .codex-build 中的一次性 Npgsql 工具，未變更產品程式或結構，無須重建或重啟站台；未重跑功能測試。同步更新 agents 與 elearning 的現行名稱說明，歷史舊名稱保留原時間脈絡。

## 2026-10-30：課程分班、同班小組與資源範圍

- 使用者要求同課程不同時間分班、小組限定同班，資源可限本班或全課通用。新增 010_course_sections.sql，課程學生／小組／資源加 section_id 與課程複合外鍵；未移動或推斷真實學生班別，既有資源共用，舊小組未分班保留。
- 新增班別列出／建立／編輯、時間說明與學生分配 API／介面。每人每課最多一班與一個有效小組，班內小組可同名但同班唯一；跨班、跨課成員或班別拒絕。調班／取消分班結束原小組關係，歷史不刪除；非學生角色變更也釋放班別與小組。沿用課程列鎖，班別更新留稽核。
- 資源上傳與後續範圍編輯可共用或指定班。資源列表、下載與課程卡片計數都檢查班別，拒絕下載不累計次數；學生練習仍無管理權且只看共用。評量、PDF、案例、加權成績保持原課程與個別提交流程。
- 010 交易套用、001–010 重跑略過。後端 147 通過（DB 啟用、0 略過）、前端 28，production build／publish 通過。既有 Visual Studio 程序鎖住預設 bin，改隔離輸出完成測試，未停止使用者開發程序。初次 CLI 路徑／沙箱連線失敗已改正，最終 5097 四項 browser/API 全通過，涵蓋分班隔離、調班、資源範圍、課程工作區與評量成績／導覽回歸。檢視教師與學生手機畫面，沒有模型推論。
- 最終預覽 PID 32960（執行前仍須重新核對），5097 沿用 p0-publish 與本機設定；5107 測試服務已停止。QA helper 加入班別清理順序，合成帳號／課程／資料／state 全清理，真實課程及資料保留。未提交或推送 Git。
- 新增 docs/course-sections.md，更新 elearning／agents 與課程工作區文件；本輪未新增班別刪除、匯入、依班派題或成績篩選，未做真實班級驗收。

## 2026-10-30：帳號學號唯讀與平台角色編輯

- 使用者要求學員管理學號不再可編輯、空設定顯示「(空)」，並可自行將既有帳號調整為助教／學生。本輪提供功能，未直接修改真實帳號或課程角色。
- MembersView 清單改唯讀學號，加入角色選單與明確儲存；後端教授限定 PUT /api/accounts/{id}/role，expectedRole 衝突檢查、交易／advisory lock、變更前後角色稽核、有效教授與自行降權保護。既有 Cookie 驗證使角色更新後須重新登入。無 schema 變更。
- 實測 dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/account-roles-verified/ --verbosity quiet：148 通過、0 略過，FORMATIVE_TEST_CONNECTION 啟用真實 PostgreSQL、僅合成帳號並清理。首次沙箱內 DB 測試無進展已取消，改具網路權限後通過。
- 於 ClientApp 使用既有 Node 執行 node_modules/vitest/vitest.mjs run：30 通過；node_modules/vite/bin/vite.js build 成功。dotnet publish --no-restore -p:SkipFrontendBuild=true -p:UseAppHost=false 成功。5097 更新並保留原內容根目錄；未執行瀏覽器或真實模型測試。
- 同步規格與交接，保留既有未提交工作；未提交／推送 Git。

## 2026-10-30：匯入使用者提供的 11 位假學生

- 只取中文姓名與 E2023001–E2023011 作學號；忽略職稱、部門、主管、員工旗標與原始時間欄。使用現有 app_users／portal_credentials 建立有效 Student，測試 Email 使用學號小寫加 @students.example.invalid，獨立隨機密碼經既有 PasswordHasher 雜湊，本機登入交接檔置 Git 忽略目錄，不寫文件。
- 一次性 Npgsql 工具使用整批交易與既有 advisory lock，遇學號／Email 衝突整批拒絕，不覆寫既有帳號；系統維護 audit actor NULL，不冒用教授。未增加課程、班別、小組、作答或成績。
- 實際執行：初次缺少一次性專案 restore 資產未執行資料寫入；還原後匯入 11、跳過 0，提交後查核 11 個有效 Student。再次執行新增 0、跳過 11，未重設密碼；建立時驗證密碼雜湊可核對。此為使用者要求保留的展示資料，不屬自動清理 QA。
- 無程式行為／schema 修改，不重跑軟體測試、不重啟網站；保留既有未提交工作。同步交接及歷程，未提交或推送 Git。

## 2026-10-30：假學生登入 Email 後綴調整

- 依使用者要求，僅指定 11 位 E2023001–E2023011 假學生登入 Email 改為學號小寫加 @nsysu.edu.tw，作測試識別；未建立或寄信至實際信箱。
- 交易鎖定帳號、核對原 Email 與 Student 角色、檢查衝突，同步 app_users.email 與 portal_credentials.normalized_email 並留稽核。提交後查核 11 筆一致；本機 Git 忽略登入交接檔同步更新，密碼未改。
- 未改程式／schema、未重啟或重跑軟體測試，未提交／推送 Git。交接現行後綴同步，原匯入紀錄保留歷史。

## 2026-10-30：課程圖片縮圖與 PDF 報告預覽

- 依要求加入課程圖片縮圖／點開大圖與 PDF 頁內閱讀，PDF 報告也提供預覽；使用原生 PDF，不新增上傳轉檔管線。FilePreviewPolicy 僅接受 PNG／JPEG／GIF／WebP／PDF 的副檔名與檔頭一致，HTML／SVG 保留下載。
- 課程 GET resources/{id}/preview 沿用班別／有效成員／practice 授權，且不更新 download_count。資源列表讀 16-byte header 提供 previewType。報告 preview 共用原下載授權查詢，新增 report.previewed 稽核，不開放其他學生。API no-store／nosniff，Vue 用帶身分與 view header 的 fetch 取得 Blob，切換／卸載釋放 object URL、忽略過期回應；無公共網址、無 schema 遷移。
- 實測 dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/file-preview-tests/ --verbosity quiet：157 通過、0 略過（FORMATIVE_TEST_CONNECTION 啟用）。ClientApp 既有 Node 執行 vitest.mjs run：33 通過，vite.js build 成功。dotnet publish --no-restore -p:SkipFrontendBuild=true -p:UseAppHost=false 成功。
- focused studio-files.spec.js Chrome／真實 API 1 項於 5107 及最終 5097 各通過，使用合成圖片／完整單頁 PDF；驗證圖片 naturalWidth、PDF 正確 bytes／MIME、no-store、不計下載、跨班／非成員／practice／報告本人隔離，並檢查手機無水平溢位。已檢視桌面截圖，Chrome 原生閱讀器實際顯示 Synthetic PDF preview。第一次測試檔路徑重複 ClientApp 而失敗，修正後通過；非成功結果未計入。
- 合成帳號／課程／資源／報告及 state 透過 scoped helper 清理，5107 停止；5097 更新、使用者資料與原內容根目錄保留。未呼叫模型，未重跑全套 browser/API。文件同步規格、交接與 docs/file-previews.md；未提交／推送 Git。

## 2026-10-30：縮窄成員角色選單與藍色儲存按鈕

- 依使用者要求，MembersView 角色選單固定 110px，與主要藍色儲存按鈕並排，保留原 disabled／儲存行為。
- Vite build 成功，靜態資源複製到現行 5097 發布目錄，未重啟。初次從根目錄使用不支援的 --root 參數失敗，改 ClientApp 工作目錄正常完成。樣式調整未新增測試、未重跑後端或瀏覽器。同步規格與交接；未提交／推送 Git。

## 2026-10-30：學生名單直接移動班別

- 使用者反映班別整份名單難維護；新增逐人選擇班別／未分班與藍色儲存，班別名稱／時間表單不再提交 Members，因此不覆寫全班名單。舊明確 Members API 保留相容；提供重新載入清除未存選擇。
- 新 PUT /api/courses/{course}/classmates/{student}/section：限定有效課程教師／助教、拒絕 practice／封存，課程交易鎖、同課有效學生與班別檢查、必要 expectedSectionId 原班別核對與 409、稽核。只移目標學生，退出原有效小組保留歷史，不改其他學生、作答或成績；無遷移。
- 實際 dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/student-move-tests/ --verbosity quiet：157 通過、0 略過（DB 啟用）；ClientApp Node vitest.mjs run：33 通過、vite.js build 成功。dotnet publish 與 PortalSmoke build 成功。
- 擴充 studio-sections.spec.js，真實 Chrome／API 使用合成資料驗證逐人 UI 移動、同學班別保持、小組退出、過期來源班別 409、跨課班別 400、學生／practice 403、時間更新保留名單、助教取消分班。5107 與最終 5097 各 1 項通過；既有分班資源隔離亦同測試通過。未呼叫模型、未重跑其他 browser/API。
- scoped QA／state 已清理、5107 停止，5097 已更新並保留原內容根目錄／真實資料；同步規格、交接及 docs/course-sections.md。未提交／推送 Git。

## 2026-10-30：班別垃圾桶與影響範圍確認

- 使用者要求刪除空班別，並確認小組刪除與學生移出班別。CourseTools 新增 Trash2 與明確可及名稱；先讀刪除影響範圍，再原生 confirm 顯示學生數、小組名稱及關係紀錄刪除，不刪學生帳號／課程資格／作答／成績。
- 新 GET sections/{id}/deletion 與 DELETE sections/{id}：有效教學權限、封存／practice 拒絕，課程及班別鎖、學生／小組／關係／資源狀態 hash 綁確認，變更 409。刪除學生班別關聯與班內小組，保留其餘資料、留下計數稽核。專用資源未移轉或小組有任何作業紀錄拒絕，FK 競態轉 409；不自動改共用。無遷移。
- 實際後端 dotnet test（隔離 section-delete-tests OutputPath、FORMATIVE_TEST_CONNECTION 啟用 PostgreSQL）157 通過、0 略過；ClientApp Node vitest.mjs run 33 通過，vite.js build／dotnet publish 成功。
- studio-sections.spec.js 擴充取消／確認、空班助教刪除、學生改未分班、小組刪除、專用資源保護、過期確認 token、學生／practice 拒絕；5107 及最終 5097 各 1 項通過。首輪 dialog handler 提早重綁競態已以等待按鈕恢復修正，未放寬產品限制。未重跑其他 browser/API，也未呼叫模型。
- QA 合成帳號／課程／資料／state 已 scoped 清理，5107 停止、5097 更新，真實資料保留。同步規格、交接與分班文件；未提交／推送 Git。

## 2026-10-30：開放本人學生體驗提交與獨立練習成績

- 使用者要求自己切學生視角也可提交作業／測驗，並查看自己成績；明確選擇切回教學視角、從獨立本人練習紀錄人工評分，再回學生視角查看。取代 10/29 禁止 practice 提交政策，保留舊決策時間脈絡。
- AssignmentWorkflow／Vue 開放既有 practice 提交，沿用精確版本、不可改寫快照及截止等政策。StudioWorkflow／StudentCaseView 開放本人案例 practice 提交，仍排除教學歷程／統計，不新增案例成績，也未改模型 profile 或提示。
- 011_practice_reports.sql 新增 report_submissions.is_practice default false、唯一鍵含模式；PDF 提交、清單狀態、讀取／下載／預覽皆依 namespace，正式 grade endpoint 只處理非 practice。原帳號／內容／成績不搬移。011 交易提交，重跑 001–011 略過。
- 新 PracticeAssessmentsController 的課程 practice GET、quiz／report grade PUT、本人 practice PDF preview GET；均實際核對平台 staff 與有效 course teacher／ta，actor 只取身份、不接受指定學生；教學模式才可評分，封存拒寫。所有查詢只綁本人 is_practice。測驗 grades 綁快照與前 grade ID 樂觀衝突、PDF version 衝突；範圍／精度驗證，不呼叫正式 GradeSync。沒有將私稿／他人資料開放給 staff。
- 課程成績頁 PracticeRecords 教學模式可檢閱本人作答／PDF 並人工評分，學生模式只讀得分／評語與練習加權平均。多列未存草稿保留原評分版本，離頁／重整提醒。正式 GradebookPanel 在練習 UI 不顯示，正常學生及教學正式成績流程保留。
- 實測 dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/practice-submit-tests/ --verbosity quiet：157 通過、0 略過，PostgreSQL 明確啟用。原拒絕練習提交的 DB 回歸已改驗證成功快照且正式名單不含。ClientApp Node vitest.mjs run 35 通過，Vite build／dotnet publish 成功；首輪前端舊「禁止提交」預期已依新政策修正後重跑通過。
- focused studio-practice.spec.js 與原 studio-assessments.spec.js 在 5107 共 2 通過；補強教學 UI 人工儲存與案例提交／統計再 focused 1 通過，最終 5097 完整這 2 項通過。驗證本人／其他 TA 隔離、學生／practice 拒絕評分、分數與版本錯誤、10/20 權重30及80/100權重70得71、練習不寫正式成績、實際 TA 視角測驗／PDF 提交、真正學生原發布／評分與連動流程。無模型請求或品質主張，未重跑其餘 browser/API。
- QA 合成帳號／課程／練習／正式作答與 state 已 scoped 清理，5107 停止，5097 保留原內容根目錄更新。本輪無真實學生提交或評分。同步規格、交接、學生體驗／評量文件與 README；未提交／推送 Git。

## 2026-10-30：測驗客觀題全對得分與申論題人工合計

- 使用者確認是非／單選／多選電腦批改，多選完全吻合才得分，只有申論由教師評分；混合測驗待申論完成才完整評完。取代先前現行測驗全部人工評分。
- 新 QuizGrading 以伺服器答案鍵和提交快照計算，逐題明確配分或剩餘均分，精度與總分驗證；多選忽略順序但漏／多／錯／未答 0，非法索引／重複選項仍拒絕。客觀題可留白提交得 0，申論仍須填答；一次提交、期限、封存、版本快照保持。
- 012 交易套用並重跑略過，新增 quiz_grading FK 到提交快照、grades.essay_scores；grader 可 NULL 表示自動評分。CaptureSnapshot 同交易保存判定，純客觀產生系統成績；正式不自動公開或寫正式成績簿，教師發布後才連動，練習自動完整分數立即可見。混合題由教師評所有申論，伺服器驗證範圍、總分與原 grade ID，不允許修改客觀題。單申論舊 API 可用總分相容，仍扣除固定客觀分並驗證申論上限。
- TeachingPanel 顯示客觀題已批改、待評申論、各題正誤／配分與申論欄；純客觀只有發布按鈕。新 quiz-grading GET 沿用 snapshot ownership／practice namespace，學生發布前遮蔽正誤與自動分數，不回答案鍵。PracticeRecords 同規則評本人申論，不同步正式 gradebook。出題器加入選填配分，原一般作業／規準／PDF／案例政策保留；舊人工提交不追溯重算。
- 實際 dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/quiz-auto-tests-final/ --verbosity quiet：167 通過、0 略過，FORMATIVE_TEST_CONNECTION 啟用 PostgreSQL。新增 10 項全對得分／均分與餘數／申論邊界單元測試，調整未答客觀題舊拒絕預期。ClientApp Node vitest.mjs run 35 通過，Vite build／dotnet publish 成功。
- loopback API 5107 的 studio-autograde、studio-practice、studio-assessments 共 3 通過；最終 5097 再驗這 3 項皆通過，新增純客觀 UI 不顯示人工分數且可發布、教師 UI 申論 60 加客觀 30 得 90、學生發布前隱藏／發布後可見、是非對單選錯多選順序／漏選／多錯項、練習自動 50 分、正式成績連動、既有 PDF／練習回歸。無真實學生／模型請求，不宣稱模型評分改善；未重跑其他 browser/API。
- QA helper 清理 quiz_grading（FK cascade 亦保留），合成帳號／課程／成績／state 已 scoped 清理；5107 停止，5097 保留原內容根目錄更新。001–012 重跑略過，無既有人工成績改寫。同步規格、交接、README／評量／練習文件與 docs/quiz-autograding.md；未提交／推送 Git。

## 2026-10-30：教師錯誤對照、即時計分與確認正式成績

- 使用者要求教師打開測驗看到學生錯在哪、即時計分，教師確認後寫入正式 DB。以既有正式成績簿／已發布成績作正式界線，作答、客觀判定、草稿仍持久化供恢復，不建立第二份 DB。
- 新 QuizAnswerReview.vue 將選項索引轉為可讀勾選，文字標示誤選／漏選／選對，色彩只輔助；學生 showKey=false 不呈現標準答案。TeachingPanel 即時總分／待評小計與統一確認按鈕，確認最新分數時必要先存新 grade，再原發布交易同步成績簿；保留另存草稿。PracticeRecords 同步可讀對照與練習暫算，後端僅教學模式本人 practice 查詢給答案鍵實值。
- 回歸抓到 essays 直接參照已載入 grade.essayScores，修改 60→65 污染原 grade，會誤判沒修改並發布舊 90 分；改複製原分項後修正。新增 TeachingPanel 單元驗證最新 95 分會存新 grade 再發布、取消完全不 POST；瀏覽器重現並驗證修復。
- 實際 dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/teacher-review-tests/ --verbosity quiet：167 通過、0 略過，PostgreSQL 啟用。ClientApp Node vitest.mjs run 最終 40 通過（新增 3 項答案對照及 2 項確認回歸）；Vite build 與 dotnet publish 成功。
- 5107 首輪三套 browser/API 中 2 通過／1 抓到舊分問題，不計成功；修正後 autograde／practice／assessments 共 3 通過，最終 5097 再跑同 3 項通過。驗證誤選 2／漏選 1、90→95 即時預覽、取消無正式 95、確認發布最新 95，以及既有真正學生／PDF／練習隔離。未呼叫模型、未重跑其他 browser/API。
- 無遷移，QA/state 已 scoped 清理，5107 停止、5097 更新並保留原內容根目錄與真實資料。同步規格、交接與 quiz-autograding 文件；未提交／推送 Git。

## 2026-10-30：簡化已匯入測試帳號登入

- 使用者要求測試階段不要使用交接 JSON 中複雜隨機密碼。以原始匯入稽核、名單、學號與 Email 核對，只重設該批 11 個假帳號，密碼內容不寫文件。原 Git 忽略交接檔已同步更新。
- 首次嚴格 Student 條件發現一個假帳號已被改為 TeachingAssistant，交易回滾、無更動；核實後保留該角色，依這批測試帳號範圍重設。其他既有帳號與所有角色／課程／作答保留。
- 採現有 PasswordHasher PBKDF2 210000 次雜湊，重設失敗計數與鎖定；不存 DB 明文，不放寬正式建立／修改密碼規則。交易提交後以 PortalAccountStore.AuthenticateAsync 逐一驗證，11 個皆成功。
- 本次帳號資料維護無產品程式／schema 變更，無須重啟，未重跑全套功能測試或模型推論；同步交接與歷史，未提交／推送 Git。

## 2026-10-30：匯入臨床心理學基礎試卷與配分組成

- 使用者提供 GPT 產生的完整測驗，要求鍵入系統並考慮老師日後設計100分組成。保存 docs/examples/clinical-psychology-basics-quiz.json 為教學草案，逐字保留題意、客觀答案、案例與申論五段評分參考；不作模型提示／答案校準資料，無學生真實作答。
- 查核唯一 LEARN-101 個案分析課程及有效擁有教授／teacher後，以單一交易建立 quiz draft、13題、grade item 真實來源 FK。ID f99b5828-640f-4eb7-bb2a-9b1ae5204d4a；單選25、多選15、是非20、申論40，滿分100、無期限、課程權重1。grade item 依現有 NOT NULL 建立者欄關聯課程擁有者，quiz.imported actor NULL、source user_requested_clinical_basics_quiz 記錄系統代建，不建立登入或發布。重跑確認 already-imported，不覆寫或重複。此非 QA，必須保留。
- 013_question_grading_guidance.sql 加教師參考欄；AssignmentWorkflow 寫入、校驗並只在教學角色 Load 回傳，學生 null；practice 查詢同樣僅教學模式提供。出題器／TeachingPanel／PracticeRecords 支援參考，不將40分五區間誤套4級規準或交LLM批改。
- 新 QuizScoringPlan／quizScoringPlan 顯示題型、題數、每題配分、小計、自動／人工總計，與後端均分／尾差規則一致；出題總分不符 UI停用保存，後端驗證保留。閱讀頁亦顯示，明確分開試卷配分與課程成績權重；同步修正評量頁舊「全部人工」說明。
- 013 交易套用、001–013重跑略過。dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/quiz-composition-tests/ --verbosity quiet：167通過、0略過，PostgreSQL明確啟用。ClientApp Node vitest.mjs run：43通過（新增3項配分規劃測試），Vite build／dotnet publish成功。
- studio-scoring-plan.spec.js 合成副本驗證13題25／15／20／40、配分40改35時禁止保存、改回40保存／讀回、老師參考可見與學生不可見、客觀全對60且申論待評40。與autograde／practice／assessments在5107共4通過，最終5097再跑同4皆通過。沒有對真實試卷發布／提交，無模型推論，未重跑其他browser/API。
- scoped QA／state已清理、5107停止，5097沿用原內容根目錄更新；真正測驗草稿保留。同步規格／交接／quiz-autograding與README，未提交／推送Git。

## 2026-10-30：課程資源多欄卡片flow

- 使用者要求資源多×多排列。CourseTools 以auto-fill網格、最小260px卡片自動換行，600px以下單欄；保留原DOM順序，檔名斷行、下載按鈕置卡片底部。FilePreview新增expanded-change事件，展開大圖／PDF時卡片跨整列，收合恢復。
- ClientApp既有Node執行vitest.mjs run：43通過；vite.js build成功。靜態資源直接更新現行5097發布目錄，沒有重啟／DB遷移／後端修改，未重跑後端測試。
- studio-files.spec.js最終5097 Chrome/API 1通過，新增桌面實際多欄、預覽卡片全寬及390px單欄斷言，既有資源／報告授權與預覽回歸沿用。已檢視桌面截圖；本輪不主張新增PDF跨瀏覽器支援。合成QA及state經scoped helper清理，真實課程／資源／測驗保留，無模型呼叫。
- 同步產品規格與交接；未提交／推送Git。

## 2026-10-30：PDF 非預覽狀態文件圖示

- 使用者喜歡 PDF 預覽，要求未預覽時顯示文件 glyph。FilePreview 使用既有 Lucide FileText 加 PDF 標籤與可聚焦按鈕，展開時隱藏 glyph、收合恢復；保持按需載入與原預覽／權限／下載流程。
- 更新既有 PDF 元件測試檢查初始／展開／收合圖示狀態；ClientApp Node vitest.mjs run 43通過，vite.js build成功。靜態資源更新5097，未重啟、未改DB／後端、未重跑瀏覽器或後端測試。同步規格／交接，未提交／推送Git。

## 2026-10-30：課程文件名稱與事後修改

- 使用者要求檔案有可自行指定、事後修改的文件名稱。新增上傳選填名稱與資源卡片編輯，保留原始檔名供辨識／下載；空白回原檔名，不重傳／改原bytes。名稱200字，顯示經Vue文字逸出，不視為HTML。
- 014新增course_resources.display_title空字串及title_version=1，既有檔保留。列表回名稱／版本，上傳存名稱；新PUT /api/courses/{course}/resources/{id}/title只限有效課程教學角色，拒practice／封存；課程share鎖、expectedVersion與409、交易稽核，不在稽核複製名稱全文。版本只追名稱，不與下載計數混用。
- CourseTools多列名稱草稿保留各自最初expectedVersion，其他列保存重新載入不洗掉未存名稱；衝突重新載入後可取消重開，離頁／重整提醒。FilePreview與資源flow保留，圖片可及名稱使用顯示名稱。
- 實際dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/resource-title-tests/ --verbosity quiet：167通過、0略過，PostgreSQL啟用。ClientApp Node vitest.mjs run：43通過，vite.js build與dotnet publish成功。
- studio-resource-titles與studio-files：5107首輪學生基本資料前置未完成而導頁，補齊合成profile後共2通過；最終5097同2項皆通過。驗證上傳命名／既有檔案改名／清空回退／長度／跨課／Student與practice拒絕、原bytes與下載檔名不改、多列未存與他人先改409，以及預覽／多欄手機回歸。無模型呼叫，未重跑其他browser/API。
- 014套用且001–014重跑略過，QA/state scoped清理、5107停止，5097沿用原內容根目錄更新，實際內容保留。同步規格、交接及文件預覽文件；未提交／推送Git。


## 2026-11-02：測驗作答改為批改呈現

- 使用者要求 /quizzes 的學生提交顯示評分與作答／正解。AssignmentView 移除重複純文字檢閱；TeachingPanel 在相同版本的每題顯示題目與 QuizAnswerReview 判定／分數。選項清楚標學生作答、誤選、漏選與正確答案，申論只用已存教師評分，不虛構分數。既有發布前遮蔽與教師答案鍵授權不變。
- 切換版本先清空舊判定／申論，切換學生 keyed remount；無 schema、後端、模型、既有學生資料變更。
- ClientApp 以既有 Node 執行 vitest.mjs run：45 通過，包含新增逐題0分與選項標示、未發布隱藏、申論0分測試。未執行後端／瀏覽器／真實模型驗證，未登入或更改使用者指定的真實提交。同步交接／規格／quiz-autograding 文件，未提交或推送。
- Vite build 成功，輸出至 wwwroot/portal；localhost:7173 若為開發執行個體可重新整理讀取新資源。本輪未重啟使用者開發程序。


## 2026-11-02：清除1029測試評量與新增作業內容測驗

- 先唯讀盤點；無1029命名PDF作業，確認使用者選擇永久刪除1029隨堂考試（211294c0-4093-4e03-be2b-a63ab5cf1fd9）與測試題型功能（af489c42-dc7c-4caf-b09a-2b098b7b2818），含相關紀錄；保留主題作業。
- 精確ID＋課程＋標題核對、列鎖、單交易刪除：2測驗、5題、2提交／2快照、6答案與6版本、1提問、1來源成績項目；無grades／quiz_grading／course_grade_scores／evaluation_runs待刪。保留purge稽核，未刪帳號／課程／資源／PDF作業。交易後查詢確認三份主題線上評量與一份PDF作業仍在。
- 新草稿515b76f9-8953-476c-bd3e-a017af0e71ab，10題100分，5單選3多選2是非，依現有課後作業說明編寫；來源docs/examples/homework-comprehension-quiz.json，不取私人學生答案、不做模型提示或專業核定宣稱。原匯入邏輯QuestionPolicy校驗、100分分配校驗及DB讀回題數／總分／draft驗證成功；未發布。
- 維護工具初次NuGet網路解析失敗，改用既有組件；本地受限網路改以授權資料庫連線完成，憑證未輸出。僅內容維護無產品／schema變更；未重跑單元／瀏覽器／模型測試，無遷移或服務重啟。同步規格／交接，未提交推送。

## 2026-11-03：取消班別，改一課程一授課群體（規格與評估）

- 使用者要求修改規格並評估異動，明確取消班級／班別、僅用課程。本次範圍為文件，不推定已授權搬移／刪除真實資料或部署程式。
- 重新讀取 agents／elearning／最新 history，核對 Git 狀態及原 001／010 schema、課程／班別／資源／小組控制器、課程列表與工作區、CourseTools 及相關瀏覽器／QA 引用。程式仍為分班實作；未連線盤點即時 DB，未宣稱目前沒有班別或限定資源。
- 新 docs/course-only-spec.md 定義學員、小組、資源直接屬於課程，一人每課最多一有效組、組名課內唯一；授課群體分開建課，沿用 code＋term_code 唯一性。資源只限本課有效成員，跨課分享另案；上課時間欄與改名 UI 不自動擴成新增需求。測驗／PDF／案例／成績沿用 course_id、既有批改發布及正式／練習隔離。
- 盤點 UI、API、資源列表／預覽／下載／摘要權限、角色變更、小組檢查、section_id／FK／索引退役、測試與 QA 清理。規劃新向前 migration，不改已套用歷史 SQL；頁籤 query.section 不是班別，不應誤刪。
- 遷移前須確認多個真實授課群體、限定資源可見對象、同名組與重複有效組員、時間與所有提交／成績關聯。禁止直接清空 section_id 導致擴權，禁止用刪班 API 代替轉換而刪小組；不自動拆課或搬移提交／成績。既有多班拆課與資源授權映射需另外明確核定。
- 同步更新 elearning／agents、course-hub／course-sections／course-tools 的新決策與待實作標記，保留舊時間脈絡。僅文件核對，未改產品程式／設定／SQL、未執行 migration／測試／服務重啟／模型推論，未提交或推送 Git。

## 2026-11-03：明確取消跨課程資源共用（規格補充）

- 使用者指定「取消跨課程共用資源，每個課程都是獨立的」。取代本日先前將跨課分享列為另案的現行規劃：不提供跨課分享、多課授權、共用檔案庫或同步更新。
- 同教材於各課分別上傳，資源紀錄／版本／下載數獨立，單課修改或刪除不影響其他課程；不因同名課程或同一教師自動授權。既有拆課資料轉換亦不得保留共用連結或複製歷史次數冒充新增下載。
- 同步修改 docs/course-only-spec.md、course-hub.md、course-tools.md 與 .ai/elearning.md、agents.md，新增獨立資源驗收規則。保留前條歷史脈絡，複製評量不列入本次新增需求。
- 本次僅文件更新及文字一致性核對，未修改程式／SQL／資料或部署，未執行測試或模型推論，未提交或推送。

## 2026-11-03：採用平台管理權與課程角色分離（規格）

- 使用者接受上一輪建議並要求寫入規格。新增 docs/role-permissions-spec.md：平台管理員／一般成員、獨立建課資格、每課教師／助教／學生；一般成員可任各課角色，取消全域教授／助教／學生的搭配限制。管理員本身也不自帶建課資格；建課者成為本課擁有教師，撤銷建課權不移除既有擁有關係。
- 管理員只管理全域帳號／資格／設定與稽核，不自動取得所有課程教學內容；課程擁有者仍管理本課成員。混合 A 區全域設定與案例／答案端點需拆開授權。學生體驗、首頁與基本資料要求依課程情境判定，保留正式／練習與私稿隔離。
- 核對現行 PortalRoles、CoursesController 的平台角色搭配限制與 StudioAdminController 全域 Professor 保護。新增認證／claims、帳號欄位、獨立建課權、課程授權、前端導覽、QA／測試及遷移映射的影響與驗收；包含撤權生效、最後管理員保護及不自動擴權。
- 同步 agents／elearning、course-only-spec 與 course-hub，明確新規格優先於舊「教授兼管理員」政策，歷史保留。本次僅文件更新，未改程式／SQL／真實權限、未執行 migration／測試／服務重啟，未提交推送。

## 2026-11-03：依新規格完成課程獨立及平台權限分離

- 重新核對工作文件、程式、schema、Git 與即時 DB。初盤 LEARN-101 有 2 班、11 已分班成員＋1 未分班學生、1 原小組，無限定資源、同名組或重複有效組員；3 測驗、1 PDF 無提交，無待搬移學生成績。另一數學課保留。未讀私人答案或輸出憑證。
- 使用者兩次明確回答：拆成兩課；兩課各有獨立內容副本、未分班學生留一三五。以交易保留原課 ID 897f8770-8a65-49d7-9cac-904eb00dbe53／LEARN-101 為「個案分析課程－一三五」6 學生與原小組；新 f1e66c4e-7e87-4ca6-a46d-7431ac89e11c／LEARN-101-246 為「個案分析課程－二四六」6 學生。各 3 測驗27題、1 PDF、1獨立規準／級別、4成績來源與3資源；新ID／新檔案紀錄，bytes與名稱核對一致，下載計數各自獨立。保留原狀態、題目答案、配分／規準／權重，不自動重算或複製私人提交。班別時間與關係快照存系統稽核。
- 015 將原 Professor 能力保留為 Administrator＋can_create_courses=true，其餘原 TA／Student 為 Member＋false；13 原帳號與各課角色保持，逐帳號留遷移稽核。016 在映射完成且無限定資源／重名／重複組員才移除班別表及三表 section_id，組名改課內唯一，未改已套用 SQL。001–016 已套用並重跑略過。
- 全部班別／調班／限班範圍 API 與介面移除，舊端點404；query.section仍為頁籤參數。小組直接本課有效學生，每人每課最多一組；資源列表／預覽／下載／摘要以課程成員授權，無跨課分享。文件名稱與版本、預覽、下載計次、未存提醒保留。
- 平台角色僅管理員／一般成員，建課獨立授予；擁有者成員安排與教學權限不要求平台教授，新增 owner-only eligible-accounts 最小資料。平台權限修改帶舊值、409衝突與稽核，自行降管理權／最後管理員有保護。Cookie逐請求核對角色與建課資格，異動拒絕舊session，canteach依有效課程更新。
- A2／A5／A6與概況限管理員；案例與私密答案維護／發布查本課教師／助教，管理員不自帶跨課內容權。先驗權才載入案例，修正未發布 active_version_id null 造成的讀取例外。T端按課程角色；middleware解析課程／測驗／PDF／案例／歷程的目標course，以實際角色判定練習，混合角色帳號的正式學生課程不被全域學生體驗誤寫到practice。UI按課程情境顯示切換與成績／PDF提示，基本資料獨立於practice header。
- 最終後端168（PostgreSQL啟用、0略過）、前端45通過，Vite build／publish成功。最終5097非模型browser/API回歸共9項已通過（課程、權限、客觀／申論批改、PDF成績、練習、檔案預覽、文件名稱、配分、導覽）；最後課程情境UI微調後focused4再通過。新權限測試含無建課權管理員、有建課權一般成員、跨課混合角色正式測驗／PDF／案例、撤權舊session401／重登後403、過期變更409及未入課管理員拒讀／發布。檢視教師桌面及學生手機。
- 測試初次含舊角色fixture／舊UI斷言與案例null讀取錯誤，已修正重跑；部分批次受原15次／分鐘登入限制429，測試加入有界重試，未調低產品限制。沒有模型推論、沒有修改各模型profile或有效負載；未重跑S3模型品質套件，未宣稱真實班級驗收。
- QA helper按新結構清理，合成帳號／課程／檔案／提交／成績／案例與state已清理，5107停止；5097 PID40968（後續操作須核對）沿用p0-publish並保留本機設定，spec-release保存成品。未停止Visual Studio開發程序；舊登入需重登。未提交／推送Git。
- 同步agents／elearning及course-only、role-permissions、course-hub／sections／tools與README的現行狀態，歷史脈絡保留。下次操作不得依舊「待實作」文字復原分班或全域教授權限。

- 最後補強課程頁籤／獨立評量頁的學生體驗情境更新，避免只切 query.section 後重新顯示不適用的切換；前端重新 build，5097 focused 權限＋課程2項再通過。再次清理新一輪QA/state並核對真實兩課、資源bytes與原角色映射仍正確；5097存活、5107無監聽。git diff --check無錯誤（僅既有CRLF/LF警告）。

## 2026-11-03：作業與測驗移除分析階段介面

- 依使用者要求，AssignmentEditor 移除分析階段選單與四階段快捷新增，AssignmentView 移除閱讀／作答頁階段標籤及不用的階段匯入／函式。既有 stageCode 仍保留於編輯負載，新題目沿用 null；無 DB、模型設定或有效負載修改。獨立案例學習／AI入口維持。
- ClientApp vitest 45項全通過；首次 Vite 命令誤用 --root，改在 ClientApp 執行 build 成功，輸出 wwwroot/portal。文字搜尋確認兩個介面不再引用階段呈現。未新增測試、未執行瀏覽器／後端／模型驗證。
- 即時5097未查到監聽；未啟動服務或更新發布目錄。同步規格、評量文件與交接；未提交／推送Git。

## 2026-11-04：本校跨修課學習歷程規格及11/15功能評估

- 依使用者要求先規劃、不開發，新增 docs/learning-history-spec.md。分享網址兩次擷取失敗，使用者隨後補貼完整參考文字；依其11類歷程與LMS亮點，轉為本校學生所有修課／跨學期情境，不沿用企業職務培訓或市場趨勢宣稱。
- 核對現有課程成員、測驗／PDF／案例提交、成績與教師課內歷程程式及SQL，現有T2僅案例來源，不等於全修課歷程。未連線即時DB或存取私人答案。
- 定義修課、進度、時數、影片、教材、測驗、作業、完課、證明、查詢與報表；評量提交率非完課率、下載非閱讀完成、頁面停留非實際學習時數。正式校務修課、學分與證書須可靠身分及校方資料／政策；未追蹤舊事件不造紀錄。
- 11/15建議A包為本人修課總覽／歷程、站內待辦、提交進度與基本儀表板，粗估4–5.5人日加1–2人日驗收；CSV視餘裕，教材開啟／公告另列條件候選。列明11/11功能凍結建議、驗收門檻與工作量不確定性，非教授核定承諾。AI助教／推薦／出題後期，既有客觀規則批改仍可展示。
- 保留學生本人跨課、教師限本課、管理員不自帶學生內容權及正式／練習／私稿隔離；補上退選／封存歷程、來源版本／去重、成績遮蔽、CSV稽核與後續完成規則設計。同步elearning／agents。
- 本輪僅文件與連結／內容核對，未改產品、設定、SQL或真實資料，未部署／啟停服務／執行產品測試或模型推論，未提交推送。

## 2026-11-04：新增線上測驗教材時間門檻規格

- 使用者要求先記規格：影片、講義與其他教材的閱讀／觀看時間可作線上測驗門檻。同步elearning、learning-history-spec、learning-assessments及agents。
- 記錄可選啟用、指定本課教材與最低有效累積時間、學生進度／尚差時間、未達標阻擋開始與寫入、達標仍遵守期限與權限。時間追蹤排除背景／閒置／重疊，不以下載當完成；保留本人／課程／版本依據及正式／練習隔離。
- 多教材逐份全達標列初版設計建議；倍速／重看、閒置門檻、發布前時數及教材改版沿用須實作前確認。發布固定門檻及開始時保存依據為規劃，不追溯使已開始／提交失效。
- 僅文件，尚未實作；依賴有效活動追蹤，未自動納入11/15。未改程式／SQL／資料／模型、未部署或執行產品測試，未提交推送。

## 2026-11-04：實作第一批學習歷程及測驗教材時間門檻

- 使用者要求「先照規格做下去」，依序完成先前A包及新教材門檻；本條取代同日前兩條僅文件的現行狀態，不把全部11類後續目標視為已交付。
- LearningHistory唯讀投影與本人API：有效正式學生課程（含封存）、跨課／學期卡片、評量提交進度／待評／原權重課內平均、待辦、類型／日期／分頁時間軸。整合最新測驗快照／公布成績、PDF提交／成績、案例正式提交、線下分數及教材累積時間；不讀答案鍵／私人草稿／附件bytes、不建立提交、不重複計來源成績，0分不當空值。教師／管理員無全修課繞過權。
- 新LearningTime及閱讀器：授權PDF／圖片／MP4／WebM（沿用20MB），人工開始、10秒心跳、伺服器經過時間上限、25秒斷點不計；文件前景且60秒內頁面互動，PDF內部事件不可靠，明示使用繼續閱讀；影片1x、跳轉不計、重看片段可累積，不聲稱影片不重複觀看比例。單人跨分頁／裝置／namespace單一有效計時，正式／練習累計分開。前景活動訊號仍依瀏覽器，不是監考或理解保證。
- 測驗編輯勾本課教材、各1–86400秒且全部達標；未開始伺服器不回題、Save拒寫。明確Start交易驗證原期限／封存／身分及門檻，保存教材／要求／累積時間證據；發布後不可變更，原開始依據保留。沿用同資源發布前有效紀錄，現行內容不可覆寫，檔案ID為內容版本、名稱版本不影響時間。
- 新017三表quiz_learning_requirements／learning_time_sessions／quiz_learning_starts，DB交易套用成功，001–017再跑略過；初次受限網路socket拒絕，經工具核准的資料庫連線完成，憑證未輸出。無真實資料搬移、無歷史時數回填、無成績重算、無模型profile／有效負載變更。
- 後端完整176通過、DB啟用0略過（新增8項含多教材、時間隔離／去重、偽造elapsed拒絕、截止仍拒、未公布遮蔽、分頁與不建立草稿）；前端既有45通過。Vite build與dotnet publish成功。
- 5107新門檻／歷程Chrome/API及真實PDF／WebM計時皆通過，既有assessments／autograde／practice3項通過。最終5097相同5項全通過，實測教師UI設定、學生計時及解鎖、提交後未公布遮蔽／公布0分、跨學期查詢、無權帳號拒教材、桌面與390px手機無橫向溢出；檢視歷程與兩閱讀器截圖。未執行其他全套browser/API、校方真實全校驗收或模型品質測試。
- 測試修正：初版學期select缺明確aria-label造成exact查找逾時，補標籤；重跑保留上一輪合成已公布評語導致測試跨輪干擾，改每輪唯一評語。測試呼叫ReleaseGrade參數順序修正後重跑通過。首建置需指定csproj，單次Vite工作目錄錯誤已更正，最終build無警告／錯誤。
- 部署learning-release至p0-publish，5097啟動PID31220、contentRoot專案根／webroot發布wwwroot，最終session HTTP200（下次須重新查程序）。原5097無回應，未停止使用者開發程序。QA helper補新表範圍／非QA guard，合成帳號／課程／教材／計時／門檻／開始／提交／分數及state清理成功；只停止本次命令列核對的5107 PID18380。
- 同步elearning、agents、learning-history-spec、learning-assessments與README。待後續：校務全修課身分／匯入、退選歷史、完整單元／完課證書、影片比例／續播、新跨修課匯出與公告／擴充教師報表；AI仍後期。未提交或推送Git。
