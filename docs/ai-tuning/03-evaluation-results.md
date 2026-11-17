# 四階段回歸與行為評估

## 2026-11-17 B回饋v4修正及剩餘限制

保留教授完整TeachingInstructions與原文驗證，只對正式課內B追加ABCD／子碼層級及四個全合成時點對照。B的outputRequirements改為有問題才修改，不再強制每次提出改錯；schema以annotations在前、feedback在後，引導先分類再寫評語，仍是一次HTTP請求、相同欄位與驗證。未採用臨時ChatML、raw generate、簡化教授規則或兩次模型呼叫方案，未修改遠端模板。A/C/D、Studio、Raw與四份模型profile保持。B版本為professor-four-phase-20261117-b-v4，其他階段仍v3。

調校過程的hierarchy重申、角色包裝、較短規則、user示例、system示例及兩次回饋試驗均以合成文字執行；只選擇保留完整規則的B指南＋annotations-first方案。模型seed相同仍有不同回覆，不能拿一次結果當確定性保證。真正實作Provider最終再測原7例及4個不同措辭／新內容例，11次均完整JSON／stop、有效引用，輸入2493–2503 tokens；本輪未使用真實作答、無正式DB聊天寫入。

| 最終合成測試 | 觀察 |
| --- | --- |
| 受傷時間／機制 | B1b/B1c，未再要求從B轉B1；仍提未提供年齡 |
| 影像及明載病灶 | B3，未再否認已寫病灶 |
| 本次手術 | B5，無反覆要求換子階段 |
| GCS／LOC | B2a/B2b，原文引用有效 |
| 未來OT及新措辭PT目標 | 均B7b，未標目前C5，回饋指出與B相符 |
| 舊手術＋本次手術 | 仍錯將舊手術一併B5；未解決 |
| 純病前活動，原例及新措辭 | 能指出A與目前B不符，但IADL仍誤A2a、缺活動習慣A3，status仍unverified |
| 新受傷機制措辭 | 有B1b/B1c，另無據加入職災B1d，評語亦混受傷年齡；未解決 |
| 新影像措辭且無正式診斷 | B3，未自行補正式診斷，保留無法核實限制 |

**本次完成提示／生成順序及版本修正，仍未通過完整教學品質驗收。** 不會在程式中猜碼、補標、重寫AI評語或把misplaced硬轉成其他狀態。細碼、混合時點、來源外缺漏及文字與status一致性仍需教師核對；不能把11次格式成功稱11次語意正確。

B system為7704 UTF-8 bytes，原6500-byte測試約束對B調成8500（其他階段不變），實際12,000-byte應用預檢未放寬；新增755個合成中文字可容納的回歸。較長B輸入仍可能預檢413，不裁字、不丟歷史；Stage本來只送本輪。DB新增版本測試先觸發既有metadata不可變保護，改為直接種入舊版測試記錄，沒有鬆綁trigger。最終隔離DB385/385、036→039升級及指紋保留通過，schema刪除。無Migration，舊B資料不重寫；舊版失敗訊息不可用新版偷偷重試，須重新開始B並自行提交。

## 2026-11-17 B階段短句診斷（現行Omni，未調Prompt）

使用者觀察B較困難。依現行qwen25-omni profile、真正OllamaClient.CaseFeedbackAsync、教授v3及compact schema，將7個全新合成短例逐一送入B。每例單一system＋本次user，不帶舊稿、不寫DB。輸入2138–2148 tokens，全部HTTP路徑完成、JSON可解析、done_reason=stop；這是格式通過，**不是教學品質通過**。

| 合成例主題 | 有效引用的分類結果 | 問題觀察 |
| --- | --- | --- |
| 受傷時間及滑倒機制 | B1 | status=misplaced，要求從B階段改到B1，混淆階段與子分類 |
| 影像報告及明載病灶位置 | B3 | 已選對根碼，回饋卻稱未提病灶並要求額外資料 |
| 本次骨折固定手術 | B5 | 根碼吻合，但回饋反覆重述直至compact字數附近 |
| 最低完整GCS及LOC時間 | B2a/B2b | 細碼可辨識，回饋仍重複要求「歸到B2」，未清楚表示其本來屬B |
| 未來職能治療及尚未達成的穿衣目標 | B7b、B7a、C5（重複） | 要求改成醫療治療，將未達成目標混為現在C5，多筆misplaced |
| 舊手術史＋本次手術 | A1e/B5（重複） | 引用可辨識兩種時點，回饋卻概括原文屬A，再要求轉到已選的B；多筆misplaced |
| 純病前IADL及活動習慣放在B | A2a | 大階段A辨識得到，但細碼不符IADL，未涵蓋活動習慣，status仍unverified |

