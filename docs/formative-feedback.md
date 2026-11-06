> 2026-10-29 最新更新：新 S3 使用獨立 studio 版本與純文字回饋，原作業 ai-feedback API 仍 403；不是復原本文的 1–4 分形成性流程。現行政策見 [案例站台](case-studio.md)。

> 2026-10-29 政策更新：作業 AI 評分與私人回饋全部暫停，相關 API 回傳 403；獨立 AI 入口保留。本文中 AI 作業流程為先前實作紀錄，現行範圍見 [課程工具與考試](course-tools.md)。

# 學生提交前 AI 形成性回饋

更新：2026-10-28。正式成績仍由教授／助教執行人工評閱。JSON 合法與功能測試通過，不代表模型的教學或臨床判斷準確。

## 學生流程

1. 在已發布、開放且未截止的作業中作答。
2. 按「儲存草稿」。題目有未儲存修改時，「請 AI 提供回饋」停用，畫面提醒先存草稿，不會暗中改評舊內容。
3. 於該題按「請 AI 提供回饋」。後端驗證整份草稿版本及指定逐題版本，獨立評閱這個已儲存答案。
4. 回饋就近顯示證據發現、推理品質及 1–4 級練習參考分數，標示私人、非正式成績且可能出錯，附答案版本識別、序號、模型及提示版本。
5. 修改文字立即標示舊回饋過期；儲存新答案後也以新的 revision ID 判定過期。可重新要求回饋。
6. 按「提交給教師／助教」。未有成功回饋或回饋過期時，只有確認提示，不阻擋提交。維持既有提交快照及人工評分／發布流程。

AI 操作不會鎖住編輯器、儲存或提交。推論期間可修改；回傳仍綁定原 revision，不能悄悄改套新文字。

## 適用範圍與題目設定

- 目前僅適用**首次正式提交前**的私人草稿。P1 已提交後的修訂仍須直接提交新快照，尚無私人「提交後修訂草稿」；本次不改動此政策。
- 題目須設定四個分析階段之一。未設定時明確停用，不預設病前功能，也不轉成一般對話。
- 教師的「作業說明與案例資料」是目前唯一混合參考欄位，傳入 `caseFacts`；空白時為 null。指令明確要求只有個案事實可作證據，教學指示與題目本身不可當作個案表現。未新增獨立案例欄位或以模型判斷來猜測是否有案例。
- 人工規準屬於整份作業，目前沒有分配到單題的規則，因此不硬套到逐題練習，也不建立假 rubric。使用既有證據優先的 1–4 級形成性契約，不能換算為正式作業滿分。

## API

`POST /api/assignments/{assignmentId}/questions/{questionId}/ai-feedback`

```json
{
  "expectedVersion": 2,
  "answerRevisionId": "00000000-0000-0000-0000-000000000001"
}
```

以上 UUID 只是格式說明，必須使用自己的實際已存版本。請求不接受使用者、案例、題目、階段或規準覆寫；額外欄位由嚴格 DTO 拒絕。

`GET` 同一路徑回傳本人該題最新 20 筆形成性請求，依建立時間由新至舊排序。畫面目前顯示最新一筆；不是完整歷史瀏覽器。關閉或截止後仍可讀取自己既有回饋，但不能要求新回饋。

回應包含 evaluation ID、question ID、answer revision ID／序號／SHA-256、狀態、時間、模型、profile、提示版本、解析後 Assessment、`isStale` 與安全錯誤訊息。沒有 requestInspection、原始模型本文、thinking 或端點 URL。

- 400：資料格式、空白答案或未設定分析階段等問題。
- 403：非平台學生、非有效課程學生，或教師／助教存取私人端點。
- 404：不可見作業或不屬於本作業的題目。
- 409：草稿版本衝突、尚未儲存、已正式提交、未開放／截止／關閉／封存。
- 已建立的 AI 請求以 200 回傳 `completed` 或 `failed`；模型失敗是練習結果狀態，不是作答儲存失敗。

## 後端與並行處理

`AssignmentWorkflow` 的形成性 partial 在短交易內沿用作業列鎖，從登入者身分載入自己的提交，驗證 `expectedVersion` 與 `current_revision_id`，建立 pending 評閱後即提交交易、釋放連線。

隨後直接呼叫 `IOllamaClient.AssessAsync`，只有本階段系統指令及本題 JSON；不使用 `IMemoryOrchestrator`、一般對話、歷史作答、其他題目答案或其他學生資料。JSON 包含伺服器取得的 assignment ID、question ID、題目文字、階段、答案版本與原始答案。

繼續沿用 Breeze／Llama3／Qwen 各自的訊息配置、抽樣、JSON Schema 與嚴格解析器。形成性提示版本為 `rubric-evidence-v3-formative-v1`，未更動獨立入口的提示版本或記憶策略。

推論完成後只更新自己的 run。另一分頁儲存新版本，不會被 AI 寫回覆蓋；查詢時用 revision ID 比對目前 pointer，回傳是否過期。前端另外檢查未存文字，立即顯示過期狀態。

## 持久化與資料保留

`Database/004_formative_feedback.sql` 已套用，以現有 `evaluation_runs` 保存形成性評閱：

