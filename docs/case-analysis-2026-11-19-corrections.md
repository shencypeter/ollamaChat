# 11/19 會議修正交付與驗收（2026-11-20）

## 現況差異與完成範圍

修改前盤點詳見[執行紀錄](superpowers/plans/2026-11-20-case-formal-submissions.md)。保留現有四階段、模型選擇白名單、Raw LangChain 記憶、AI 分類與既有 E-learning。

| 項目 | 修改前 | 本次結果 |
|---|---|---|
| 四階段／AI歷程／模型註記／Diff／垃圾桶 | 已完成 | 保留，不重建 |
| 輸入與歷史 | 輸入在下方，左欄長清單與分類競爭 | 右側上方大型可調高度輸入框；左側精簡輪次、預設收合清單 |
| 分類標記 | 預設全底線、單選／淡化 | 預設無線；多選大類／子類、全部顯示／隱藏；未選完全不畫線 |
| 徽章互動 | 已有原文定位／分類說明／連續合併 | 保留外觀與合併，hover／鍵盤focus只暫時畫該片段，移開恢復持續篩選 |
| 正式提交／退回 | AI分析被當成唯一提交紀錄 | 明確確認的正式快照、獨立版本／時間／提交者，退回意見與重新提交 |
| 教師審閱 | 可讀全部學生分析草稿 | 預設正式快照；學生私人AI歷程API關閉，聚合統計仍保留 |
| 分類維護 | 靜態catalog／Prompt | 課內版本化名稱／說明／子碼／提示詞／停用設定，實際套用後續Prompt及schema |
| 舊紀錄相容 | 原文不可變 | 不回填正式提交、不改舊答案；新分析保存規則與system快照，舊null metadata用原靜態定義解讀 |

底線是Vue顯示規則，不是LLM的臨床分類指令。模型繼續傳逐字引用與分類JSON，不輸出HTML；前端才依篩選決定畫線，沒有新增關鍵字比對分類器。AI辨識不等於充分分析、臨床正確或成績。

## 角色與流程

- **學生**：預設進入A階段，在上方輸入、取得AI分析、從本人歷史複製修改；按獨立「正式提交」並確認後才分享快照給教師。學生學習工作區不顯示Raw、模型比較、Prompt或診斷工具，後端亦拒絕學生呼叫Raw及指定模型。
- **教師**：本人四階段教學練習與Raw測試保留選模及診斷；「正式提交與統計」僅讀正式快照，預設最新正式版本，可查看舊正式版本／同一學生版本Diff並填理由退回。沒有直接讀私人草稿的入口或API權限。
- **助教**：正式審閱／退回依既有grading限制；聚合統計依statistics限制，兩者獨立。分類設定可讀，寫入限實際本課teacher；平台角色不提升課內權限。
- **學生POV**：仍唯讀，不建立正式提交／測試對話、不讀他人資料。

AI呼叫十次與正式提交一次是兩個獨立數字。正式提交只接受本人已完成、非practice且未trash的AI分析來源。若輸入框已修改且不同於選取分析，需先再分析；避免正式答案與所附回饋不一致。

每個課程／學生／階段有獨立正式版本序列；最新版本退回後才能建立下一版。UUID重送／重複點擊回傳同筆，跨來源重用UUID回409，並行提交有鎖定與版本檢查。正式快照不能被trash、規則修改或再次分析覆寫；退回理由只存一次，舊提交保留。

## API

所有路徑前綴為 `/api/courses/{course}/case-analysis`；登入、課程成員、寫入CSRF與學生POV檢查沿用既有機制。

| Method／路徑 | 行為／權限 |
|---|---|
| GET `/submissions` | 學生本人正式快照；教師／grading授權TA看本課正式快照；附可關聯的開放個人作業 |
| POST `/submissions` | 學生確認提交；requestId、messageId、assignmentId（選填）、expectedPreviousId；回傳正式版本清單 |
| POST `/submissions/{id}/return` | 教師／grading授權TA，必填1–2000字理由，只退回最新正式版 |
| GET `/rules` | 本課教師／TA讀分類規則版本 |
| PUT `/rules` | 本課教師，以expectedVersion儲存新版本，409保護同時編輯 |
| GET `/records` | 原聚合AI使用統計，沒有私人答案 |
| GET `/records/{student}/{stage}` | 改為403，說明私人草稿不開放；改用正式快照審閱 |

本人 `/stages/{stage}` 歷史仍完整，DTO增加ruleVersion／ruleSnapshot；不回傳保存的完整system prompt。Raw清除仍可由原持有人清理，即使權限已撤銷；新增測試對話／送訊息限本課staff。

## 資料庫及相容性

- `041_case_formal_submissions.sql`：獨立正式快照、course／student／stage／version唯一鍵、既有assignment同課複合FK、原AI訊息FK、模型／Prompt／分類快照、退回人／時間／理由、保護快照內容的trigger。
- `042_case_classification_rules.sql`：課內append-only規則版本／修改者／時間；AI訊息增加nullable rule_version／rule_snapshot／prompt_snapshot並保護不可變性。新回合保存完整定義，停用父碼排除子碼；舊NULL資料不回填。
- 規則修改會改後續模型的啟用代碼schema、驗證與system分類資料，沒有全域可變catalog。重試沿原規則／system，編譯版Prompt版本不同仍拒絕冒充原回合。
- 已保存的分類身分不硬刪，以停用保留；未儲存的新分類可移除。至少保留一個有效啟用碼，整個A/B/C/D大類可停用；A2c／B2c仍不可啟用。提示詞是模型的語意線索，不是命中即分類。
- 分類設定不保證模型遵守；仍需教授校閱。維護介面沒有完整底層system prompt編輯器，也沒有自創審批流程。

