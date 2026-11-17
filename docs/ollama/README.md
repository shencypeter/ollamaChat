# Ollama 模型設定與有效負載保存

## 2026-11-17 中文預檢與教師錯誤診斷

修正將UTF8bytes當token而擋下2125中文字的問題：獨立bytecap與token估計，只有Omni profile採24000bytes、CjkContextTokensPerCharacter=2及ContextTokenOverhead=512；其他三份不改，預設3／16保留原行為，仍預留實際輸出。這不是精確tokenizer，原文不裁字、Stage不加歷史。真正Provider合成五段2125字完整送達、2messages、prompt4031／response567／stop。Bv4及模型／抽樣未改；本節取代下方較早Omni預檢仍12KB的現行效力。

本人課程teacher／TA當次失敗可看固定原因／HTTP狀態並開實際失敗請求；控制器完成後重新授權，學生POV／學生／降權不提供。任意服務本文、內層例外及thinking不回傳；已知模型未安裝／記憶體／context／逾時只映射固定文字，未知明說未知。資料只留頁面，不寫DB／log／storage，舊失敗不重建。[請求dialog](request-dialog.md)及[五模式說明](../case-analysis-chat.md)。

另有使用者長文回報重複，僅讀metadata prompt4015／response1762／stop、feedback600字／有效ann5：不是容量錯誤，合法JSON不代表可靠，未擅自調模型／分段或讀出病例。Vue288、Chrome30、隔離DB394與正常VS含Vue通過，7173已更新。

## 2026-11-17 B提示v4與生成順序

課內正式B保留完整教授system，再加層級／時點對照，user提醒取消強迫修改；format.properties及required先annotations後feedback，仍單次api/chat。四份profile、抽樣、Raw及其他階段不改，未採用實驗的ChatML／generate／兩次呼叫。B的請求彈窗及DB版本為professor-four-phase-20261117-b-v4，A/C/D仍v3，Studio不變。舊B失敗訊息須另開本階段再自行提交，不將新提示冒稱舊版重試。

system7704 UTF-8 bytes，預檢仍12KB，未增加context或截斷學生文字。385項隔離DB回歸及升級指紋通過；11次最終合成請求完整JSON，不等於11次正確分類。細碼及混合時點仍有錯，詳[模型評估](../ai-tuning/03-evaluation-results.md)。

## 2026-11-17 輸入長度實測

已比較Taipei Omni／Breeze與IAN02 Llama3的實際載入容量、prompt token量及首／中／尾合成標記。Omni可在16384設定讀10079 tokens，沒有固定卡4096；刻意溢位時兩台均有前文截斷跡象。正式Stage合成2375輸入tokens仍有錯碼，不能把分類失準全部歸因截斷。未改設定／Prompt／模板或重啟，詳[容量診斷與限制](context-size-check.md)。

## 2026-11-17 Omni正式階段獨立輸出預算（現行）

修正qwen25-omni把Raw的512上限直接套到正式JSON的問題：Raw及原AssessmentSampling仍512，僅CaseFeedback以CaseFeedbackMaxTokens=4096、CompactCaseFeedback=true送出。schema上限收斂為feedback600、annotations8、quote160、explanation80；其餘三模型及原預設schema不變。sampling使用複本，context budget亦扣實際Stage輸出預算，不會污染下一次Raw呼叫。實際payload dialog可看到不同options／format。

教學System Prompt與v3原文未改，未修補原文引用或補造分類；舊同model／prompt失敗作答仍可同ID重試。正式回覆length會明確報輸出上限（包含content空白的情況），不保存不完整assistant；Raw length行為維持。failure_code仍provider_failed，無Migration。

HTTP mock與隔離DB383項全部通過、schema已刪；實際Provider合成長文約7.8s完整JSON／stop，Raw多輪與短Stage可用。模型仍有過度分類等品質問題。沒有讀出或重送使用者真實作答，原失敗封包未留存，歷史原因僅能結合metadata與合成重現判讀。7173已更新且仍Taipei指定Omni。

## 2026-11-17 Taipei Qwen2.5 Omni（現行設定）

