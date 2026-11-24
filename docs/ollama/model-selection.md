2026-11-20 角色政策更新：學生正式學習使用課程預設模型，前端不再顯示模型測試選擇，後端拒絕明確ModelProfile；舊失敗回合未指定profile重試時仍自動解析原白名單模型。Raw／比較四模型保留給本課staff本人測試。舊學生逐次選模說明保留歷史，現行以[11/19修正](../case-analysis-2026-11-19-corrections.md)為準。

# 師生逐次選模、B長文評語與Omni聊天協定

## 2026-11-18 精簡選稿資訊

回饋輪次sidebar不再逐列顯示模型名稱，減少長tag佔據選稿空間。各回覆與效能dialog仍標示當時模型，正式DB model_name／原模型retry／輸入區選模完全保持；只是導覽資訊精簡。

## 2026-11-18 現行驗證

使用者明確選擇每次只送一模型。輸入區對教師／TA／學生提供相同選擇，Raw切模不再清空，逐pair保留模型名稱；正式沿原model_name欄位。全隔離DB411/411及036→039升級通過，schema刪；Vue301、Chrome34、標準VS含Vue通過，手機學生畫面已檢視，靜態審查無可行動問題。健康檢查兩台online及四模型存在（Breeze省略tag等同latest）；本輪未進行新臨床生成品質評估。

目前7173正常bin/Debug PID46904、環境預設Omni不變、bundle index-CjJH7E-c.js、/courses200／匿名AI401，舊埠無listener。重啟清Raw，正式歷史保留。未修改四profile／模型Prompt／正式DB，無Migration，未提交推送。

更新：2026-11-18，唯一開發站 https://localhost:7173。

## 師生操作

1. 重新整理，以本課教師、授權TA或實際修課學生身分進入「個案分析 AI 助教」。學生視角預覽及教師查閱他人紀錄不提供選模／傳送。
2. 輸入區、提交按鈕旁的「本次回覆模型」預設沿環境設定，可選 IAN02 Llama3.1 8B、IAN02 Qwen3 8B、Taipei Breeze 7B、Taipei Qwen2.5 Omni 7B。
3. 正式階段切模型會保留草稿、修訂來源與歷史；提交為新輪次。要比較同一作答，可自行複用原文，再選另一模型提交。
4. 每個正式user／assistant沿既有`model_name`保存實際tag，輪次及回覆顯示該筆模型。歷史標籤不隨下拉改變。失敗重試須原模型及原提示版本；換模型或新版B提示請新提交。
5. Raw每次Send可選一個模型；切換保留本次LangChain完整對話及草稿，下一次提交才使用新模型。每pair標記當時模型並只留process memory，清除／刷新／TTL會釋放。Raw不寫DB，不作正式統計或可持久化比較紀錄。
6. 「查看 Ollama 請求」可查本人本頁新回覆真正送出的body。舊回覆、刷新或離頁後不重建封包。

## 授權與設定隔離

`CaseChatModels`為scoped resolver，只接受`OllamaOptions.ProfileNames`白名單，不能由前端指定任意主機、tag或檔案路徑。從base／環境appsettings→選定profile→Local→環境變數重新載入，不繼承目前active profile overlay。Local及環境變數依既有專案規則仍可覆寫部署設定。

每個請求獨立client／options，完成後釋放；不修改全域IOptions。教師／TA／學生皆先驗本課寫入資格，正式turn開始時鎖定runtime，same requestId不能換model，完成前重查課程資格及起始／目前Practice是否一致，防止升降權混入不同歷程。Raw每次Send的選模優先於session預設；省略時相容沿舊session預設或環境模型。Registry只留LangChain記憶、逐pair模型／量測及選用舊預設快照，不保留scoped provider或inspection。每次送出及完成重查授權，學生選模不取得staff inspection／診斷。

既有`model_name`足以區分目前四個不同模型tag，沒有新Migration或profile欄位。不宣稱資料庫已保存主機、模型digest、完整參數或完整請求快照；若日後同tag部署多個不同實體，需要另外定義實驗provenance。