判讀依現有專案規格，未由教授另行核定為標準答案。B1–B7是B的分類層級，不是額外學習階段；未來治療計畫亦不能自動視為目前已達功能。既有Prompt已寫四階段、相關時點及來源限制，模型仍未可靠遵循。根碼辨識、子碼精度、錯置status及回饋語意需要分開驗收；不可僅因引用逐字吻合或JSON合法就判通過，也不能將所有問題歸因輸入截斷。

本輪未改正式Prompt／版本／payload／profile／解析器，未將錯誤status或分類以程式改成「正確」。若後續調校，優先實驗明確ABCD與子碼層級，再驗證跨階段背景及未來／現在時點；不能只拿單一例通過就套用。探針與合成結果僅於忽略的`.codex-build/BPhaseProbe/`及`B-phase-atomic-results.json`；不含真實案例／學生作答。探針建置成功（NuGet弱點資訊來源連不上，NU1900警告，0錯誤），正式前後端未變，未重啟7173；無理由重跑全量產品回歸。本記錄、規格／交接／history同步，未提交推送。

## 2026-11-17 Omni JSON截斷修正驗證

使用者回報B階段失敗。僅讀指定時點metadata，確認provider_failed而非context_limit；失敗upstream本文原先未保存，未直接還原或重送實際使用者內容。使用新且不同的全合成長案例測試，Stage獨立4096輸出與compact schema（feedback600／annotations8／quote160／explanation80），原教學Prompt及v3不變。

臨時邊界試驗及其後真正修正Provider都在約7.8s返回完整JSON，done_reason=stop、feedback600字、1筆逐字引用有效；Raw保持512且兩輪／短Stage可用。但該合成例標出無據碼，精確引用通過不表示分類正確，**本項只修復格式／截斷，不是教學品質驗收**。短schema可能減少涵蓋，不能要求模型一定標滿或用程式補造缺漏。

隔離資料庫383/383通過，含半份回覆不保存、原user保留及same-ID retry，舊資料升級指紋保持、schema清除。其他模型配置、Raw記憶與教學Prompt不變，無Migration；實際使用者原文未進測試、文件或本機診斷結果。

## 2026-11-17 Taipei Qwen2.5 Omni初步觀察

依使用者指定，Taipei `rockn/Qwen2.5-Omni-7B-Q4_K_M:latest`確認存在，Ollama0.40.11、qwen2vl／Q4_K_M；模板為單純System＋Prompt，未修改遠端模板。原生輸出上限512。最初套共用4096上限，Raw首輪約31.9秒產生過長回覆，第二輪被完整history budget拒絕；沒有截斷原歷史或放寬限制。

新獨立profile改沿原生512上限（context16384、其他抽樣不變），合成兩輪Raw及短Stage JSON成功，約4.1／3.8／2.0秒。另用上輪相同純B病程作答：HTTP200但生成512 tokens、done_reason=length，JSON未結束，被嚴格解析拒絕；feedback複述及重複，且將B1子分類當成要切換的階段。這不是分類成功，也不能以短延遲稱優於Qwen3：輸出上限已不同。單次小樣本未通過教學品質驗收。

平台已依使用者指定切至此模型供比較，四份profile保留；沒有新增多模態輸入、沒有修補不完整JSON、沒有補造分類。輸入全合成、無正式聊天／成績寫入，忽略結果位於`.codex-build/qwen25-omni-labels-experiments.json`。

## 2026-11-17 Qwen3初步比較

使用者要求改回早期IAN02 qwen3:8b，沿既有Qwen profile及同一套Provider／參數，不加think flag或更改分類提示。2輪Raw及1輪Stage JSON回饋成功；首Raw約51.2秒、次輪約4.5秒、Stage約46.5秒（此為程序累計時間差，不將全部延遲歸因載入）。

再對上輪相同的純B病程合成作答做單次stage-aware分類：HTTP200且JSON可解析；1筆逐字引用通過，codes為B1b／B1c，主要涵蓋受傷日期及機制。影像與手術等內容沒有標註；feedback要求補受傷年齡、職災相關性等來源未提供細節，仍有無據缺漏要求。實際metrics約71.1秒、prompt2173／response1035 tokens。沒有完整／混合案例驗收，不據此推論Qwen整體優於其他模型。

所有內容合成、沒有DB歷程／成績寫入或真實病例，結果僅留本機忽略的`.codex-build/qwen-labels-experiments.json`。7173依使用者要求切為Qwen供後續人工比較，Llama3／Breeze保留。