使用者指定`rockn/Qwen2.5-Omni-7B-Q4_K_M:latest`，新增獨立`qwen25-omni` profile，端點沿Taipei192.168.1.195:11434。預設與7173已切換，VS選項`https-Taipei-Qwen25Omni`；原`https-IAN02-Qwen3`、`https-IAN02-Llama3`、`https-Taipei-Breeze`及三份profile完全保留，全部只用7173。支援名稱集中於OllamaOptions.ProfileNames，主程式及實驗工具共用白名單，不開任意檔案路徑。

實際模型show為qwen2vl／Q4_K_M，模板只有System＋Prompt，因此合併單system、未套Breeze的legacy user重申。新profile沿模型原生num_predict512（原模型仍4096），context16384及其餘共用抽樣不變；沒有改遠端模板、think flag或加入音訊／圖片流程。初沿4096時首Raw太長而次輪budget拒絕，512下2Raw＋短Stage JSON可用；較長B例仍以length截斷JSON、重複feedback，後端拒絕，品質未驗收。完整[觀察紀錄](../ai-tuning/03-evaluation-results.md)。

後端326通過／54外部DB略過，新增5個profile封包案例、evaluation helper及標準VS含Vue成功；7173 profile／精確model／頁面200／匿名AI401已核對。切換清Raw暫存，Stage歷史保留，沒有自動fallback。512與4096的延遲不可直接視為同條件速度比較。

## 2026-11-17 目前切換Qwen3比較（現行設定）

依使用者要求，預設Profile現qwen，使用既有IAN02 qwen3:8b。新增VS選項https-IAN02-Qwen3，與https-IAN02-Llama3／https-Taipei-Breeze均只用7173，停止目前服務後再選另一個F5。三份模型profile／sampling／think設定未修改，未採用Breeze實驗提示，沒有自動fallback。

IAN020.40.16及模型存在已核對；2輪Raw與1輪Stage JSON均成功，約51.2／4.5／46.5秒。另1則B病程合成例約71.1秒，有效引用標B1b／B1c，但影像／手術未標、feedback要求未提供細節，未通過教學品質驗收。相關封包13及標準VS含Vue通過，7173已核對profile=qwen、頁面200／匿名AI401。切換清Raw暫存、Stage保留，建議新開階段對話。實際合成觀察見[評估紀錄](../ai-tuning/03-evaluation-results.md)。

## 2026-11-17 使用者確認切回Llama3（現行設定）

在Breeze分類診斷後，使用者選「切回Llama3，保留Breeze供比較」。appsettings預設Profile現為llama3，IAN02的llama3.1:8b；Breeze／Qwen的各自設定及兩個VS7173選項完全保留，沒有自動fallback，也未部署四種Breeze實驗提示。切換前原IAN02名稱短暫解析失敗，重新解析／連線成功後，version／tags及合成2Raw＋1Stage均成功；沒有改用固定IP。

唯一7173已重啟且核對profile=llama3、頁面200／匿名AI401，標準VS含Vue建置成功。Raw暫存隨重啟清除、Stage歷史保留，建議切模型後新開階段對話。Llama3仍有混合內容過度分類，切回不代表已通過教學品質驗收。

## 2026-11-17 分類遵循診斷：格式成功不代表分類通過

Breeze已收到完整分類表及目前階段，但合成測試仍有複述規則、引用非原文被拒絕及錯選碼。只在本機測4種替代包裝（共12次，加原版3次），未找到足以採用的方案；精簡版雖較易產生有效引用，仍錯碼、回英文且刪掉原教學方法，沒有套用到正式Provider或profile。分類schema能約束合法碼與結構，不能保證選對語意。

Llama3當下HTTP比較亦有混合內容過度分類，不能當專業正確性保證。兩份模型設定均保留不改，不因模型不遵循而放寬原文驗證或補造標註，沒有自動fallback。完整[合成診斷表](../ai-tuning/03-evaluation-results.md)。診斷後使用者已確認切回Llama3，詳上方現行設定。

## 2026-11-17 Taipei恢復、預設切回Breeze（現行）

依使用者要求先實測：Taipei的version／tags成功，Ollama0.40.11及ycchen/breeze-7b-instruct-v1_0存在。既有Provider合成2輪Raw＋1輪結構化Stage成功（首次約4.3秒），之後以網站明確current-stage overload再驗3筆成功；不寫DB或輸出對話內容。只證實當下連線、推論及JSON格式，不代表臨床教學品質。