## 設計假設與待確認

1. **私人草稿存取**：預設僅學生本人；教授日後若需要全部AI逐輪內容，需明確業務授權及存取政策，本次沒有另外開後門。
2. **正式提交粒度**：目前每課／學生／階段一條版本序列。可選關聯既有同課、已公開、開放且未截止的個人作業；未選則為課內階段作答。若要每一份作業各自有四階段、強制必選作業、不同截止／補交規則，應另確認，不擅自建立新考核制度。
3. **成績**：正式快照不會自動建立既有測驗提交或評分資料，不生成AI分數、不改既有成績簿。若要從這類正式快照直接啟動原作業批改／成績發布，需確認映射到題目、配分、作答次數的規則後另接；本次已可在個案分析內審閱與退回。
4. **正式版來源**：要求先完成AI分析再提交該選取版；尚未分析的輸入文字不直接提交。若教授希望完全不經AI也能正式繳交，需再定義無回饋提交的流程。
5. **臨床驗收**：本次驗證資料流程／封包與權限，未以真實病例重送模型，也沒有宣稱分類正確率提高。

## 主要檔案

- `ClientApp/src/components/CaseAnalysisChat.vue`：上方輸入、精簡歷史、角色入口、正式審閱。
- `CaseFormalSubmissions.vue`：確認提交、正式版本／Diff與退回。
- `CaseClassificationRules.vue`：教師分類設定與新版本。
- `PhaseAnnotatedAnswer.vue`／`PhaseAlignmentNotice.vue`：多選與局部hover、預設無線及教學限制。
- `Services/CaseAnalysisChat.Submissions.cs`／`Models/CaseFormalSubmissionModels.cs`：正式快照、版本、授權與audit。
- `Services/CaseAnalysisChat.Rules.cs`／`Models/CaseClassificationRuleModels.cs`：分類版本及Prompt組裝。
- `Services/CaseAnalysisChat.Turns.cs`／`CaseAnnotationContract.cs`／`OllamaClient.cs`：每回合規則快照與啟用schema；四profile相容性保留。
- 兩個controller、041／042 migration、對應後端／Vue／Chrome與升級驗證。

## 本週 Demo 驗收

1. 學生進入個案分析A／B／C／D任一階段；上方輸入長篇合成作答，按「取得AI分析」。確認原文／回饋出現，但正式提交仍為零。
2. 分類預設無底線；點選兩個不同分類，確認僅這兩類有線；取消一個、全部顯示／隱藏。切到徽章，滑鼠移到徽章只暫時畫該片段，移出不改原篩選。
3. 複製歷史作答至輸入框修改，再分析；確認歷史多一輪、正式次數不變。按「正式提交」確認，選填課內作業，形成正式第1版。
4. 教師切「正式提交與統計」；看見正式版而非學生所有分析。檢查內容、時間及版本，填理由退回。
5. 學生看退回意見，修改／分析後正式提交第2版；確認第1版保留、可比較兩個正式版本，教授預設新版本。
6. 教師展開分類維護，改一項說明／提示詞或新增子碼後儲存；確認版本增加。新分析帶新規則，舊分析與正式快照仍用舊名稱／定義。
7. 教師在本人教學練習使用Raw／四模型比較；學生沒有測試入口，修改API ID或指定模型亦不得繞過授權。

## 驗證與部署紀錄

- 後端 **476/476**，0失敗、0略過；包含新正式流程、授權、並行／idempotency、pool1重送、規則快照與原有課程／作業／成績回歸。
- Vue **398/398**；Chrome **53/53**，包含1440／1024／390、淺深色、分類多選／局部hover、師生角色、正式v1→退回→v2、模糊連線失敗同UUID重送及新舊規則隔離。
- 036→042非空資料庫分段升級、rerun與course／membership／grade／chat指紋成功；測試schema已刪除。
- 正常Visual Studio Build（包含Vue依賴／Vite）成功；既有>500KB chunk提示仍為warning。無獨立TS檢查命令（目前JS專案）。
- 041／042已套用7173所連public；原chat／sessions／courses／members／assignments／submissions／grades指紋一致，未回填正式版或規則。沒有代真實學生提交／退回。
- 唯一7173已重建並啟動正常bin/Debug程式（最終PID17908），qwen25-omni預設及四profile保持；Raw暫存因重啟釋放。`/courses`200、匿名AI／新API401、Swagger routes及最新bundle已核對。
- 最新靜態資產：`index-ieyN-WuW.js`／`index-1lDeOsds.css`。手機／桌面正式流程與規則儲存畫面已視檢。
- 未執行真實LLM臨床品質測試；四profile以HTTP Mock檢查schema／Prompt／metadata，不宣稱模型判讀正確。沒有讀出或重送真實病例。
- 初次sandbox testhost IPC失敗、瀏覽器讀取遇並行build清理與modal焦點還原等待問題，均在修正執行方式／同步等待後完整重跑成功；實際抓到的規則編輯狀態問題已補回歸測試。
- 保留既有未提交工作區；本次未commit／push。

下一輪只需就上述業務假設向授課教授確認：是否授權查閱私人AI草稿、每份個案作業的四階段／截止規則，以及是否接入既有人工成績項目。未自行開始新的個案活動或AI功能。