## 2026-11-17 Breeze分類遵循診斷

使用者回報Taipei Breeze不如Llama3遵循分類碼。先核對實際模板／設定及網站Provider，未使用學生作答或參考病例；以3則新合成情境比較：A主要作答放B、B病程作答、A背景加B病程。所有Stage請求僅目前system＋本次作答。

Taipei Ollama0.40.11的Breeze使用`{{ .System }} [INST] {{ .Prompt }} [/INST] {{ .Response }}`模板；既有送出payload已有43碼目錄、目前phase、answerText及受enum約束的schema，因此不是前端未提供分類碼。原提示3/3返回可解析JSON，僅2/3有通過原文引用驗證的標註，仍錯選未出現的人口學碼、ADL/IADL等，feedback多次照抄分類規則；純病程例的1筆提案因不是原文引用被拒絕。合法JSON／合法code／精確引用各自不代表語意分類正確。

只在本機忽略目錄建立臨時診斷程式，保留模型及抽樣、同一批3則合成輸入，試4種包裝，**沒有修改正式Prompt／Provider／profile**：

| Breeze包裝實驗 | 回傳JSON筆數 | 至少一筆原文標註有效的作答 | 有效標註總數 | 觀察 |
| --- | --- | --- | --- | --- |
| 原版基準 | 3 | 2/3 | 2 | 複述規則、引用非原文及選錯碼 |
| user前重申分類與引用要求 | 3 | 0/3 | 0 | 引用規則／改寫文字，後端拒絕 |
| plain作答＋完整碼表 | 3 | 2/3 | 2 | 仍錯選碼、複述規則 |
| 完整教學提示重複放入instruction區 | 3 | 0/3 | 0 | 仍引用失敗及無據評論 |
| 大幅精簡提示，僅分類任務 | 3 | 3/3 | 6 | 有改善但錯碼、漏碼、英文回覆，且移除了教授教學方法，不可直接上線 |

上表的「有效」只指格式、合法碼及逐字引用通過程式檢查，**不是分類正確率**。這3則是診斷小樣本，不是完整模型排名或專業驗收。四個替代方案都未達可採用程度，不放寬原文驗證、不以程式補造分類、不強迫每次產生標註。

IAN020.40.16可達、模型存在且短Raw回覆成功。初始舊helper比較回傳失敗（無法據此判斷網路或品質）；再由HTTP邊界擷取同批3則，皆200及可解析，純B例辨識B1c／B3／B5，混合例仍選多項未提及人口學碼、回饋有無據缺漏。故亦不能將Llama3視為可靠自動編碼器；使用者觀察到的相對改善可作比較，不可當專業正確性保證。

只保存合成診斷於本機忽略的`.codex-build/*labels*.json`；未查詢或寫入正式聊天、成績、案例。正式7173仍沿既有Breeze設定，未自動切回其他模型，後續模型選擇待使用者指示。

日期：2026-11-16。程式驗證與模型教學品質分開判定；Mock Provider 能證明封包、持久化及授權，不能證明臨床判斷。所有模型測試使用全新合成案例，不發送四份參考文件中的病歷或真實學生答案。

## 2026-11-16 階段核對 v3（最新增量）

使用者示範B階段提交A內容，要求回饋指出錯置、A底線／B淡色背景。先重現缺少stage-aware Provider入口；UI也先以測試確認缺少目前階段資訊。實作後完整Vue265／63檔、後端312通過＋50條件略過，另隔離DB完整362/362（零略過）、Chrome17/17、標準VS含Vue均通過。無新Migration；原文、舊AI回覆／annotations、成績及Raw行為不改。獨立review未見程式／授權blocker。

新增確定性驗證：B請求明確含currentPhase=B、currentStage=illnessCourse及標籤，仍保留exact answerText；Stage workflow及Studio從儲存／伺服器stage傳入。UI已驗B內A／B內B、同段合法A+B、unverified／unsupported、其他內容未標註不推論成缺漏、舊版／教師查閱、Raw無核對提示及1440／390淺深色／鍵盤／觸控。原AI文字維持，系統核對提示另行標明來源，避免冒充模型原評語。

實際用目前IAN02 llama3.1:8b執行`--case-phase-alignment-live-check`，全合成、無DB寫入；不是發送使用者截圖中的作答。第一輪三則均格式成功，但混合背景仍被模型概括錯置。之後補強逐句及「目前階段→原文分類→修改建議」指示，最終同三情境如下：