appsettings預設Profile由llama3切回breeze。三份Configuration/Ollama及VS的https-IAN02-Llama3／https-Taipei-Breeze保留原樣；兩者都只用7173，沒有自動failover。選原https即沿目前預設breeze；若要IAN02則明確選https-IAN02-Llama3。切換需重啟，Raw暫存清除，Stage歷程及原模型metadata保留。相關封包13項及標準VS含Vue成功，唯一7173已核對profile=breeze，頁面200／匿名AI401。

## 2026-11-17 C2顯示名稱與精簡Prompt名稱

共享分類C2的可見名稱更新為「Muscle Power（肌力與相關動作檢查）」。CaseCodingDefinition選用PromptLabel以JsonIgnore排除於API目錄，模型catalog渲染採PromptLabel或原Label；C2仍送既有精簡「Muscle Power」，不增加原prompt bytes或更動既有詳細規則、payload文字、v3及三個profile。避免單純UI標籤修改擠壓既有context預算。相關後端19項及標準VS含Vue通過，未呼叫實際LLM。

## 2026-11-17 四階段本輪輸入與直接請求入口

四階段send／retry僅有目前phase system與本次user（原有answerText／phase包裝），歷史留DB供輪次／diff／統計；workflow與CaseFeedbackCore排除舊user／assistant。Raw仍保留LangChain多輪，三profile／prompt v3／schema／抽樣不改。本人課程teacher／TA新回覆旁「查看 Ollama 請求」直接開實際payload；效能入口保留。預檢413尚無送出封包，歷史不重建。[操作與限制](request-dialog.md)。隔離DB375、Vue275、Chrome22＋更新fixture後4及標準VS含Vue通過；本輪無實際LLM推論。

## 2026-11-17 效能彈窗的實際請求

課內本人教師／TA新回覆可從效能dialog切至Ollama請求，查看真正序列化送出的messages／phase／format／options及複製payload。明確課程授權才啟用request-scoped conversation capture，完成後重查角色；學生／預覽／他人歷程不提供，舊回覆不重建。只在HTTP scope與頁面暫存，不入DB／storage／log／診斷，不擷取conversation response body或thinking，Raw仍無system／持久化／正式統計。此為先前全面排除conversation inspection的明確staff自用例外，非開啟全站內容紀錄。[操作、生命週期與驗證](request-dialog.md)。

## 2026-11-16 選定階段明確傳入

CaseChatPrompts現在professor-four-phase-20261116-v3，最前顯示目前ABCD；CaseFeedbackAsync的stage-aware overload由課程workflow傳入伺服器stage，在現有answerText包裝追加currentStage／currentPhase／currentStageLabel。無病例僅限制事實核實，不影響判讀明確分類錯置；必要背景與合法多碼不能一律判錯。Studio同理從saved answer.stage加入phase，原schema維持，版本case-text-feedback-phase-v3。原二參數provider方法保持相容，Raw與三profile／抽樣不改。

實際IAN02三個合成情境最終2/3有效格式回覆，仍有錯列分類、不實時序及混合背景判斷問題；不是教學品質通過。UI因此獨立標明系統依有效annotation列階段核對，不重寫AI原文。[03最新結果](../ai-tuning/03-evaluation-results.md)記錄首輪及最終限制。

## 2026-11-16 切回 IAN02 並保留雙設定

依使用者要求，預設Profile改為llama3：`http://IAN02:11434`／`llama3.1:8b`。Taipei Breeze仍完整保留於`Configuration/Ollama/breeze.json`，沒有把其單system／重申相容性覆寫成Llama規則；qwen亦保留。基礎appsettings移除重複的Breeze端點／模型，只設定預設Profile，端點與模型由對應profile提供。

Visual Studio啟動選單新增`https-IAN02-Llama3`與`https-Taipei-Breeze`，兩者均只綁https://localhost:7173，停止目前執行後選另一個再F5即可。原`https`也只綁7173、沿用預設llama3；未新增平行埠。命令列亦可選`dotnet run --launch-profile https-IAN02-Llama3`或`dotnet run --launch-profile https-Taipei-Breeze`。切換會重啟服務，Raw記憶體對話隨之清除，Stage的DB原始歷程保留；建議新開階段對話，避免混用模型回覆。

