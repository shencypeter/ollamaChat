# 開發歷程與決策紀錄

## 2026-11-17 教師逐次選模與Omni聊天循環修正

使用者要求教授自行比較四個既有模型並記錄model，且補充短Raw問候也重複，推翻只因B長篇的假說。新增scoped CaseChatModels白名單resolver，重新從base／environment appsettings→選定profile→Local→env載入，不繼承active profile overlay；每request獨立HttpClient/options，不改全域。正式turn在開始鎖runtime，user／assistant沿model_name保存實際tag，same-ID不得換模型，retry須原model／prompt，完成前重查角色。Raw entry只存設定choice，不保留scoped provider／inspection；每次送出另建provider並檢查owner／course／Practice，無DB／統計。前端staff-only native下拉、保留正式草稿／歷史、Raw切模確認清除、actual model逐輪顯示，學生POV同步隱藏。

長B>=1000才追加自然語言評語提醒及機械檢查。重複佔主要內容或大量箭頭編碼配對觸發最多一次feedback-only；只送當次作答與有效annotation參考，不重新分類、不送先前壞評語或歷史。第二次仍失敗明確報feedback_repetition，不存assistant，原user保留。原首輪annotations原樣保留，ProviderCalls2及數值量測合計，缺少的量測保持不可用，正式往返仍1。B版本v5，其他phase／Studio未改。這是輸出呈現守門，不是臨床品質評分，也不以程式去重冒充模型原回饋。

以不同於使用者的合成短句比對Omni：原api/chat模板512tokens／length、重複14次；repeat_penalty1.15仍512／length、重複9次，未採用；api/generate raw=true＋明確Qwen user／assistant delimiters只23tokens／stop，有無額外stop皆同，未新增stop。因此只Omni採qwen-chatml-generate選用transport；共享SendAsync產生實際prompt、raw=true及既有options／format，normalize response欄位，Raw不加system、Stage仍完整教授提示。其他profiles及抽樣保持，遠端模板不改。本人請求dialog顯示真正generate prompt，不假造messages。最終實際C# Provider Raw兩輪均23tokens／stop，合成五段2125字B prompt4103／response166／stop、完整JSON且無循環；未使用真實病例／學生作答，不代表分類全正確。

新增授權／並發／原模型retry／實際DB tag、Raw snapshot、no-system ChatML與完整Raw roles、長B最多兩次／保留標註／不送舊稿、量測合計與workflow不存循環回覆回歸。完整隔離DB406/406及036→039指紋升級通過，finally刪schema。Vue301/301、最終Chrome32/32、VS含Vue建置成功。初E2E新案例揭露chooseMode在Raw清除await期間loading=false，立刻選模會取消切頁；先補deferred cleanup失敗測試再修。靜態審查另發現唯讀切Raw載入不結束，先重現再修startRaw early guard；另補fixture等待初始化，沒有用固定sleep掩蓋。Chrome最終手機畫面已檢視。NU1900弱點來源及原>500KB chunk提示仍非失敗，沒有獨立型別檢查命令（專案JS／Vite）。

核對PID36752確為本專案後重啟唯一7173為正常bin/Debug PID7332，qwen25-omni、部署generate設定、bundle index-CaiDGwMW.js、/courses200／匿名AI401核對，5097／5173／5174無listener。Raw清除、正式歷史保留，無Migration／正式資料寫入。初步其他模型容量探針使用預設JSON escaping而非正式序列化，不以其數字宣稱產品可用容量或放寬原profile限制。不同模型仍有細碼／時點／語意問題，不新增自動評分。文件／交接／Ollama／五模式／輪次／評估／計畫同步，未提交推送。

## 2026-11-17 教師診斷、聊天頁籤及中文容量

依使用者要求將模式按鈕改語意／視覺頁籤，5個聊天tab加授權紀錄tab，單一關聯panel，左右/Home/End僅移焦點、native Enter／Space啟用，既有Raw清除／草稿確認保持；桌面平列、手機橫向捲動。新增固定CaseChatFailure碼／原因／HTTP狀態，涵蓋preflight、輸出length、空回覆、provider JSON、feedback JSON／格式、連線／逾時及已知HTTP error分類。已知上游原因只轉固定文字，不回任意error本文／inner／thinking。Send／Retry／Raw錯誤在server完成後重查course teacher／TA，排除學生POV、教授但只修課及降權；本人失敗payload沿同capture權限回傳。API／Vue顯示教師診斷並可開失敗請求dialog，離頁／角色變更清除；不存DB／storage／log、不重建舊原因。

使用者同時回報2125中文預檢拒絕。先以五段合成文重現；根因是UTF8bytes當token及12KB硬cap混用，非Vue8000字驗證。分離bytes與估計tokens：Omni profile增加CaseChat bytecap24000、CJK2／overhead512；其他三profile未改，預設CJK3／overhead16重現原計算。ASCII及非基本CJK仍依UTF8bytes保守估，扣除Stage4096或Raw512輸出；不裁字、不加舊稿。測試中文2125接受、ASCII token及byte兩上限分別拒絕。真正Provider合成2125字五段原文完整、2messages、HTTP200、prompt4031／response567／stop；未寫正式DB。

測試先確認UI缺tabs及教師診斷2項失敗，再修正；後續Vue全量288/288、Chrome30/30（含2125字真的AJAX、teacher/student、失敗dialog、鍵盤、390深色及全部既有流程），已檢視桌面／手機合成截圖。初Chrome4失敗是舊滿版按鈕CSS期待及新fixture模型名寫錯，按實際tab設計／fixture修正後通過。後端完整隔離DB394/394、036→039升級指紋保持，schema finally清除；新增當次staff／TA／student／降權／Raw與原因去敏、容量回歸。靜態審查無可行動問題。標準VS含Vue成功，NU1900弱點來源與506.13kB chunk提示非失敗。

核對PID27984為專案後更新唯一7173正常bin/Debug PID36752、qwen25-omni，部署profile24k／2／512、bundle index-5twnEPzG.js、/courses200／匿名AI401，舊埠無listener。Local與Process/User/Machine無容量覆寫；模型／sampling及Bv4、其他phase版本不變。重啟清Raw、正式歷史保留，無Migration。規格／交接／Ollama／請求dialog／五模式與計畫同步，保留dirty工作，未提交推送。

完成部署期間使用者提供長病例與重複回饋。未複製／重送病例作測試；只讀指定時間窗metadata：prompt4015／response1762／done_reason stop、feedback600字、有效ann5。這不是16K硬上限或num_predict4096耗盡，且JSON合法故不觸發失敗診斷；重複及分類品質仍未解決。8筆配額不能單憑有效ann5推論漏標原因，上游原回覆未留存，不反推原提議數。不擅自啟動分段管線或自動品質判定，後續應用合成情節做長／短及逐段對照。

## 2026-11-17 B回饋修正實作與部署

正式B新增短層級／時點示例，保留教授完整規則；只對B用較中性的outputRequirements，取消每次強迫改錯。B schema先annotations後feedback，欄位／上限／精確引用驗證不變，仍一次api/chat，無舊稿重新送入。A/C/D、Studio與Raw不改；四profile與appsettings相對HEAD無差異。B新增stage-aware版本professor-four-phase-20261117-b-v4，user／assistant保存、retry版本檢查、教師本人請求capture及live工具均採對應stage。舊記錄保留v3，不能將新版誤算舊版重試。

先擴充封包測試確認VersionFor不存在，再驗schema順序斷言失敗後修正；相關25通過，普通全後端329／56外部DB略過。隔離schema初新增測試以UPDATE種舊version，被既有metadata不可變trigger正確擋下；改為INSERT合成舊記錄，不放寬保護。完整385/385、036→039升級／指紋及舊可選欄位NULL保持通過，schema清除。B system7704bytes，測試界線8500、其他階段6500不變，實際12KB guard不放寬，755個合成中文字可用。

合成試驗包含hierarchy重申、ChatML、generate、縮短教授規則、user例子與兩次模型回饋，均未部署；採完整規則＋B指南＋先標註的一次呼叫。真正Provider最終原7＋新4完整JSON／stop，輸入2493–2503：B1/B3/B5/B7b同階段誤判及重複改善，純A回饋能說與B不符，但IADL細碼／status、舊手術與本次混碼、無據職災等仍不穩，不能稱教學品質驗收。沒有程式補標／猜碼／重寫AI評語。詳細方法與限制已寫評估文件。

依requesting-code-review技能作一次靜態審查，無可行動問題（審查者未另跑測試）。最終Chrome請求dialog與標註桌面／390淺深色11/11、標準VS含Vue成功；NU1900為NuGet弱點資料來源網路警告，504.08kB chunk提示非失敗。首次受限testhost無法連線而逾時，切到允許環境後測試成功。驗證PID18048確為本專案後，唯一7173更新正常bin/Debug PID27984，qwen25-omni、Bv4／Av3部署DLL反射核對，/courses200／匿名AI401、舊埠無listener，bundle index-BOZ0N_8q.js。已告知重啟清Raw、正式歷史保留，舊B失敗需重新開始再自行提交。無Migration／正式資料寫入，規格／Ollama／評估／交接／計畫同步，未提交推送，保留既有dirty工作。

## 2026-11-17 B階段困難分解

使用者觀察B較困難，依最新HEAD90aeaa6及當下profile檢查（原模型設定等工作已由使用者提交，不沿舊dirty清單推定）。建立忽略的BPhaseProbe，用現行CaseFeedbackAsync／教授v3／compact schema測7個全新合成短例，無歷史或DB。2138–2148輸入tokens、全完整JSON／stop，B1卻判misplaced並要求改階段為B1；B3否認已寫病灶；B7b未來目標混B7a/C5；A1e／本次B5混合時回饋概括A且建議切到已選B。B5及B2細碼可辨識但回饋重複；純A放B能辨識A但細碼／status不穩。這是具體模型遵循失敗，不能稱B比其他階段的統計性結論或都歸因截斷。

沒有改教授原規則、版本、payload、profile或來源驗證，不硬修模型status；後續調校候選為ABCD與子碼層級、時點及有據回饋，尚未實驗／部署。本輪探針建置成功，NU1900是NuGet弱點資料來源網路警告；正式程式未改，不重跑全量測試、不重啟7173。詳docs/ai-tuning/03-evaluation-results.md，規格／交接／本歷程同步，未提交推送，既有dirty工作保留。

## 2026-11-17 行內編碼徽章分類配色

使用者要求徽章與ABCD底線的分類配色一致，取代先前全部綠底。PhaseAnnotatedAnswer共用既有category-A/B/C/D底色白字，各code獨立span以處理同片段跨phase碼；同組button與tooltip／鍵盤／touch維持。A藍、B綠、C紫、D橘；深色保留分類深色底，避免把亮底線色當白字背景造成低對比。原文／UTF-16／metadata／模型payload不變。

擴充既有Vue測試先2項失敗，再驗49/49；既有Chrome流程加入四phase實際computed背景色／白字、跨phase同組、原文、切換與tooltip斷言，桌面1440及手機390淺深色6/6。已檢視390深色合成畫面，Vite成功（504.08kB chunk提示，非失敗），部署index-BOZ0N_8q.js。只前端無重啟、DB／Migration／模型改動，Raw記憶不清除。規格／交接／標註與回饋說明同步，保留dirty工作，未提交推送。

## 2026-11-17 Taipei輸入截斷假說驗證

區分輸入截斷與上一輪輸出length問題。唯讀show確認Omni模型預設context4096、native32768；Breeze預設8000、native32768，兩者模板簡單；profile實際仍送16384。11次合成首／中／尾key探針（Taipei各4、IAN02 Llama3共3）同步讀ps，實際載入context全匹配請求。Omni同23,167-byte輸入在4096時回報2050 tokens／key錯，16384時10079 tokens／三key全對。46,167-byte超量輸入於兩Taipei模型降至8194／8195，key錯；Llama3因tokenizer較省仍13088可讀，另69,167-byte溢位同降8194且首中key遺失。故有超限截斷證據，但無Taipei固定較低上限證據。Breeze中型例11105輸入、輸出128達length，本身不能判定key可讀性。

再用既有實際Provider及合成骨折Stage，2375輸入／747輸出、stop、完整JSON約8.3s，仍過度分類；不把教學遵循失敗當截斷證據。未取得遠端host／proxy／runner log，亦無法重建前次失敗封包，不改遠端模板、sampling、Prompt或放寬驗證。所有資料全新合成、無真實學生作答／DB寫入、無Migration。新增docs/ollama/context-size-check.md保存方法／表格／官方參考；Ollama索引、規格／交接／本歷程同步。未跑build／全回歸（無程式改動）；7173仍正常bin/Debug PID18048／qwen25-omni、/courses200，未重啟。保留dirty工作，未提交推送。

## 2026-11-17 等待提示改接收／閱讀／分類／著色流程

使用者希望動態訊息模擬助教收到作答後閱讀、分類及著色。改caseChatRequestGuide為流程文案：前端接收提交、閱讀段落、核對編碼、整理原文引用、準備底線與徽章著色資料、整理回饋；保留目前stage及該phase公開分類參考。AiReplyWaiting的Stage標題改處理中、label改作答處理流程，caption明示非模型即時進度，Raw改用聊天前文及等待回覆流程。沒有新增server進度事件／SSE／串流回覆或自動宣告儲存／標註完成。

更新既有文案斷言，相關Vue46/46、Chrome桌面Raw／390深色Stage2/2及Vite通過，驗證暫停、下一則、reduced-motion、成功後移除及request content原樣，已檢視手機深色。既有7173無重啟，/courses200供index-DudL5rjP.js，Vite原chunk警告非失敗。無後端／DB／Prompt／模型設定改動，Raw不因部署清除；交接／規格／回饋說明及合成截圖同步，保留dirty工作，未提交推送。

## 2026-11-17 Omni長作答回覆失敗修正

使用者提供失敗畫面與作答。僅讀production指定時點status／failure_code／字數／stage，確認provider_failed而非context_limit；沒有讀出、重送或保存該內容，上游失敗本文原本未留存，不能逐包證實舊回覆。使用完全不同的新合成長案例做探針，確認既有512輸出會截斷；試驗階段獨立4096＋compact schema後可完整JSON／stop，約7.8s，仍有不正確過度分類，未宣稱教學品質通過。

新增選用CaseFeedbackMaxTokens與CompactCaseFeedback，只於qwen25-omni啟用。Raw仍512；Stage用4096，feedback600、最多8標註、引用160、說明80。CaseAnnotationContract原預設仍12000／48／16000／200，其他profile檔完全未改；SendAsync只複製sampling、絕不改共享Options，CaseChatBudget預留Stage實際輸出。Prompt文字／v3不變，未改評分／Studio／Raw策略或來源資料。正式回覆length在provider（含空content）及workflow拒絕，明確安全錯誤、不存partial assistant；仍用既有provider_failed，不需Migration／變更資料庫check constraint。

先以新2項HTTP mock回歸重現512／未報截斷，再修正；新增DB回歸驗證截斷後只留user、0往返、同ID retry產生1正式回覆。初次全DB回歸發現舊修訂成功fixture用length作量測樣本，改成stop，截斷語意由新測試獨立覆蓋。最終隔離schema383/383、036→039升級與舊資料指紋通過、finally刪schema。真實修正Provider（非臨時HTTP轉換）合成長文完整JSON／stop約7.8s，2Raw與短Stage也成功；沒有實呼使用者答案。標準VS含Vue成功（原chunk警告仍在），唯一7173由核對PID24724更新正常bin/Debug PID18048，profile=qwen25-omni、部署Raw512／Stage4096／compact核對，/courses200／匿名AI401、其他舊埠無listener，bundle仍index-CunPqfQQ.js。原失敗user不改、不自動retry，重啟清Raw、正式歷史保留。規格／交接／Ollama／評估／README同步，保留dirty工作，未提交推送。

