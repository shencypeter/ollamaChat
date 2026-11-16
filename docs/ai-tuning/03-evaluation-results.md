# 四階段回歸與行為評估

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
