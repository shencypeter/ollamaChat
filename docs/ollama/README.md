# Ollama 模型設定與有效負載保存

更新：2026-10-22。目的：切換模型時保留各自的訊息組裝方式、抽樣參數與已知限制，不再覆寫上一個模型的經驗。

## 選擇模型

於專案根目錄，以啟動環境變數選擇設定，重新啟動服務後生效：

```powershell
$env:Ollama__Profile = 'llama3' # 或 qwen、breeze
dotnet run --launch-profile http
```

發布版本亦使用同一環境變數啟動 DLL。未指定時使用 `breeze`，不會在失敗時自動改用其他模型。啟動時依序套用基礎設定、所選 profile、`appsettings.Local.json`、環境變數；本地檔及環境中的 BaseUrl／Model 會覆寫 profile，切換前應檢查是否存在舊覆寫。Profile 的選擇在讀取本地檔前完成，請用環境變數 `Ollama__Profile` 選擇，不要只寫進本地檔。

設定保存在 `Configuration/Ollama/`，會隨建置及發布複製。切換設定不會更動資料庫、學生資料或全域臨床評分規準；請在新的對話中驗收，避免沿用舊模型一般對話內容。

| 設定 | 模型／端點 | 評閱訊息方式 | 驗證狀態 |
| --- | --- | --- | --- |
| `breeze` | `ycchen/breeze-7b-instruct-v1_0`，`192.168.1.195:11434` | 一個 system，user 前附本次格式要求 | 少量單份即時請求格式成功；評分不可靠 |
| `llama3` | `llama3.1:8b`，`IAN02:11434` | 語言、階段規準、Schema 各為 system；user 為 JSON | 舊 v2 有歷史成功及逾時；目前 v3 組合尚待即時驗收 |
| `qwen` | `qwen3:8b`，`IAN02:11434` | 保留早期多 system 方式；user 為 JSON | 模型名稱見既有程式及測試；目前服務可用性、Schema 及 v3 評分未驗證 |

三者共同保留 JSON Schema、繁體中文輸入、嚴格後端解析、案例存在性限制及相同規準；不復原受污染的答案鍵。共同抽樣為 temperature=0、seed=42、top_p=0.9、top_k=40、num_ctx=16384、num_predict=4096。這是可重現設定，不保證不同硬體／Ollama 版本完全決定性。

## 保存的有效負載

### 網站上的唯讀檢視

2026-10-27 新增教授／助教專用檢視：於 AI 入口送出後展開「系統詞與實際請求（唯讀）」。從與 HTTP 傳輸相同的序列化本文取得內容，顯示系統詞、脈絡、參數與 Schema；並在每筆請求下直接顯示對應 HTTP 狀態碼及 Ollama 原始 JSON 回應。錯誤回應仍可檢視。記憶摘要請求會另外列出，避免將摘要指令誤認為主要評閱指令。

此處不是新建全站日誌入口。只有目前登入教授／助教自己的請求可隨該次 `/api/ai/send` 回傳；學生沒有 `requestInspection` 欄位。內容保留於請求 scope 與頁面記憶體，重新載入即清除，不入對話歷史；請求與回應 JSON 中所有 `thinking` 欄位均由後端遞迴移除，端點亦移除可能的憑證與查詢字串。非 JSON 回應不傳送未清理本文。開發用去識別診斷仍依原開關控制。

畫面目前不能編輯指令。JSON 是應用層實際請求，不能當作 Ollama 套用內部模板後的 token 紀錄。

### 離線合成範本

`payloads/{profile}/` 為實際 `OllamaClient` 組裝並序列化的請求快照，以攔截 HTTP 的方式離線產生。每個設定有四階段 × 單份／比較 × 有／無案例，共 16 份。包裝欄位記錄 profile、提示版本、端點及 `liveVerified=false`；`payload` 是送到 `/api/chat` 的 JSON 本文。全部使用合成內容，不含預期分數或真實學生資料。

這些是目前版本的可重建請求範本，**不是三個模型都成功執行過的原始歷史封包**。評分規準、中文輸入與案例 Schema 已在後續修正，故 Llama／Qwen 範本也需重新驗收。歷史證據與限制見 `.ai/history.md`，不可將早期測試結果挪用到新組合。

重新匯出（從根目錄執行；只攔截請求，不呼叫模型）：

```powershell
dotnet run --project tests/AssessmentEvaluation -- --profile breeze --export-payloads
dotnet run --project tests/AssessmentEvaluation -- --profile llama3 --export-payloads
dotnet run --project tests/AssessmentEvaluation -- --profile qwen --export-payloads
```

變更 prompt 或 profile 時先保留 Git 差異，再匯出更新範本並檢查差異。不得將含真實作答的評估結果複製進此目錄。

## 即時回歸與切換驗收

```powershell
dotnet run --project tests/AssessmentEvaluation -- --profile llama3 --repeats 2
```

這個命令真的發送合成評估作答；`--profile` 將相同 profile 套用到評估工具，`--base-url`、`--model` 可明確覆寫。預設結果位於被 Git 忽略的 `tests/AssessmentEvaluation/results/`，保留完整請求、模型回覆、提示版本／雜湊、抽樣參數、模型摘要及伺服器版本（可取得時）。預期分數不送入模型，且須待專業人工審核。