## 2026-11-17 切換Taipei指定Qwen2.5 Omni

使用者要求Taipei的rockn/Qwen2.5-Omni-7B-Q4_K_M:latest（訊息末尾反斜線視為排版，不是tag）。核對version0.40.11／tags精確存在，show為qwen2vl／Q4_K_M、單純System＋Prompt模板、原生num_ctx4096／num_predict512；沒有改遠端Modelfile或新增多模態功能。先新增5個profile封包案例重現檔案不存在，再新增獨立qwen25-omni.json，不覆寫原三份。沿模板採MergeAssessmentSystemMessages=true、RepeatAssessmentRequirements=false；共用seed／temperature／top_p／top_k與16384 context保留。

初先沿4096輸出，Raw首輪約31.9s回覆過長，次輪完整history budget拒絕；未截斷記憶或放寬budget，改新profile沿模型原生512上限。再驗2Raw及簡短Stage均成功，累計約4.1／7.9／9.9s；另1則合成B病程HTTP200、512 tokens以length結束，JSON截斷被CaseAnnotationContract拒絕，內容重複且混淆B階段與B1子碼，未宣稱分類／教學驗收通過。所有輸入合成、無DB歷程／成績寫入。輸出上限不同，不能拿延遲當同參數模型排名。

OllamaOptions.ProfileNames集中四個白名單，Program及3個PortalSmoke工具／AssessmentEvaluation沿用，保留原拒絕未知profile的檔案路徑保護。新增VS https-Taipei-Qwen25Omni並設appsettings預設qwen25-omni，全部沿7173。新5個封包案例與舊模型布局通過；全後端326通過／54外部DB略過（未跑DB，模型設定無schema變更），evaluation helper及標準Visual Studio含Vue成功；Vite仍僅505.2kB chunk警告。核對舊PID34716後重啟為正常bin/Debug PID24724，明確profile=qwen25-omni、部署Model精確值，/courses200／匿名AI401，其他舊埠無listener，bundle仍index-CunPqfQQ.js。Raw清除、Stage歷史保留，未提交推送，文件同步並保留既有dirty工作。

## 2026-11-17 等待泡泡輪播請求教學要求

使用者希望長時間等待時輪播Ollama請求中的要求、呈現AI串流感。採前端公開教學要求摘要，明示不是即時生成內容或實際模型進度，不假造partial response／thinking。新增caseChatRequestGuide（對照既有教授要求＋目前stage與授權API分類目錄）及AiReplyWaiting（40ms逐字、短暫停留後輪播、游標、暫停／下一則、reduced-motion靜態）。Raw有獨立摘要，避免宣稱Raw帶教學prompt；沒有讀取私有System Prompt、requestSnapshots或學生作答。CaseAnalysisChat用原busy生命週期掛載，成功／錯誤／切模式及卸載清計時器，不動HTTP payload或持久化。

新增timer／暫停／手動切換／卸載清除、reduced-motion動態變更、Unicode／HTML安全及Raw／Stage摘要隔離測試；先確認功能不存在，完成後Vue全量285/285。既有延遲API Chrome流程擴充逐字文字、暫停／下一則、提示模式正確、reduced-motion、原送出content不變，完整26/26；已檢視1440淺色Raw與390深色Stage。Vite成功（單chunk505.2kB警告，gzip164.11kB；未因此重構），/courses200供index-CunPqfQQ.js。模型Qwen與既有7173 PID34716無重啟，未實呼LLM、無後端／API／DB／Migration／Prompt／模型參數變更，Raw暫存未因部署清除。規格、交接、回饋說明及合成截圖同步；保留既有dirty工作，未提交推送。

## 2026-11-17 使用者要求切換Qwen3比較

核對持久文件、Git、三profile與覆寫；IAN02解析／version0.40.16／qwen3:8b存在成功，無本地或環境相關覆寫。沿既有Provider／抽樣作合成2輪Raw＋1輪明確Stage成功，累計51.2／55.7／102.2秒；單輪差值約51.2／4.5／46.5秒，未將第一輪延遲全歸因載入。另以同批純B合成作答做1次分類：HTTP200、JSON可解析、1筆精確引用、B1b/B1c，約71.1秒、prompt2173／response1035 tokens。影像與手術未標，feedback要求未提供的細節，僅是初步觀察而非教學品質驗收。

appsettings預設llama3→qwen，新增VS https-IAN02-Qwen3並沿7173；保留Llama3、Breeze及三profile原內容，不改think flag、sampling、Prompt或Provider。相關三profile／conversation封包13/13及標準VS含Vue建置成功。核對原PID36892後，唯一7173更新正常bin/Debug PID34716，明確--Ollama:Profile=qwen，部署設定同值，/courses200／匿名AI401、其他舊埠無listener，bundle仍index-DD-5sG5c.js。無DB／Migration／真實作答，Raw暫存重啟清除，Stage歷史保留。README、模型說明、規格／交接／評估同步；保留所有dirty工作，未提交推送。

## 2026-11-17 確認切回Llama3、保留Breeze比較

使用者回覆模型選項，明確要求切回Llama3。先重查持久文件、Git、三profile與模型覆寫，沒有相關覆寫；初次IAN02的version／tags因名稱解析失敗，未停止7173。再查DNS及本機名稱得到ian02.local、IPv4 ping成功，原IAN02 BaseUrl重試version0.40.16／模型存在，既有Provider合成2輪Raw＋1輪明確stage結構化回饋皆成功，無DB寫入。未猜測固定IP、未修改hosts／BaseUrl或模型相容設定。

appsettings預設breeze→llama3；三份profile及VS雙選項原樣保留，未套用上一輪Breeze實驗。核對原PID17972後，標準Visual Studio含Vue建置成功，唯一7173更新正常bin/Debug PID36892，命令列--Ollama:Profile=llama3、部署appsettings同值，/courses200、匿名AI401，其他舊埠無listener。前端bundle仍index-DD-5sG5c.js，無Migration／正式資料或metadata改寫，Raw重啟失效。README／模型說明／規格與交接同步，保留既有dirty工作，未提交推送。

## 2026-11-17 Breeze分類遵循問題診斷（尚未修復）

使用者指出Breeze不如Llama遵循分類標籤。依systematic-debugging先核對實際payload／模板：43碼目錄、目前phase與enum schema均存在。3則新合成基準皆可解析，但Breeze有抄規則、非原文引用被拒絕、錯選A1b／A1d等；不是UI漏送碼或服務不回覆。僅在忽略目錄建暫時診斷程式，以原Provider生成payload，測4種提示包裝共12次；重申／plain／重複instruction／精簡分類皆未達可靠程度，精簡版雖有6筆有效原文引用，仍錯碼及英文，且刪掉教授方法，未上線。

IAN02version／tags可達、短Raw成功，初始helper比較失敗不能當網路斷線；HTTP邊界重測3例皆200，純B例有B1c/B3/B5，但混合例Llama也有過度分類與無據評論。只接受格式／原文驗證不等於語意正確，沒有放寬驗證或補造標註。完整小樣本表及限制見docs/ai-tuning/03-evaluation-results.md。

本輪未修改正式Prompt／profile／Provider／前端，未重啟7173或寫DB，所有輸入為合成資料，本機結果在忽略目錄。不因實驗失敗改掉Llama設定或自動換模型；目前仍Breeze PID17972，已詢問使用者後續模型選擇。文件紀錄同步，保留先前dirty工作，未提交推送。

## 2026-11-17 Taipei連線恢復驗證與切回Breeze

使用者要求先確認Taipei能產生回覆再切回。先核對持久文件、三profile、dirty Git、Local及Process／User／Machine三項Ollama覆寫；沒有覆寫。實查version／tags成功（0.40.11，Breeze模型存在），以既有Provider／抽樣做合成2輪Raw＋1輪結構化Stage，全部回覆成功，首次合計約4.3秒；未寫DB或輸出模型內容。發現舊live helper仍用相容二參數方法，改為網站使用的明確Premorbid overload後再次3筆皆成功；驗證實際currentPhase包裝與annotation JSON合約，非教學品質驗收。

依條件切換appsettings預設llama3→breeze，不修改三份profile／參數／VS兩個7173選項。相關三profile／conversation封包13/13及標準Visual Studio含Vue成功。核對舊程序後將唯一7173由PID39792更新正常bin/Debug PID17972，命令列明確--Ollama:Profile=breeze；/courses200、匿名AI401，5097／5107／5173／5180無listener。前端bundle仍index-DD-5sG5c.js，無Migration、正式資料或歷史model metadata變更；Raw重啟失效，Stage保留。README／Ollama／交接與規格同步，所有dirty工作保留，未提交推送。

## 2026-11-17 C2擴充顯示名稱

依使用者指定，CaseCodingCatalog.C2.Label改「Muscle Power（肌力與相關動作檢查）」，沿共享目錄更新圖例／tooltip／徽章描述，規準表文案同步。初始直接延長catalog Label使B／C的既有6500-byte prompt保護測試失敗；不放寬限制或動教學規則，改新增JsonIgnore選用PromptLabel、C2保持原「Muscle Power」模型名稱，prompt組裝採精簡名fallback，維持既有payload文字／v3與三profile。API目錄仍只有原有可見欄位，code及歷史不改。

相關後端19/19及標準Visual Studio含Vue建置通過，建置DLL直接核對C2 Label精確值；無實際模型呼叫／DB／Migration。確認原7173為本專案程序後更新正常bin/Debug PID39792，/courses200，其他舊埠無listener，前端bundle仍index-DD-5sG5c.js。重啟清Raw暫存，正式歷史保留。交接／規格／規準／Ollama紀錄同步；既有dirty工作保留，未提交推送。

## 2026-11-17 同色編碼邊界與行內徽章模式

使用者先提標點後立即澄清是B1／B2不同class同色底線的界線；未實作標點切割。於相鄰不同code集合的交界各留3px底線縮排，相同集合維持連續，保留原segments／文字／UTF-16 offset。接著使用者確認底線／Wikipedia式編碼徽章可切換；PhaseAnnotatedAnswer新增displayMode model及原生按鈕群組，預設底線。徽章模式每個有效顯示片段末端加綠底白字code，多碼共用徽章；純文字渲染、不畫底線，鍵盤／hover／touch沿原tooltip。CaseAnalysisChat及Studio以頁面ref保留輪次／版本間選擇，無storage／API／DB寫入；階段核對依模式同步文案，Raw不改。

先以同色邊界／徽章測試重現失敗，修正後Vue完整280/280、Chrome完整26/26、Vite成功。測試含原文去除顯示徽章後逐字不變、編碼／多碼／說明、XSS文字、不同code有間隙／相同code不切、鍵盤／手機touch／Escape、切回底線、輪次模式保留及既有chat／payload／spinner流程。首次手機新測試未開hasTouch而失敗，補上正確觸控context後新2項與完整26項通過，未更動產品行為。已檢視1440淺色／390深色合成圖，文件截圖同步。

既有7173無重啟，/courses200供index-DD-5sG5c.js；無後端／Migration／Prompt／LLM／正式資料改動，Raw暫存保留。交接／規格／回饋／UI文件同步，既有dirty工作保留，未提交推送。

## 2026-11-17 取消原文黃色、統一分類底線

使用者指出黃色削弱分類底線效果，明確選擇原文統一分類底線、目前階段只在標題標示。PhaseAnnotatedAnswer將底線集合改為全部有效phases，不再排除目前phase，移除原文背景及字色注入、保留current-phase metadata供核對。多碼保持疊線，選取細外框與分類按鈕仍照原規則；PhaseAlignmentNotice及提示文案同步，沒有改原AI回覆或資料。

更新既有component與瀏覽器斷言：Vue56/56、Chrome1440／390淺深色8/8與Vite通過，驗證透明底、原字色、目前phase底線、多phase疊線、互動及原文不變，已檢視390深色。7173無重啟，/courses200供index-CGkt_bT-.js。無後端／DB／Prompt／模型修改；交接／規格／UI文件與合成截圖同步，保留dirty工作，未提交推送。

## 2026-11-17 深色模式黃字取代黃底

依使用者截圖，PhaseAnnotatedAnswer在dark覆寫目前階段背景變數為transparent、字色#ffe066，同時影響原文及目前phase標題；淺色保留黃底。移除原本為深色黃底強制設深底線的segment變數，讓深色底線回用亮色；分類按鈕配色與資料不變，說明文案同步。相關Vue12/12、Chrome1440／390淺深色8/8與Vite通過，已檢視手機深色，UI文件與合成截圖同步。既有7173無重啟、/courses200供index-BsBZnsWa.js，無後端／DB／Prompt／模型修改；既有dirty工作保留，未提交推送。

## 2026-11-17 回饋輪次降冪排列

依使用者要求，CaseAnalysisChat新增由原rounds衍生的displayRounds，先保存原輪號再反轉，sidebar最新在上；原rounds及latest預選、回饋配對、效能／修改／diff不變。更新既有排序與輪號斷言，擷取測試由末項改取首項以對應新顯示。Vue40/40、Chrome1440／390回饋流程2/2、Vite通過，/courses200供index-CDgavGfJ.js。無重啟、後端／DB／模型改動。規格／交接與回饋文件同步，保留既有dirty工作，未提交推送。

## 2026-11-17 AI回覆區轉圈提示

依使用者要求，在CaseAnalysisChat既有回覆區追加busy專用assistant泡泡，沿用TutorAvatar及Lucide LoaderCircle，保留約110px內容空間、文字狀態與減少動態偏好。Raw與四階段送出／retry共用busy生命週期，成功／失敗／切換模式即消失，沒有訊息／持久化／API改動。初始空白文案在busy時隱藏。

deferred API測試先確認兩模式缺少placeholder，再修改後相關Vue40/40通過（含成功與錯誤保留草稿）；Chrome1440 Raw／390深色Stage、轉圈與reduced-motion兩項通過並檢視截圖。首次手機E2E因未等待切換完成、輸入被原有clearComposer清掉而逾時，修正測試等待正式階段按鈕可見後通過，無業務邏輯變動。Vite成功，既有7173無重啟，/courses200與index-DY5UQN2s.js核對；未改後端／DB／Prompt／模型。持久文件與回饋說明同步，保留既有dirty工作，未提交推送。

## 2026-11-17 四階段排除歷史上下文、直接檢視請求

追查使用者413：BeginTurn確實全讀session歷史組裝context。依新要求改只傳目前phase system＋本次作答，provider也排除先前user／assistant，Raw多輪不變。DB歷程、diff、統計、immutable metadata與授權保留。Prompt v3／原文與三profile不改，以保留既存失敗作答重試相容；無Migration。Stage 413與說明文案區分本次輸入及資料庫歷史，另新增staff本人「查看 Ollama 請求」直接開對應capture；學生隱藏，舊回覆不重建。

三profile封包先重現4則而非2則、dialog直接入口先失敗，修正後通過。隔離DB375/375（8輪長歷史仍每次2則，DB16則與8往返完整，拒絕後可retry）、Vue275/275、Chrome22/22；更新synthetic fixture只回本輪後另跑4/4桌面／手機淺深色請求流程並檢視截圖。schema finally刪除，039升級與舊資料指紋保留。標準VS含Vue通過，唯一7173由核對PID43760更新正常bin/Debug PID12164，/courses200供index-CDZEaQS1.js，舊埠無listener。無public寫入、實際模型推論或臨床品質驗證；重啟Raw暫存失效。交接／規格／聊天／Ollama／回饋文件與合成截圖同步，保留dirty工作，未提交推送。

## 2026-11-17 移除重複分類總列