- 允許 `rubric_id` 為 null，沒有假外鍵或另一套評閱表。
- 新增 purpose（舊資料為 legacy）、requesting user、model profile、answer hash 及形成性識別約束／索引。
- 經 `answer_revision → submission_answer → submission` 關聯可歸屬題目、作業與擁有學生。
- model_parameters 保存抽樣設定、訊息合併／格式重申開關及輸入 context hash；不保存憑證或端點。
- findings 只保存嚴格解析後的公開評閱結構及練習 score；不寫 grades、不發布人工成績、不建立提交快照。
- 不保存 raw response、thinking 或完整 wire payload；原始答案沿用既有 answer revision，避免重複另存。
- 失敗保存安全分類訊息與完成時間，原始例外細節不回傳學生。

沒有新增自動清除政策；評閱隨現有作答保留，正式個資保留／刪除期限仍待產品決定。已更新 scoped QA 清理順序以先刪 evaluation_runs，再處理答案版本。

遷移方式沿用根目錄的 `--migrate`，不重設資料庫；不得修改已套用的 004，後續結構調整需新增遷移。

## 隱私與失敗處理

平台 Student 角色及課程 student 成員都必須符合；擁有者一律取自驗證 Cookie，不接受瀏覽器提供的身分。其他學生的查詢沒有別人的紀錄；錯誤 revision 不會拿去推論。教授／助教即使可人工評分，也不能呼叫私人形成性端點。

獨立 AI 入口的教授／助教 request-scoped 檢視維持原有請求、HTTP 狀態、遞迴排除 thinking 的原始 JSON 回應功能；本次學生端不回傳也不呈現該資料。

逾時、傳輸錯誤或解析失敗只將 run 標為 failed；儲存好的答案不變。即使瀏覽器中止，後端會以獨立短期限嘗試結束 run。若程序崩潰或資料庫不可用，可能留 pending；不加阻擋重試的唯一 pending 限制，因此仍可重試或直接提交。未新增背景恢復工作。

## 實際驗證

2026-10-28：

- `dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj -p:UseAppHost=false -p:OutputPath=...`：**108 項通過**。執行時以程序環境 `FORMATIVE_TEST_CONNECTION` 啟用其中 7 項真實 PostgreSQL 整合測試；未提供時這 7 項明確略過。所有模型呼叫以合成 IOllamaClient 代替。
- `npm test`：**18 項通過**，包含獨立動作、未存文字、版本過期、可選提交確認、載入／逾時／失敗、非正式標記與不顯示 staff diagnostics。
- `npm run build`：成功。
- 既有 loopback Ollama fixture 的 focused 瀏覽器／API：形成性流程及 staff 檢視 **2 項通過**，使用真實登入與 PostgreSQL。驗證模型故障後仍可提交不可改寫快照，沒有正式成績。
- 原有 P0／P1／獨立入口 browser/API：**6 項通過**，包含記憶模式與主題介面回歸。
- `004` 套用交易已提交；未重設真實帳號或課程。第一輪測試的 cleanup 多命令參數化錯誤已修正，留下的 8 個合成 fixture 課程在嚴格核對身分及無真實成員後清理；後續 .NET 整合測試正常清理。
- 本次未向真實模型送出測試，不宣稱模型評分準確度提升。PortalSmoke 建置仍有 NU1900 弱點資訊來源不可達警告，建置成功。

Focused 瀏覽器重跑：依 `docs/ollama/README.md` 啟動 loopback fixture 與隔離 API，設定 `PORTAL_TEST_URL`、`OLLAMA_INSPECTION_STUB=1`，在 ClientApp 執行 `npm run test:e2e -- formative.spec.js request-inspection.spec.js`；結束後清理合成資料。不要將測試端點寫入正式 profile。

實際成功執行的完整命令（連線資訊只透過程序環境供應，以下不記錄其值）：

```powershell
# 專案根目錄；執行時已提供 FORMATIVE_TEST_CONNECTION
dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj -p:UseAppHost=false -p:OutputPath=D:/AI_TeachingAssistant/.codex-build/formative-tests/ --logger 'console;verbosity=normal'
dotnet publish AiTeachingAssistant.csproj -p:UseAppHost=false -p:SkipFrontendBuild=true -o .codex-build/formative-publish --no-restore --verbosity quiet
dotnet .codex-build/formative-publish/AiTeachingAssistant.dll --migrate

# 工作目錄 ClientApp
npm test
npm run build
# PORTAL_TEST_URL=http://127.0.0.1:5107，OLLAMA_INSPECTION_STUB=1
npm run test:e2e -- formative.spec.js request-inspection.spec.js
# 同一隔離 API，原 P0/P1 回歸
npm run test:e2e -- assignments.spec.js teaching.spec.js portal.spec.js
```

最終發布已更新至 5097；測試 API／模型 fixture 已停止，合成 QA 狀態檔已移除。瀏覽器整合測試針對隔離 API 與同一版已建置資源執行，未另對最終 5097 服務發送模型測試請求。

## 2026-10-29：教師／助教學生體驗

教師與助教可切換學生視角，使用本人隔離的 practice 草稿、形成性回饋與提交，不變更帳號／課程角色，不計正式提交或成績。005 遷移新增 submissions.is_practice；原學生流程保留，staff 仍不能讀正式學生私人回饋。一般對話記憶另分 namespace，practice 不擷取或顯示原始 Ollama 診斷。完整政策、限制及驗證見學生體驗文件。

[學生體驗文件](student-experience.md)
