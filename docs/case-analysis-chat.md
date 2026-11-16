# 課程個案分析五模式：交付與維護

> 後續更新：038已加入回饋輪次sidebar、原始作答修訂關聯／次數、任兩版學生原文diff及逐回覆效能dialog。舊量測維持NULL，Raw仍不持久化。現行操作與新增驗證見 [回饋輪次交付](feedback-rounds.md)，下列037為前次實作紀錄。

更新：2026-11-16；開發站 https://localhost:7173。只以去識別化合成內容測試，不代表臨床資料／IRB／教學品質認證。

## 1. Git來源與實際重用

舊分支`codex/pre-vue-ollama`，commit **66b67953e070e2c45244b7c176566703c444f1fd**。`b4da694`將一般聊天與單次TA評閱分開，`75c58af`含原始四階段內容。舊版及現版均使用 **LangChain.Core 0.17.0**，沒有升級套件。

重用`ChatMessageHistory`、`ConversationBufferMemory`與Human/Ai角色保留方式，不使用0.17.0不相容的`WithHistory()`。舊Buffer其實被SessionStore截成40則，Window只保留近期、Summary為應用自寫摘要且加system；因此Raw沒有直接沿用該store／HTTP Session／summary。現有資料存取為Npgsql＋SQL Migration，並無EF Core，本次不另外引入ORM。

## 2. 五模式與Provider

| 模式 | Memory／Prompt | DB／正式統計 |
| --- | --- | --- |
| 純AI聊天（無教學提示詞） | 每user/course/randomID各自ConversationBufferMemory；完整成功user/assistant順序，無應用system | 不寫DB、不計入 |
| 病前功能 | 本人本課該階段DB歷程＋專用system | 持久化、計入 |
| 疾病病程 | 同上，階段互不混用 | 同上 |
| 功能評估結果 | 同上 | 同上 |
| 心理社會條件 | 同上 | 同上 |

新增透明`IOllamaClient.ConversationAsync`，共用原`SendAsync`HTTP transport。Raw與Stage同模型、同`AssessmentSampling`（temperature、seed、top_p、top_k、num_ctx、num_predict），不修改三份profile或既有Assessment／Feedback路徑。新路徑不加全域system／語言限制、不加schema或格式重申、不執行summary、不捕捉diagnostics／inspection，thinking及敏感exception內容不外傳。Stage明確傳server system；Raw只傳user/assistant。Raw可能不使用繁體中文，符合baseline用途。

`CaseChatPrompts`從66b6795無分數教學模板抽離，版本 **legacy-66b6795-chat-v1**。保留各階段分析範圍、證據／推論、不補造案例／不過度診斷；修正舊校名、舊stateless-only限制與通過／解鎖措辭，以支援同session多輪。這是明示適配，非宣稱舊提示已獲客戶核定。原`AnalysisStageInstructions`評分契約不改。

## 3. Raw記憶生命週期與隱私

- 後端`RawChatSessions`是process-local registry，**每個entry各有一個LangChain Memory**，沒有跨人共用單一memory。查找同時比對登入user、course、random ID；猜他人ID404。
- 前端只送本次content及session route ID，不接受前端角色、system、歷史或學生ID。成功human/assistant一次加入buffer；失敗草稿留前端，不製造假assistant。
- 前端內容只在Vue記憶體；不寫localStorage、sessionStorage、IndexedDB、cookie或後端HTTP Session。Raw沒有歷史GET恢復API，也不建立CaseAnalysisSession／Message、持久summary、cache、教師統計或應用內容log。
- 新頁／重新整理／切回Raw建立新ID。清除／離開取消inflight、清空前端及server Memory，晚回覆不可補回；模式切換、路由、關頁有未保存提醒。BFcache返回不恢復Raw，四階段則重讀DB。
- `pagehide`同步清本地並用keepalive DELETE嘗試釋放；**關頁刪除不能保證送達**。斷線／強制關閉時依20分鐘閒置回收（每分鐘掃描），服務重啟亦清空。
- 預設每人3個／全站200個context。新配置429時提供明確確認的「清除本帳號的暫存聊天並重新開始」，只清本人所有課程／分頁，不清他人，亦不自動中斷其他分頁。
- Raw推論完成前再次驗證帳號、修課、課程狀態與開關；中途退選、停權、封存／停用即拒絕晚回覆並移除記憶。本人清除可在退選後執行。