依使用者圈選截圖，PhaseAnnotatedAnswer外層details改為具可存取名稱的section，移除頂部總summary及無用樣式，保留下方四個phase-group與說明。調整既有測試，直接操作各階段分類，不再先展開外層。相關Vue12/12、Chrome22/22及Vite通過，已檢視390深色合成畫面並同步截圖文件。7173頁面200且供應index-B7VvPu6H.js，沒有重啟、後端／模型／Migration或正式資料修改。agents／elearning／UI規格同步，保留既有dirty工作，未提交推送。

## 2026-11-17 圖例已辨識分類底色白字

使用者提供分類圖例截圖，要求作答提及的類別以分類色底＋白字呈現。PhaseAnnotatedAnswer從已驗證原文segments取碼，包含已知父類，標示category-mentioned與data-mentioned／aria-label；未提及維持中性，不以文字關鍵字猜分類。父類點選同步匹配子碼，已辨識狀態與selected按鈕狀態獨立。深色模式採相同深版A/B/C/D底色維持白字對比，黃底原文／目前phase圖例不變。

更新既有component與Chrome回歸檢查：相關Vue12/12、1440／390淺深色4/4、Vite通過，驗證已提及父／子碼、未提及保持中性、父類篩選、藍／綠底白字及黃底不變，已檢視桌面合成畫面。7173頁面更新index-KY1YZEZ9.js，不重啟，不修改後端／DB／模型。UI規格與合成截圖同步；既有未提交工作保留，未提交推送。

## 2026-11-17 效能dialog加入實際Ollama請求

使用者要求直接檢視建構請求，先讀clean HEAD7c24509、持久規格、既有readonly inspection及三模型profile。選擇重用request-scoped OllamaRequestInspection，以明確course access啟用conversation capture，在真正Serialize的HTTP body擷取，不用目前prompt拼湊歷史。新controller選用requestInspection僅成功本人send/retry/Raw可回，完成後重查teacher／TA；一般student、教授在課內僅student、preview及GET／review不提供。無DB schema／模型profile改動。

新增效能dialog統計／Ollama請求頁籤，可讀messages／schema／sampling、完整payload及複製。單次JSON是實際payload，不是inspection wrapper，多次明示陣列。原只有closebutton的Tab處理改為正確循環新頁籤／copy／code focus，保留Escape、backdrop及焦點回復。CaseAnalysisChat以獨立頁面map按replyTo/requestId或Raw session/index綁定；不進messages／後續模型context／localStorage，generation及帳號／角色／view變更阻擋晚回覆，Raw清除／失效與離開釋放。原AI內容、efficiency及Raw正式統計不改。

TDD先重現缺少capture入口／新UI；後端一般318通過、53外部DB條件略過，另授權獨立schema完整371/371及舊升級指紋通過、schema刪除。新測試比較實際HTTP serialized body，含teacher/TA平台普通Member、Professor課內student、preview、完成後demotion、冪等重送、GET無capture、DB無wrapper；舊Raw default不capture／無schema/system與三profile保持。Vue273/273、Chrome22/22（含5新請求流程、1440/390淺深色／copy／old-unavailable／student隱藏）、標準VS含Vue成功，已檢視合成截圖。獨立review無安全／正確性blocker。此輪無實際LLM呼叫，無模型品質聲明。

擷取僅本次HTTP scope與頁面、URL移除憑證/query/fragment，request去thinking且conversation不擷取response body／headers，不入DB／log／diagnostics。原服務端／模型內建行為及可能留存限制不變。唯一7173核對後由本專案PID42896更新正常bin/Debug PID43760，/courses200、新bundle index-CFfvnS5W.js、匿名401，5097／5107／5173／5180無listener。README、Ollama及feedback-rounds文件同步；沒有正式資料寫入、commit或push。

## 2026-11-16 相符分類改黃色

使用者反映淡色標註與作答容器混淆，要求目前階段統一黃色。PhaseAnnotatedAnswer改不透明#ffe066背景、#332a00深色字，legend目前phase同色；篩選淡化採不透明淡黃，避免深色模式失去字色對比。跨phase底線不變，黃色內混合碼使用深版底線顏色。更新既有樣式斷言與四個瀏覽器顏色檢查，沒有新增鏡像測試或更動業務。

初次從repo根執行Vitest未載入Vue設定、無測試執行；改回ClientApp後相關10/10通過，Vite及Chrome1440／390淺深色4/4通過，已檢視手機深色混合標註。僅前端，不重啟既有7173，/courses200提供index-DSmCZuoZ.js；無後端、Migration、模型或正式資料操作。UI規格與合成截圖同步，未提交推送。

## 2026-11-16 B內A錯置回饋及相符背景色

使用者以B階段提交病前內容，AI辨識A卻只稱讚，要求錯置底線、目前B背景色。追查發現元件未接目前stage，只能按碼一律畫線；Prompt無病例用unverified的指示又可能掩蓋獨立的階段錯置。先寫失敗Provider三參數／UI測試，再加明確stage-aware相容overload（舊Provider二參數保留），workflow由已授權stage傳currentStage/currentPhase/label。Studio由saved answer.stage提供同資訊，四階段方法v3置前目前階段、要求主要錯置要說明且必要背景可留；未改模型profile／抽樣、Raw或正式業務流程。

共享UI依目前stage將相符碼做淡色背景、他phase細底線，合法多碼可兼具；tooltip保留事實未核實與unsupported／misplaced。另在AI回饋區加明確系統來源PhaseAlignmentNotice，只依exact原文通過的annotations及當前stage列核對，不改舊AI文字，不把無B標註推成無B內容；未標記不算缺漏，背景引用不一概判錯。相同版次、教師查閱、Raw隱私及安全文字渲染保留。

最終Vue265/265、一般後端312通過／50條件略過、隔離DB完整362/362（含stage實傳、授權／immutable／舊升級）、Chrome17/17及標準VS含Vue成功，獨立review無程式／授權blocker。已檢視1440淺色及390深色，合成截圖更新docs/ai-tuning/screens；沒有Migration或public資料寫入，測試schema清除。

IAN02 llama3.1:8b實際全合成三例，原始使用者畫面內容未複製到測試／日誌。首輪3/3格式成功但混合背景仍過度判錯；補強措辭後最終2/3有效回覆，能提目前B與A分類，但仍錯列未寫子類／不實時間問題，混合例失敗。詳03最新段落，不能以程式通過宣稱模型教學品質合格，不改用硬編造臨床回覆。所有檢查均不寫正式學生聊天或成績。

核對並替換原本專案7173程序後，正常bin/Debug PID42896、僅7173，/courses200與index-CQCWQczi.js確認，維持IAN02。02／03／06及agents／elearning同步；保留既有dirty工作，未提交推送，本次止於階段核對與顯示改善。

## 2026-11-16 四階段顯示ABCD

使用者要求依教授文件在階段名稱前標ABCD。修改後端共用AnalysisStageCatalog.Label與前端questionStages.Label；自動涵蓋課內聊天tab／下一階段／教師查閱、既有案例學習／維護與其他分析選單。StudentCaseView移除①–④避免雙重編號。既有聊天測試fixture也改成正式完整標籤，確認下一階段及師生閱讀仍正常；不新增鏡像測試，不改stage keys／Prompt／DB／授權／模型設定。

Vue256/256、相關後端24/24、Chrome聊天／註記13/13與標準Visual Studio含Vue建置通過。唯一7173由核對後的本專案PID31760更新正常bin/Debug PID44276，維持IAN02 llama3；/courses200、新bundle index-B6vVQs2p.js，5097／5107／5173／5180無listener。合成UI截圖同步更新；無Migration、資料寫入或模型呼叫，文件同步、既有dirty工作保留，未提交推送。

## 2026-11-16 由Taipei切回IAN02，保留雙設定

使用者回報Taipei服務不可用，要求切回IAN02 llama3，隨後指定保留兩份config方便切換。先讀持久文件／Git、Ollama組裝與全部profile；Local沒有Ollama覆寫，Process/User/Machine相關Profile／BaseUrl／Model三鍵無覆寫。IAN02服務實查0.40.16及llama3.1:8b存在，未嘗試更名／下載模型或覆寫Breeze相容規則。

appsettings只設預設Profile=llama3，移除基礎層重複Breeze端點及模型，實值由獨立profile載入。Properties/launchSettings新增https-IAN02-Llama3及https-Taipei-Breeze，均為https://localhost:7173；既有https去除額外5084綁定。既有三份Configuration/Ollama無差異。VS切換需停止再選另一profileF5，CLI亦可--launch-profile指定。README、Ollama、agents／elearning同步操作及限制。

9項三profile封包回歸全過，隔離工具build與標準Visual Studio MSBuild含Vue成功。實際IAN02以合成資料完成2Raw＋1Stage，確認多輪與逐筆效能／回饋JSON；內容未輸出、不建立DB歷程。未重跑完整教學品質／教授評估，不將先前Breeze實測判定當成Llama表現。沒有Migration、作答／成績資料寫入或模型profile payload修改。核對原7173為本專案PID33528後更新正常bin/Debug PID31760，/courses200，未另開埠。Raw暫存隨重啟清除，Stage歷史保留，新模型應新開對話。保留所有既有未提交工作，未提交推送。

## 2026-11-16 教授四階段對齊與互動分類標註

使用者授權兩份任務、提供四份DOCX；先核對dirty main及持久文件、三模型profile與現有CourseChat／Studio。參考文本含表格段落、07流程20則註解且無圖片；不將原病例全文放進通用prompt／tracked文件。採較晚明確編碼定義，保留參考演進差異：A2c／B2c no coding、C5不分細碼、B4歷史／C1目前、實際照顧與聯絡角色。43碼共用catalog，四階段方法與五種按需回饋，保留不代寫／無成績／證據限制。文件01–06記錄規準、變更、實測、疑義、下一輪及UI，流程註解的最終版／採納率等未擅自擴充。

課內CaseFeedbackAsync透過既有Ollama傳輸及抽樣提供內部JSON envelope，最新user包成decoded answerText＋格式提示，DB原文不改；Raw不包裝、不帶schema或system。Stage公開content仍純文字，另保存annotations；Studio維持原四欄並追加optional annotations，使用既有caseFacts和精確answer.Text。共享Vue標註元件依原版及replyTo／answerVersionId顯示，union範圍不重複文字；純文字插值、四色細線、無color-only語意、hover／focus／touch、Escape及圖例強調。教師讀回、手機深色／長文及原編輯流程保留。

TDD先重現缺少contract／UI、quoteanchor與舊prompt失敗，實作後Vue256/256、一般後端310通過／49條件略過；授權group_test schema完整359/359、036→037→038→039含既有成績／聊天指紋及重跑通過，schema已刪。Public039已交易套用，僅nullable annotations與不可變trigger，沒有改歷史答案／成績或編造舊標註。Chrome20/20合成API流程（13聊天／標註＋7師生／成績）及標準VS含Vue成功，已檢視1440淺色／390深色，截圖為合成fixture。完整歷史E2E／實機螢幕閱讀器／其他模型即時品質未跑。

實際公司Breeze多次合成測試揭露JSON示例複誦、英文及不可靠數字索引；移除具體示例、明確目前answerText，最後AI改逐字引用text＋0起算occurrence，由server嚴格轉UTF-16，不修補無效offset／混用表示。最終實際12次格式成功，9筆引用採用／22筆拒絕、往返0.863–6.175秒。逐筆開發者檢視仍見錯誤GCS、虛構時間、不實遺漏及修訂問題，**模型教學品質未通過**；程式成功不代表專業驗證，不擅自換模型／profile或硬編造正確回覆。無教授病例／學生答案送外站或寫測試日誌，合成測試無DB歷程。

完成時唯一7173更新正常bin/Debug PID33528，/courses200及index-m5Icu7gl.js、匿名課內AI401，5097／5107／5173／5180無listener。兩次獨立review未見程式／授權blocker。README、Ollama及agents／elearning同步；後續仍重查環境，保留所有既有dirty工作、無提交推送，不再延伸新AI模組。教授需審閱實際輸出、指定可信來源與少數編碼歧義；詳03／04／05。

## 2026-11-16 補充教師練習提示

依使用者指定，在教師／助教本人練習／不計正式統計說明後追加「各階段系統目前為 AI 提供，尚未經過專業調教與驗證。」只改CaseAnalysisChat顯示文案，規格與交接同步；Vite成功，7173 /courses200提供index-BddY0QGk.js。無後端／Migration變更，未為單純文案新增測試。

## 2026-11-16 AI回覆泡泡與心理健康教授剪影

依使用者視覺要求，CaseAnalysisChat新增message-author標題列，僅assistant顯示TutorAvatar原創inline SVG（眼鏡／書本／腦部徽記）。AI回覆改12%青綠混合主題surface底、柔和邊框與18px不對稱圓角，行高1.9；Raw／正式／教師讀回共用，不改學生泡泡。SVG依主題accent／surface著色，42／48px響應尺寸，裝飾aria-hidden，不假稱真人或更動提示詞。

Vue60檔241/241、Chrome8個聊天流程與Vite成功，已檢視手機泡泡／頭像，效能與diff／修訂仍正常。既有7173 /courses200提供index-DFncjnAJ.js，不重啟後端、不增Migration或新埠。文件同步記錄視覺規則，保留所有既有未提交變更，未提交推送。

## 2026-11-16 AI回饋輪次、作答diff與效能dialog

依使用者截圖與補充，確認「任兩版prompt」是學生作答／提問。沿既有五模式新增四階段feedback sidebar、user/replyTo配對閱讀、從選中原文修訂、新requestId與明確parent、修改次數及手動下一階段。老師查看維持唯讀；沒有Prompt編輯／自動評分／過關。原文與metadata不可變，retry沿同user不重複；所有既有未提交變更保留。

原生dialog顯示每筆AI的Token、tok/s、模型及往返耗時（ns／ms正確換秒）；舊無資料保留NULL。新Conversation回覆解析逐項AvailableFields，避免缺少快取Token等欄位時把default0當實測。只存白名單數值／結束碼。Stage持久化與assistant原子保存，Raw只在原LangChain entry暫存，不入DB／模型context／正式往返統計，清除或expiry同時釋放。三profile／outbound payload不改。

Student diff採prefix/suffix＋250k cell有界LCS，Unicode codepoint／空白可完整重建，過大明示整段刪增fallback，純文字防XSS、不送LLM。任選同學生／課程／stage歷史兩版，切換學生／階段會清除對話框。Review找到覆寫／切換會丟未提交草稿，補confirm與取消還原後無blocker。

038已交易套用public。036→037→038含既有user/assistant、名冊及已發布成績升級／重跑指紋不變，舊metrics與parent為NULL，所有隔離schema已刪。後端一般283通過／47條件略過，另DB完整330/330；Vue60檔241、Chrome15流程（其中8個聊天，含1440／390 native dialog／diff／草稿鍵盤）及標準VS含Vue成功。同Provider合成2Raw+1Stage成功，逐筆效能可用性確認且未輸出內容／種DB聊天。測試先紅後綠，無待修失敗。

核對舊7173 PID27380執行檔後更新，新正常bin/Debug PID26108僅綁7173；/courses200與index-s9KPMSBt.js確認，5097／5107／5173／5180無listener。README、五模式與Ollama文件、agents／elearning及[本次交付](../docs/feedback-rounds.md)同步。未提交推送；完整歷史E2E、獨立TS檢查（未設定）及教學品質未測，原基礎設施隱私限制保持。

## 2026-11-16 五模式個案分析聊天整合

使用者提供附件與Raw LangChain修正，要求直接實作。先盤點clean main 7234dc5及codex/pre-vue-ollama 66b67953e070e2c45244b7c176566703c444f1fd，重用LangChain.Core0.17.0 ChatMessageHistory／ConversationBufferMemory與四階段舊無分數提示；舊Buffer受40則截斷、Summary自加system、Session缺課程scope，未直接沿用。新CaseChatPrompts適配校名、多輪與無通過判定，版本legacy-66b6795-chat-v1。現有有分數評閱、三profile、作業測驗流程保留，沒有新AI自動分數或解鎖。