| ID | 情境／預期 | 實際最終觀察 | 判定 |
| --- | --- | --- | --- |
| P01 | A主要作答交在B：明說B目前、A實際分類、引導移置 | 有說目前B及可能錯置，1筆精確引用有效、2筆拒絕；但文字又列出原文不存在的人口學子類 | 階段提示部分改善，語義未通過 |
| P02 | B作答交在B：辨識相符、勿虛構問題 | 有辨識B，1筆有效B範圍；仍捏造時間順序不一致及不足 | 定位有效，教學語義未通過 |
| P03 | B事件包含必要A背景：逐段說明、不可全判錯 | 最終未取得有效回覆（安全錯誤不保留模型本文）；首輪曾過度概括成A | 最終未通過 |

最終2/3取得可解析回饋，不能宣稱三例教學全過。這也是UI採用獨立、保守的「依AI分類標註」核對提示的原因：可靠呈現**所選階段與有效標註之差異**，並不保證模型分類或臨床判讀正確。不把未標記文字當成缺漏，不以此自動扣分。下方Breeze結果為本日先前v2調教紀錄，仍保留其時間脈絡，不代表現行Llama表現。

```powershell
# 先依前述方式建置PortalSmoke至隔離OutDir，再指定既有模型profile
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Ollama__Profile='llama3'
dotnet .codex-build/phase-match-tool/PortalSmoke.dll D:/AI_TeachingAssistant --case-phase-alignment-live-check
```

測試完成後暫存build可清理；合成結果僅留ignored `.codex-build/phase-match-live-results.json`，不建立正式學習歷程。

## 確定性測試

| ID | 階段 | 情境／預期 | 實際結果 | 狀態 |
| --- | --- | --- | --- | --- |
| D01 | A–D | 共用形成性規則、五種按需回饋、各階段範圍及43碼目錄 | 四階段 prompt、排除碼、版本及一般 Raw 排除均驗證 | 通過 |
| D02 | A–D | 原文 UTF-16，中文／英文／emoji／重複片語位置一致 | 原始 substring 相等；代理對切割與越界拒絕。AI精確引用＋指定occurrence由伺服器定位，不猜測或修補無效offset | 通過 |
| D03 | A–D | 同一範圍多碼、重疊／相鄰不改原文 | 分段重建原文完全相同，呈現所有有效碼 | 通過 |
| D04 | A–D | 空集合、錯誤型別、假碼、超量、錯誤 text | 無效項丟棄；最多48筆、8碼、200單元說明 | 通過 |
| D05 | A–D | 分類辨識不得自動等於正確 | unsupported／misplaced 保留，未知 correct 回到 unverified | 通過 |
| D06 | A–D | 三個 profile 不改最新原文／Raw 封包 | Stage 明確 JSON schema，Raw 無 format／應用 system；抽樣保持 | 通過 |
| D07 | A–D | 不完整 envelope 可重試且不偽造往返 | 原 user 標 failed、重試只保留一對，例外無模型內容 | 通過（真實DB） |
| D08 | A | 修訂後原回饋標註仍屬原版本 | replyTo／revisesMessageId、師生讀回、不可變 trigger 驗證 | 通過（真實DB） |
| D09 | A–D | 舊037／038及課程成績升級不變 | 036→037→038→039、重跑、既有資料指紋保持；舊 annotations NULL | 通過（真實DB） |
| D10 | A–D | Studio 保留既有回饋欄位及案例版本 | 另外傳 caseFacts／answerText，追加 optional annotations，未給原文則不採用標註 | 通過 |
| D11 | A–D | 學生、課程、TA、練習／正式紀錄隔離 | 全套原授權／重試／Raw 不入DB及正式統計回歸 | 通過（真實DB） |
| D12 | A–D | 桌面／手機、淺深色、鍵盤／touch／hover | Chrome 13流程，含原8流程、版本切換、教師查閱及 tooltip | 通過（合成API） |

## 實際模型行為

使用目前公司設定的 Breeze，同模型與既有抽樣；`--professor-alignment-live-check` 依序送出十二種合成情境，包含後續修訂及模型層空字串。公開 API 仍拒絕空白作答（400），不把模型層測試誤稱為可提交空白。

第一輪實際12次均能解析 JSON，但模型照抄 prompt 中的示意 JSON：回饋為欄位佔位文字，虛構的 span 全被驗證器拒絕。**第一輪12項行為評估皆失敗**，不能以 HTTP／JSON 成功宣稱教學通過。已據此移除可照抄的示例，後續結果於下節補記。