## Omni短Raw也循環的原因與修正

合成短句走現有Omni模板達512輸出tokens／length且重複14次；只加repeat_penalty1.15仍循環，未採用。使用相同模型／抽樣，改為明確Qwen角色分隔的`api/generate`、`raw=true`後23tokens／stop、約0.2秒。額外stop沒有差異，未新增。

因此僅`qwen25-omni.json`新增`ConversationTransport=qwen-chatml-generate`。共用`OllamaClient.SendAsync`送實際`prompt`、`raw=true`、`stream=false`、既有`options`及必要`format`，並將generate的`response`欄位統一回傳；沒有第二套HTTP實作。非conversation的Assessment／Studio原路徑仍依原設定；其餘三profile仍`api/chat`，不改抽樣或遠端Modelfile。

角色格式為`<|im_start|>role`、原訊息、`<|im_end|>`，末端為assistant cue。Raw只組原LangChain buffer的user／assistant順序，沒有應用System Prompt、教學角色、分類背景或Summary；多輪記憶與重新整理／清除生命週期保持。Stage則仍有完整當前階段教授提示及當次作答，歷次草稿／回饋只留DB。

請求dialog安全顯示真正的generate prompt／raw／schema／options，其他模型顯示messages，不假造模型服務內部token展開。應用Raw不持久化不等於模型、遠端服務或基礎設施保證完全不留存；未取得遠端log／telemetry設定，不能作此保證。

## B長篇評語的有限重整

只對B且原文至少1000字追加自然語言提醒及機械呈現檢查。若主要內容為同句反覆，或有至少三個箭頭編碼配對，最多再呼叫一次feedback-only：送本次原文、已驗證的分類引用與階段關係，不送壞評語、舊稿或先前AI回覆。重整只產生短評語，第一輪annotations原樣保留，不重新猜碼。

再次循環／格式失敗明確報錯、不保存assistant，原user保留。這不是臨床正確性判定，不以程式刪重冒充模型自然回饋。B提示版本改`professor-four-phase-20261117-b-v5`，A/C/D仍v3，Studio原版本保持，舊資料不重寫。

兩次呼叫時`ProviderCalls=2`，可用token／時間量測合計並於dialog說明；缺失欄位保持不可用。教師可看兩份實際請求，其中第二份purpose為`case-feedback-repair`；正式學生完整往返仍計1。

## 2026-11-17 前輪驗證與仍適用限制

- 完整隔離PostgreSQL回歸406/406；036→039升級、重跑及舊資料指紋通過，測試schema已刪除。無正式資料寫入。
- Vue301/301、桌面1440／手機390 Chrome32/32；含選模、Raw清除／取消、歷史模型標籤、頁籤、失敗診斷及權限隔離。另修切頁清Raw期間立刻選模的競爭，以及唯讀切Raw載入不結束。
- 標準Visual Studio建置含Vue成功；既有NU1900弱點資料來源及>500KB chunk提示不影響成功。專案為JS／Vite，沒有獨立型別檢查指令。
- 真正C# Provider合成Raw第一／第二輪均23tokens／stop且無循環，第二輪prompt63tokens；合成五段2125字B prompt4103／response166／stop、完整JSON且無循環。HTTP mock驗最多兩次重整、保留標註／原文、不帶舊歷史及失敗不存assistant。
- 只用全新合成內容，未重送使用者病例或保存真實學生答案。格式成功不代表臨床／教學品質通過；分類細碼、混合時點及語意仍需教師核對。
- 其他模型保留各自既有容量預算，不保證同一長文在所有模型都可用；初步不同序列化的探針不能作為放寬證據。不會自動裁字、默默丟Raw history或自動fallback。
- 7173已核對正常bin/Debug PID7332、Omni新transport、bundle `index-CaiDGwMW.js`、課程頁200／匿名AI401；5097／5173／5174無listener。重啟清Raw，正式DB歷史保留。未提交或推送Git。