Raw每user/course/randomID獨立暫存成功完整pair，無DB／summary／browser storage／HTTP Session／內容log／正式統計。新ConversationAsync共用Ollama transport，同模型／sampling且無隱含system／schema／diagnostics／thinking。20m閒置、每人3／全站200；清除／expiry取消inflight，重整與BFcache新ID。Pagehide DELETE為best effort，遺失舊ID429時需明確確認才清本人所有暫存，不自動干擾其他分頁。

四階段DB先存原user，再無交易推論，成功保存assistant才算往返；requestId／replyTo唯一、租約與attempt守衛防並行／重試重算及舊回覆覆寫。錯誤僅存通用code。原文字含空白保留，超context413保留history，不偷偷摘要／截斷。學生限本人，教師／statistics能力TA查本課正式名冊、階段／全課次數與完整歷程；教師practice另存且不進正式統計。課程擁有者設定開關。

037已交易套用public，既有課預設opt-in。沿用現有個案分析課程A／B兩門啟用，115上等義轉115-1留audit，IDs／名冊／教材／作答／成績保留。工具preview2、apply2、audit0、再跑0，不建平行課、不種假聊天。隔離036舊課與已發布83分升級037／重跑指紋不變，demo啟用保留成員成績。

最終Vue57檔208通過、後端一般280／45條件式略過，另完整隔離DB325/325無略過；Chrome12流程（五個新聊天＋七個既有課程／批改）與標準VS MSBuild含Vite通過。真實同Provider合成兩輪Raw（4則memory）＋一輪Stage收到回覆，無DB內容寫入／輸出。全DB回歸揭露兩個舊CourseUnitTests JSON大小寫fixture錯誤，改Web options後通過；舊資料fixture補必填weight。審查另重現並修正Raw中途撤權、刷新孤兒context、空白retry與課程設定並行40P01死鎖；新增lease過期／晚回覆測試。各次隔離group_test schema均刪除。

唯讀實查目前Ollama0.40.11／Breeze有模型System、template與parameters；無主機／代理管理權限，不能保證遠端日誌／APM／swap等不留內容。正式案例、規準、PHI治理及模型教學品質仍待確認。唯一7173更新正常bin/Debug PID27380、/courses200、新bundle index-nrJjhDuj.js、11組case-analysis API與匿名401確認，其他舊埠無listener；未提交推送。README、Ollama、elearning／agents與[交付](../docs/case-analysis-chat.md)同步，設計／執行文件在docs/superpowers，後續仍核對執行狀態。

## 2026-11-16 當學期依系統日期預設收起歷年課

使用者回答11/13學期待確認問題：依系統當年日期即可，功能只為預設收起已結束學期。AcademicCalendarOptions新增共用判定，Controller沿用既有TimeProvider／授權回傳當學期；null／空白自動，明確值仍相容覆寫。採台灣UTC+8、既有民國學年度，預設8月1日上學期、2月1日下學期，兩月份可設定且驗證先後範圍；暑修保留手動查詢。此為可調整產品預設，非宣稱校方正式上課日。2026/11對應115-1，跨年1月仍同學年度。只改預設篩選，不更動課程status、成績或授權，舊課仍可歷年篩選。

若本學期沒有課但有歷年課，CoursesView空狀態改提示「目前篩選沒有符合的課程」與歷年查詢方式，避免誤稱沒有修課。保留所有既有未提交變更。README、elearning、agents與原交付報告新增11/16現行規則，取代早先「必須人工指定／未設定」待辦；歷史決策仍保留日期脈絡。

先新增日期／月份設定／權限API回歸，確認缺少實作失敗；空狀態測試亦先重現失敗。最終後端266通過、35條件式DB略過；另授權group_test隔離schema完整001–036及21項AcademicCalendar測試通過、0略過，完成即刪除。Vue54檔188項、Chrome師生首頁／當學期歷年／分享共4流程通過。標準VS MSBuild含Vite成功，無Migration／正式資料寫入。確認7173舊PID31956為本專案bin/Debug後更新，新PID21464僅綁7173；/courses200並提供index-ozAiTLef.js，未登入calendar401，5097／5107／5173／5180無listener。未提交推送Git，未擴充其他功能。

## 2026-11-13 CSMU 會議 UI／UX 修正與學生Email

最終檢查補齊舊靜態教材歷程的媒體類型標記：原事件／歷史值保留，畫面不再顯示為觀看秒數；共用影片簽名判斷。相關回歸先重現失敗再修正，Vue最終187通過，30項DB與9個Chrome流程重新通過。本次3個隔離建置輸出目錄已清除，保留唯一7173執行日誌與驗證摘要。

依使用者11/13逐項需求盤點並增量修正：學生管理狀態／繳交比例進度／重複總數移除，安全原始附件名與格式下載，靜態教材後端不再計時，分享登入返回本機教材，明確AcademicCalendar設定與學年／學期篩選，小組換組長／取消指定／增減成員保留原加入時間，檔案繳交作業命名與PDF20MB限制提示，CSMU品牌及現行說明文件。原未提交checkbox／radio修正保留，未引入UI框架或擴充AI／個案功能。

當學期未獲校方指定，設定null並明示，不猜日期。舊靜態門檻原秒數／已達標／開始依據保留，未達標者採正式點閱確認；新要求僅能選影片。無新Migration。使用者另要求學生帳號改校域：19個明確純學生登入原地改csmu.edu.tw，Email與normalized login同步交易更新，碰撞先拒絕，原密碼／角色／ID／學習歷程保留，逐筆audit；教師／TA／管理員排除。public preview19、apply19、audit0、再跑0，未建立校方信箱或舊Email別名。

Vue54檔187項、Vite、.NET253通過／35外部DB條件式略過；授權隔離schema完整001–036及30項DB整合通過，另學生Email碰撞回滾／preview／原密碼雜湊驗證／教師排除／可重入通過，測試schema已刪。Chrome9流程（含師生、1440／390、分享登入返回、靜態附件、當學期與歷史暑修、評分）通過。Code review找出舊示範帳號啟用工具可能只改顯示Email而未改登入key，已修正為已有憑證時不擅自更名，由專用交易工具處理。未執行完整歷史E2E、獨立TypeScript檢查（專案無設定）或外站YouTube實播。

標準Visual Studio MSBuild（含Vue）成功。核對舊7173 PID17604為本專案bin/Debug後停止，僅以新版同路徑PID31956綁7173；/courses200、CSMU title、index-BnImiIQw.js及academic-calendar／term API均確認；5097／5107／5173／5180無listener。完整結果、主要檔案、Demo步驟及業務待確認見 [CSMU交付](../docs/ux-corrections-2026-11-13.md)。README、學習歷程規格及agents／elearning同步最新規則，保留早期決策時間脈絡；未提交／推送Git。

## 2026-11-13 全站核取方塊與標籤位置修正

使用者要求全站checkbox及label位置一致。查明通用input寬100%、padding及label input的margin-top流入原生選項；只有部分標籤使用choice-label，其他混用grid／block，甚至另有inline display:block。先以真實PDF建立頁的合成API瀏覽器測試重現：原checkbox寬1034px，預期18px而失敗。用Vue模板AST盤點並改好12個Vue檔案21處choice宣告，保留綁定與權限，補choice-text文字容器，統一18px、首行對齊、10px間距、整列可點及44px手機列。補原生focus-visible；同分意願radio加相同submission的name以支援方向鍵。平台帳號列操作允許換行，舊MVC思考開關尺寸／aria-label亦更新。

驗證：Vue48檔165/165、Vite production build、.NET隔離OutDir編譯0警告錯誤。新增桌面1440px與手機390px Chrome檢查PDF個人／小組、長組名、教材門檻、命題選項、助教權限、帳號建立／表格及學生同分意願，測量18px／文字右距與首行中心差，驗證label文字點選、Space取消、focus outline與radio方向鍵；與既有師生／小組批改／紙本登分／成績矩陣共7流程全過。初次測試的精確select標籤名稱包含option文字導致定位逾時，改用現行語意定位後通過。已檢視桌面與手機截圖，無整頁橫向溢出。

只改呈現及原生鍵盤分組，不改資料、授權、計分或Migration。既有7173 PID17604保持，前端bundle index-ea1aQqqd.js；舊MVC樣板已編譯驗證。agents／elearning同步更新，未提交Git。

## 2026-11-13 清理固定展示評語與檔名

使用者指定目前教師評語中的「Codex 示範批改」及相關不自然展示標記。先盤點只有資料種子產生這些字樣，前端／正式API沒有固定Codex文案。新增 DemoPresentationText 精確規則及 Development-only DemoPresentationCleanup 工具（preview／apply／audit），只依線上 assignment seed 事件及 PDF codexDemo 事件確認來源，避免修改一般教師文字或正常提及 Codex 的檔名。移除固定前綴與 PDF 評語的展示草稿尾註，確定性檔名改作業報告.pdf，互評／意願去展示前綴；未來種子同步修正。

public 單交易完成 grades 125、report_submissions 72（含草稿／公開評語與檔名）、report_peer_reviews 216、report_votes 6，共419列；複查0待整理、再套用0修改。操作逐筆新增文字修訂稽核（欄位與前後雜湊，不存敏感內容），原種子／評分事件不改寫。前後保護欄位雜湊一致，分數、發布值／時間、評分者、PDF內容bytes及作答歷史均未變。不可變交卷快照仍保留原展示尾註，不停用trigger或重建交卷。

新增純規則測試先編譯失敗後通過；後端最終234通過／28條件式略過、PortalSmoke建置0警告錯誤。獨立group_test_* schema實跑預覽／套用／稽核／重跑，驗證無標記但含相同前綴者不變、NULL保持、分數與發布、PDF bytes、不可變快照及原始audit保留；新增5筆維護audit，重跑0，schema已刪除。合成fixture初次漏attempt_no與submitted_at，補齊後通過。無Migration，沒有修改正式畫面計分或重啟7173，資料重新整理即生效。agents／elearning及維護文件更新，未提交Git。

## 2026-11-13 成績總覽改評量列、學員欄

使用者希望把總覽的作業放 rows、學員放 columns。GradebookPanel 教師總覽改為所有篩選評量的逐項對照表，包含 PDF／線上作業、測驗及紙本考試；首欄顯示活動連結、單元、類型與滿分並固定，上方學號／姓名亦固定。各格顯示原始分數及發布狀態，0 不視空白，草稿不混成公開值。原作業／測驗／平時／正式考試分類平均改為表尾列，另顯示全課程暫算及公開快照；篩選範圍與全課程範圍明確分開。可鍵盤聚焦的表格區獨立捲動，說明文字放外側避免水平捲動時被裁掉。得分查詢按 item/student ID 建 Map，避免寬表每格重複掃描完整分數清單。

先修改舊總覽斷言取得預期失敗，再驗證多位學員分數欄位對應、0 分、缺分、另有草稿、公開零總分及單元篩選。Vue 48 檔 165/165、Vite build 通過；Chrome 8 位學員矩陣驗證固定欄（水平捲動 420px 仍保持位置）、學員欄數、得分與單元篩選，加上既有師生流程共 3 項全過，390px 無整頁溢出。初次多學員 fixture 漏掉既有 itemAverages 欄位，補齊後重跑通過。已檢視桌面與手機畫面。

無資料庫、後端或發布算法變更，不需 Migration；既有 7173 PID 17604 未重啟，新 JS index-C6K-xmxA.js。agents／elearning 更新記錄此顯示方向取代五欄摘要；未提交 Git。

## 2026-11-13 Portal 每頁預設五筆

依使用者要求，在共用 PortalPanel 加入每頁 5／10／20／50 選單、局部清單分頁與 remote 分頁事件；用具名稱的 select，收合時可見，阻止點選／鍵盤事件冒泡造成誤切換。OverviewView 所有資訊面板與 PortalActivity 訊息／行程均接入，換筆數回第一頁。學生待辦與訊息會實際以 page/pageSize 查詢，換訊息頁不重置行程分頁；教師討論與最近繳交移除 slice(0,6)。維持原近期 12 筆活動的成績摘要及行程 100 項來源範圍，不宣稱新增全量成績 API。每頁筆數在新進首頁時回預設 5，收合偏好照原規格保存。

新增失敗後通過的分頁測試涵蓋 12 筆資料按 5／10 顯示、最後一頁不漏、remote 換筆數回第 1 頁、學生待辦實際換 API 頁及訊息／行程相互獨立。最終 Vue 48 檔 164/164、Vite build 通過；Chrome 師生 2 個 fixture journey 驗證所有選單預設 5、收合時改成 10 仍保持收合且送出 API pageSize=10，鍵盤、reload 及手機無溢出仍過。已檢視新桌面／手機截圖。

瀏覽器初跑發現 7173 connection refused；確認無 listener 後重啟既有 bin/Debug，PID 17604，僅使用 https://localhost:7173。未修改後端、資料庫或開第二環境。最終 JS index-PJij9hnb.js；agents／elearning 同步更新，未提交 Git。

## 2026-11-13 Portal 原生收合面板

使用者希望資訊豐富的 Portal 每個區塊可收合。新增共用 PortalPanel，沿用既有設計與 Lucide chevron，以原生 details／summary 提供整列點擊、鍵盤、持續可見的標題與數量；接入 OverviewView 全部資訊面板及 PortalActivity 訊息／行程。預設展開，localStorage 依帳號 ID、教師／學生視角及固定面板 key 存開關值，無帳號不寫、儲存被禁不阻擋操作；不存課程內容。內容保留 DOM，避免關閉後重掛 API 或遺失表單／篩選。摘要捷徑可再次打開已收合的待辦／批改／討論，原錨點導航保留。兩欄面板頂端對齊，關閉時不被鄰欄拉長。

先確認缺元件的失敗，再加入狀態保存、帳號／視角隔離、儲存例外、內容保留及錨點專項。最終 Vue 48 檔 160/160、Vite production build 通過；既有 7173 上的教師／學生 2 個 Chrome fixture journey 加入 Enter／Space、收合後數量、reload 還原、摘要展開及桌面／390px 手機無橫向溢出，全數通過。已檢視完整收合的桌面與手機截圖。沒有後端或資料庫變更，沒有重啟程序／新增埠；新 JS index-BO9310bi.js，由既有 7173 提供。agents／elearning 同步更新，尚未提交 Git。

## 2026-11-13 會議差距：訊息、行程與基本評分

使用者核准差距示意後，補齊跨課活動流與行程、持久化課程公告、同成績簿的紙本期中／期末考後登分、左側選組的 PDF 批改，以及課程首頁本課進度。基本小組流程先填基礎分、全組同分，已有個別調整會自動展開且收合／儲存不遺失；互評與同分意願獨立收合，不自動計分。正式新增 PDF 個人／小組整班發布 API，核對完整名冊／指定組、快照、分數及版本後同交易同步成績簿與稽核；單份發布拒絕。缺交、退回及空白不偷偷變成零分。

公告管理沿用 materials 能力（助教權限標籤改教材與公告編輯），封存／預覽不可寫；新公告也列入安全刪課依賴。跨課 feed 僅授權範圍，學生只有本人公開成績事件，不含分數或私密評語。行程含一般評量及線上／紙本正式考試；審查發現線上考試原連結誤落紙本區，已拆 onlineExam query、自動展開線上區且只選本課授權清單 ID，外課 ID 不發明細請求。