切換驗證：IAN02 Ollama0.40.16、llama3.1:8b存在；三profile封包9/9、標準VS建置通過，實際合成2Raw＋1Stage回饋及量測成功，不寫DB。沒有改動三份profile、其他模型的相容設定或自動降級策略。此為連線／格式驗證，未宣稱Llama已通過完整教學品質驗收；較早Breeze測試保留作該模型歷史結果。

## 2026-11-16 教授規則及原文標註

CaseChatPrompts更新為professor-four-phase-20261116-v2，Studio沿用TeachingInstructions及原caseFacts/schema。新CaseFeedbackAsync共用原傳輸與抽樣，無隱含全域system／capture，Stage最新user封裝decoded answerText及格式提醒；原DB原文不變，Raw維持完全透明無schema。AI以精確text＋occurrence提議標註，由server嚴格轉UTF-16，公開content維持純回饋文字。三個profile配置均未改，原Assess／Feedback的相容組裝保留。

已實際呼叫公司Breeze，多輪調整移除JSON示例複誦及不可靠索引；最終12次格式成功，9筆引用接受／22筆拒絕，仍有錯誤初始GCS、不實遺漏及修訂判斷問題。這些是模型行為失敗，不是通過教學驗收；[逐項結果](../ai-tuning/03-evaluation-results.md)。只用合成資料、無DB歷程，其他模型即時品質未驗證。模型內建system及主機／代理可能留存資料的原限制不變。

## 2026-11-16 回覆效能欄位

課內ConversationAsync現在依實際response欄位產生AvailableFields，只保留已知量測名，不保留raw JSON。缺省數值與真實0分開顯示；衍生tok/s須有生成數及有效生成耗時。Stopwatch往返為應用程式到模型服務，不含完整瀏覽器／DB流程。Stage逐回覆持久化，Raw僅暫存且不進模型歷史／正式統計。此次未改任何profile／System Prompt／outbound payload；[回饋輪次交付](../feedback-rounds.md)記錄038與驗證。

## 2026-11-16 個案分析透明對話路徑

新增`ConversationAsync`共用原HTTP transport，Raw與Stage採同模型／AssessmentSampling，但不自加全域SystemPrompt／schema／評閱重申、摘要或diagnostics／inspection；不回傳thinking，敏感例外不帶body／inner。Stage明確提供legacy-66b6795-chat-v1教學system，Raw只user／assistant。既有Chat／Assess／Feedback與三profile相容設定不變。LangChain.Core維持0.17.0，Raw專用完整ConversationBufferMemory，不使用舊40則SessionStore或summary。

目前服務唯讀實查Ollama0.40.11、Breeze模型有System且template引用System；「應用不注入」不等於模型無預設／所有基礎設施無日誌。未取得主機／代理設定，不能確認OLLAMA_DEBUG／APM／body logging。已用合成2輪Raw＋1輪Stage驗證同Provider傳輸，無DB、內容未輸出；模型教學品質未驗收。完整 [五模式交付](../case-analysis-chat.md)。本段為新課內對話路徑，下方原評閱路徑仍有效。

更新：2026-10-22。目的：切換模型時保留各自的訊息組裝方式、抽樣參數與已知限制，不再覆寫上一個模型的經驗。

## 選擇模型

於專案根目錄，以啟動環境變數選擇設定，重新啟動服務後生效：

```powershell
$env:Ollama__Profile = 'llama3' # 或 qwen、breeze
dotnet run --launch-profile http
```

發布版本亦使用同一環境變數啟動 DLL。專案預設現為 `qwen25-omni`（2026-11-17使用者指定Taipei模型，原三份保留），不會在失敗時自動改用其他模型。啟動時依序套用基礎設定、所選 profile、`appsettings.Local.json`、環境變數；本地檔及環境中的 BaseUrl／Model 會覆寫 profile，切換前應檢查是否存在舊覆寫。Profile 的選擇在讀取本地檔前完成，請用啟動選單、基礎appsettings或環境變數 `Ollama__Profile` 選擇，不要只寫進本地檔。

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