切換後檢查 `/api/tags` 與 `/api/show` 的實際模型名稱、digest、template、parameters，以及單份／比較、有／無案例、全部四階段。另評估重複與改寫一致性、證據準確性及回饋分數一致性。通過格式不表示通過教學評分驗收。

若有推理模型，私人 thinking 不得顯示於教學介面；目前沒有未經驗證地加入 Qwen `think` 開關或模板控制字。新增時須以實際 API／模型驗證並建立回歸測試。

## 已知模板與歷史識別

- Breeze 在 2026-10-22 的 `/api/show` 模板為 ` {{ .System }}  [INST] {{ .Prompt }} [/INST] {{ .Response }}`；目前由 Ollama 套用模板，應用程式不自行插入 `[INST]` token。
- 早期 Llama digest 記錄為 `46e0c10c039e019119339687c3c1757cc81b9da49709a3b3924863ba87ca666e`，服務版本為 0.40.4；不保證現時仍相同。
- Qwen 沒有完整可核實的歷史封包與 digest，不補造成功證據；先以保存設定重建，再測試。

## 本次軟體驗證

後端 76 項測試通過，含三個設定的實際序列化回歸；48 份合成有效負載匯出成功。此輪未向三個模型送出即時推論，也未改變目前使用中的 Breeze 模型。

## 唯讀檢視的回歸測試

後端測試驗證請求與傳輸本文一致、原始回應配對、角色限制、請求 scope 隔離、請求／回應 thinking 與端點憑證排除、非 JSON 回應不外洩及未授權欄位省略；前端元件測試驗證唯讀、文字轉義與原始回應呈現。

整合測試使用合成模型，避免為測試產生實際模型推論：

1. 依現有 helper 準備合成 QA 帳號。
2. 在 ClientApp 執行 `node tests/fixtures/ollama-inspection-stub.cjs`（只聽 127.0.0.1:11535）。
3. 在獨立 API 程序設定 `Ollama__BaseUrl=http://127.0.0.1:11535`、`Ollama__Model=inspection-test`，啟動於 5107；不得將此設定寫入共用 profile 或正在使用的預覽。
4. 在 ClientApp 設定 `PORTAL_TEST_URL=http://127.0.0.1:5107`、`OLLAMA_INSPECTION_STUB=1` 後執行 `npm run test:e2e -- request-inspection.spec.js`。該測試未設旗標時略過，禁止對真實模型使用此合成測試模式。
5. 結束兩個測試程序，依 scoped helper 清理合成帳號。

## 2026-10-28：作業私人形成性請求

作業學生端另以 assignment-scoped ai-feedback API 從伺服器組裝當前題目、階段、參考資料與精確已存答案 revision，直接用 AssessAsync；不經 LangChain memory，也不帶其他答案或先前回饋。三個 profile 的 payload 相容性設定保持原樣。

形成性提示版本為 rubric-evidence-v3-formative-v1，沿用既有 JSON Schema／嚴格解析。只有解析後 Assessment 與最小模型／版本／hash 資訊存入 evaluation_runs；不保存完整請求、raw response、thinking 或端點。學生回應不含 staff 檢視資料；獨立入口的教授／助教完整請求、HTTP 狀態與安全化原始 JSON 回應仍保持 request/page scoped。

推論前短交易記錄 pending 後釋放鎖；推論完成標記 completed／failed，無法核對目前 revision 時顯示舊版回饋，不改套新答案。失敗不阻擋正式提交。完整政策與合成測試見 [形成性文件](../formative-feedback.md)。

## 2026-10-29：教師／助教學生體驗

教師與助教可切換學生視角，使用本人隔離的 practice 草稿、形成性回饋與提交，不變更帳號／課程角色，不計正式提交或成績。005 遷移新增 submissions.is_practice；原學生流程保留，staff 仍不能讀正式學生私人回饋。一般對話記憶另分 namespace，practice 不擷取或顯示原始 Ollama 診斷。完整政策、限制及驗證見學生體驗文件。

[學生體驗文件](../student-experience.md)

## 2026-10-29：新案例純文字回饋

新 S3 使用 StudioFeedbackContract（case-text-feedback-v1）與 FeedbackAsync，schema 僅 summary、supportedPoints、improvements、nextSteps，不含 score，未知欄位嚴格拒絕；不走既有 assessment 的分數契約。案例版本、本人已存單階段答案及私密教師清單由伺服器組裝，不帶一般對話記憶。管理模板是版本化風格，固定證據、文字／不代寫規則保留。只存解析內容與版本／模型 metadata，不存 thinking、raw response 或完整封包。

三個 profile 設定檔未修改，文字請求仍沿各 profile 的 system 合併／近端格式提醒／sampling；原 assessment 契約及既有 48 份匯出不變。新增三 profile 實際序列化測試已通過；新 S3 完整瀏覽器只用 loopback 合成模型驗證逾時／重試，沒有真實模型內容可靠度驗收。新測驗與 PDF 作業使用人工成績，不走此模型路徑。操作見 [案例站台](../case-studio.md)。