Migration 036 已套用 public 並 COMMIT：新增 course_announcements、選填 scheduled_at、course_grade_scores.published_at 與維護 trigger。歷史公開分數以 updated_at 近似回填時間，既有分數及發布快照未改；之後未發布草稿不會更新公開訊息時間。獨立 group_test_* schema 實跑 035→036 舊資料升級，項目／歷史權重／草稿與公開分數雜湊一致。13 項新 DB 整合全部通過（公告／角色／跨課／發布時鐘 4、紙本整班登分 4、PDF 全班發布 5），包含失敗後真正交易回滾；schema 均已刪除。初次 DB run 揭露兩套新 fixture 漏填舊 weight NOT NULL，已修正後重建重跑。

最終 Vue 47 檔 157/157，後端一般回歸 231 通過／28 需外部條件略過；13 項上述 DB 測試另實跑全過。Visual Studio MSBuild 正常 Build 自動建置 Vue，0 錯誤；Chrome 4 個 fixture journey 通過（師生首頁／公告／成績／單元、小組 PDF、紙本全班草稿與發布），已檢視桌面及手機截圖、檢查整頁無橫向溢出。瀏覽器使用合成 API，沒有在真實學生資料執行交卷或發布。

精確停止原 VS app PID 36860，保持 Visual Studio 開啟；新版由 bin/Debug 啟動 PID 28360，只綁 https://localhost:7173。session 200，/courses 載入新 production bundle；5097／5107／5173／5180／5084 無 listener。沒有新增工作樹或平行站台。操作流程、假設與後續項目見 docs/meeting-followup-2026-11-13.md；文件同步更新 agents／elearning，尚未提交 Git。

最後視覺檢查修正首頁課程篩選標籤斷行，Vite 與 4 個瀏覽器流程重跑通過；最終 JS 為 index-DC1ODivo.js。暫存 exam／report／meeting 編譯與升級目錄均清除，保留忽略的瀏覽器截圖供驗收；執行站台來自正常 bin/Debug，沒有依賴暫存輸出。

## 2026-11-13 移除評分項目權重

使用者要求權重留在測驗命題層，並明確確認課程平均採每項百分制等比平均。GradebookPolicy、分類摘要、練習平均及發布總成績 SQL 均移除 course_grade_items.weight 因子；未評空值排除、0 分計入，發布只採已發布分數並保留調分。既有 published_total 不改寫，下一次教師明確重新發布才採新算法。相容 input Weight 固定回傳 1、忽略舊 payload；歷史資料欄位不刪、不批次更新。學習歷程仍讀已發布總分快照。測驗配分／題內評分規準未改。

驗證：Vue 41 檔案 134/134、Vite production build、後端編譯與 217 測試通過／19 條件式略過。另於獨立 group_test_* schema 套用 001–035，實跑 2 項資料庫整合：舊權重 30/70 得分 10/20 與 80/100 的平均為 65，已發布快照 71 保持，明確重新發布後為 65；前一項 YouTube 教材按課程與型態計數亦通過。測試 schema 已刪除。Chrome 兩個 fixture journey 通過，新增成績頁不含權重／加權文案的斷言。既有 VS 程序不重啟，使用者須重新 F5 載入新的後端算法。

## 2026-11-13 數學改英文後的討論內容調整

唯讀確認英文課 f7ffac9b-a7e0-437e-bc2f-cd3a517a1679 仍有原數學百分比／平均數／估算示範討論，依使用者要求原地更新 3 串與 9 則確定性 demo 回覆，內容為 do/does、一般過去式 did + 原形與咖啡店情境對話。交易保留作者、IDs、投票及關閉狀態，沒有更新其他討論或課程。增加 Development-only 指定課程 refresh／preview／唯讀 audit；未來一般 seed 也識別英文課名。工具建置 0 警告／錯誤，資料庫 audit 逐一核對 3 串標題／問題與 9 則回覆全文通過。原全課程 audit 仍可能因其他已改名課程而失敗，本次只宣稱英文課專項 audit。

## 2026-11-13 我的授課顯示 YouTube 教材

使用者希望課程清單即可辨識含影音的課程。CourseSummary 增加預設 0 的 YouTubeCount，同課 course_resources 按 youtube 型態計數，List／Get 均提供；沿用原 membership 隔離。授課卡片增加 Lucide Video 圖示與實際部數，零部隱藏。沒有 Migration。前端 132/132、Vite build 通過；新後端在 ignored youtube-verify 編譯／測試為 215 通過／19 條件式略過。新增課程隔離／型態計數整合案例，但未設 FORMATIVE_TEST_CONNECTION，此 DB 案例未實際執行。保持既有 VS 程序，使用者重新 F5 後才會載入新增 DTO。

## 2026-11-13 11/15 展示 UX 修正

依使用者完整 UX sweep 要求，重用既有課程／單元／通知／成績／學習歷程／PDF API，將登入導向個人儀表板，補全域歷程及帳號選單 Portal 入口；課內歷程改為課程學習進度。成績介面由活動橫向膨脹改為摘要與可篩選明細，學生取消重複表；保留既有相對權重與課程調分。小組回饋與教師成績分開呈現、增加全名冊與載入／重試；單元儲存失敗保留編輯內容。沒有 Migration、後端或模型設定修改。

展示資料唯讀 audit：成績線上 151/151、成績簿 151/151、PDF 72/72、未發布小組成員 0；舊討論 audit 因「個案分析課程A」不在內容對照中失敗，未覆寫討論。後端一般測試建置被 Visual Studio 正在執行的 apphost PID 35060 鎖定，改以無重新建置模式執行既有後端測試，214 通過／19 條件式略過。實作、瀏覽器驗證範圍及未完成項目記錄於 docs/ux-sweep-2026-11-13.md。

整合驗證為 Vue 41 檔案 130/130、Vite 2023 modules production build 通過。完整 VS MSBuild 改至 ignored ux-verify 暫存輸出以避開執行中 DLL 鎖後通過，沒有啟動第二服務。Chrome 兩個教師／學生 fixture journey 通過，測試 Portal 選單、課程／單元、成績、跨課歷程與 1440px／390px 無整頁溢出；已實際檢視教師首頁與學生手機成績截圖。視覺檢查後將手機學生成績改為完整六欄卡片，避免得分被橫向裁切。討論儀表板連結加入同課授權清單內的 thread 深連結，新增跨課 ID 不會送出明細請求的測試。

最終獨立審查另修正成績連結一般作業誤用 quiz-only 路由，新增回歸後完整 Vue 131/131，Vite 重建成功。審查無其餘 Important／Critical 問題。此次沒有執行登入後交卷／調分／發布的資料庫寫入 E2E。

## 2026-11-12 修正 Visual Studio npm 9009

Visual Studio F5 首次使用一般 Build target 後回報 `npm run build` code 9009。查核 csproj 並無 `npn` 拼字錯誤；系統 PATH 只有 Codex runtime 的 Node，沒有 npm，但 Visual Studio 2026 Community 自己隨附 npm 11.19.0 於其 MSBuild `Microsoft\VisualStudio\NodeJs` 目錄。根因是該目錄未加入 Visual Studio build process PATH。

採版本與 edition 無關的修正：由 `$(MSBuildExtensionsPath)` 衍生 Visual Studio npm 路徑，存在時設定可覆寫的 `NpmExecutable`，否則退回 PATH 上的 `npm`；restore 與 build Exec 共用並引用該值。TDD 先將假 npm 移出 PATH、提供含空白的 Visual Studio npm 路徑，確認舊 target 精確回傳 9009，再完成 fallback 後通過。Visual Studio 18 Community 的真實 MSBuild property 解析到 bundled npm 並完成 Vite／.NET Build；完整 Vue 100/100、.NET 214 通過／19 個條件式略過。沒有安裝系統軟體或寫死本機 Visual Studio 路徑，7173 保持關閉供使用者 F5。

使用者實際 F5 後進一步出現 npm code 1；Visual Studio Output 的第一個錯誤為 `"node" 不是內部或外部命令`。根因是 bundled `npm.cmd` 本身可用相鄰 node 啟動，但 `npm run build` 執行 Vite shim 時仍由 PATH 尋找裸 `node`；先前 Codex shell PATH 恰有 Node，掩蓋此差異。回歸測試因此升級為 PATH 同時沒有 Node/npm、bundled 路徑含空白且 npm package script 必須再次解析 node。csproj 現只對兩個 frontend Exec 將 bundled NodeJs 目錄置於 PATH 前端，並以 MSBuild Escape 保留完整既有 PATH；不修改系統或 Visual Studio 全域 PATH。真實 Visual Studio MSBuild 在 PATH 僅保留 .NET 與 Windows 目錄時已完成 2019 modules 的 Vite build。

## 2026-11-12 修正 Visual Studio 啟動舊 Vue bundle

查核同事取得 stale site 的交付鏈：兩個 GitHub main 均為同一 SHA，Vue source、lockfile、Vite 與 launch settings 皆已提交；根因是 `wwwroot/portal` 正確被 Git ignore，但原 csproj 只在 Publish 執行 npm，普通 Visual Studio Build／F5 不重建，故舊 ignored bundle 可長期殘留。未改為提交 generated bundle，而是在 csproj 新增增量 `RestoreVueDependencies` 與一般 Build 前執行的 `BuildVue`，並設定 `DisableFastUpToDateCheck=true`，避免 Visual Studio 在只改 Vue 時跳過 MSBuild；Publish 只復用並收集產物。npm working directory 錨定 `$(MSBuildProjectDirectory)`，避免從不同 caller directory 執行錯誤目錄。

先建立隔離 MSBuild 行為測試並確認舊專案因不存在 `BuildVue` 而失敗；完成後涵蓋初次 `npm ci → npm run build`、未變更 lockfile 只 build、lockfile 更新再 ci、skip 不呼叫 npm、publish build 一次及 npm 失敗使 MSBuild 失敗。實際一般 build 自動執行 Vite且 0 警告／錯誤；publish 只出現 1 次 Vite 並包含 `wwwroot/portal/index.html`。本機 Codex runtime 只有 Node 無 npm，實測使用 ignored adapter 將 `npm run build` 轉到既有 Vite，且拒絕意外 `npm ci`；正式開發先決條件仍是 README 的 Node/npm PATH。

獨立審查發現 Visual Studio Fast Up-to-Date Check 仍可能完全跳過 MSBuild；先新增 property evaluation 回歸並確認空值失敗，再設定 `DisableFastUpToDateCheck=true` 後通過。完整回歸為 Vue 33 檔案 100/100、.NET 214 通過／19 個外部條件測試略過；最終一般 build 再次自動執行 Vite且 0 警告／錯誤。最新版 PID 32760 只綁定 7173，`/courses` 回 200 且載入 `index-BnAXc7hJ.js`；5097、5107、5173、5180 無 listener。

## 2026-11-12 測驗題目排序改為圖示控制

使用者確認不新增 Font Awesome，沿用專案既有 `@lucide/vue`。`AssignmentEditor` 的拖曳、上移、下移由文字按鈕改為 `GripVertical`、`ArrowUp`、`ArrowDown`，使用全站 `icon-button` 樣式並保留中文 `aria-label`／title、disabled 邊界、既有 drag/drop 與 `move()` 資料流程。沒有修改單元活動或單元卡片排序。

新增真實元件測試，先因舊控制沒有題號化 label／SVG 而失敗，再驗證圖示無可見文字、首末邊界及第二題上移後送出的題目順序。專項 1/1、完整 Vue 33 檔案 100/100 通過；Vite production build 及標準 .NET build 成功。精確停止舊 PID 31080 後，最新版 PID 30864 只綁定 7173；`/courses` 回 200 且載入 `index-BnAXc7hJ.js`，5097、5107、5180 無 listener。

## 2026-11-12 課程分層工作區與 Migration 035

使用者選定並核准「分層工作區」。參考 `teacher-course-creation-flow.html` 的逐層脈絡，但未搬入固定週次、歷史教材標籤、假資料、先教材後測驗限制或連鎖刪除。前端新增單元 canonical route 與聚焦工作區；課程首頁合併單元總覽，側欄移除重複的單元／教材入口，單元內仍可切換課程討論、分組、成績等課程層功能。教材、線上評量及 PDF 作業可在所屬單元建立並回到同一單元；正式考試頁只保留既有 `ExamManager`。

Migration 035 為 `course_resources`、`assignments`、`report_assignments` 新增 `unit_sort_order`，跨表依 `created_at,id` 穩定回填並建立索引；既有 `exams.unit_id` 清空，使期中／期末等正式考試回到課程層，未刪除任何考試、作答或成績。新增同課單元詳情與完整重排 API，拒絕跨課 ID、重複、漏項及 exam kind；新建或移動活動會在 transaction 內配置單元順位。

最終程式審查發現並修正五個相容／並行缺口：學生仍須看見已發布的未歸類舊活動；單元內教材要保留完整編輯與統計；授權助教可建立教材與評量；重排與移動共用單元交易鎖；PDF 作業同單元編輯不可用舊讀值覆寫並行排序。另將 E2E 返回連結定位限制在單元導覽區，避免文字相同造成歧義。專項測試均先取得失敗再修至通過。

最終驗證：Vue 32 檔案 99/99；.NET 214 通過、19 個需特定 PostgreSQL／外部條件的測試略過；Vite production build 與標準 .NET build 均成功；登入 E2E 1 項因未提供 `PORTAL_DEMO_PASSWORD` 條件式略過。035 已由既有 migrator套用 public 並 COMMIT。收尾時發現原 PID 31332 已失去 7173 listener 但仍鎖定 DLL，核對命令列後精確終止並重建。最新版 PID 31080 只綁定 `https://localhost:7173`，`/courses` 回 200 且載入 `index-B7eGRHUa.js`；5097、5107、5180 均無 listener，沒有建立 worktree 或平行環境。

## 2026-11-12 Demo 學員改為常見姓名

依使用者要求將編號式「教學展示學員」改為菜市場名。安全查核發現實際只有 6 位仍使用編號舊名，首次以預估 8 位執行時因數量不符而整筆回滾；沒有部分改名。修正範圍後，DEMO115003–008 依序改為陳怡君、林志明、王美玲、張家豪、李雅婷、黃建宏，另外已有姓名的展示同學保留。

新增 Development-only、可重跑的 `--rename-demo-classmates` 及 audit。工具只鎖定 `.invalid` Demo Email、Member 且不可建課的編號帳號，保留 ID、登入與所有關聯，同步 PDF `report_snapshot` 顯示姓名並寫入 rename audit。第二次執行 0 修改；audit 6/6 帳號及快照一致。PortalSmoke 與主專案建置 0 警告。

## 2026-11-12 全部 Demo 成績交易式發布

使用者明確要求發布全部成績。先唯讀確認 14 份線上評量的 151 組學生作業及 12 份 PDF 作業的 72 份繳交均已有非 null 草稿分數，再新增 Development-only `--publish-demo-grades` 與 audit。工具在單一 PostgreSQL 交易內逐份重算 active 應評名單；線上作業鎖定最新提交快照與最新 grade，PDF 個人作業要求全班皆有有效繳交／分數，小組作業要求每個指定小組皆有繳交且保留成員快照。任一不完整即全部回滾。

發布成功：線上 14 份／151 位學生，PDF 12 份／72 份繳交，小組成員 55 人。`grades.released_at`、PDF `published_score`／`published_feedback`、小組個人 `published_score` 及 `course_grade_scores.published_score` 同步更新；作業／報告 audit 與 PDF batch release event 已寫入。發布後 audit 為線上 151/151、成績簿 151/151、PDF 72/72、未發布小組成員 0；原 AssessedDemo audit 逐份確認 72/72 PDF 均已發布。未新增假 submission，未發布課程總成績，亦未觸及非 Demo 課程。

## 2026-11-12 單元評量、雙欄批改與全課程示範交卷