## 4. History與模型Context上限

原始Conversation History與當次LLM Context分開看待。預設單則8,000字、回覆16,000字，context12,000 UTF-8 bytes＋role開銷，並扣除既有模型輸出token預算。這是**保守估計，不是精確tokenizer／模型容量保證**。

超限明確413，原history保留，不偷偷window、摘要或截斷。Raw可清除另聊；Stage可開始新session，舊原文仍可查。保留原文不表示任何長度都可一次送入模型。不新增向量DB或長期記憶。

## 5. 資料庫與統計

**037_case_analysis_chat.sql**新增：

- `courses.case_analysis_enabled`預設false，既有課opt-in。
- `case_analysis_sessions`：課程／學生／stage／practice、active/closed、開始／活動／完成時間與sequence。相同組合僅一active。
- `case_analysis_messages`：原始content（含前後空白）、role、sequence、狀態、replyTo、modelName、promptVersion、attempt token與lease。Trigger保護內容／身分／版本不可變；assistant只可連同session user，replyTo／sequence唯一。System不作學生可見訊息保存。

學生提問先短交易保存pending，**不持有交易呼叫LLM**；成功後再短交易重查授權、保存assistant。失敗仍保留原user與通用failure code，不存上游error body。Lease過期可重試，attempt token拒絕舊慢回覆；並行送出409，同requestId／成功retry不重算。只允許最後一則未完成提問重試，否則可開始新session。重新開始只關閉舊session，無聊天刪除API。課程開關先鎖exclusive row，避免並行SHARE升級死鎖。

統計可由DB重新計算：學生訊息＝正式user數（含AI失敗），AI成功回覆＝已保存assistant數，完整往返＝有唯一assistant且user completed的提問數。失敗只增加提問，重試成功只算一次往返。各階段、全課合計、最後互動時間均可查。Raw、教師／助教practice完全排除，沒有互動次數換成績。

## 6. 師生操作與授權

學生：課程 → 左側「個案分析 AI 助教」→ Raw或四階段。階段重新整理／登入可恢復；Raw不恢復。教師：同入口可本人試聊（practice明示），點「學生紀錄與統計」查看全名冊（含0次）、分階段／全課合計，再選學員、stage、session讀完整對話。歷史退選學生仍列教師查閱；學生退選後不可存取。封存課程只讀。

TA必須具本課statistics能力才可看正式紀錄。課程擁有者在課程設定啟用／停用，停用不刪歷程。學生不能指定他人ID讀取。所有API在`/api/courses/{course}/case-analysis`，含configuration、settings、stage history/send/retry/restart、Raw create/send/delete、教師records；沿用登入／CSRF，配置／送出／重試每帳號20次/分鐘、body64KB及字數限制。UI隱藏不是授權。

## 7. 現有課程與部署

Public已交易套用037，001–037完整。沿用兩門既有「個案分析課程A」及「個案分析課程 B」，沒有建立平行課程。兩課`115上`等義正規化`115-1`、啟用模組並留audit；IDs、名冊、教材、作答及成績不變。未種假聊天或正式互動次數。

Development工具在`tests/PortalSmoke`：首參數專案root，再用`--case-chat-demo-preview`／`--case-chat-demo`／`--case-chat-demo-audit`，搭配既有`ConnectionStrings__Portal`與Development環境。Preview不寫入；本次apply2課，audit0待改，再跑0修改。只有完全沒有個案課時，才從既有教師教學群體建立可重入示範課。

## 8. 驗證