後續實測先移除示例，再明確包裝answerText，最後將AI定位改成逐字text＋0起算occurrence。最終版本已重新建置並實際呼叫12次。下表為**開發者逐筆檢視**，不是教授臨床驗收；不能把有效JSON或有效位置當成正確分類。

| ID | 階段 | 情境／預期 | 最終實際觀察 | 判定 |
| --- | --- | --- | --- | --- |
| L01 | A | 正確詳盡，承認涵蓋不製造遺漏 | 加入他階段要求及來源未記載事項；無有效標註 | 失敗 |
| L02 | A | 部分涵蓋，提示相關缺漏 | 誤把未寫的教育／ADL稱為已掌握；1有效引用 | 失敗 |
| L03 | A | 接受等義生活／家務描述 | 能辨識改述，但仍要求未知細節與傷後資料；1有效引用 | 失敗 |
| L04 | B | 識別現在肌力／移位屬C | 主要複述作答，未明確提供錯置指引；1有效引用 | 失敗 |
| L05 | B | 後期GCS不能冒充初始 | 誤肯定初始值並捏造案例未提供的具體時間；1有效引用 | 失敗（高優先） |
| L06 | B | 缺初始指標不當學生遺漏 | 仍要求未記載GCS／LOC及其他影像；1有效引用 | 失敗（高優先） |
| L07 | C | 籠統作答需引導具體程度 | 僅複述，未提供實質引導；1有效引用 | 失敗 |
| L08 | C | 篩檢不能直接確診 | 有說明不可僅憑篩檢診斷，但對學生原主張的轉述不準確；1有效引用 | 部分符合，未通過 |
| L09 | C | 接受有保留的合理假設 | 承認嗜睡影響與後續衡鑑需要，不升格確診；1有效引用 | 本次符合（未經教授驗收） |
| L10 | D | 正誤混合需保留優點與質疑 | 對未證實的家庭關係仍有不當肯定；1有效引用 | 失敗 |
| L11 | D | 空白只引導開始、不給答案 | 列出大量分類，還把不存在的作答說成已掌握；15虛假引用均拒絕 | 失敗 |
| L12 | D | 評最新修訂，保留未知 | 能承認新版內容，仍要求來源不存在的照顧／家庭細節；無有效標註 | 失敗 |

最終傳輸／JSON解析12/12成功，引用定位接受9筆、拒絕22筆（格式位置有效不代表臨床碼正確）。這些單次樣本不是穩定準確率。模型往返0.863–6.175秒、輸入1982–2121 token、輸出86–811 token；數值僅本機本輪、受快取與服務負載影響，不能外推效能承諾。最終文字均為中文，但證據判讀、範圍控制、修訂與完整答案邊界仍未通過。**程式整合完成；目前部署模型尚不符合完整形成性回饋品質要求，不能當可靠臨床判讀或自動評分依據。**

模型回覆及逐筆效能僅存 ignored `.codex-build/professor-live-results.json`（全合成），不寫學生DB。這是開發檢視，不是正式評量品質報告。

## 執行方式與限制

```powershell
# ClientApp
node node_modules/vitest/vitest.mjs run
node node_modules/vite/bin/vite.js build
node node_modules/@playwright/test/cli.js test studio-case-chat.spec.js
# 根目錄；OutDir避免覆寫正在執行的7173
dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:SkipFrontendBuild=true -p:UseAppHost=false -p:OutDir=D:/AI_TeachingAssistant/.codex-build/annotation-tests/
dotnet build tests/PortalSmoke/PortalSmoke.csproj --no-restore -p:SkipFrontendBuild=true -p:UseAppHost=false -p:OutDir=D:/AI_TeachingAssistant/.codex-build/alignment-tool/
dotnet .codex-build/alignment-tool/PortalSmoke.dll D:/AI_TeachingAssistant --professor-alignment-live-check
```

模型工具需 Development；DB測試需既有 FORMATIVE_TEST_CONNECTION 指向獲授權的獨立 group_test schema，升級檢查以 ConnectionStrings__Portal 同樣隔離。切勿將憑證寫入命令或文件。最終一般後端310通過／49外部DB條件略過；另外隔離DB完整359通過／0略過，完成已刪schema。Vue256通過；最終Chrome20通過（13聊天／標註及7師生／成績／教材回歸）。標準Visual Studio MSBuild含Vue成功，public039交易套用、唯一7173回200並提供新bundle，匿名AI401。完整歷史E2E、未設定的獨立TS型別檢查、輔具實機螢幕閱讀器及其他兩個模型的即時品質未測。