依使用者要求把一般線上作業、隨堂／課後測驗與 PDF 作業的作答／批改入口收進課程單元。`CourseUnits` 對教師顯示「批改作答／批改 PDF」，Assignment／Report 深層頁返回單元；側欄移除獨立「批改與評分」，原「測驗與作業」改名「正式考試」。`AssessmentsView` 接回既有但先前未掛載的 `ExamManager`，讓期中／期末等獨立考試成為外層主功能；一般評量建立及舊清單保留在 staff 相容收合區，沒有搬移資料或另建成績系統。

新增 `AssignmentReviewWorkspace`，沿用 class-roster 與 `TeachingPanel`。左欄先列已交、再列未交，組內依學號／姓名排序並可前後切換；已交顯示真正版本、答案、自動得分、人工評分與草稿歷程，未交只渲染唯讀空白卷且不呼叫 submission／grade API。TDD 先確認元件不存在與單元無批改入口的預期失敗，再完成 5 項專項測試。

新增 Development-only `--submitted-assignment-demo`／`--submitted-assignment-demo-audit`。它只處理 active 且具 Demo 標記的課程、已發布／關閉個人線上評量，使用實際 active 學生建立缺少的 submission、answer revisions、submission snapshot 與 quiz grading，並為沒有 grade 的最新快照建立未發布 Codex 草稿。第一次執行涵蓋 14 份評量，新增 149 份交卷與 125 份草稿；第二次新增 0；最終 audit 為 151/151 學生作業皆已交卷且有未發布草稿。既有作答未覆寫，0 筆因本工具發布。

驗證：Vue 29 檔案 89/89；後端 210 通過、19 個外部條件測試略過（sandbox 內 testhost 連線兩次逾時，改於 sandbox 外執行後全綠）；Vite production build、PortalSmoke build 0 警告。Migration runner 重跑確認 public 001–034 全數已套用並 COMMIT。精確停止舊 7173 PID 31860，最新版 PID 9788 只綁 7173。實際 Chrome 驗證「正式考試」側欄、單元批改連結、10 人交卷 sidebar、答案／Codex 草稿評分及正式考試頁；未操作發布按鈕。

## 2026-11-12 靜態教材點閱名冊與影片觀看時間分流

依使用者要求，教師教材統計改依媒體類型呈現。影片／YouTube 沿用正式學生實際時間、內容時間、倍速分段與最近觀看；非影片教材不再於教師面板顯示時間，改列曾點閱學生、每人點閱次數、最近點閱、總瀏覽與現有讚數。課程資源列中影片顯示「觀看紀錄」，靜態素材顯示「點閱紀錄」，不支援計時的格式也可開啟點閱統計。教材互動的留言數量改成與按讚按鈕相同高度、邊框與排列。

Migration 034 新增 `resource_views`；教材頁以 UUID request id POST 點閱，同一次失敗重送不重複。後端只為 active、非練習、非封存的正式學生寫入；教師／助教與預覽安全 no-op，跨課仍由課程授權拒絕。既有 learning sessions 與測驗門檻資料未移除或重算，靜態時間只是不再顯示於本教師面板。

TDD 先取得四項預期失敗，再實作。第一次 PostgreSQL 整合測試發現名冊 reader 未關閉前查讚數的命令衝突；將讚數查詢移至 reader 前後，獨立 `group_test_*` schema 的 001–034 升級及真實點閱冪等／名冊／讚統計測試通過，schema 已刪除。完整回歸為 Vue 28 檔案 86/86、.NET 210 通過／19 條件式略過；Vite production build 及 .NET build 成功。034 已套用 public。最終 7173 app PID 31860，session／Swagger 200，5097／5107／5180 無 listener；Chrome 實際檢視靜態「點閱紀錄」、影片「觀看紀錄」、靜態名冊總覽與讚／留言同列對齊均正確。

## 2026-11-11 課程資源條列與教材 tag 退役

依使用者確認，教材改以單元作為唯一主要分類。`CourseTools` 由長卡片／日期與 tag 分組改為單元順序的緊湊條列；新增 `ResourceVisual`，圖片透過既有授權 preview API 延遲載入且失敗回退圖示，其他文件與 YouTube 顯示格式圖示。每列整理名稱、原檔名、大小、授課日、上傳日、下載數、兩行摘要與角色允許的操作；教師可就地移動單元。搜尋只涵蓋名稱、原始檔名與摘要。

新增 Migration 033 實體刪除 `course_resources.tags` 與 `tags_version`，並移除 tag controller、policy、前端工具及建立／查詢欄位。檔案與 YouTube 新增 API 現要求同課 `unitId`；缺少、空值或跨課單元均拒絕。既有 null-unit 教材仍可讀並顯示於相容區，不猜測歸屬。獨立 `group_test_*` schema 由 001–028 建立含教材與繳交的舊資料，再升級至 033；教材內容／名稱／課程歸屬雜湊與繳交雜湊均不變、教材全數回填單元、tag 欄位不存在，重跑 Demo seed 可重入，schema 最終已刪除。

TDD 先確認必填單元與新清單測試在舊實作失敗，再完成實作。驗證結果：.NET 209 通過、19 個需要特定外部條件的測試略過；Vue 27 個檔案共 82 項全數通過；主專案與 Vite production build 成功且 .NET 0 警告。033 已由既有 migrator 套用 public 且重跑顯示 001–033 全數已套用。新版 bundle `index-e0FgNjt1.js` 實際含「課程資源」「未歸類教材」「請先建立課程單元」且不含「教材標籤」。最終 7173 app PID 32148（父 dotnet PID 14144），session／Swagger 皆 200；5097、5107、5180 均無 listener。

## 2026-11-11 課程編輯、生命週期與教學單元

完成現況稽核後沿用既有活動與授權，沒有重建評分或作答。新增 Migration 030：`course_units`、課程版本／更新時間，以及教材、`assignments` 線上測驗、PDF 作業和相容 `exams` 的同課複合外鍵。隔離 schema 以完整 001–029 舊資料升級驗證，四種活動回填同一相容單元，線上提交與 PDF 提交均保留；測試 schema 已刪除。030 後套用 7173 public，查核 6 個有活動課程、6 單元、四種活動未歸類皆為 0，既有 3 份線上提交與 72 份 PDF 提交仍存在。

課程擁有者可用 expected version 編輯基本資料、封存或只刪除完全空白草稿；有成員、單元、教材、評量、成績、分組、案例或問答一律拒絕破壞性刪除。教師可 CRUD／原子排序單元、移動同課活動，空單元才可刪；學生與預覽唯讀、草稿評量隱藏、小組 PDF audience 保留。課程首頁與側欄改為單元導向，新增教材可直接選單元。

驗證：獨立舊資料 migration 通過；含 PostgreSQL 的後端 227/227、Vue 78/78、.NET 與 Vite production build 通過。新增整合測試涵蓋教師／TA／學生寫權、學生草稿隱藏、預覽唯讀、排序、跨課拒絕、占用刪除限制、課程版本衝突／刪除／封存。正式考試重整、公告、開放選課、拖曳排序與單元學習分析列後續。詳見 `docs/course-units.md`。

## 2026-11-11 全課程示範討論與批改巡檢

新增 Development-only `--discussion-demo`，以固定 SHA-256 衍生 ID 交易式建立示範討論；只處理同時具有 active 教師與學生的 active 課程，使用各課實際成員帳號，不建立占位作者。6 門課各新增教材整理、避免過度推論及小組分工 3 串，共 18 串、39 回覆；每串 3 個主題讚、回覆各有少量讚，每課第三串關閉。第二次 seed 為 0 新增。

新增唯讀 `--discussion-demo-audit`，逐課以固定 ID 驗證 3 串、至少 6 回覆、9 個主題讚、6 個回覆讚及 1 串關閉，6/6 通過。另新增 `--assessed-demo-audit`：12 份個人／小組 PDF 作業共 72 份正式繳交，72 份均有 Codex 分數、非空回饋、評分人／時間及草稿事件，0 筆 published score；沒有把示範評分發布給學生。

PortalSmoke 連同主程式建置 0 警告／0 錯誤。7173 重啟時，受限沙箱因無開發憑證造成一次診斷執行失敗；Windows 使用者內容中的背景子程序實際已啟動，最終 `https://localhost:7173/api/auth/session` 與 Swagger 均為 200，未啟動 5097／5107／5180。

## 2026-11-11 示範討論改為課程專屬內容

依使用者回饋移除 6 門課共用的泛用討論文字。原 18 個 deterministic Demo 討論 ID 原地更新：個案一三五涵蓋事實與推論、證據強度及資料缺口；個案二四六涵蓋方案標準、利害關係人及風險備案；數學課涵蓋百分比分母、平均數與估算驗算；科學 A 涵蓋變因、對照組及測量誤差；兩門測試課依其作業情境分別改為校園觀察與學習策略。教師／學生／助教回覆亦同步改寫，作者、時間、按讚與關閉狀態保留。

先擴充巡檢要求 18 個課程專屬標題，對舊資料執行如預期失敗；更新 seed 與 public 後逐課巡檢 6/6 通過，第二次執行新增 0 討論、0 回覆。PortalSmoke 與主程式建置 0 警告／0 錯誤。

## 2026-11-11 將既有展示同學補成真實平台帳號

依使用者要求，沒有另造一批同學，而是把既有 8 位「教學展示」學員原地補齊展示 Email、學號、local identity、`portal_credentials` 及 `studio_profiles`。帳號為 Member、不可建課，Email 使用 `.invalid`；原 38 筆有效修課、48 份 PDF 繳交、分組、互評與 Codex 評分草稿因 user id 不變而保留。每筆建立 `demo.student_account.activated` 系統維護稽核，不冒用教授。

新增 Development-only、交易式且可重跑的 `tests/PortalSmoke --activate-demo-classmates`；要求由環境變數提供至少 12 字元密碼，文件不保存密碼。第一次執行新增 8 個憑證，第二次執行新增 0、驗證 8，並逐一核對 PasswordHasher、Member 權限與不可建課。PortalSmoke build 0 警告／0 錯誤；資料庫最終查核為 8 帳號、8 profile、38 修課、48 PDF 繳交及 8 稽核。7173 當時沒有 listen，故未做該埠瀏覽器 session 驗證。

## 2026-11-11 移除作業與測驗內提問

依使用者指示移除測驗頁的「提問（公開設定）」導覽、提問面板、前端元件及作業討論 API。課程層級「課程問答」維持原入口與權限。既有 assignment_discussions 資料表暫時保留，避免在介面調整時刪除歷史資料。

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

## 2026-11-06：加入開發環境 Swagger API

- 使用者詢問是否已導入並要求開啟；核對原 csproj／Program 無 Swagger。新增 Swashbuckle.AspNetCore 9.0.6、AddPortalSwagger／UsePortalSwagger 與開發UI／v1 JSON；Production顯式404，避免SPA fallback假200。
- 沿用Cookie登入，端點文件標登入需求與401／403；UI每次寫入先同源取得CSRF token，不繞過防偽／授權。GET不額外取token，跨源或token失敗停止請求。未加JWT或保存真實憑證。IFormFile移除多餘FromForm，保留自動表單綁定與原上傳限制。
- 新文件生成測試涵蓋所有控制器、主要路徑、匿名／登入標示與兩類檔案multipart。完整文件成功產生92個API路徑。以同程序pipeline驗證Dev UI／初始化JS／JSON及Production404，Node測實際interceptor成功。build／publish 0警告錯誤；swagger-release成品已建置。
- 環境限制：提高權限的程序啟動回OS存取被拒，正常shell可還原既有Swagger套件與建置，但本機HTTP／CIM及VSTest testhost連線受限；編譯改單節點、停用共享編譯避named pipe timeout。VSTest未完成，改直接執行新測試方法通過；未聲稱既有176回歸或瀏覽器登入通過。額外TestHost工具依賴還原失敗，未加到正式專案，改無socket同程序pipeline。
- 未管理或重啟原5097／Visual Studio；短暫本輪前景服務啟動後已終止，原Data Protection設定不變。沒有資料庫變動／QA建立／模型推論。啟動現有開發服務新版後即可開/swagger，本輪沒有宣稱預覽已更新。
- 同步README、docs/swagger、規格與交接／歷史；未提交推送。外部依據為Microsoft .NET8 Swashbuckle文件、NuGet版本頁與維護者FormFile說明，連結見docs/swagger.md。


## 2026-11-06：教材週次標籤及 YouTube 回放觀看時間

- 使用者先要求教材上傳由老師加tag供每週查詢，續要求YouTube回放嵌入及觀看時間；本輪合併完成。新增018標籤／獨立版本、019來源類型／影片ID，檔案bytes與歷史資料保留；001–019重跑全略過。
- 教師／助教新增檔案或YouTube教材與自由標籤，週次正規化、去重／10個各30字限制；學生依週次／主題／未標記及名稱／檔名搜尋。標籤API有課程權限、封存阻擋、409版本與稽核，未存離頁提醒；標籤不重設原下載次數／學習時間。
- YouTube只保存白名單11碼ID，支援常用影片網址但不接受任意HTML／外站／僅播放清單；官方IFrame API與nocookie嵌入，無影片代理下載。影片ID不覆寫，改片須新增。回放自動播放事件連結017計時，背景／暫停／緩衝／倍速停止、快轉不給跳過時間，伺服器wall-clock及位置雙重上限，10秒心跳／超25秒斷點不計。close保存有效尾段，時間投影floor避免四捨五入提前達門檻；序列化心跳避免關閉事件被在途請求吞掉。
- 新課內教師／助教正式時間統計API與介面，列有效學生逐人秒數／最近觀看／未看0及總計；學生及練習視角拒讀，無跨課繞過。YouTube可加入既有測驗教材門檻並由伺服器鎖題／拒作答。不可嵌入／載入失敗與站外觀看不累計，仍為平台活動而非注意力或YouTube官方觀看數。
- 後端193通過（DB啟用、0略過），前端53通過；新增政策與DB影片時間／練習隔離／統計授權測試，前端標籤篩選／衝突保留及播放器事件。Vite build與publish成功；首次publish缺npm，改用已完成的前端輸出並SkipFrontendBuild發布，無產品依賴變更。
- 初次新browser測試因合成iframe滿高令測試按鈕溢出被連結遮住逾時，改合成iframe高度後通過；另修正教材template中插入統計導致v-else錯接、意外顯示同學區的問題。5107教材／既有計時3項通過，最終5097檔案預覽、PDF／WebM計時、跨課歷程／門檻、新標籤／YouTube／統計、檔名共5項通過。桌面／390px手機截圖檢視，合成播放約9秒達門檻且快轉不膨脹；真實官方YouTube示例播放器成功載入且無頁面錯誤，未做真實學生影片完成率或模型品質驗收。
- 原5097未啟動，materials-release複製p0-publish並以contentRoot專案根／webroot發布目錄啟動PID9188，session／Swagger JSON均200（下次重查）。合成QA及state清理成功，核對命令列後停止本次5107 PID49724。未動真實課程／教材／模型設定，未停止使用者開發程序，未提交推送。
- 同步docs/course-materials、elearning、agents及README；下方前次Swagger受限為當時紀錄，本輪工具已可正常執行提高權限DB／瀏覽器驗證。


## 2026-11-06：YouTube逐字稿與播放速度／內容時間