- Vue **57檔208/208**；Chrome **12/12**，其中5個case chat流程涵蓋1440／390、Raw三輪、刷新新ID、BFcache、卸載遺失／429明確恢復、教師查閱／設定；另7個既有教材、首頁、作業評分／全班發布流程。
- 後端一般 **280通過／45條件式DB略過**；另啟用隔離PostgreSQL後完整 **325/325、0略過**。包含真LangChain48則buffer、角色、隔離、清除、expiry、Raw實際controller不建DB／統計、Stage失敗／去重／lease／撤權／並行設定及既有作業測驗。
- 真正036舊課／名冊／已發布83分fixture升級037及重跑，protected fingerprint不變；課程啟用／等義學期轉換重跑亦保留名冊／成績。所有group_test schema已刪。
- 三profile wire tests驗證Raw無應用system/schema、Stage明確system、參數相同、診斷啟用仍不擷取、thinking／敏感錯誤不外傳。原有assessment／feedback測試保留。
- 真實Ollama合成傳輸：**兩輪Raw（4則記憶）＋一輪Stage成功**，沒有DB寫入、內容不輸出。這不是模型教學品質／Prompt效果驗收。
- 標準Visual Studio MSBuild含Vite成功。唯一7173正常bin/Debug、頁面200、新bundle、11組case-analysis路徑，未登入401。未跑所有歷史E2E；專案無獨立TypeScript設定，Vue由Vite編譯。
- 全DB回歸最初發現兩個舊CourseUnitTests沒有採HTTP Web JSON大小寫，修正fixture後全過；升級fixture缺必填weight亦已補。審查發現的中途撤權、刷新孤兒記憶、空白重試、並行開關死鎖均有重現失敗再修正的回歸。

## 9. Ollama與基礎設施限制

唯讀`/api/show`、`/api/version`實查：目前Breeze `ycchen/breeze-7b-instruct-v1_0`，Ollama **0.40.11**，模型有非空System、template引用System與model parameters。**應用不注入System不代表模型沒有預設指令**。[官方API](https://github.com/ollama/ollama/blob/main/docs/api.md)、[官方Modelfile](https://github.com/ollama/ollama/blob/main/docs/modelfile.mdx)說明messages、system、template與context參數。

沒有遠端主機／代理管理權限，無法確認其OLLAMA_DEBUG、body logging、APM、備份／swap／crash dump政策，不宣稱全部基礎設施不可能留資料。應用未新增request-body或Npgsql敏感參數logging。Raw是單process記憶，多節點需另決定路由／生命週期，不偷偷用Redis／DB持久化。

正式案例、Highlight、評估重點／規準、回饋語氣深度、通過條件及PHI/IRB治理仍待客戶提供；未新增自動分數、過關、排名或品質量測報告。

## 10. 主要修改檔案

- `Services/CaseAnalysisChat*.cs`、`CaseChatPrompts.cs`：持久對話、重試、統計與舊prompt適配。
- `Services/RawChatSessions.cs`、`CaseChatBudget.cs`、`CaseChatException.cs`與`Options/CaseChatOptions.cs`：暫存LangChain與限制。
- `Services/OllamaClient.cs`：單一transport的透明ConversationAsync。
- `Controllers/CaseAnalysisChatController.cs`、`Models/CaseAnalysisChatModels.cs`／`RawChatModels.cs`、`Program.cs`：API、DI、角色／速率。
- `Database/037_case_analysis_chat.sql`、`CoursesController.cs`／`PortalModels.cs`：課程開關、相容模型與安全刪除。
- Vue `CaseAnalysisChat.vue`、`CourseDetailView.vue`、`courseNavigation.js`及對應單元／E2E測試。
- `tests/PortalSmoke/CaseChatUpgrade.cs`／`CaseAnalysisDemo.cs`／`CaseChatLiveCheck.cs`：升級、啟用與合成真實傳輸工具；README、Ollama與.ai交接文件。