- 使用者要求YouTube教材提供逐字稿，播放速度和觀看時間共同參考。新增YouTube表單選填逐字稿、回放頁逐字稿區，教師／助教新增／修改／清除、學生唯讀。最多100,000字、純文字保留換行／時間文字，不執行HTML，不自動擷取／轉錄或同步跳播。獨立版本與409衝突，保留未存草稿、離頁提醒，稽核不存逐字稿正文；課程／封存／練習授權沿用。
- 提問速度如何計入門檻，使用者選「以影片內容時間計入（2倍速實看10分鐘計20分鐘）」，取代原1倍速限制及當次暫採實際時間的建議。0.25–4倍速固定session分段，切速關閉舊段、新開新段，伺服器實際經過時間及位置／速度共同限制；跳轉／背景／暫停／緩衝仍不計，重看可累積。
- 觀看時間保留實際credited_milliseconds，另保存content_milliseconds與playback_rate；門檻Gate改採內容（文件內容與實際相同），本人歷程原實際秒數不膨脹。學生顯示兩種時間與當前速度；教師逐位實際／內容合計、各速度實際／內容分段。既有已開始測驗依據不重寫，舊成績不變。
- 新020增加transcript／transcript_version及速度／內容欄位，已交易套用，001–020重跑全略過；舊1倍速已計入時間等量承接內容欄，未製造站外／歷史觀看事件或搬移真實教材。無模型設定／有效負載變更、無AI推論。
- 最終後端199通過（DB啟用、0略過），新增2倍速實10秒／內容20秒達門檻、錯誤速度／快轉不給分、0.5倍速及無效速度邊界等；前端55通過，含逐字稿純文字／學生唯讀／衝突草稿與清空。Vite及publish成功。首次後端test輸出被使用者開發程序apphost鎖定，改獨立replay-tests通過，未停止該程序。
- 5107原政策3項browser通過後，依使用者答覆改門檻並重新驗證最終5097共3項通過：PDF／WebM、跨課歷程／門檻、新逐字稿／學生唯讀／HTML純文字／清除與409／教師UI編輯／1倍切2倍／實際少於25秒而內容達25秒解鎖／快轉不膨脹／正式統計。測試合成播放器的緩衝回播回呼補狀態檢查，避免按暫停後測試碼自行恢復播放；手機及統計截圖已檢視。未宣稱真人影片完成率或注意力驗收，本輪未重跑實際YouTube播放或模型測試。
- 部署replay-release至p0-publish，核對命令列後更新本專案預覽；最終5097 PID41988，contentRoot專案根／webroot發布wwwroot，session200，下次重查。QA合成帳號／課程／教材／逐字稿／計時／提交及state已清理，僅停止本次5107 PID39940；未停止使用者開發程序，未提交／推送。
- 更新elearning、agents、course-materials、learning-history-spec及README，以新確認的內容時間政策取代舊限制；歷史原決策保留。


## 2026-11-09：11/6會議閱讀、gap分析與分功能規格

- 使用者貼上11/6專案討論整理（李志堅、沈欽譽、李冠億），要求先共同閱讀分析與規格，不開發；來源為所貼摘要，未宣稱取得原始逐字稿、簽核或原12/15驗收項。
- 核對課程／角色、四題型／PDF／加權成績、歷程、教材018–020、YouTube／逐字稿／計時、一般AI停用403與案例回饋、公開提問及Swagger程式。確認新缺口為匿名觀摩、授課日期、獨立教師摘要；通過／完課規則仍缺，已提交不能當通過。公開提問有作者名稱，不當成匿名觀摩。
- 新增docs/meeting-2026-11-06-gap-spec.md差距矩陣／來源優先序／里程碑／驗收與D1–D5；exemplary-work-spec定匿名副本、不可變來源版本、授權確認／個案檢查、教師發布提案、助教準備與緊急撤下、防洩題、附件獨立授權及撤下；teaching-material-metadata-spec定可空授課日／教師摘要／版本及日期查詢；ai-draft-feedback-spec定第二階段規準／私人草稿／版本及資料政策。新增欄位／API／權限細節皆標草案，未視為教授已核定。
- 保留歷史與後續決策脈絡：會議偏好暫緩複雜觀看監控，但11/6後續使用者已核定倍速內容時間並實作，本輪不移除／重算／停門檻；一般測驗／作業AI仍403，既有案例AI保留。11/15為候選範圍、12/15待原計畫、年底雛形不等於商用；不新增全校／社團／視訊或監控範圍。
- 更新elearning、agents、README與教材文件導引。僅文件／程式讀取、Markdown連結與diff核對；沒有存取即時DB／學生內容／Notion、沒有模型設定／有效負載或產品／SQL變更，沒有執行產品測試／服務操作／推論，也未commit／push。Notion分享另由擁有者確認授權，本輪未發送或分享。


## 2026-11-09：依使用者要求追蹤團隊開發連線設定

- 使用者明確確認將實際資料庫連線與設定秘密保留在repo，且推至原origin及paladin兩目的地；僅appsettings.Local.json納入實際設定，Markdown不寫值。
- 移除該檔.gitignore規則、更新README及agents的現行交接；原設定值未改，環境變數覆寫及publish排除原行為保留。會議gap文件與其他待提交修改不納本次連線設定commit，既有pre-vue Ollama分支不移動。
- 驗證本機JSON可解析、只含ConnectionStrings:Portal及Portal:BootstrapToken兩項，沒有輸出值；本次設定追蹤／文件變更不跑產品測試、不連DB或執行模型，不改執行中服務。


## 2026-11-09：提交會議規格作為後續實作依據

- 使用者明確要求將待提交的會議規格推至兩個repo，供團隊後續開發。納入會議gap入口、匿名觀摩、教材日期／摘要、第二階段AI四份文件，及README、教材導引、elearning與代理交接／歷程。
- 僅文件提交；不把D1–D5待確認項自動變成已核定，不修改產品、SQL、模型或執行中服務，不移動pre-vue Ollama分支。檢查Markdown本地連結及git diff --check，不重跑產品測試。


## 2026-11-09：會議第一批教材日期／摘要與匿名觀摩實作

- 使用者要求依會議規格開發，另透過問答確認採提案工作流：助教準備、教師發布、兩者可撤下，申論及另傳去識別PDF；教材每份日期／摘要並依日期分組。D1–D3範圍定案，逐案學生展示授權仍由教師確認；D4不改已完成計時，D5與第二階段AI後續。
- 新021欄位及metadata GET／PUT、檔案與回放新增表單；API／UI日期交集篩選、穩定排序、未指定與日期分組、摘要搜尋。日期用DateOnly／YYYY-MM-DD，不跨時區；舊資料null／空摘要。摘要與逐字稿／原bytes分開，版本409及離頁保護保留未存輸入；檔案新增補課程鎖／稽核交易，封存不可競態新增。
- 新022課內觀摩項目／新增式版本、023逐次審查確認／授權識別及DB拒UPDATE trigger；私有來源綁正式最新申論快照／PDF提交，正式已評且公布才可選，關閉且不可再作答才可發布。來源資料、作者、成績與原附件不進學生DTO；PDF另傳展示副本、固定下載名。發布前確認同版本匿名／授權／防洩題，TA不能發布，雙方可撤下；重新開放來源或資格失效後學生讀與下載同時拒絕。編輯published的新草稿不變舊發布，來源／舊成績不重寫。
- UI新增課程觀摩頁籤、來源選單、去識別草稿／教學說明、學生預覽、教師三確認及授權識別、展示PDF與撤下。僅純文字呈現，無自動去識別／IRB判定／AI摘要；來源選單每類最多200筆候選，完整分頁後續。QA helper新增觀摩建立／審查者guard與先刪觀摩再刪來源順序。
- 001–023交易套用且重跑全略過，無真實課程內容搬移／日期回填／成績或時數重算；schema部署不代表真人作答可自動公開。後端214通過（PostgreSQL啟用0略過）、前端60通過；Vite／dotnet build／publish成功，新增日期政策、觀摩版本／發布權／關閉門檻／去識別副本／撤下／來源重開／DB不可變及前端學生視圖／TA／篩選測試。
- 測試修正：PDF草稿上傳補先檢教學權，避免先讀草稿造成權限回應不一致；舊tag單元測試改以資源名稱定位，避免新穩定排序改變索引；vi beforeEach加區塊避免回傳mock被當cleanup。新browser修正multipart的CRLF斷言與PDF提交清單端點，產品不因測試降低限制。
- 5107新教材／觀摩2個browser/API及舊批改／歷程／閱讀器／回放／權限5項通過；最終5097同7個情境通過，含教師／TA／學生UI、舊版409、HTML純文字、未填／未來授課日及洛杉磯時區不偏移、摘要逐字稿同頁、教師發布／新草稿不變舊公開、另備PDF與撤下封鎖URL。首項批改測試在新服務ready前ECONNREFUSED，其他6通過，確認session200後補跑該1通過，非隱匿單次全綠。手機與教師截圖已檢視，無真實班級／教授內容審查、校方驗收、壓力或模型品質測試。
- p0-publish載入meeting-release，5097 PID51068、contentRoot專案根／webroot發布wwwroot，session與Swagger JSON200（下次重查）。合成QA／state已清理，只停止本次核對的5107 PID32100，使用者Visual Studio程序不動。生成截圖保留忽略的.codex-build，還原本輪重寫的追蹤測試圖，避免二進位雜訊。
- 同步會議gap、兩功能規格、course-materials、README、elearning／agents；產品及文件尚未commit／push。原pre-vue Ollama分支不動，未切換模型／改有效負載或推論。


## 2026-11-09：提交會議第一批實作

- 使用者明確要求push本批教材日期／摘要與匿名觀摩實作；納入程式、021–023遷移、單元與browser測試、QA清理補強及更新規格／交接，推送至已設定的origin與paladin兩目的地。
- 使用上一輪已完成的214後端、60前端與7個browser情境驗證；本輪只核對差異、Markdown及Git狀態，不因提交重跑產品測試，不改DB／模型／服務或保存的pre-vue分支。

## 2026-11-09：資源互動與課程問答規格落地

- 使用者確認畫面名稱、Lucide 圖示及功能配置，要求執行，續要求寫入 Markdown 規格書；新增 docs/resource-interactions-course-qa-spec.md 並同步 elearning／agents。
- 記錄分享仍需原授權、資源 ID 統一讚／留言、本人與課內教師互動檔案權限、課程問答未回覆即同課可見及師／助教回覆、保留作業私人問題、評量名稱區隔與桌機隱藏手機漢堡；另列驗收及尚未逐項核定細節。
- 核對目前 Vue、控制器、migration 結構與 Git；本次僅規格，尚未改產品、DB、模型或服務，未跑產品測試、未提交推送。後續依文件實作，不將規格建立宣稱功能完工。


## 2026-11-09：依規格實作資源互動、學員檔案與課程問答

- 使用者再次要求依 Markdown 規格實作；新增 024 三表、CourseInteractions／InteractionProfiles API，教材分享、互動元件、學員檔案與課內名單入口，以及獨立課程問答。舊作業私人提問不變，未複製私人答案／附件或重算成績／時數。
- 每資源每人一讚可取消；留言4000字、問題8000字、答覆16000字、每頁20筆。留言／問題UUID重送去重與嚴格拒絕作者欄位，答覆expectedVersion／409保留文字。單題一份可修訂答覆，未回覆即同課可見，教師／助教皆可答；本批無編刪／附件／通知／巢狀討論。
- 互動檔案依資源＋類型彙整正式計時、目前讚及最近留言；本人有效學生修課跨課，師／助教指定本課，其他學生／未任教管理員拒讀。封存／練習不寫社群；讀寫鎖課程／成員／資源防狀態競態，讀取分頁與總量使用一致快照。一般不可計時格式也能分享／互動／下載，下載不當已讀。局域網HTTP的UUID使用getRandomValues備援，複製失敗可手動選取網址。
- 完成測驗與作業／作業評閱與給分／課程成績名稱與圖示，帳號設定人像齒輪、課程設定滑桿及既有管理集中入口，手機Menu／X與aria-expanded，桌機以較高CSS優先序隱藏手機漢堡。學員檔案篩選補明確可存取名稱。
- 024已交易套用，001–023略過；原受限環境DB連線遭socket拒絕，使用工具提高權限後成功，未輸出憑證。後端220通過（DB啟用0略過，新增6項含並行讚、重送留言、分頁、答覆權／版本、封存／練習、檔案範圍及偽造作者），前端63通過（新增3項唯讀、純文字、重送ID及衝突草稿）。Vite、dotnet build／publish成功。
- 5107新互動1項及既有評量、教材閱讀器、歷程門檻、回放計時、日期摘要、匿名觀摩、混合角色權限共7項browser通過。最終5097評量給分、課程導覽及新互動3項分別通過；新互動包含一般格式下載、固定分享網址、重複讚／純文字留言、檔案權限／課程篩選、未答即見、TA與教師答覆、409保留、桌機與390px手機且無頁面JS錯誤。截圖已檢視；未做真人課程／校方驗收、壓力測試或全裝置原生分享驗證。
- 測試過程：首輪把瀏覽器原生分享預期為已複製，改驗證另設的複製按鈕；最終跨課檔案已含先前合成資料，改先篩選本次課程；選單標籤含選項文字導致exact定位逾時，補aria-label後重跑通過。非隱匿首輪全綠。最初npm不在PATH改直接Node執行既有Vite／Vitest；一次腳本工作目錄錯誤已更正。發布複製層級亦已修正並核對最終API／靜態成品。
- 5097原無回應，更新p0-publish並啟動PID53232，contentRoot專案根／webroot發布wwwroot，session與Swagger JSON200。僅停止本輪5107 PID43712及本輪發布更正時的短暫5097程序，未停止使用者開發服務。QA helper增加新表非QA guard與問答刪除順序；所有合成QA資料及state清理成功，helper已自刪state，後續重複移除提示不存在，未影響清理結果。
- 同步專項規格／API、README、elearning與agents；下方原僅文件狀態由本條取代。未切換模型、變更Ollama設定／有效負載或執行模型推論，未commit／push。


## 2026-11-09：新作業／提問通知徽章與Copy按鈕

- 使用者要求紅色數字徽章提示新作業及學生問題，過程續要求複製連結按鈕與重疊圖示；本輪合併完成。新增025_notification_reads.sql與NotificationsController，讀取即時授權投影，學生已發布測驗／PDF、教師／助教本課問題逐人計數，草稿／closed／封存／無效成員／學生體驗不列；初次既有符合條件未讀項目仍計數，不偽造歷史已讀。
- 已讀主鍵user＋kind＋item，POST只接受登入者有資格的整批ID（最多200），無權混入整批拒絕；交易鎖成員／課程／帳號並ON CONFLICT支援多分頁並行重送。清單渲染後只標記所顯示項目，問答分頁外及讀取後新項保留；不同師／助教互不消除。GET不寫已讀，無另外通知內容表或站外發送。
- 工作區側欄／圖示列、課程卡片／分類入口、課內頁籤與手機選單顯示紅底白字1／99+，0隱藏。前景30秒、路由與聚焦刷新；帳號／視角切換清空且忽略舊回應，刷新失敗不阻斷導覽。課程標題及導航保留可存取名稱，徽章另提供實際筆數。問答與評量新增重新整理按鈕。
- 教材「複製連結」固定顯示secondary按鈕及Lucide Copy重疊方框；分享、剪貼簿成功訊息及無權時手動選取備援保留。
- 025已套用，001–024略過，無真實作答／成績／時間搬移或重算。後端223通過（DB啟用0略過，新增3項通知整合測試含草稿發布、並行已讀、個人隔離、未知ID拒絕、分頁／新問題、練習封存撤權），前端66通過（新增3項徽章／99+、跨帳號延遲回應、已顯示ID已讀）。Vite、dotnet build／publish成功。初次前端build發現try/for缺右括號，修正後全測與build重跑通過；未將初次失敗報為全綠。
- 5107及最終5097各3個Chrome情境全通過：既有測驗／PDF給分、分享讚留言／學員檔案／問答、以及新作業發布出現1、渲染後清除／重新載入保持、學生問題出現1、教師已讀不替TA消除、手機徽章及無JS錯誤。桌面／390px手機與Copy按鈕截圖已檢視；未做真人班級、全裝置或壓力驗收。
- p0-publish部署並以PID30996啟動5097，contentRoot根／webroot發布wwwroot，session及Swagger JSON200且有/api/notifications。先核對並僅停止本專案舊預覽PID53232；合成QA／state及通知已讀資料已清理，僅停止本輪5107 PID41144，未動使用者開發程序。
- 新增專項規格、同步resource-interactions／README／elearning／agents及QA清理順序；舊「無通知」由此新要求擴充站內徽章，仍無email或瀏覽器推播。未更動Ollama設定／有效負載或執行模型推論，未commit／push。


## 2026-11-10：YouTube計時資訊移至播放器右側／下方

- 使用者要求將影片計時放到YouTube播放器右側或下方；改LearningResourceView呈現結構與scoped CSS，1280px以上播放器＋300px計時欄，較窄畫面垂直排列、播放器先於計時卡。保留原計時數值／狀態／控制／說明，新增學習計時標題；其他教材用order保留計時在閱讀器上方。修正全域相鄰surface樣式額外24px margin，避免右側卡片頂端錯位與手機雙倍間距。
- 播放事件、播放器元件／ref、心跳、位置與倍速換算、權限、資料及成績均未修改。前端66項全過、Vite build成功；使用本地Chrome及合成API／播放器驗證1440px右側、1024px／390px下方、無水平溢出與JS錯誤，並檢視桌面／手機截圖。驗證腳本及截圖放忽略的.codex-build，未建立DB測試資料，未重跑後端或真實YouTube播放。
- 5097操作前session200；只更新p0-publish/wwwroot/portal的index及assets，不啟停服務。初次並行前端測試／build仍執行時先複製到舊成品，待build完成後已重新複製最終成品並重跑瀏覽器配置核對通過。同步elearning／agents／course-materials；未commit／push，既有未提交功能全保留，未改模型或推論。


## 2026-11-10：明示教師／助教YouTube預覽不記錄觀看時間

- 使用者要求頁面明確說明教授及助教帳號不追蹤觀看；利用已取得的本課courseRole與meta.isPractice辨別教學預覽，在頁首及計時卡加明確提示，可正常播放但不記錄學習時間／學生正式紀錄。
- 教學預覽不顯示誤導的累積數字、計時已暫停或自動計時規則，正式學生保留原功能；學生體驗明示只記本人練習，封存學生維持唯讀提示。依目標課程角色判斷，不讓跨課任教者的正式學生紀錄被誤判。播放／心跳／倍速／後端政策完全未變。
- Vite build成功，5097以合成API／播放器驗證教授、TA、正式學生、練習、封存學生五種呈現；檢視教授畫面，無JS錯誤或測試中的session寫入。僅更新p0-publish前端，不建立DB資料、不重啟服務，未宣稱真實YouTube播放驗收或全套測試重跑。同步course-materials、elearning、agents；未commit／push。


## 2026-11-10：依後續決策改為教師／助教本地示範計時

- 使用者改採教授／TA也可看到計時運行，但因本課教學身分僅供示範、不計入紀錄，取代同日先前隱藏數字決策的現行效力；保留歷史。
- LearningResourceView新增獨立demoActualMs／demoContentMs與1秒本地取樣，以performance時間、影片位置及倍速保守計算，排除超25秒間隔、快轉／負位移、改速重設樣本；暫停／緩衝／背景停止，離頁清理，重整歸零。教學預覽事件分支不進start／session API，沒有DB或後端政策變更；學生與學生體驗仍原持久session路徑。
- 介面保留右側／下方配置，明示本課教師／助教、計時僅供示範，不儲存或計入學習時間／成績／測驗門檻。示範數值與速度／狀態皆用示範名稱，避免誤認正式累積。
- 新增LearningResourceView測試3項（teacher及TA參數化各1、正式／練習原session1），完整前端69全通過與Vite build成功。5097合成API／播放器動態檢查教授／TA播放累加、改2倍、暫停不增、重載歸零，無session請求／JS錯誤；檢視教師示範截圖。未進行真實YouTube或後端／DB整合回歸。
- 更新p0-publish前端成品，不重啟服務、無測試資料建立，更新course-materials／elearning／agents；未commit／push，未改模型或推論。

- 本輪最後文字微調後重驗時5097拒連；查無對應程序，未停止其他服務，重新啟動同一p0-publish預覽PID27352，合成教授／TA動態示範重驗通過且session200。此補記更新本條前述「不重啟服務」的最終狀態；無後端程式或DB變更。


## 2026-11-10：區分教師摘要、播放及學生參與紀錄動作

- 使用者指出教師摘要收合三角像播放，要求教師播放圖示及另一種學生參與紀錄圖示。教材卡片摘要改FileText＋ChevronDown，展開箭頭旋轉、移除原生marker，保留原生details／summary與focus-visible／Enter操作。
- YouTube播放連結加Play，教師／TA顯示播放課程回放（示範）、學生原觀看／計時名稱保留；原查看學習時間改UsersRound＋查看學生參與紀錄，ResourceLearningStats標題同步明示正式學習時間，更新對應既有e2e定位名稱。不新增其他參與監控或出席／成績判定，原統計API／權限及示範計時不動。
- 既有CourseTools2項測試通過、Vite build成功；5097合成API教材Chrome驗證滑鼠／Enter收合、原播放href、學生紀錄按鈕開啟原統計面板，1440／390px皆無橫向溢出或JS錯誤，桌機／手機卡片截圖已檢視。未執行修改定位後的完整materials e2e、後端或真實YouTube測試。
- 更新p0-publish前端靜態檔，不啟停服務、不建立DB資料；同步course-materials／elearning／agents。未改模型或推論，未commit／push；既有未提交工作保留。


## 2026-11-10：計時說明強調背景播放不計入

- 使用者附實際YouTube示範畫面並要求把前景計時特點放入說明；新增醒目Eye圖示／邊框區塊「前景播放才計時」，明示本教材分頁可見且影片播放才累計，切其他分頁或最小化後即使仍有聲音也不計時。老師／TA區塊稱示範時間；學生稱學習時間。
- 核對程式使用document.visibilityState，文字採「可見／前景」而非過度宣稱window focus、專注／眼神偵測。只改提示及樣式，不變更示範或正式計時政策。
- LearningResourceView既有3項測試、Vite build通過；5097合成API／播放器檢查1440／1024／390px位置／無橫向溢出與JS錯誤，檢視桌面提示截圖。未新增鏡像文字測試，未跑全套回歸或真實YouTube播放。已更新p0-publish前端，不連DB、不啟停服務；同步course-materials／elearning／agents，未commit／push。

## 2026-11-10：課程導向師生入口與多課示範

依 11/10 review 實作單一響應式側欄、唯讀預覽、教材清單、背景暫停、限時與大題、全班批改、評分草稿與發布、調分及總分發布。另含逐課 TA 限制、組長、多方問答、PDF 附件、投票、選用通知、學籍唯讀及學期欄位。新指南記錄站台地圖、權限矩陣與啟動步驟。保留既有工作區修改及舊功能資料。

隔離 PostgreSQL 套用 001–028。含 DB 的後端測試 224 通過，前端 71 通過，Vite 與 .NET 建置成功。兩個 Chrome/UI 與直接 API 情境通過，覆蓋預覽禁寫、跨課授權、個人提交、批改草稿及發布、PDF 歷程隔離、手機導覽、分組與討論。校務同步、作業專屬分組及真實 YouTube 播放未驗收；既有全套 Playwright 未重跑。臨時 QA schema 與程序已清理。另建乾淨示範 schema，種子第二次執行略過，5180 教師及學生已實際登入。未變更共用 public schema、提交或推送。

## 2026-11-10：恢復 5180 課程清單
5180 無監聽後重新啟動，沿用獨立示範 schema；實際登入教師、學生各取得兩門課。共用 public 查到版本 025。清單載入失敗提供重試並避免同時顯示空課程，Vue production build 通過。

## 2026-11-10：右上角帳號選單
個人資料、更改密碼及登出移入使用者選單；Vue build 成功。5180 Chrome 教師 1366px、學生 390px 從登入表單進入課程首頁，驗證三個入口及實際登出。未更改帳戶密碼。補正上筆服務判讀：restart-error.log 顯示啟動嘗試因 5180 已佔用失敗，上筆驗證命中既有服務。

## 2026-11-10：修復 7173 共用開發站課程清單
使用者明確同意後，public 由 025 套用 026–028，交易已提交。初次驗證 7173 拒連且 QA 已清理；確認埠可用後啟動開發站，再以合成教師與學生登入 https://localhost:7173，兩者皆取得所屬課程清單與明細。QA 已清理。

## 2026-11-11 小組 PDF 作業交付

- 在既有 PDF 作業、課程分組與成績項加入指定小組、組長／代交、繳交快照、退回重交與版本下載。調整學生可見名單、通知、作業清單、成績簿及歷程。教師可分別檢視私人五級互評、完成度與同分表態，儲存基礎分和個別調整後再發布。
- 新增 Migration 029、三課六組示範、含舊資料升級驗證、權限／成績 API 及 Playwright 流程與共識元件測試。測試發現學習歷程的線下成績分支誤用了小組欄位，已修正。
- 本機 PostgreSQL 以實際 028 個人 PDF、草稿分、已發布分及回饋升級 029，驗證檔案摘要、成績與異動紀錄並重跑 seed。後端 224/224、Vue 74/74、Chrome E2E 3/3 通過，前後端建置成功。測試服務已停止。共用開發站仍待部署 Migration 029。

## 2026-11-11 同步 7173 共用開發資料庫
依使用者授權將 public 從 028 升級至 029，交易已提交。重新連線確認版本與 report_group_audience 視圖。直接執行教師及學生課程清單 SQL，兩次均成功並返回零筆。檢查時 7173 無監聽，留給 Visual Studio 啟動。

## 2026-11-11 正式修課與授課側欄
同一帳號依課程身分分別列出授課／助教與正式修課，課內唯讀預覽移到側欄。加入醒目課名、學期與班別，以及依課程身分返回清單的左箭頭。Vue 76 項測試及 production build 通過。Chrome 使用模擬混合身分課程資料，驗證桌面及 390px 手機側欄導覽。

## 2026-11-11 老師在我的修課查看授課課程
學生入口清單加入已開放與封存的本人授課課程。授課課程以學生可見範圍統計評量，點選時自動切至唯讀預覽，返回箭頭回到我的修課。正式修課維持原學生身分。前後端建置及 76 項 Vue 測試通過，以模擬課程在 Chrome 驗證桌面、手機進入及返回。

## 2026-11-11 修復 7173 草稿授課課程漏列
共用資料庫課程全為草稿。清單、路由及後端允許任課教師唯讀預覽草稿，正式學生仍等課程開放。以實際清單 SQL 在 public 抽驗教師取回 4 筆草稿、學生 0 筆。獨立輸出後端與 Vue 建置成功、Vue 76 項通過、Chrome 模擬草稿課程導覽通過。執行中的 Debug 程序鎖住 exe，Visual Studio 須停止再重新啟動以載入新版後端。

## 2026-11-11 開放 7173 示範課程
依使用者指示將現有六門草稿課程開放。各新增一份個人 PDF、一份支援互評與同分表態的小組 PDF、一份兩題測驗及兩個小組，原分組保留。六份合成小組繳交可供批改。交易提交後重新連線核對六門 active、12 份 PDF 作業、6 份測驗、12 個新組、6 份示範繳交、12 筆互評與 12 筆表態。

## 2026-11-11 Codex 示範繳交與批改

- 為 12 份示範 PDF 作業產生 72 份一頁中文答案，依六個主題填入證據、解釋、限制、後續驗證及小組分工。PDF 已以 Chromium 產生並用 Poppler 渲染抽查，中文字型、卡片、長文與頁尾均正常。
- 72 份應繳全部完成，分數介於 72 至 96。每份有 Codex 批改評語與評分異動紀錄。小組作業共保留 55 位成員快照、216 筆互評及 55 筆同分表態。
- 分數目前是教師評分草稿。自動核准審查拒絕將模擬成績發布至學生正式成績簿，因使用者尚未明確授權發布。資料庫核對為 72 份繳交、72 份 Codex 草稿、0 份已發布。

## 2026-11-11 課程討論區雙欄化

- 在既有課程問答上完成雙欄討論區：左側全部討論串、新增主題表單，右側選取閱讀、上下前後篇、公開回覆、討論串與回覆按讚。教師／助教依本課角色顯示 Moderator badge，可關閉／重新開放；關閉只停止新回覆，閱讀與按讚保留。
- 新增 031 主題及回覆按讚表，保留舊問題／答覆／投票／附件並回填主題；032 放寬部署切換相容，舊 API 可暫存空主題，新 API 以內容摘要顯示。兩 migration 已交易套用 7173 public。
- 獨立 `group_test_*` schema 從 028 舊資料升級至 032、重跑 demo seed，後端 228/228 通過後刪除 schema。前端 78/78、討論元件 3/3、Vite production build及獨立輸出 .NET build 通過；原 Debug 程序鎖住預設 bin，未停止使用者服務。尚未重啟 7173 或執行真實瀏覽器驗收。

## 2026-11-11 7173 單一環境與 migration 確認

- 使用者指定只使用 7173，後續不得為避免占用另開其他站台。檢查時 7173 無監聽程序，因此未執行強制終止；重跑 migration 後 001–032 全部顯示已套用並提交。
- 正常 `bin/Debug/net8.0` 建置零警告成功；以 `--no-launch-profile --urls https://localhost:7173` 啟動 Development，避免原 https profile 同時開 5084。`/api/auth/session` 與 Swagger JSON 均回 200，Swagger 含回覆按讚端點；未開 5097／5107／5180。

## 2026-11-11 清理舊建置與執行環境

- 盤點專案程序後只發現新版 7173 與一個舊 `.codex-build/course-portal-delivery` 5180 程序。核對 PID／命令列後終止舊 5180，保留 7173。
- 驗證路徑均位於 `D:\AI_TeachingAssistant` 後，刪除 `.codex-build` 約 1.76 GB、`test-results`、`output`、`tmp`；首次因舊 5180 鎖檔未完全刪除，程序結束後重試成功。保留正式原始碼、`bin`／`obj` 及前端相依套件。

## 2026-11-11 新增討論入口顯著化

- 使用者找不到新增討論入口；確認原入口只是「所有討論串」旁的小 `＋`。改為藍色「＋ 新增討論」文字按鈕，展開時顯示「取消」並提供 `aria-expanded`。
- 討論元件 3/3 通過、Vite production build 成功。7173 實際回傳新雜湊資產，內容包含「新增討論」與「所有討論串」。公司 Work 設定停用瀏覽器控制，未進行登入後畫面截圖；正式可寫成員顯示、封存／學生預覽隱藏政策不變。

## 2026-11-11 課程首頁設定捷徑

- 教師／助教課程首頁標題右側加入 `SlidersHorizontal` 設定圖示，具「進入課程設定」可存取名稱，直接導向同課程 `?section=settings`。學生及教授唯讀學生視角不顯示，不擴大設定權限。
- Vite production build 成功；7173 回傳最新雜湊資產且包含設定圖示標籤與 settings 路由。公司 Work 設定停用瀏覽器控制，未取得登入後截圖；未開其他埠。

## 2026-11-11 課程清單快速管理

- 使用者確認安全生命週期並指定按鈕直接顯示於 `/courses`。擁有者卡固定顯示修改名稱；草稿另顯示公開課程與刪除課程。助教、學生入口及學生預覽無快速管理。
- 改名沿用完整課程欄位與 expectedVersion；公開沿用既有單向 activate。刪除第一次只展開卡片內不可復原警告，第二次確認才呼叫既有安全 DELETE；後端仍只允許完全空白草稿。
- 前端 80/80 與 Vite production build 通過。7173 最新資產含五個快速管理／確認文字。未新增 migration 或 API，未以真實課程執行刪除，未開其他埠。
