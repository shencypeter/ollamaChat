## 2026-11-24 撰寫狀態紅框統一置於輸入框下方（現行）

使用者補充「模型暫時無法回應」紅框位置。除既有推論失敗教授卡外，共用error-box在可見composer時亦移到textarea／字數下方；涵蓋Raw建立失敗及模型失敗後歷程刷新失敗。回饋／唯讀／尚未取得設定時保留頁首fallback，避免錯誤消失；教師診斷權限保持。新增2Vue回歸先紅後綠，111相關Vue、6Chrome（1440淺／390深位置與原師生診斷重試）、Vite及diff check通過，手機深色截圖已視檢。static index-C_3y-H-w.js／index-D-YehcAR.css已供唯一7173，原PID14664不重啟、不清Raw。無後端／DB／Prompt／模型變更、未commit/push；規格與歷程同步。

## 2026-11-24 Omni輸出上限與作答下方失敗卡（現行）

使用者確認output_limit模型為Taipei Qwen2.5 Omni 7B。僅qwen25-omni正式CaseFeedbackMaxTokens由5120升8192，Raw512／32K context／48KB及其他profile相容設定保持；不改Prompt／歷史組裝，不將半份正式JSON當成功。教師output_limit診斷新增當次輸出額度及provider實際生成量，未提供則明說未知，不記為零。

送出／重試失敗改留在composer下方教授輪播卡，停止動畫，顯示原作答保留、原因與就地重試；教師依原權限看診斷／當次失敗封包，學生不取得診斷。一般載入／設定錯誤仍在頁首。重試沿同一失敗作答，不新增成功回饋或假輪次；卡片只暫存頁面，切模式／模型／清除後重設。

105聚焦後端＋3隔離DB、109Vue、8Chrome、VS含Vite及diff check通過，1440淺／390深失敗卡截圖已視檢。合成66片段案例在舊5120與新8192均stop完成，未重現原個案截斷，未重送使用者原文；提高額度提供餘裕，不保證所有長文完成或臨床正確。非空036→043升級／rerun與schema清除完成，無新Migration。唯一7173同bin重啟PID14664，courses200／匿名API401，bin確認8192／512／32768；static index-B7EwF-Ce.js／index-B6oflx8_.css。Raw暫存已清、DB歷史保留，未commit/push。

## 2026-11-24 分類頁籤全部採字母徽章（現行）

依使用者補充，作答分類標註下方所有可見A／B／C／D頁籤的字母均用對應分類色底白字徽章，中文仍不填色；移除原藍點及相關文案，目前作答階段徽章加細外圈／title，與目前瀏覽tab底線區分。保留aria目前階段標示及跨頁籤選取，無提及的階段仍不新增。49Vue、6Chrome淺深／1440／390及Vite、diff check通過，截圖已視檢。7173 static index-C_nglg3B.js／index-Cd4Y5nbL.css，不重啟／清Raw，無後端／DB／Prompt變更，未commit/push。

## 2026-11-24 徽章範例僅顯示階段字母（現行）

依使用者更正，編碼徽章tab範例移除數字1，僅A／B／C／D、保持目前階段色及無stage回退A；文章實際代碼不改。49Vue、2Chrome淺深及Vite／diff check通過。7173 static index-CG0cDsgW.js／index-Cgusnev-.css，不重啟／清Raw，未commit/push；無後端／DB／Prompt更動。

## 2026-11-24 徽章範例跟隨目前階段（現行）

編碼徽章tab的裝飾範例依props.stage顯示A1／B1／C1／D1及同階段分類色，無有效stage保持A1 fallback；不依下方分類瀏覽／選取而變動，aria-hidden保持。既有測試加四phase動態切換先紅後綠，49Vue／2Chrome桌面淺與手機深／Vite／diff check通過，範例截圖已視檢。7173 static index-CjyZ3Y0L.js／index-Dn85U33t.css，不重啟／清Raw、無後端／DB／Prompt更動，未commit/push。

## 2026-11-24 指引對齊核心問題（現行）

教師CaseStageGuidance的展開描述／條列／撰寫重點移到核心問題正下方，桌面左欄留階段、右欄問題與指引；手機與問題文字同縮排。欄名同步「階段／核心問題與撰寫指引」，學生內容及native details保持。原2Chrome對齊斷言先失敗，修復後4Chrome師生1440／390、103Vue、Vite／diff check通過，淺深截圖已視檢。7173 static index-D2vJDsVH.js／index-B1O_tgjl.css，不重啟／清Raw、無後端／DB／Prompt變更，未commit/push。

## 2026-11-24 作答分類標註與緊湊按鈕（現行）

PhaseAnnotatedAnswer將ABCD分類標題／region改「作答分類標註」，縮短目前階段與操作說明但保留臨床正確性／未標註非錯誤提醒。分類選取按鈕由固定1／2／3欄改content-width flex wrap，保留44px點擊高度、✓預留與長標籤自動換行；只移除主分類區grid，說明dialog多欄保持。174相關Vue、8Chrome（1440／1024／390、淺深、寬度／44px／無溢位、選取／hover／dialog欄數）與Vite、diff check通過，桌面／手機截圖已視檢。7173只更新static index-CEIYq4HM.js／index-QbdXrYNr.css，不重啟／清Raw，無後端／DB／分類／Prompt變更，未commit/push；規格／標註UI／歷程同步。

## 2026-11-24 教師本人練習不顯示正式提交提示（現行）

CaseFormalSubmissions在非review且courseRole教師／TA或isPractice時不渲染整個正式提交區，移除本人AI測試的「此階段尚無正式提交」誤導；CaseAnalysisChat作答中的正式提交引導也只給實際非practice學生。教師review仍顯示學生正式狀態／版本／退回；學生原送交不改。2教師／TA單元檢查先紅後綠，相關125Vue、6Chrome師生1440／390及提交→教師退回、Vite／diff check通過。7173只更新static index-ClxCPL1-.js／index-ncOD_3sK.css，不重啟／清Raw，無API／DB／授權／Prompt更動、未commit/push。

## 2026-11-24 Raw回覆語言指示（現行）

使用者明確要求Raw依本次提問語言回覆，中文採臺灣繁體。RawChatSessions每次組請求先加一則language-only system（含明確要求另一回覆語言時優先依其要求），再接LangChain完整user／assistant與當次user；先納入原預檢，禁止截歷史。此節取代Raw完全無應用system的舊規則，仍不帶階段／教學／角色／評分提示。指示不放LangChain buffer、不回傳成歷程、不寫DB／log，四profile transport／採樣／timeout保持；A–D提示不變。

新四profile wire及預檢測試5項先紅後綠；84聚焦後端＋4隔離DB（無Raw持久化／統計、跨模記憶／授權）與24效能dialog Vue通過，非空036→043升級／rerun／清schema及正常VS含Vite、diff check通過。未呼叫真實模型驗語言遵循、不宣稱模型必定遵守。更新Omni請求dialog文案及transport註解，其他功能保持。

唯一7173已同bin重啟PID36420，courses200／API401，新static index-DEoMdYjt.js／index-DL59iGy8.css；Raw暫存清空、DB歷史保留。無新Migration／profile變更、未commit/push，規格／Ollama／個案流程與歷程同步。

## 2026-11-24 合併教師階段概覽與核心問題（現行）

CaseStageGuidance教師／TA四階段summary同列顯示階段中英名稱及核心問題，點選展開原描述／面向／撰寫重點；移除教師重複比較表。桌面雙欄、手機階段與問題上下排列，目前階段預展與native details鍵盤保留。學生仍單階段指引及四階段對照，原資料來源與AI提示不動。既有角色測試先紅後綠，103 Vue／4Chrome師生1440／390／Vite／diff check通過，淺色桌面及深色手機視檢。static index-CDFFRM79.js／index-ZkweeFXR.css供7173，不重啟／清Raw，無DB／後端／Prompt變更、未commit/push。

## 2026-11-24 功能頁籤圖示（現行）

作答／回饋／AI對話測試分別新增既有Lucide PencilLine／ClipboardCheck／MessagesSquare，17px隨文字色且不壓縮，SVG aria-hidden，保留文字名稱／鍵盤／角色限制。103 Vue、4Chrome師生1440／390、Vite與diff check通過，桌面已視檢。7173 static index-BPZ_ANmJ.js／index-OjgOwlXe.css；不重啟／清Raw，無後端／DB／Prompt變更、無新套件、未commit/push。

## 2026-11-24 Raw Enter傳送開關（現行）

只有Raw composer提供「Enter 傳送」switch，預設off、僅目前元件記憶體，重整重新off；開啟時未修飾Enter走原send，Shift／Ctrl／Meta／Alt+Enter保留原輸入。防IME composition／isComposing／229、長按repeat及busy重複，沿既有readonly／loading／expired／長度／空白保護；A–D不提供開關且Enter不傳送。開啟顯示Shift+Enter換行。

新增2聚焦Vue情境先紅後綠，相關103/103、Chrome1440淺／390深2/2（真換行／IME／送一次／正式隔離／reload off）、Vite與diff check通過，手機開關截圖已視檢；首輪Chrome未接受原離開Raw確認故留原頁，測試明確accept後通過，不改產品guard。7173只更新static index-DIrj1uEv.js／index-IGV6GYEa.css，不重啟／清Raw，無後端／DB／Prompt變更、未commit/push。規格與歷程同步。

## 2026-11-24 階段頁籤字母徽章（現行）

CaseAnalysisChat分析階段tab僅A／B／C／D各自包為藍／琥珀／紫／玫紅白字徽章，中文標籤保持透明背景與原選取樣式。沿theme code tokens，原完整aria-label、字母與中文間空白、鍵盤導航及頁籤行為保留。101相關Vue、2Chrome桌面淺／手機深、Vite與diff check通過；桌面預覽已視檢。7173只更新static index-B5AaW4p6.js／index-Dewg2_Hr.css，不重啟、不清Raw，無後端／DB／Prompt改動，未commit/push。

## 2026-11-24 功能對比主題（現行）

依使用者要求捨棄Teams紫色，並明確允許重配ABCD色。新增ClientApp/src/theme.css統一語意tokens，main於原style.css後載入；原根palette移除、既有排版／技術架構保留。深海軍藍導覽、白／深藍內容卡、分離panel標頭、藍灰作答框、暖中性色學生原文、綠色AI回饋；實心青綠代表操作，替代操作維持有對比外框。移除等待完成連結／分類維護／分類操作中的硬碼紫色，刪除操作有獨立深紅底，避免深色錯誤文字色套白字按鈕。

ABCD改藍#1d4ed8／琥珀#9a5b05／紫#7c3aed／玫紅#be185d，徽章及已選保持白字；深色底線／外框用亮版、選中✓及實心保持。分類定義參考的標題／hover沿相同色，沒有更改代碼／模型分類／prompt／DB或歷史內容。theme tokens替代各元件舊色，本次只改呈現。

Vite、154相關Vue、最終23Chrome（師生Portal／成績、預覽四階段、ABCD徽章／選碼／tooltip／複製、1440／1024／390與淺深、4項實際text>=4.5及input border>=3檢查）、diff check通過，桌面首頁／作答及手機深色表單／回饋截圖已視檢。未重跑無關後端／完整Vue／完整Chrome，不宣稱全站WCAG稽核。初次腳本工作目錄錯誤及新測試參數語法均已更正後實跑，沒有把失敗當成功。

唯一7173原PID31388未重啟、不清Raw，courses200／新CSS200／匿名API401，static index-Bopz87Kt.js／index-BQzxVR6L.css。無Migration／API／授權／模型更動，未commit/push；既有未提交變更保留。規格、標註UI與歷程同步。

## 2026-11-23 Qwen3 慢回覆與逾時（現行）

使用者提醒早上擴大token容量並提供逾時封包。確認僅system＋本輪user、32K context／4096輸出，非送入歷史；745字仍有66個固定分類欄。IAN02 Qwen3預設會thinking，合成短答及結構probe可只產生thinking而無content。僅qwen profile新增Think=false、CompactCaseFeedback=true及TimeoutSeconds=600；32K／48KB／輸出4096、教學system與其他三profile相容設定保持。Raw亦沿該模型think參數但仍無應用system及DB歷程。

OllamaClient可選think（null不送），教師provider_timeout顯示實際HttpClient等候秒數，不洩漏transport內容。正式pending lease覆蓋分類及一次分類重試（2 calls），B長文另預留評語修復（3 calls），每次timeout乘次數加30秒；不增加實際retry。73聚焦後端及1隔離DB lease測試先紅後綠通過，非空036→043升級／rerun與schema清除通過，VS含Vite及diff check通過；未重跑無關全套。

真實Qwen3合成66欄、think=false／compact：340.68秒、input4067／output2793／stop、66欄全回、thinking0，非臨床品質驗證；32K載入footprint約9.62GiB、VRAM約4.01GiB，不能以此斷言確切硬體或原逾時唯一原因。未重送真實作答、未保存模型內容。唯一7173重啟PID31388，courses200／API401，static仍index-DSVpev3x.js／index-eR4FEaQw.css；Raw暫存清空、DB歷史保留。無Migration、未commit/push。新參數降低無效生成並容許較慢完成，不保證所有長文在600秒內完成。

## 2026-11-23 教師學生作答預覽與聚焦測試（現行）

教師／TA學生POV（experience.student且course.canPractice，或原教學role）預設開「作答」，可切A–D／回饋並讀學生本階段指引、核心問題及分類定義。canViewWriting與readonly分離；預覽textarea／送出disabled，send/retry仍readonly guard且後端原Preview授權不改，無模型選單／Raw／分類維護／正式提交入口。明確「返回教學模式做模型測試」同課AI section之view=teaching原生連結，經既有router guard清預覽，重新取得教學權限；一般學生／非preview停用課程不擴權。

本次先紅後綠單一Vue工作流程覆蓋四階段，相關101/101、Chrome1440淺色／390深色2/2及Vite、diff check通過；腳本test:case-chat約4.5秒、test:e2e:case-preview約2.5秒已驗。只跑影響範圍，未重跑無關515後端／全Vue／全Chrome，亦未為降數刪測試。新增docs/testing.md與README說明日常聚焦、合併前完整回歸，沒有500–600項目標。

7173仍PID35924、不重啟／清Raw，courses200，新static index-DSVpev3x.js／index-eR4FEaQw.css；手機預覽截圖已視檢。無後端／DB／Migration／Prompt／模型變更，未commit/push。規格／權限及歷程同步。

## 2026-11-23 重新啟動7173（當時執行狀態）

使用者要求啟動；先確認7173無listener，再以既有bin/Debug/net8.0/AiTeachingAssistant.exe、Development、--urls https://localhost:7173隱藏啟動。現PID35924、/courses HTTP200；無重新建置、程式或資料異動，沒有另開埠。下次仍須重查執行狀態。

## 2026-11-23 回饋歷程按鈕分頁（現行）

正式A–D本人回饋歷程改用api(fetch)初次5份作答／配對回饋；僅「載入更多（5筆）」續抓append，無scroll/IntersectionObserver。模型及垃圾桶採後端篩選，全部模型／未記錄與退役標籤metadata仍完整；清單顯示已載入／總筆數，下一頁失敗保留原內容及游標並可重試，重複點擊鎖住、模式／身分／篩選切換abort且拒收晚到結果。全域#N由server先排名後篩選，新增頁保留選取與草稿，修改來源另保存當時#N。

既有stage GET／Send／Retry／trash/restore可選paged=true，固定5pairs＋metadata，未指定仍舊full API。新HistoryPage partial只讀5份body與配對reply，游標是全域ordinal；metadata與頁面同repeatable-read，query均owner/course/stage/practice。Target直接定位本人舊完成／失敗／還原項，一樣最多5pairs，不自動掃頁；有新資料造成prefix不相交則有界重設，避免跳過缺口。比較僅列已載入且未trash作答，可手動載入更早版本；Raw仍暫存多輪，不新增DB歷史。

完整隔離DB515/515、非空036→043升級／rerun／指紋及清schema、Vue460/460、Chrome全81/81＋最後分頁2/2、VS含Vite及diff check通過。獨立review六項跨分頁問題與晚回應guard已補；含篩選失敗回復、跨模型失敗走retry、不漏併發新列、舊回覆target、#N不漂移。1440淺色／390深色清單截圖已視檢。唯一7173已同bin重啟PID28444，courses200／paged API匿名401，index-zB-ie8v_.js／index-C6LzIue2.css；Raw暫存清除、DB歷史保持。無新Migration／模型／Prompt改動，未commit/push；規格／權限／輪次與plan同步。

## 2026-11-23 四模型容量修正（現行）

使用者回報預檢UTF8 14056/48000、估token14056/12288；原三profile以CJK3及16K context造成應用端拒絕，Omni亦16K。唯讀show確認原生context：Llama131072、Qwen3 40960、Breeze／Omni32768。四profile統一num_ctx32768／CaseChat.MaxContextBytes48000／template overhead512，仍預留實際輸出；三model正式／Raw4096、Omni正式5120／Raw512不改，CJK估算、transport／採樣／prompt／歷史策略不改。不關預檢或裁切，超新預算仍拒收，8000字元上限不保證所有wrapper均可容納。

新增四profile容量與16種phase/full prompt長文wire驗證，含2125字85slots、完整原文、不含舊稿、Raw3則無system與輸出額度；47相關通過，最終可執行後端426通過／85DB條件略過（本次無DB變更），VS含Vite建置與diff check通過。真實兩主機四模型各一筆合成bounded1-token探測皆接受32K（length是刻意一token輸出上限，非輸入截斷）；不作分類品質結論。唯一7173已重啟PID11408，courses200／API401，四份bin設定正確，bundle仍index-Pfpdydt9.js／index-DV5yUXLi.css；Raw暫存已清，DB歷史保留。無Migration／額外環境，未commit/push；Ollama與規格同步。

## 2026-11-23 教師分類逐項編輯（現行）

使用者澄清不希望在modal一次編輯A1–D末項。CaseClassificationRules預設僅ABCD分區精簡清單，每列代碼／名稱／停用狀態與編輯；點一列才顯該分類唯一表單，其他欄位不在DOM。儲存此分類沿原expectedVersion完整版本API，只有本次編輯變更；成功回清單並聚焦原列。取消返回清單／Esc／關閉還原最後接受資料，不能夾帶上一項未儲存修改。新增亦逐項：新增基本資料→單項設定→儲存，未存可移除；已存分類仍只可停用，409保持目前表單。

Vue449/449、Vite、Chrome1440／390深色2/2及diff check通過；桌面清單／手機單項截圖已視檢。7173 static index-Pfpdydt9.js／index-DV5yUXLi.css；不重啟、不清Raw、無後端／DB／Migration／API／Prompt變更，未commit/push。下方同日完整清單modal為先前呈現，由本節取代。

## 2026-11-23 教師分類說明改用彈窗（當時設計）

CaseClassificationRules頁面僅留紫色「編輯分類說明」入口，原ABCD階段／增減／名稱／說明／關鍵詞／啟用與版本歸入native dialog。首次開啟才GET，header及footer固定、內容獨立捲動；Tab／Shift+Tab在可見可用控制間循環，Esc／X／關閉捨棄未儲存修改並還原最近成功載入／儲存版本、焦點回入口。取消載入會abort且忽略遲到結果，重開使用新signal；PUT期間暫禁關閉，409保留本地修改及重新載入入口。teacher＋flag＋非學生POV限制沿用，儲存仍發新版本、舊AI與正式快照不重寫。

Vue448/448、Vite、Chrome桌面1440／手機390深色2/2與diff check通過，兩尺寸彈窗截圖已視檢。只更新7173 static index-CPiil10V.js／index-CIkHRmNG.css，原PID26768未重啟、不清Raw，無DB／Migration／後端／API／Prompt改動。未commit/push；規格與UI文件同步。

## 2026-11-23 ABCD工具收進徽章頁籤（現行）

ABCD分類區（分類提示、全選／隱藏、階段頁籤、代碼及未提及）只在有有效標註且選「編碼徽章」時顯示，位於同一annotation tabpanel的原文下方；原文頁籤只保留文章與複製操作，無標註不顯空ABCD區。以v-show保持分類區DOM／頁籤狀態，兼容舊legendTarget的外部host亦按badgeMode隱藏；切原文沿既有watch清選取／hover／dialog，hide-all回原文並移焦該tab，避免焦點留隱藏區。

新元件可見性／tooltip清理／焦點紅綠測試與兩尺寸Chrome驗收，既有互動測試改先開徽章再操作，不改分類證據或選取邏輯。Vue444/444、Vite、Chrome相關14/14與完整78/78、diff check通過；1440淺色原文／徽章及390深色徽章截圖已視檢。僅static更新7173 index-BIQdpuRV.js／index-BEP4FGGE.css，不重啟／清Raw、無後端／DB／API／Prompt／模型改動，未commit/push；規格、輪次與標註UI文件同步。

## 2026-11-23 單層作答回饋歷程與模型篩選（現行）

依使用者要求移除正式階段的「對話內多份版本」導覽：全部已授權stage歷史的每份user與同session配對reply合成單一項目，依記錄日期由新至舊顯示，全域#N在模型篩選／垃圾桶間不重編。新caseFeedbackHistory純投影保留原message/session ID、內容／revision ancestry、正式快照；不重寫DB或新建平行儲存。舊缺日期沿既有session順序與訊息順序穩定退回。

舊對話select改「模型篩選／全部模型」，取歷史reply.modelName優先、request fallback，缺值標未記錄模型；退役模型也可查，與composer下一次送出模型分離。篩選不發請求、不清草稿／revision parent、不授予學生指定模型權限；只含trash的模型正常清空詳情，不回落至其他模型。完成新回覆若被篩選隱藏，會揭露新結果；完成連結可清篩選、選原全域#N、沿原捲動／focus。

移除重新開始階段與返回目前對話UI，Raw仍原多輪。看closed本人舊作答不再鎖住當前composer；completed可copy並沿既有後端跨session ancestry提交。closed或非active尾筆失敗不能retry，唯讀／學生POV／正式提交保護不改。效能／請求snapshot及trash清除以實際owner session取鍵，避免跨舊房間混用。

新增紅綠Vue回歸及純工具測試、調整7項舊房間導覽假設而保留資料／草稿／權限測試，Vue全443/443、Vite、Chrome完整76/76及diff check通過；獨立read-only review無可行動發現。桌面教師1440／手機學生390深色模型篩選截圖已視檢，無溢位。只更新7173 static index-0PXAQKoZ.js／index-DYWbBA_F.css，courses200／API401，現有PID26768不重啟、不清Raw，無後端／API／DB／Migration／Prompt／模型配置變更，未commit/push。

## 2026-11-20 四階段撰寫指引同步教師／學生／AI（現行）

使用者提供四階段通用說明及「尚待原始教材核對」限制，並明確要求一起改。新增Configuration/CaseAnalysis/stage-guidance.json：A–D中英標題、說明、所有面向、撰寫重點、核心問題及來源註記；Vue直接import，C#以EmbeddedResource同源載入，不新增API／DB。CaseStageGuidance讓教師／TA在正式階段查四階段（目前預展）與核心問題表，學生作答區查目前階段及對照，皆原生details預設收合；Raw不顯示、學生POV無教師區，展開不發請求或改草稿。

正式AI user.outputRequirements追加本階段問題、面向、撰寫重點與未核對限制；原system教學／代碼表／課內規則及四profile不改，不擅加碼或把指引當必填。B長文評語重整也帶同份指引。A/C/D新v8、Bv10，舊快照不改。實際完整wrapper皆做容量預檢；B精簡重複user指示、AI省略重複B一般描述，原system全文／例示保留，四階段實際Omni2125字仍通過，未調context或token額度。

503後端全DB、非空升級rerun、436Vue、Chrome9及VS含Vite通過；共享resource、payload原文／指引、Raw隔離、role、草稿、手機淺深與鍵盤驗證。獨立review的Brepair漏指引已先紅後綠修復；首版新Chrome測試預設view卻填write造成等待，改明確write後9/9，不為測試改產品。1440教師／390學生深色指引截圖已視檢。真實Omni合成66分句新指引回65非空標註／65唯一位置至744/745、input4116/output3677/stop（各slot皆回填，允許空碼），只驗結構而非品質。

唯一7173已重啟，PID26768，index-Bzzk7Ace.js／index-mHeLbH0X.css，courses200／API401；Raw暫存重啟清空，舊持久化資料／正式提交不動、無新Migration。新作答才採新指引，舊base版本重試沿409保護。未commit/push；規格、Ollama、輪次／維護與評估文件同步。

## 2026-11-20 複製靠右／移除重複垃圾桶按鈕（現行）

PhaseAnnotatedAnswer的actions slot包為annotation-actions，inline-start:auto靠右；窄螢幕換行仍靠右。CaseAnalysisChat移除round-heading一般「移至垃圾桶」，垃圾桶檢視的還原按鈕及版本卡右上X／確認／授權／正式提交保護保留。既有trash流程測試改走卡片X，不刪測試意圖。

相關Vue130/130、Vite、Chrome10/10（trash／closed／卡片保護7＋1440／1024／390複製幾何及分類3）通過；桌面淺色／手機深色截圖已視檢。7173 static為index-CndzKNyp.js／index-G2Ca_jsR.css，不重啟／清Raw、不改DB／API／模型／Prompt，未commit/push。規格／輪次同步。

## 2026-11-20 紫色回覆完成入口整合輪播（現行）

AiReplyWaiting沿同一張composer下方卡片由pending轉completed：avatar保留、完成圖示／全部輪播指示線亮起、紫底白字「查看最新回饋／回覆（#N）」；完成停止typewriter／spinner／timer並收起pause／next／示意說明，開新請求重設pause後重新播放。CaseAnalysisChat沿既有visitLatestResult選最新版本、切回饋、history捲頂與結果smooth scroll／focus，reduced-motion仍auto。完成卡不算聊天訊息、failure不產生完成入口。

完成／下一請求及paused跨輪測試先紅後綠，相關Vue86/86、Chrome桌面／手機4/4與Vite通過，1440淺色及390深色截圖已視檢；diff check通過。僅static更新7173，bundle index-EXuKRheX.js／index-uzDMnNJ2.css，不重啟／清Raw、不改DB／API／Prompt／模型。未commit/push。規格與輪次文件同步。

## 2026-11-20 長文分類完整性與文章下方分類工具（現行）

合成範例重現：745字／66分句，舊回覆48筆但僅24組位置／碼，單一位置同碼重複24次，末標註332；output3160/5120、stop，非context或解析漏失。單增128仍輸出length，因此正式長文32–128個有效候選採固定s000等欄位，每分句回codes/explanation/status、無據可空；完整answerText與本輪slots送模型，不帶舊稿。伺服器只還原既有精確UTF16 annotations陣列，不補語意分類。缺漏／重複／未知欄拒收。短文原契約，機械同位置同碼同狀態大量重複最多fresh retry一次，失敗明確報錯。Raw及四profile設定不變；Studio仍48，不套正式新容量。

新043只改assistant annotations陣列上限128，非空042→043／rerun／歷史trigger不變已驗；public已套，聊天／課程／成績等七表指紋不變。新A/C/D prompt20261120-v7、B20261120-b-v9，舊快照不改，舊結果要重新送出。真實Omni同範例回66筆／66唯一位置至末句744/745（末標點不編）、input3964/output3905/stop，未增加5120或更換模型；此驗結構完整性，不保證每碼或臨床正確。超128分句／單片過長沿舊自由quote路徑；超context／output仍拒收，不默默截斷。

使用者追加ABCD工具放文章下：CaseAnalysisChat移除前置categoryHost，PhaseAnnotatedAnswer在學生原文後顯示同一組ABCD頁籤／分類／未提及，版本歷程仍側欄。保持plain、選碼自動徽章、hover、定義與正式快照。495後端（DB全開）、非空升級、432Vue、Chrome68及VS含Vite建置通過；獨立review兩次無可行動問題。唯一7173已重啟新後端，PID27164，bundle index-H9-uCcCN.js／index-CmBP1hjR.css；courses200、受保護API401。Raw暫存因重啟清空，持久化歷史不變。未commit/push。

## 2026-11-20 原文純文字／選分類自動徽章（現行）

使用者要求原文固定純文字，分類啟用選取時自動切徽章，避免沒有代碼的底線。PhaseAnnotatedAnswer 沿原 underline key 保持caller相容；只有 badgeMode 渲染 annotated spans／references，且原文 spans 不再 role=button 或處理hover／focus／click。emphasize與showAll有選碼時切badges，sync mode watcher在顯式plain清篩選，hideAll真正回plain；取消最後一碼／取消全部選取保持badge但無持續底線。徽章局部hover／focus／說明dialog、ABCD色／引用快照／run合併／文字copy均保持。PhaseAlignmentNotice同步plain未顯示標註文案。

新增自動切換／嚴格plain／show-hide流程測試先紅後綠；更新既有14項對舊無代碼span的假設，說明操作改走badge，來源比對移除呈現的badge文字、選取只計有underlinePhases者，沒有刪證據測試。Vue全432/432、Vite與diff check通過；獨立read-only review無可行動發現。Chrome新增驗收整合於原測試（總數68不變），targeted26/26、compact2/2及最終full68/68皆exit0，1440淺色／390深色的選取與嚴格原文截圖已視檢，無溢位或可行動問題。7173 /courses200已供 index-BAo8G1w5.js／index-mXH6VVv2.css。僅前端、不重啟／清Raw、無DB／API／Prompt／模型改動、不跑無關後端，未提交推送。規格、標註UI、輪次與既有短plan同步。

## 2026-11-20 撰寫參考只顯示標題、滑入看定義（現行）

依使用者澄清，CaseCategoryReference 移除常駐完整定義卡片，改為可換行的「代碼 — 標題」小按鈕；ABCD 階段展開與目前階段預設保持。滑鼠移入／鍵盤聚焦才顯示定義 tooltip，滑出延遲關閉；點選或 Enter／Space 可將焦點移入說明以捲動長內容，Escape 關閉後返回原標題且不重開，外部指標／焦點與離開作答也會關閉。

Tooltip 以 Teleport 到 body 避免容器裁切，依視窗定位、必要時可內捲；僅提示框保留長文的必要捲動，標題列自然換行。Parent visible prop 防止作答切回饋時彈出內容殘留；stage／規則變更及 unmount 清除提示與監聽器。沿原 categoryDefinitions 與課內說明，仍只供閱讀，不改草稿／AI 提示／DB。

既有教師／學生參考整合測試先驗標題仍包含完整定義而失敗，改為hover／focus／Escape、HTML安全輸出及明確aria-describedby。獨立read-only review發現body tooltip對鍵盤長文不可直達，先補失敗測試，再以 click／native Enter 可移焦與Esc防重開修正；未留其他可行動問題。最終Vue431/431（chat82）、Vite、Chrome相關9/9與diff check通過。Chrome含1440／390淺深、鼠標離開收起、鍵盤／點選、260px短視窗長課內定義：Enter聚焦提示、PageDown確實捲動、Escape返回原標題；作答切回饋無殘留、草稿／API次數不變。桌面標題列與手機提示截圖已視檢。

7173供 index-B6pcSby0.js／index-Cnul1_59.css，僅static、不重啟／清Raw、無API／DB／模型／Prompt改動，不跑無關後端，未提交推送。規格／標註UI同步；此節取代前一版常駐說明的顯示方式。

## 2026-11-20 作答區下方的分類定義參考（現行）

A／B／C／D 作答的 composer 下方提供「分類定義參考」。各階段使用原生可展開區塊，預設展開目前作答階段（該階段沒有啟用分類時開第一個可用階段），可同時展開其他階段。每項顯示代碼、名稱與完整短定義；寬處三欄／中寬兩欄／手機單欄，以頁面正常捲動、不新增內層捲軸。

沿課程當前啟用分類及 categoryDefinitions 共用說明，課內 description 優先，原碼原名稱才套既有閱讀定義；自訂或更名而缺說明時如實顯示未提供，不將舊定義誤套新碼。內容以 Vue 文字輸出，不信任 HTML。只供閱讀，不推算提及、不修改原稿／選碼／送出內容、不發 AI 或其他查閱請求。作答／回饋切換保留草稿，回饋與 Raw 不顯示此參考；唯讀無 composer 時也不顯示。

新增 CaseCategoryReference.vue，CaseAnalysisChat 在 form 後以 writing v-show 顯示，沿現有結構保持元件。教師／學生兩項新整合測試先紅後綠，涵蓋實際 DOM 順序、目前階段展開、原定義／課內覆寫／改名缺定義、停用分類排除、HTML安全輸出、切回饋隱藏與草稿／請求不變。中途 detached mount 的 jsdom 樣式快取使 isVisible 與實際 display:none 不一致，確認 DOM 後將可見性測試掛到 document.body，沒有以產品改動繞過。

Vue全431/431（chat82）、Vite及Chrome相關9/9通過；Chrome增強既有workspace案例，確認參考在整個form下方、原生summary鍵盤可展開B、說明可讀、未改草稿或發請求，並驗Raw／rules及角色流程。1440淺色／390深色完整畫面已視檢，diff check通過。7173供 index-DbP--17r.js／index-BZ8ytDJ5.css，僅static、不重啟／清Raw、無DB／API／模型／Prompt修改，不跑無關後端，未提交推送。規格／標註UI同步。

## 2026-11-20 未提及分類分區／ABCD換行（現行）

PhaseAnnotatedAnswer 底部按實際作答 stage 分為「本階段未提及」與「其他階段未提及」，每階段獨立 p 行、保留頓號／定義控制。本階段無缺項顯示「無」，其他空階段省略，所有項目已提及時隱藏整區；未知 stage 僅逐階段列「未提及」。切分類 tab 不改本階段的歸屬，修改 props.stage 才變動。選碼、原文／徽章、hover／focus／點選說明均不改，無 API／DB／Prompt／模型修改。

兩項既有測試先重現無分區／行標記，再改至元件47/47；驗切分類頁籤不誤改目前階段、變更stage／空階段／unknown與all-mentioned。Chrome9/9（定義桌面／手機、ABCD行bounds、多選及B淺深）與Vite／diff check通過，1440淺色／390深色截圖已視檢。7173供 index-BXUt-W6y.js／index-BwzGF1L3.css，僅static、不重啟／清Raw、不跑無關後端，未提交推送。規格與標註UI同步。

## 2026-11-20 工作區在上、分析階段在下（現行）

最上層依序為「作答／回饋／AI 對話測試」，作答與回饋下層共用 A 病前功能／B 疾病病程／C 功能評估結果／D 心理社會條件。AI 對話測試時不顯示階段列；沿既有角色限制只提供本課教師／TA測試，學生不因改版取得 Raw 或指定模型權限。教師「正式提交與統計」改為標題旁工具入口，其批閱以相同下層階段頁籤切換，不再重複階段 select。

同階段作答與回饋只改顯示狀態，保留草稿、正式元件、所選作業與回饋；跨階段維持所選上層工作區，沿原離開確認清除未送草稿。離開 Raw 仍確認、清除暫存並隔離上下文；從 Raw 返回時開啟最近的正式階段與明確選取的作答／回饋。初次教師仍沿原測試預設、學生沿原 A 階段／有歷程查看預設。完成連結與複製作答仍分別導向回饋及作答。

兩層各有獨立 tablist／tabpanel／ARIA 關聯；方向鍵與 Home／End 只移焦，Enter／Space 才切換，避免移焦即清除 Raw。手機導覽各自橫向捲動。階段 AI 請求期間上層停用；Raw pending 仍可確認離開並隔離遲到回覆。無 API、資料庫、模型或 Prompt 修改。

新增層級／Raw返回階段測試先紅後綠，既有 helper 改走可見的上層入口再切下層；Vue 全429/429、Vite、Chrome targeted15/15（含Raw位置及手機分類view狀態）與獨立read-only review兩次通過（含form移動，未發現可行動問題），最終完整Chrome68/68、exit0；diff check通過。桌面作答與手機回饋的新兩層畫面已視檢，Raw桌面1440／手機390上下位置補驗2/2，截圖已視檢；上層三tab順序及訊息在輸入框上方皆正確。舊測試預期空階段自動跳作答，依本次「保持上層狀態」改為維持回饋；鍵盤改手動選取及正式審閱入口改button。7173 /courses200提供 index-CN-zKKOx.js／index-B3qSXk2g.css，僅static更新，不重啟／清Raw、不開其他port、不跑無關後端，未commit/push。規格、輪次及既有短plan同步。

使用者追加：AI 對話測試的聊天訊息置於上方，輸入框放在下方；採真實 DOM 閱讀順序（先 log 再 form），保留輸入框下方等待／完成提示。A／B／C／D 作答／回饋仍以原 v-show 分離且保留元件與草稿。既有 Raw 單元先驗失敗再移動 form，桌面／手機 Chrome 增加訊息位於輸入框上方的 bounds 驗證。



## 2026-11-20 已提及分類：未選外框／選取填色（現行）

已辨識分類及全類按鈕預設透明底、ABCD分類色外框與文字；選取畫底線後才填分類色、白字與勾選。取消後回外框；部分選取的大類以虛線外框表示，不填色。深色主題外框／文字沿既有亮分類色，選取填色沿既有白字對比色。顯示／隱藏全部、取消選取與copy等操作保持平台紫色；未提及文字列、徽章、hover定義與底線互動不變。

PhaseAnnotatedAnswer僅局部CSS與hint，更新既有Chrome外觀斷言（含mixed及ABCD色）未新增測試模組。Vue47/47、Chrome相關7/7（1440／1024／390，淺深及多選／B）、Vite及diff check通過，桌面外框與手機深色填色截圖已視檢。7173新bundle index-DWIj2eqn.js／index-BtfCM8Zl.css，無後端／API／Migration／Prompt／模型修改、不重啟／清Raw，不跑無關後端或全套測試，未提交推送。規格與標註UI同步。

## 2026-11-20 分類水平頁籤／版本卡片 ×（現行）

分類accordion改水平tab，只列mentioned phases，優先目前stage否則第一個；藍點代表真實stage，選其他tab不移動，跨phase已選分類保持。RevisionHistoryList每個可整理本人版本新增獨立右上 ×，先確認後原trash API，可restore；移除未選卡不改目前選版／草稿，pending及busy停用，readonly／POV不提供。CaseFormalSubmissions ready事件避免student未載入正式metadata時顯示X。任何formal來源含returned／舊版，parent與後端一律拒trash；同stage lock防submit/trash並行雙成功，legacy已trash formal仍可restore。沒有schema／模型／Prompt改動。

前端全428/428、後端含DB479/479與非空036→042升級／rerun／schema清除通過，修review焦點後focused90/90；Chrome最終68/68（targeted phase／X4及B4皆過），diff check通過，1440／390階段tab及X截圖已視檢。初次VSbuild受原PID24040鎖exe，核對exactpath後只停止該程序，正常VS含Vue重建成功，唯一7173重啟PID16224，/courses200供index-DEvx0Jp2.js／index-BR_B5i4H.css，匿名case API401，5173／5097無listener。Raw暫存因重啟清除，DB歷史保留。規格、輪次、標註UI、權限與短plan已同步；未提交推送。

# 代理工作規則與目前交接

## 2026-11-20 作答版本常駐清單／單一#N（現行）

使用者移除下拉、要求粗體#7標題與日期副標題，不再混合原始／修改次數。RevisionHistoryList.vue/.test.js取代Select版本，native ul/li/button常駐、aria-current／roving Tab／方向鍵HomeEnd；roundOptions依原對話ordinal顯示#N+date，新到舊且trash不重編。主作答標題／latestlink／copy來源提示同步#N，刪除revisionLabel UI祖先計數與過時resetScroll ref。DB source chain、正式版號、native對話select與日期diff不變。

舊dropdown對新list6項測試先全紅，再6/6通過；fullVue419/419、Chrome targeted9/9與full64/64、Vite／diff check成功，獨立review無可行動問題，1440／390常駐清單與日期已視檢。7173 /courses200供index-DHybl6E9.js／index-DNeZEX71.css，無重啟／API／DB／模型／Prompt修改，未跑無關後端、未提交推送。三文件與feedback-rounds同步。

## 2026-11-20 版本選單移除內層捲動（現行）

依截圖要求，RevisionHistorySelect options移除max-height／overflow-y／scrollbar，feedback-rail取消sticky及viewport max-height／overflow；展開全部選項、只沿頁面捲動。既有控制／選取／latest／作答查看保持，未改其他tooltip／diff的必要scroll。Vue picker6/6、Chrome相關4/4（1440／390選版及比較）、Vite／diff check通過，展開截圖已視檢。7173 /courses200供index-D_RtCZYg.js／index-DxXxR4w1.css。僅CSS／既有測試期望，不重啟、無API／DB／模型／Prompt變更、未提交推送；三文件與輪次說明同步。

## 2026-11-20 作答／查看分離與分類圖例去重（現行）

使用者選定同頁「作答／查看回饋」兩tab。CaseAnalysisChat本地workspaceView：有紀錄預設view，空白可寫預設write，closed／trash／readonly不可write。Write只顯示composer／選模／等待與完成連結，view才顯示對話／分析選單、正式版本、分類與readback。V-show保留草稿／來源／正式child，切換不發請求；send／retry留write，latestlink選精確round轉view後focus／scroll，copy確認後轉write聚焦。Raw及staff正式review不變。

後續截圖要求dedupe：PhaseAnnotatedAnswer以完整碼集合＋status＋原explanation精確合卡，每卡保留不同start/end/text引用，重複occurrence只顯一次；不同評語／警示不合，寬度依去重卡數。原annotations／badge-run／底線／DB與Prompt不改。

Vue419/419、focused77及dedupe相關53、Chrome最終64/64（targeted12/12）、Vite／diff check通過。Workspaces獨立review無可行動問題，1440／390作答與查看、dedupe一張3quotes截圖已視檢。初次舊browser helper在Raw→stage async cleanup時提早跳過tab，已修等待，無product defect。最新index-DBQYjJ3B.js／index-LJeSfA8j.css。最終發現7173已無listener／app程序，以現有Debug exe恢復同一7173為PID24040，沒有後端重建／其他環境；/courses200供最新bundle、匿名case API401。無API／DB／模型／Prompt修改，未跑無關後端、未提交推送。規格、history、feedback-rounds、annotation UI及短plan同步。

## 2026-11-20 分類操作改紫色（現行）

依使用者後續指示，分類、show／hide、取消選取與copy原primary藍改與平台.button.primary相同#5b5fc7白字／hover #4a4eb3，mixed外框同步；selected原分類色與✓保持，目前階段header／原文與badge色不變。只改元件CSS與說明、既有Chrome顏色斷言。相關Chrome7/7、Vite及diff check通過，最新index-BxfGzycd.js／index-D7myIkL0.css。無後端／DB／模型變更、不重啟／清Raw、未提交推送。三文件及標註說明同步。

## 2026-11-20 精簡分析選稿摘要（現行）

依截圖移除RevisionHistorySelect所用roundOptions的分析狀態／phase／正式提交資訊，只留日期與第N次分析／revisionLabel；trigger及options同步。移除未使用roundSubmission helper，原生對話與正式功能不變。Vue相關82/82、Chrome日期比較1440／390兩項及Vite／diff check通過。最新index-BtRKXXYP.js／index-BNoU3jhk.css，僅static、不重啟／清Raw，無API／DB／模型修改、未提交推送。規格／歷程／feedback-rounds同步。

## 2026-11-20 分類操作 primary 對比（現行）

使用者要求 clickable action buttons 更明顯。局部調整 PhaseAnnotatedAnswer：分類、show／hide、取消選取與copy改#0d6efd白字／hover深藍；selected保持phase色＋✓、mixed白虛線，未提及列／badge不變。未引入Bootstrap套件或改全站CSS。Vue相關42/42、Chrome相關9/9（1440／1024／390、淺深、多選／定義／B跨phase）、Vite及diff check通過，手機深色已視檢；primary對白字4.50:1。7173最新index-6bjM_rVM.js／index-DxCADvJH.css；僅static，不重啟／清Raw，無DB／API／模型／Prompt改動，未提交推送。規格／歷程與標註UI說明同步。

## 2026-11-20 可選分類優先／未提及列與定義（現行）

使用者要求取消停用分類方塊，已辨識（含父碼）保留選取按鈕與有可選碼的階段；其餘改底部「未提及：代碼 名稱、…」文字列。後續澄清要滑鼠移入定義，例如利手＝慣用手（handedness），故兩種項目都有定義tooltip／focus，未提及可點讀但不畫線。categoryDefinitions.js僅閱讀說明，來自既有professor-rubric；優先用所選回合／正式快照description，只有原碼原名稱才fallback預設，改名或新碼缺說明明示未提供。不改LLM分類、rules、DB、Prompt或授權。

Vue415/415、最終補驗分類元件42/42、Chrome60/60、Vite及diff check通過。審查發現tooltip互斥／焦點捲動／長文內捲／聚焦時mouseleave問題，皆已先加失敗測試再修；桌面／390深色截圖已視檢。7173 /courses200供index-D9MNvlsP.js／index-BdFAoRFZ.css，未重啟／清Raw，未跑無關後端、未提交推送。三持久化文件及標註UI說明同步。

## 2026-11-20 比較欄位用語（現行）

依使用者要求，AnswerRevisionDiff兩側選單／欄標題／aria-label／差異圖例改「基準版本／比較版本」，避免兩側可選卻假設日期先後。更新既有測試locator；相關Vue4/4、桌機／手機Chrome2/2、Vite通過。最新產物index-CJXrYVHS.js／index-CFM5dvgS.css；僅靜態文案、不重啟、無DB／API／模型修改，未提交推送。規格與輪次說明同步。

## 2026-11-20 日期版本選單／並排作答比較（現行）

使用者新增要求取代inline code diff及收合輪次清單。AnswerRevisionDiff沿原有界Unicode演算法，左右各完整原文、分別標色刪除／新增，不用刪除線，日期作欄標題；手機仍雙欄、僅比較區水平捲動，鍵盤可聚焦與scroll。分析歷程新增RevisionHistorySelect，單一combobox＋可換行日期／details options；新到舊與原ID／分析次數保持，主分析／正式快照標題及比較labels日期化。上方原生對話紀錄select不變；trash／draft／正式版／role流程保持，完成連結關選單、reset捲頂後聚焦回覆。

Vue411/411、Chrome58/58、Vite與diff check通過；獨立審查無可行動問題並另驗119相關tests。1440並排、390自身scroll及日期dropdown截圖已視檢。7173 /courses200核對index-DffxwRPS.js／index-Bt5xOGNf.css，僅static更新、不重啟或清Raw，無API／DB／模型／Prompt／授權修改，未跑無關後端、未提交推送。三持久化文件與feedback-rounds同步。

## 2026-11-20 原生對話選單加寬（現行）

使用者要求更多寬度或custom wrapping，採最小變更保留native：將既有history-select搬到feedback-workspace跨欄上方，width:min(100%,52rem)、min-width0／max-width100%防手機overflow，保留formal guard。未加新selector或改labels／sorting／選取／正式提交。相關Vue76/76、Chrome6/6（含1440／1024／390主布局）與full日期label補驗2/2、Vite通過；desktop原210px失敗重現後加寬，canvas驗完整日期與status文字可容納，特寫已視檢。7173 /courses200供index-w-bWWURn.js／index-DkzNE2P-.css。僅static更新，無重啟／Raw清除、API／DB／模型修改，未跑無關後端、未提交推送。規格／歷程與輪次說明同步。

## 2026-11-20 對話紀錄選項格式（現行）

使用者經短問答確認目標為對話紀錄（非正式版本歷程）。CaseAnalysisChat local computed sessionVersions以既有紀錄日期排序／產生display序號，只替換原select options文字成版本—日期—第N次—已提交／撰寫，新到舊；不新增selector／utility或修改跨頁編號。FormalEnvelope關聯決定status，未把AIcompleted算submitted；selectedSessionId與session資料順序不變，正式version及AI輪次保持。Vue405/405、相關Chrome5/5與Vite通過，產物index-iHvwmw5o.js／index-Cx1PvmaY.css；只static更新，無後端／DB／模型變更或重啟／Raw清除，未跑無關後端，未提交推送。規格／歷程／輪次說明同步。

## 2026-11-20 滑入提示／點開圖例及響應欄位（現行）

使用者澄清hover保留局部底線，但tooltip只說「點選開啟分類圖例」，不是tooltip按鈕／其他區導覽。PhaseAnnotatedAnswer hover/focus只短hint，click／Enter／Space才開非模態tag legend，保留引用／原AI說明／status與合併來源。詳細卡片最多3欄、中寬2、手機1；主分類按鈕也修正舊legend-in-rail強制全寬直排，container>=520兩欄，viewport>=1200且container>=760三欄，窄處1欄。

Close／Escape回原trigger，outsidepointer／focus關閉但不搶目的地焦點；pinned變reactive，focusin cleanup、viewport resize／scroll定位與top/bottom clamp。保持選碼、原文、annotations及teacher/student邊界。Vue404/404、Chrome56/56（含新3種viewport場景）及Vite通過，1440／1024／390圖例已視檢，diff check乾淨。7173 /courses200供index-CxLYHwSh.js／index-CCPweT5S.css；僅static更新，無服務重啟／Raw清除，無API／DB／模型／Prompt更動，不重跑無關後端，未提交推送。規格／歷程／標註與輪次說明同步。

## 2026-11-20 原文／徽章兩頁籤（現行）

PhaseAnnotatedAnswer將原「無／底線」合為「原文」（內部仍underline key保持caller相容），保留點選分類畫線與預設無線；編碼徽章旁加blue/white A1示例，aria-hidden、非互動、不列入原文或annotations。移除舊none UI分支、兩tab鍵盤與selected filters保持。Vue398/398、相關Chrome13/13及特寫2/2、Vite成功，390dark特寫已視檢；7173 /courses200核對index-B8cpv9eP.js／index-ZfveVGJu.css。僅前端靜態更新，無重啟／Raw清除、API／DB／模型／Prompt變更，未跑無關後端、未提交推送。規格／歷程與標註說明同步。

## 2026-11-20 11/19會議流程調整（現行）

本輪已依附件完成盤點後增量實作，見[交付／Demo／API／待確認](../docs/case-analysis-2026-11-19-corrections.md)與plan。學生主輸入移右欄上方，大型可resize；左側歷程預設收合、分析與正式版本分開；分類預設無線、多選及徽章local hover，原徽章色／合併保持。最新回饋連結會展開歷程並捲頂及聚焦新回覆。

新增041正式快照與確認提交／退回／重新提交，使用completed本人非practice分析；UUID與scope鎖保護重複／並行，正式原文／回饋／annotations／模型與規則快照不可覆寫。可選FK關聯同課開放個人作業；未選為課內階段作答，percourse/student/stage版本序列，既有grade不改。教師私人草稿ReviewHistory改403，自己的History保留；正式審閱／退回依grading、聚合統計依statistics。學生不能Raw或指定模型，staff本人practice保留測試，前後端同時落實；本節取代早期學生選模／教師全草稿讀取。

新增042課內分類append-only版本／actor／time、消息rule_version/rule_snapshot/prompt_snapshot。後續turn真正載入啟用碼到prompt/schema/parser；失敗重試保留原規則與system，basePrompt版本不同仍409。舊NULL歷史DTO用原靜態catalog，正式提交舊來源也保存此catalog，沒有用新規則重解舊文。只有teacher寫規則，TA讀、student拒絕；整phase可停用但至少一個啟用碼，既有身份停用不刪，reserved A2c/B2c不可enable。

041／042已實際套用public，既有chat／sessions／courses／members／assignments／submissions／grades指紋一致，正式表與rules初始空，未代學生正式提交。唯一7173由核對的PID12840→30356→17908正常VS含Vue重建，同一qwen25-omni預設保持，Raw暫存因重啟清除、正式歷史保留；不開其他port。/courses200／匿名AI401／Swagger新routes已驗。最新bundle index-ieyN-WuW.js／index-1lDeOsds.css，未提交推送。最終476後端／398Vue／53Chrome皆通過，非空036→042升級／rerun／schema清除通過。手機／桌面與儲存規則畫面已視檢，diff check通過；最後所有build完成後才執行Chrome，避免測試讀取Vite清理中的靜態檔。

## 2026-11-19 完成連結／歷程捲至頂端（現行）

使用者澄清prompt link指AI已完成的「查看最新回饋／回覆」。visitLatestResult在選輪及nextTick後同步round-list.scrollTo(top:0)，保留回覆focus／scrollIntoView及reduced-motion；Raw無roundId保持原行為。相關Vue71/71、Chrome桌面／手機2/2（先把歷程捲底再點連結）與Vite成功，靜態產物index-250qcTnD.js／index-BwZRl5dm.css。無API／DB／模型／Prompt更動、無重啟、未提交推送；規格／歷程及輪次文件同步。

## 2026-11-19 Raw 聊天提示文案（現行）

CaseAnalysisChat.vue依使用者原句將Raw說明改為自由AI聊天室／模型回應效能用途；只改文案。Vite成功，產物index-C7WgNFUm.js／index-5zGu9h-9.css。無後端／DB／模型／權限更動、無重啟；低影響文字修改未新增或重跑測試，未提交推送。規格與歷程同步。

## 2026-11-19 回覆診斷側圖示／聊天隱藏模型行（現行）

新增ReplyDiagnostics.vue，以Gauge／CodeXml取代AI泡泡兩文字按鈕；固定位置tooltip有操作說明及該回覆modelName，缺metadata明示未記錄，openPerformance亦不再fallback目前config。使用者後續要求已送出的chat也隱藏model，因此所有user／assistant泡泡移除model footer；header／提交選單、效能詳情、DB記錄保持。僅原有staff request權限顯示CodeXml，學生仍只有效能。無後端／模型／資料更動。

Vue356/356、Vite成功；Chrome完整50/50後，針對resize修正及最後隱藏user footer再驗8/8 desktop/mobile模型選擇／Raw／圖示。初次手機tooltip覆蓋icon已修為上／下載；獨立review另發現short→tall viewport重測問題，補nextTick二次定位及red/green regression。手機深色已視檢。7173 /courses200供index-5vN8HwJF.js／index-DzaaRmMN.css，匿名AI401；僅靜態更新，無重啟／Raw清除，無migration。原chunk警告非失敗，未跑無關後端、未提交推送。同步規格／歷程／輪次與request-dialog文件。

## 2026-11-19 本人作答垃圾桶／還原（現行）

使用者要求學生／教授可trash bad attempts。採可還原的顯示整理：學生本人正式回合、教師／TA本人practice可移除／還原；course需active且case enabled及本人有效成員，closed session亦可整理。他人student review只有查看垃圾桶、不可變更；學生POV仍readonly。正式學生統計／immutable內容／revision祖先保持，無永久刪除endpoint。Raw沿既有清除，不新增DB。

新增040_case_chat_attempt_trash.sql：user message nullable trashed_at及role check；paired assistant DTO以join繼承。CaseAnalysisChat.Attempts.cs沿Access＋stage advisory lock，先session再message row lock；live pending409、expired pending轉failed，late completion拒收；trash／restore idempotent，audit只存actor／course／message／session／stage與action。重試／sameID replay與新revision不得引用trashed來源；舊descendant不破壞。完整history API帶trashedAt，不擦除教學資料，UI正常清單／比較排除trash，垃圾桶可還原。草稿保留，若來源被trash則只清revisionParent與request identity。成功清當輪request capture／latest link，session不跳回active。

後端449/449隔離DB、036→040升級與舊資料指紋／rerun通過、schema已刪；Vue351、Chrome48及標準VS含Vue成功，手機student trash已視檢，安全／concurrency審查無阻擋事項。040已套用實際public schema，既有message／session／course／grade指紋相同、初始全部未trashed；未修改真實作答。只有7173，核對當時listener變更成26464且path為專案bin/Debug後重啟至PID12840，qwen25-omni default保持；/courses200／匿名AI401／Swagger兩route／DTO已核對，bundle index-BV1Pf_QS.js，舊埠無listener。重啟清Raw、正式歷史保留。無模型／Prompt修改，未提交推送。新增plan並同步角色／規格／歷程及輪次文件。

## 2026-11-19 連續相同分類徽章合併（現行）

依使用者要求，編碼徽章模式把相同完整codes集合的連續片段合為一個badge，放在該run最後片段末端。標點／空白可銜接；不同codes集合或未標註實質文字中止，C1,C1,C1→一個C1，C1,C2,C1保持三個；多碼集合需完全相同，順序不影響。只有顯示合併：保留原segments／UTF16／文字，underline／none不變，原始AI annotations及DB不改。

新增annotationBadgeRuns回傳members及原annotation物件聯集，badge tooltip顯示所有原說明／status，並明示合併連續片段；同一annotation跨多segment不重複列。選碼仍針對原始span，copy直接原作答。Vue340/340、Chrome43/43、Vite及diff check通過，1440淺／390深的合併／插碼中止／tooltip警示／原文及三mode／copy已驗，手機深色已視檢，靜態審查無可行動問題。

7173 /courses200供index-CRBaJaqe.js／index-CSIb617G.css，無服務重啟／Raw清除。未改API／DB／Prompt／模型，不跑無關後端；未提交推送。同步規格／歷程／標註說明。

## 2026-11-19 標註新增「無」模式（現行）

依使用者要求頁籤順序改「無／底線／編碼徽章」，預設仍底線。無模式直接以Vue文字插值顯示完整原文，不產生互動span／badge／底線／框／tooltip；分類與既有選碼保留，切回原模式可繼續強調。三個tab維持唯一ID／aria關聯及左右／Home／End鍵，切輪仍記住顯示mode且不寫storage。無模式選碼狀態改「已選取／取消選取」，legend提示須切模式才強調，PhaseAlignmentNotice明示原文標註已隱藏，不冒稱有底線。

Vue全337、後續相關90及Chrome41、Vite成功，無模式原文／Unicode／清tooltip／左分類保留／跨輪／鍵盤順序已驗；390深色已視檢。7173 /courses200供index-DUf17mVc.js／index-DSVzXY9V.css，無服務重啟／Raw清除；無API／DB／Prompt／模型更動，不跑無關後端，未提交推送。同步規格／歷程及標註說明。

## 2026-11-19 左欄 ABCD／下方歷程（現行）

使用者先討論右側分類，隨後改採左側分類、歷程放其下；在任何右欄程式變更前已依新指示切換。CaseAnalysisChat既有左欄擴為feedback-rail，ABCD section在round-sidebar之前；右欄維持學生作答／AI回饋，tabs與copy仍在作答內。桌面左240–280px、sticky top90及viewport內捲動；<=1050px兩欄改上下、rail不sticky／不限高。只有左欄，不另建第三內容欄。

PhaseAnnotatedAnswer以選用legendTarget DOM ref及Vue Teleport搬現有legend／selected狀態，不另建選碼controller；獨立使用無target時inline fallback，顏色palette隨phase-category-explorer搬移。原文／分類三態／disabled／兩模式強調與stage色保持，選輪／session切換清舊選碼，沒有有效annotation顯示空狀態。用DOM ref而非global selector，支援未attach的unit mount與多元件。

Vue335/335、Chrome41/41及Vite通過；驗1440左右欄／1024與390上下載、分類先於歷程、round/session替換、原模式／copy／readonly review／Raw隔離。桌面淺／手機深已檢視，獨立靜態審查無可行動問題。7173 /courses200供index-CY3JHfu1.js／index-DiLuXeex.css，無服務重啟／Raw清除。無API／DB／Prompt／模型變更，不重跑無關後端，未提交推送。同步規格／歷程／標註說明及2026-11-19-left-category-history-rail plan。

## 2026-11-19 未辨識分類改停用外觀與操作（現行）

未mentioned的圖例button新增native disabled，灰字／opacity .55／not-allowed游標，保留完整標籤、不可點選也不占Tab停駐。已辨識（含父碼）仍可選，外框／選取色底及原文強調維持。局部覆寫全站disabled的wait游標及pointer-events，使用not-allowed但仍由native disabled阻止click；無全站樣式異動。legend文案與未辨識aria-label同步。

相關Vue84/84、Chrome相關10/10與Vite成功；手機深色已檢視，未辨識B及C5灰化，C1填色／C2外框保持。7173 /courses200供index-Cs22iH2l.js／index-Sgg0pc7Z.css，無重啟或Raw清除。無後端／DB／Prompt／模型變更，未跑無關測試；未提交推送。同步規格／歷程／標註說明。

## 2026-11-19 圖例辨識／選取視覺分離（現行）

依使用者截圖，分類圖例mentioned但未選取改為該phase色border＋中性底；已辨識且選取才phase深色底白字。未辨識分類保持中性（如沒有B標註時整個B名冊），不因目前stage或展開就著色。深色border用既有較亮phase色以可辨識，選取fill沿既有category深色，原文實心badge不改。保留父碼包含子碼、aria-pressed、再按取消及兩模式原文強調，legend提示同步。

只PhaseAnnotatedAnswer CSS／提示及既有Chrome期望更新，相關Vue83與Vite通過；Chrome38案例原通過，另2選取案例最初同步取色撞到既有160ms transition，改用retrying toHaveCSS後2/2通過（合計40情境）。手機深色已檢視：C1選取有底、C2僅框、未標B中性。7173 /courses200供index-Cgw0F7Es.js／index-ZwLPBCXo.css，無服務重啟／Raw清除。無API／DB／Prompt／模型修改，不重跑無關後端，未提交推送。同步規格／歷程／標註說明。

## 2026-11-19 分類選取依模式改強調方式（現行）

使用者希望點選C1等分類時：底線模式原點線框改實線outline；編碼徽章模式不加框，僅相符原文加所選分類階段色底線，所有實心徽章仍保留。PhaseAnnotatedAnswer按displayMode分流underlinePhases與emphasized class，重疊多碼只畫被選phase一條底線；同phase但不同code（如C2）不因選C1被畫線。既有父碼含子碼／再按取消／顯示全部標註及切模式保留選碼不變，切輪仍清選碼。

Vue334/334、Chrome40/40與Vite通過，1440淺色／390深色驗outline solid1px、badge只所選碼underline、混合phase不多畫線／原文與徽章保留／取消清除；手機深色已檢視。7173 /courses200供index-BLAIWNoW.js／index-DVRD4xIQ.css，僅靜態更新，不重啟／不清Raw。無後端／DB／Prompt／模型變更，因此未重跑後端或migration，未提交推送。同步三文件與標註說明。

## 2026-11-19 標註模式改頁籤／目前階段改 primary（現行）

依使用者截圖，PhaseAnnotatedAnswer「底線／編碼徽章」改為tablist／tab／tabpanel，兩個頁籤對應同份原作答呈現。每個元件獨立ID，aria-selected／aria-controls／aria-labelledby與roving tabindex一致，左右／Home／End即時切換並移焦點。複製到輸入框保留為頁籤外的一般action button；無annotation只顯複製，唯讀不增加操作。保留跨輪次displayMode狀態、原文／tooltip／分類色及修訂流程。

目前階段分類標題從淺色黃底／深色黃字改Bootstrap primary #0d6efd底白字，兩theme一致；不引入Bootstrap套件、不改全站accent或原文ABCD色。Vue333/333、Chrome38/38（1440／390及淺深色、頁籤鍵盤／copy／blue-white computed）及Vite通過，390深色畫面已檢視，靜態審查無可行動問題。只有靜態前端更新，7173 /courses200供index-ZjVKySyV.js／index-CHNX2RI6.css，無服務重啟或Raw清除；本輪不重跑無改動後端／DB。沒有API／Prompt／模型／DB修改，未提交推送。

## 2026-11-19 Omni 標註上限 48／正式輸出 5120（現行，已解除下方8筆問題）

使用者確認提高上限。CaseAnnotationContract compact annotations.maxItems由8改用既有MaximumAnnotations=48；保留compact text160／explanation80／feedback600、codes最多8、UTF16 exact validation及source-clause enum。server原就最多48，其他非compact schema不變。僅qwen25-omni profile CaseFeedbackMaxTokens4096→5120，Raw仍512／context16384／輸入bytecap24000；其他三profile不變。新正式A/C/D版本professor-four-phase-20261119-v6、B為professor-four-phase-20261119-b-v8，舊歷史不重寫，需新提交使用。沒有自動補標舊文或重送真實作答。

預留6144曾使既有B2125字預檢失敗，已退至5120並完整回歸，不擴context或放寬估算。最終Omni合成48分句590字→48有效標註、末端589、input2743／output3253／stop，確認越過舊第8筆且達末段；不等於臨床編碼正確性驗收。先前24分句亦24筆／2809tokens／stop。舊payload測試JSON deserialize未用production binder而漏NumPredict設定，且把ConversationAsync誤認formal override，已修測試使用ConfigurationBinder並分開驗兩API，正式provider無額外變更。

完整隔離DB443/443、036→039指紋升級及schema清除、正常VS含Vue通過；獨立靜態審查無可行動問題。唯一7173正常bin/Debug PID5436／qwen25-omni，反射核對48／5120／512／16384及新版、/courses200／匿名AI401，舊埠無listener。bundle仍index-C7zGXJu-.js；本輪未改Vue，不重跑前端unit／Chrome（上輪332／38）。重啟清Raw、正式歷史保留；無Migration／正式資料寫入，未提交推送。同步規格／歷程／Ollama及標註文件，先前待修紀錄保留時間脈絡。

## 2026-11-19 待修：Omni 分句標註仍限 8 筆

使用者請查C長文中途停止標註是否context不足。唯讀metadata確認近期C回合input2667／output983／done_reason=stop，annotations=8，末端104／原作答574字；沒有讀出／重送學生原文。根因CaseAnnotationContract.SchemaFor(compact:true)仍maxItems=8，Omni CompactCaseFeedback=true，逗號細分比原段落模式更早耗盡名額；並非16,384 context或4,096正式輸出上限耗盡。其他模型非compact最多48，服務驗證亦最多48。下一步應專門調整compact annotation數量及其輸出預算／回歸，保留compact quote／explanation與其他profile差異，勿僅擴context或默默截原文。本輪僅診斷，未改程式／設定／DB或重啟7173，尚未修此8筆上限。

## 2026-11-19 ABCD 範圍核對與複製到輸入框（現行）

保留現有分句候選與底線／徽章。使用者要每份作答也檢查ABCD scope leaks；原有階段核對及misplaced已存在，這輪強化正式每次outputRequirements：依完整脈絡逐片段檢查全部ABCD，不只找目前階段、不改真正碼以迎合目前階段；必要背景／有據多碼保留，錯置才misplaced並在評語說明兩階段及調整方向，不確定待確認、保留否定語意，不憑標題補碼。B長文feedback-only重整現在帶annotation Status並明示背景例外，避免丟失既有錯置發現；不重新分類、不補舊歷史。正式A/C/D版本professor-four-phase-20261119-v5，B為professor-four-phase-20261119-b-v7；system bytes仍A6284/B7717/C6497/D6219，四profile／教授規則／容量不改。不是語意或臨床正確性保證，這輪無真實模型品質比較。

正式學生作答的底線／編碼徽章旁新增「複製到輸入框」，取代上方重複的修改按鈕；沿既有revision來源連結，複製原始字串（含空白換行），不複製badge／回饋／HTML、不碰剪貼簿、不自動提交。已有不同草稿先確認，可取消；聚焦並捲至textarea，尊重減少動態。無標註也能複製，pending／failed／busy／loading不可操作，他人review／唯讀不顯示，Raw不增加此按鈕。

驗證438/438隔離DB、036→039指紋升級及schema移除，Vue332、Chrome38（1440／390、淺深色）及正常VS含Vue通過；390深色複製控制已檢視，靜態審查無可行動問題。唯一7173正常bin/Debug PID31272／qwen25-omni，bundle index-C7zGXJu-.js，/courses200／匿名AI401，舊埠無listener。重啟清Raw，正式歷史保持；無Migration／正式資料寫入／模型切換，未提交推送。舊prompt回合保留原版本，需新提交採新核對。

## 2026-11-19 分句級行內標籤（現行）

使用者要逗號等標點也能分別標年齡／手術等事實。現況renderer原已按有效quote末端顯示，無只依句號規則；原因是模型回整句／整段多碼。提示文字單獨修改後Omni6例仍整段，故正式CaseFeedback新增有界source-clause text.enum：中文／英文逗號、頓號、分號、冒號、句尾標點及換行分候選，保護小數／日期／時間／千分逗號／常見縮寫／括號／引號及短label:value。只限制逐字證據，不猜codes。>=2不同候選、<=128分句且各符合既有quote上限（Omni160、其他16000）才加enum；否則保留原自由引用schema，不截斷作答／丟歷史。多碼、exact occurrence／UTF16驗證保持。

只新增annotation輸出粒度及格式文字壓縮，教授完整TeachingInstructions／Raw／Studio／四profile／抽樣／schema欄位與數量上限不變。新正式A/C/D版本professor-four-phase-20261119-v4、B為professor-four-phase-20261119-b-v6；舊資料不重寫，舊失敗不可冒用新版retry，需新回合。現有粗標註不在UI猜分配，需重新提交才得到新粒度。模型仍有過度編碼及漏標，不能稱臨床正確。

最終433/433隔離DB及036→039指紋升級通過、schema刪；Vue331、標註Chrome8與標準VS含Vue通過，390深色已檢視，靜態審查無可行動問題。實際合成Omni6／Llama2均完整JSON、無整段跨多事實quote，仍有錯碼；最終B label:value例只標一段且多碼，不隱瞞覆蓋／語意限制。唯一7173正常bin/Debug PID41924／qwen25-omni，版本與bytes已核對（A6284/B7717/C6497/D6219），bundle仍index-D7wy7XLq.js、/courses200／匿名AI401、舊埠無listener。重啟清Raw，正式歷史保留。無Migration／正式資料修改，沒有重建B課程；NU1900／chunk為原提示，未提交推送。

## 2026-11-18 等待教授移至編輯器下方／輪播指示器／完成連結

依使用者最新澄清，AiReplyWaiting放在textarea及字數下方，Raw／四階段一致；原i/N可見計數改Bootstrap風格短橫carousel indicators，當前提示高亮，不引入套件。保留暫停／下一則／減少動態／非模型即時進度揭露，指示器是提示輪播位置而非完成百分比。

真正成功回覆才在原位置出「查看最新回饋／回覆」連結，正式回合連到本次requestId配對assistant，點選先恢復該輪再focus及smooth scroll，reduced-motion用auto，保留草稿；Raw用session＋index錨點。失敗／新request／切mode或session／restart／生命週期invalidate清完成狀態；restart不假裝模型生成。僅頁面state，不改API／DB／Prompt／模型。Vue全317、Chrome全36與Vite通過，手機深色已檢視，靜態審查無可行動問題。7173 /courses200供index-D7wy7XLq.js，無後端重啟／Raw清除；原chunk提示非失敗。未跑無關後端測試，未提交推送。

## 2026-11-18 效能速度量尺自動擴展（取代固定上限）

使用者改要儀表刻度依最高速度留約20%餘裕。只效能dialog：本頁已開啟回覆的速度除0.8、向上取50倍數，最低200，最高刻度只增不減以便比較；例如200→250、210→300、320→400，320填80%。重新整理回200，僅頁面numeric ref、不寫storage／DB；未量測／無效仍不顯示，不改真實數值或量測。新增utils/performanceSpeedScale.js共用頁面刻度，動態五刻度與讀屏上限同步，移除固定200溢位提示。Vue相關80、Chrome淺深色桌面／390量尺／持續比較／刷新及原dialog8、Vite通過，已檢視手機深色。7173供index-DaOp2Jec.js、頁面200，無重啟／Raw清除；原chunk警告非失敗。無後端／模型／DB變更，未提交推送。

## 2026-11-18 效能dialog生成速度刻度

只在ChatPerformanceDialog「生成速度」下加固定0–200 tok/s量尺，0／50／100／150／200刻度，128.41填64.205%；數值照實保留。超過200只讓條形滿格並提示超出，缺值／未量測／無效數值不顯示量尺，量測0仍顯示。採role=meter與實際速度aria文字，刻度固定不當成模型容量或品質上限。只前端，既有選模／效能量測／DB／payload不變。相關Vue75、桌面淺色／390深色量尺與原請求dialog Chrome6及Vite通過，已檢視手機深色；7173供index-BHmRCc5y.js、頁面200，沒有重啟或Raw清除。原chunk提示非失敗；無後端改動故不跑後端／DB，未提交推送。

## 2026-11-18 輪次選單移除模型文字

依使用者截圖意見，回饋輪次／草稿sidebar移除長model tag，只留輪次、原始／修改、提交狀態與時間。AI回覆／訊息詳情及效能dialog、輸入區選模與DB model_name保持。只前端一行，既有模型對應測試更新，不改API／授權／模型／DB。Vue相關56、Chrome既有師生桌面／390選模及輪次6與Vite成功，7173供index-COGjXA3f.js，沒有重啟，Raw暫存不清除。原chunk警告非失敗；未跑後端（無變更）、未提交推送。

## 2026-11-18 師生每次提交選一個模型（現行）

使用者確認每次只送一個模型。課程教師／TA／實際修課學生在輸入區「本次回覆模型」下拉選既有四profile：IAN02 Llama3.1／Qwen3、Taipei Breeze／Qwen2.5 Omni；未選沿環境預設，不改全域或他人設定。保留原白名單、各profile相容性／抽樣／容量與既有rate limit。學生視角預覽、未啟用、封存課程及教師查他人紀錄仍不能傳送或選模；學生選模不取得教師請求封包／安全診斷權限。

正式回合沿既有model_name記錄user／assistant實際模型，歷史標籤不隨下拉變動；retry沿原模型／提示，換模型是新requestId。完成時比較起始／當前Practice屬性，升降權不能混入正式／教學練習歷程。Raw新增選用ModelProfile於單次Send，同一LangChain記憶可跨模型，切換保留草稿／成功對話，不再因選模清空；每一成功pair的model tag及量測只存process memory，失敗不入buffer。既有RawCreate預設choice相容保留；省略Send選模沿session預設或環境設定。清除／刷新／閒置逾時生命週期不變，Raw無app system／DB／正式統計。

完整隔離DB411/411與036→039升級／指紋通過、schema刪；Vue301、Chrome34（教師／學生各1440／390及原流程）、標準VS含Vue通過，手機學生畫面已檢視；靜態審查無可行動問題。原NU1900／chunk提示非失敗。健康檢查兩台online且四模型tag可用（Breeze省略tag等同latest）；本輪不重新做生成品質評估。唯一7173正常bin/Debug PID46904／qwen25-omni、bundle index-CjJH7E-c.js、課程200／匿名AI401，舊埠無listener；重啟清Raw、正式歷史保留。無Migration、未改正式資料／模型設定、未提交推送。詳docs/ollama/model-selection.md；已刪B課程不重建。

## 2026-11-18 明確授權永久刪除個案分析課程 B

使用者已明確確認例外永久刪除「個案分析課程 B」及相關成績、作業、教材與分組，保留A；非刪除B疾病病程功能。目標f1e66c4e-7e87-4ca6-a46d-7431ac89e11c／LEARN-101-246／115-1原為archived，已永久刪除。A（897f8770-8a65-49d7-9cac-904eb00dbe53）及其他課程保留。

一次性維護交易先回滾預演，按實際FK關聯限定B子資料，處理既有deferred current_revision FK，不停用約束或trigger。正式提交前53張表其餘資料指紋一致、無跨課共用刪除範圍；保留所有帳號及稽核，新增course.permanently_deleted系統稽核（不冒用教師）。含63筆course_grade_scores、48筆grades、4份assignments、4份report_assignments、48份submissions、15份report_submissions、3份教材、3組及相關紀錄。一般課程安全刪除規則不變，沒有開放UI永久刪除有歷史課程。

只有資料維護／文件更新，無Migration／程式／模型／7173重啟。沒有讀出或存檔學生答案／檔案；忽略的一次性腳本只含ID及SQL，未提交推送。後續不要重新執行任何重建B的demo。

## 2026-11-17 教師選模、B長文評語與Omni協定修正（現行）

教師／本課授權TA可從「本次聊天模型」下拉選四個既有profile，預設沿環境設定；只影響本人，學生／學生POV不能指定。正式新回合保留草稿與歷史，用新requestId比較模型；重試仍用原實際模型及提示版本。user／assistant沿既有model_name保存實際tag，每輪顯示；沒有Migration，不宣稱保存主機、模型digest或完整參數快照。Raw選模需確認並清空舊暫存，session只留設定快照，送出重新授權，不入DB／正式統計。

Omni短Raw也循環，故不是只有B或長文。合成對照：原api/chat達512tokens／length、重複14次；單加repeat_penalty仍循環。僅Omni改ConversationTransport=qwen-chatml-generate，共用HTTP服務送api/generate、raw=true與明確Qwen角色分隔；Raw只含user／assistant，無應用System Prompt，完整LangChain多輪仍在。其他三profile／抽樣／遠端Modelfile不改。實際payload彈窗顯示真正prompt及JSON。

B原文>=1000字時，若回饋有機械循環或大量箭頭編碼配對，最多一次feedback-only重整；送本次作答及有效分類參考，不送壞評語／舊稿，保留首輪annotations。再失敗不保存assistant，教師可看安全原因。B提示版本v5、A/C/D仍v3，舊B失敗不可冒用新版重試，需新提交；舊資料不重寫。兩次呼叫效能合計並標示ProviderCalls=2，正式往返仍1，不是臨床正確性判定。

驗證：完整隔離DB406/406、036→039升級與舊資料指紋保持，測試schema刪除；Vue301/301、桌面／390 Chrome32/32、標準VS含Vue建置通過。合成實際Provider Raw1／Raw2均23tokens／stop、無循環，B2125字prompt4103／response166／stop、完整JSON且無機械重複，不等於教學品質驗收。已修頁籤切換與立即選模競爭及唯讀Raw載入狀態。唯一7173正常bin/Debug PID7332／qwen25-omni，bundle index-CaiDGwMW.js、/courses200、匿名AI401；重啟清Raw、DB歷史保留。保留dirty、未提交推送，其他模型原容量限制保留。細節見docs/ollama/model-selection.md。

## 2026-11-17 聊天頁籤／教師失敗診斷／2125字容量修正（現行）

聊天模式與教師紀錄改tablist/tab/tabpanel，選取底線、桌面平列／手機橫向捲動，左右/Home/End只移焦點、Enter／Space啟用，保留Raw／未存草稿確認。教師／TA本人當次錯誤可看固定診斷碼／說明／HTTP狀態及失敗實際請求dialog；完成後重查課程角色，學生POV／僅修課教授／降權不提供。任意上游本文、inner exception、thinking不傳送／存DB／log；未知明說未知，舊失敗不重建原因。

2125中文被擋根因是UTF8 bytes與token預算混用。CaseChatBudget分離兩限；只有Omni profile bytecap24000、基本CJK估2tokens／字、傳輸預留512，其他profile預設3／16保持原行為及12KB。仍扣輸出、保留8k字限制、不裁字、不加歷史；是估算非精確tokenizer。合成五段2125字預檢／真正Provider通過、原文完整2messages、prompt4031／response567／stop；Chrome也驗前端確實送AJAX。無真實病例進測試。

Vue288、Chrome30、完整隔離DB394與036→039指紋升級通過、schema清除，靜態審查無可行動問題；標準VS含Vue成功。唯一7173正常bin/Debug PID36752／qwen25-omni，部署24k／2／512及bundle index-5twnEPzG.js核對、/courses200／匿名AI401、舊埠無listener。無Migration，重啟清Raw，正式歷史保留。NU1900／chunk提示非失敗，未提交推送。

使用者另回報真實長文重複評語；僅查metadata：prompt4015／response1762／stop、feedback600字／有效ann5，非16K硬上限錯誤，合法JSON不表示品質通過。沒有讀出／重送其原文或存到測試文件。B細碼／混合時點及重複仍有問題，未擅自新增分段分析或自動品質判定，後續應另以合成長短文對照驗證。

## 2026-11-17 正式B回饋v4已部署（品質仍有限制）

依使用者要求開始修正，保留完整教授規則，只在正式B追加ABCD／子碼與時點對照，B提醒有問題才修改，schema先annotations後feedback，仍一次api/chat。A/C/D與Studio原規則／v3、Raw及四profile不變。B用professor-four-phase-20261117-b-v4，workflow持久化／retry及本人請求capture版本一致；舊歷史不重寫，舊B失敗不可用新版冒稱重試，需重新開始B自行提交。

真正Provider最終7原例＋4新例均完整JSON，但細碼、舊手術／本次混碼、無據職災及status仍錯，不是教學品質通過。system7704 bytes，原12KB guard不放寬，755中文字可容納；詳docs/ai-tuning/03-evaluation-results.md。禁止把格式測試當語意正確或程式猜碼修評語。

隔離DB385/385、036→039指紋升級通過且schema刪；Chrome請求／標註11/11、標準VS含Vue通過。靜態審查無需修正事項。初受限testhost逾時，已在允許執行環境重跑成功；NU1900弱點來源與原chunk提示非失敗。唯一7173正常bin/Debug PID27984／qwen25-omni，部署Bv4／Av3反射核對，/courses200／匿名AI401、舊埠無listener，bundle index-BOZ0N_8q.js。重啟清Raw，正式歷史保留。無Migration／正式資料修改，文件同步，保留dirty工作，未提交推送。

## 2026-11-17 B分類／回饋診斷（未調校）

使用者觀察B較困難。現行Omni真正Provider＋教授v3合成7短例均完整JSON／stop，輸入2138–2148，仍有B1當另一階段、B3否認已寫病灶、B7b未來目標誤C5、混合A/B概括錯置及重複回饋。細碼與status不可靠，不能視為教學驗收或都歸因截斷，詳docs/ai-tuning/03-evaluation-results.md新紀錄。

本輪診斷／文件，無正式Prompt／profile／payload／版本／DB／Migration修改、無重啟，未使用真實作答。探針成功建置（NU1900弱點來源網路警告）；無程式改動，未重跑全量產品測試。保留dirty工作，未提交推送。

## 2026-11-17 編碼徽章依ABCD配色

依使用者要求，共用PhaseAnnotatedAnswer的行內編碼改用A藍／B綠／C紫／D橘的分類底色白字；同片段跨phase多碼各自著色，保留共同tooltip觸發與原文。沿既有category palette，淺色與底線色相同，深色用同色系深底維持白字對比（底線仍較亮）。底線切換／分類metadata不變。

先擴充既有測試確認缺少逐碼phase、2項失敗，再修正；相關Vue49/49、Chrome桌面／390淺深色6/6與Vite通過，已檢視390深色合成截圖。只前端，未重啟7173／改模型／DB；現供index-BOZ0N_8q.js。文件同步，原chunk警告保留，未提交推送。

## 2026-11-17 輸入容量診斷（未改設定）

使用者懷疑Taipei截斷Prompt。11次直接合成容量探針＋1次現行Provider合成Stage：Omni在請求16384下實收10079 prompt tokens並找回首／中／尾key；超量時Taipei兩模型及IAN02 Llama3皆回報約8192並丟失前文key，非Taipei獨有固定4096上限。現行Stage合成2375輸入tokens／stop仍有錯碼，分類失準不能全歸因容量。不同tokenizer不可按相同bytes直接比較；未取得host log，不宣稱已定位runner根因。詳docs/ollama/context-size-check.md。

本輪只診斷／文件，四profile、正式Prompt、Raw及Stage策略未改，未用真實作答／寫DB。7173核對仍PID18048／qwen25-omni、/courses200，未重啟；無build／全回歸（無程式改動），保留dirty工作，未提交推送。文件同步亦記history。

## 2026-11-17 等待訊息改處理流程示意

依使用者要求，Stage等待顯示「AI 助教處理中…」，逐字輪播接收作答→閱讀段落→分類→原文標註→底線／徽章著色資料→回饋；label改作答處理流程，明示非模型即時進度。Raw使用訊息送出／聊天前文／等候回覆的獨立流程，不宣稱分組／分類。僅前端copy，原暫停／下一則／reduced-motion與清理保留；不新增真正stream或修改payload。

相關Vue46、桌面Raw／390深色Stage Chrome2及Vite通過，已檢視手機；既有7173無重啟，/courses200供index-DudL5rjP.js，Omni模型／預算不變。原505kB chunk警告仍非失敗；文件／合成截圖同步，保留dirty工作，未提交推送。

## 2026-11-17 Omni階段回饋截斷修正（現行）

使用者回報B階段失敗，僅查指定時點metadata確認provider_failed（非context_limit），未讀取／複製真實作答；上游失敗本文未保存，無法還原該封包。另以全新合成長文重現／修正。Omni Raw仍512，正式CaseFeedback另用CaseFeedbackMaxTokens4096＋CompactCaseFeedback：feedback600字、8筆標註、引用160字、說明80字。只收斂傳輸schema，不改教授Prompt v3／case原文或原文驗證；其餘模型配置不變。獨立sampling複本避免污染Raw，budget按實際Stage輸出預算計。

所有正式Case回覆done_reason=length會明確報輸出上限，不保存半份assistant；資料庫仍沿provider_failed，無Migration。保留原user，same-ID retry可成功，舊失敗作答model／prompt未變仍可重試。後端隔離DB383/383與039升級指紋通過、schema清除；合成長文用實際Provider約7.8s完整JSON／stop，Raw2輪與短Stage亦成功，但模型仍過度分類，不是教學品質驗收。

標準VS含Vue通過；唯一7173正常bin/Debug PID18048，profile=qwen25-omni及Raw512／Stage4096已核對，/courses200／匿名AI401，舊埠無listener。重啟清Raw，正式歷史保留；無正式資料寫入或自動重送使用者作答。文件同步，保留dirty工作，未提交推送。

## 2026-11-17 Taipei Qwen2.5 Omni（現行）

依使用者指定模型，7173與預設Profile現qwen25-omni，Taipei192.168.1.195:11434／rockn/Qwen2.5-Omni-7B-Q4_K_M:latest；新增獨立profile與VS https-Taipei-Qwen25Omni（同7173），原Breeze／IAN02 Llama3／Qwen3設定原樣保留。ProfileNames集中於OllamaOptions，runtime及各測試工具沿同白名單。

實查Ollama0.40.11、模型存在（family qwen2vl／Q4_K_M），模板只有System＋Prompt。新profile合併單system、不重申legacy assessment user要求；保留共用抽樣與16384 context，但用模型原生512輸出上限。初用4096使Raw首回覆過長、次輪budget拒絕；512下2Raw＋簡短Stage JSON成功（約4.1／3.8／2.0s），較長B例仍在512截斷JSON且複述／重複，未通過教學品質驗收，沒有放寬驗證或修補回覆。

後端326通過／54外部DB條件略過（含新profile5個封包回歸），evaluation helper與標準VS含Vue成功；唯一7173正常bin/Debug PID24724、命令列／部署profile與model精確核對，/courses200／匿名AI401，舊埠無listener。無Migration／正式資料改動；重啟清Raw、Stage保留。前端仍index-CunPqfQQ.js。文件同步、保留所有dirty工作，未提交推送。

## 2026-11-17 等待時逐字輪播請求重點

依使用者要求，等待泡泡改為串流風格的逐字提示／游標，輪播公開教學要求摘要（目前階段、分類、原文引用、證據限制、修改方向及該階段API目錄碼）。明示是要求摘要，不是實際生成內容／進度，不讀私有prompt、requestInspection、學生作答或模型thinking。Raw使用獨立無教學提示詞／暫存上下文提示。可暫停與下一則，reduced-motion完整靜態顯示、無自動輪播；完成／錯誤／切模式／卸載清計時器。

新AiReplyWaiting.vue及caseChatRequestGuide.js由CaseAnalysisChat共用；原messages／payload／API／DB／Prompt／模型設定不改。Vue285/285、Chrome26/26及Vite通過，已檢視桌面Raw／390深色Stage；Vite有505.2kB單chunk提示，非失敗，未擴大重構。既有7173 PID34716仍Qwen、無重啟，/courses200供index-CunPqfQQ.js，Raw暫存不因本次部署清除。文件／合成截圖同步，保留既有dirty工作，未提交推送。

## 2026-11-17 切換IAN02 Qwen3（現行）

依使用者要求比較早期Qwen，appsettings預設Profile現qwen（IAN02 qwen3:8b）；三份profile內容原樣保留，新增VS選項https-IAN02-Qwen3，同樣只用7173。Llama3／Breeze選項仍在，沒有自動fallback或採用Breeze實驗提示。

IAN02version0.40.16／tags與2輪Raw＋1Stage實測成功；初輪Raw約51.2s、次輪約4.5s、Stage約46.5s（累計約102.2s）。另1則合成B病程例約71.1s，1筆有效引用、codes B1b/B1c，影像／手術未標註且feedback有要求未提供細節，不能判定教學品質通過。所有實測無DB／真實作答。

相關封包13及標準VS含Vue通過；唯一7173正常bin/Debug PID34716，命令列／部署設定profile=qwen，/courses200／匿名AI401、舊埠無listener。Raw重啟清除，正式歷史保留；建議新開階段對話。無Migration或推論參數變更，文件同步、保留dirty工作，未提交推送。

## 2026-11-17 使用者確認切回Llama3（現行）

使用者已選「切回Llama3，保留Breeze比較」。appsettings預設Profile現llama3，三份profile及兩個VS7173選項不改；四種Breeze實驗均未上線。切換前一度IAN02短名稱解析失敗，DNS／本機名稱查詢解析ian02.local且IPv4可達後，原BaseUrl的version／tags與合成2Raw＋1Stage皆成功，未改成固定IP或調整模型設定。

標準VS含Vue建置通過；唯一7173正常bin/Debug PID36892，命令列明確profile=llama3、部署設定同值，/courses200／匿名AI401、舊埠無listener。Raw暫存因重啟清除，Stage歷史保留；建議新開階段對話，分類仍需教師核對。無DB／Migration，文件同步、保留dirty工作，未提交推送。後續仍重查實際環境。

## 2026-11-17 Breeze分類不穩定（待模型選擇）

已重現Breeze複述規則、引用不是學生原文及錯選分類；實際請求有完整43碼／目前phase／schema，前端沒有漏送。合成3例原版＋4種暫時包裝共15次，均不足以採用，沒有改正式提示或放寬驗證。Llama3後續HTTP比較3例可解析，純B較佳但混合例仍過度分類；兩者皆未通過教學品質驗收。完整證據見docs/ai-tuning/03-evaluation-results.md。

本輪僅診斷及文件，未改正式程式／設定／DB或重啟；目前仍Breeze PID17972。已詢問使用者是否切回Llama3並保留Breeze比較，未收到指示前維持現況。所有試驗資料合成、只留本機忽略結果，沒有讀取真實學生答案。保留先前dirty工作，未提交推送。

## 2026-11-17 Taipei恢復後切回Breeze

依使用者要求先確認服務才切換：Taipei192.168.1.195:11434的version／tags成功（Ollama0.40.11、Breeze模型存在），既有Provider合成2輪Raw＋1輪結構化Stage均成功（首次合計約4.3秒）。切換後另以明確current-stage overload再驗3筆成功；無DB寫入／真實作答，僅傳輸與格式驗證，不是臨床品質驗收。Local／Process／User／Machine無相關三鍵覆寫。

appsettings預設Profile現breeze；Configuration/Ollama三份profile與VS兩個7173選項完全保留，沒有自動failover。相關封包13項及標準VS含Vue通過；唯一7173正常bin/Debug PID17972，命令列明確profile=breeze，/courses200、匿名AI401、舊埠無listener。重啟清Raw暫存，Stage DB歷程保留；建議切模型後新開階段對話。測試helper改用明確stage overload。文件同步、dirty工作保留，未提交推送；後續仍重查現況。

## 2026-11-17 C2雙語顯示名稱

C2共用Label改「Muscle Power（肌力與相關動作檢查）」，圖例／tooltip／徽章說明沿API目錄自動更新，code仍C2。為維持既有prompt長度預算，目錄新增JsonIgnore的選用PromptLabel，C2模型用既有精簡名稱、規則及payload文字保持；不改三profile／v3。相關後端19及標準VS含Vue通過，已查建置DLL確有新Label。唯一7173正常bin/Debug PID39792、/courses200，舊埠無listener。重啟清Raw暫存，正式歷史不改；無DB／Migration，保留dirty工作，未提交推送。

## 2026-11-17 底線／編碼徽章切換

使用者澄清邊界是B1／B2等同色編碼，非標點，並確認兩模式可切換。共享PhaseAnnotatedAnswer預設底線；不同相鄰codes交界各縮3px，形成6px間隙，相同codes仍連續。徽章模式在有效片段末端顯示綠底白字code（多碼同徽章），無底線，支援hover／鍵盤／touch說明與Escape。課內聊天／教師查閱及Studio沿用元件；本頁切輪次／版本保留模式，無storage或DB。原文／分類metadata／授權不變。

Vue280/280、Chrome26/26及Vite通過，已檢視1440淺色與390深色合成畫面。7173無重啟，/courses200供index-DD-5sG5c.js；無後端／Migration／模型變更，原Raw記憶不清除。UI／規格／歷程及合成截圖同步，保留dirty工作，未提交推送。

## 2026-11-17 原文統一分類底線（現行）

使用者確認移除原文黃底／黃字；全部phase（含目前階段）統一分類色底線，多分類疊線，文字繼承原色。只有分類標題保留目前階段強調；已辨識碼色底白字、選取細外框、tooltip不變，階段核對文案同步。相關Vue56、桌面／手機淺深色Chrome8與Vite通過，已檢視390深色。既有7173無重啟，/courses200供index-CGkt_bT-.js；無後端／DB／模型變更，保留dirty工作，未提交推送。

## 2026-11-17 深色模式相符分類黃字

依使用者要求，深色模式目前階段相符原文與分類標題改#ffe066黃字、透明底；淺色保持黃底深字。跨phase底線恢復繼承深色亮色，已辨識分類按鈕仍分類深色底白字。相關Vue12、桌面／手機淺深色Chrome8及Vite通過，已檢視390深色。7173供index-BsBZnsWa.js、/courses200，無重啟／後端／DB／模型變更；保留dirty工作，未提交推送。

## 2026-11-17 回饋輪次最新優先

回饋sidebar改為新到舊，原輪次編號與內部時間序保留；預設仍選最新，教師查閱同規則。Vue40、桌面／手機Chrome2及Vite通過；7173頁面200供index-CDgavGfJ.js，無重啟／後端／DB變更。既有dirty工作保留，未提交推送。

## 2026-11-17 AI回覆等待泡泡

課內Raw／四階段send與retry等待時，在右側回覆區以既有教授SVG及assistant泡泡顯示轉圈與「AI 助教正在回覆…」，不再同時顯示尚無訊息。成功／失敗／模式切換依busy清除，不加入messages／DB／模型context；role=status及減少動態偏好停用旋轉。Vue相關40、Chrome桌面Raw／390深色Stage兩項與Vite通過；7173頁面200供index-DY5UQN2s.js，無重啟／後端修改。保留既有dirty工作，未提交推送。

## 2026-11-17 階段獨立作答與請求入口

四階段每輪只傳目前phase system＋本次user，歷史留DB供查閱／diff／統計；workflow與provider均排除舊user／assistant，Raw LangChain多輪不變。Prompt v3、三profile與抽樣未改，失敗原文仍可重試。Stage 413提示縮短本次作答，不建議靠重開清歷史增加容量。

教師／TA本人新回覆旁「查看 Ollama 請求」直接開實際payload，效能入口保留；學生／預覽／他人紀錄不提供，舊回覆／刷新後不重建。隔離DB375/375（含8輪長歷史與失敗retry）、Vue275、Chrome22＋更新fixture後請求4及標準VS含Vue通過，schema已刪。唯一7173正常bin/Debug PID12164、/courses200、index-CDZEaQS1.js，舊埠無listener。無Migration／public寫入／實際LLM推論；重啟清Raw暫存，保留dirty工作，未提交推送。後續仍重查現況。

## 2026-11-17 分類圖例簡化

移除作答下方重複的「分類圖例／A–D」總列與外層收合，直接顯示四個可獨立展開／收合的分類。保留已辨識分類底色白字、目前階段黃色、原文強調與提示。相關Vue12、Chrome22（桌面／手機、淺深色）及Vite通過，已檢視390深色畫面。7173回應200並供應index-B7VvPu6H.js；僅更新前端，未重啟／改模型／DB，保留既有未提交工作。

## 2026-11-17 已提及分類圖例底色

依使用者要求，作答有效標註中已辨識的分類按鈕用該phase深色底＋白字，淺深主題一致；未提及維持中性，黃色原文／目前phase圖例不變。子碼會連同父類顯示（A2a→A2），父類點選可強調已標註子碼，不能由父碼反推未標註細項。以有效exact-text標註為依據，不讀字面關鍵字假造分類或宣稱答對。

相關Vue12、Chrome1440／390淺深色4及Vite通過，已檢視桌面；7173只更新前端bundle index-KY1YZEZ9.js，無重啟／後端／DB／模型修改。保留既有未提交工作。

## 2026-11-17 效能dialog實際Ollama請求

使用者要求不只讀程式，直接在統計dialog看建構請求。已新增統計／Ollama請求分頁，顯示實際transport payload、可讀訊息／format／options及複製單次HTTP body；多次明示payload陣列。僅有效課程teacher／TA本人新send/retry/Raw有選用requestInspection，完成後重查角色；平台教授在該課僅學生也無權，學生視角／學生／查閱他人紀錄不提供。舊紀錄無擷取，不依目前設定重建。

擷取只在HTTP scope＋Vue頁面map，Stage按replyTo／requestId、Raw按session＋assistant位置綁定；模式／帳號／權限／Raw清除／失效／離開清掉。無DB／storage／log／diagnostics，Raw仍無appsystem／持久化／統計；不含conversation response body／thinking／URL憑證／headers。沒有Migration或profile變更。

後端318＋53條件略過，授權隔離DB完整371/371、Vue273、Chrome22、標準VS含Vue成功，獨立review無程式／安全問題；schema已刪。本輪模型用HTTP mock，未實呼叫LLM、不作教學品質聲明。唯一7173正常bin/Debug PID43760、/courses200、bundle index-CFfvnS5W.js，匿名AI401，舊埠無listener。完整docs/ollama/request-dialog.md；保留工作未提交推送，後續仍核對環境。

## 2026-11-16 目前階段黃色標示

依使用者要求，目前階段相符原文與圖例統一明確黃色#ffe066＋深色字#332a00，取代各phase低透明度底色；篩選淡化仍用不透明淡黃，深色模式同樣可讀。其他phase底線保留，混合碼在黃色上改用較深底線以維持對比。相關Vue10、Chrome桌面／手機淺深色4與Vite通過，已檢視390深色；既有7173無重啟、/courses200供index-DSmCZuoZ.js。無後端／模型／DB變更，既有dirty工作保留。

## 2026-11-16 目前階段與分類核對

使用者截圖B內A被分類但AI只稱讚；修正為stage-aware Provider明傳currentStage/currentPhase/label，prompt v3前置目前階段並要求指出主要錯置。無病例只限制事實核實，不抹除可辨識的分類錯置。Studio共享方法、保留schema。三profile／Raw／DB keys不改，無Migration。

UI明傳stage：同階段淡色背景（B綠）、他階段彩色底線，合法同段多碼可兼有。AI回饋區另列明「階段核對（依AI分類標註）」及系統來源，原AI文字／舊metadata不可改寫；未標記不等於缺漏，必要背景保留。Vue265、一般後端312/50條件略過、隔離DB362/362、Chrome17、標準VS含Vue通過，review無程式blocker；group_test已清。

實際IAN02合成3例：新輸入能讓模型提到目前／錯置，但仍有錯列子碼、不實時序問題，混合例最終未取得有效回覆（2/3格式成功）；不得宣稱教學品質驗收通過。完整docs/ai-tuning/02、03、06已補結果／截圖。唯一7173正常bin/Debug PID42896、/courses200、index-CQCWQczi.js，維持llama3，舊埠無listener。保留dirty工作，未提交推送；後續仍重查程序。

## 2026-11-16 四階段ABCD顯示名稱

依使用者要求，AnalysisStageCatalog及Vue questionStages顯示名統一A 病前功能／B 疾病病程／C 功能評估結果／D 心理社會條件。課內模式、下一階段、教師查閱／統計、案例維護及作答相關選單沿共用標籤；案例學習頁移除重複圈號數字。內部stage key、DB、Prompt及Raw不改。

Vue256、相關後端24、Chrome聊天13、標準VS含Vue成功。唯一7173保持IAN02 llama3，正常bin/Debug PID44276、/courses200、index-B6vVQs2p.js；舊埠無listener。既有dirty工作保留，未提交推送。

## 2026-11-16 IAN02／Taipei雙模型切換

使用者回報Taipei模型不可用，授權切回IAN02且保留兩份設定。appsettings預設Profile現為llama3，刪除基礎檔重複Breeze端點／模型；Configuration/Ollama三份profile完全保留原樣。Visual Studio新增https-IAN02-Llama3、https-Taipei-Breeze兩個互斥啟動選項，同用7173；原https也只綁7173，不同時開另一埠。Local無Ollama覆寫、Process/User/Machine無相關三鍵覆寫已核對。

IAN02:11434實查Ollama0.40.16、llama3.1:8b存在；三profile封包9/9、標準VS含Vue成功，實際IAN02合成2Raw＋1Stage／效能及JSON成功，無DB作答／成績寫入。模型教學品質未重跑，不將舊Breeze結果套到Llama。唯一7173正常bin/Debug PID31760、/courses200；兩份設定及切換操作見docs/ollama/README.md。重啟清除Raw暫存，Stage歷程保留，改模型建議新對話。未提交推送，保留既有dirty工作。

## 2026-11-16 教授方法與原文標註交付

已讀兩份任務及四份DOCX（含07流程註解），共享43碼目錄與professor-four-phase-20261116-v2方法，課內聊天／既有Studio均接上。四階段不變；Raw、正式成績／授權不改。新增唯讀原文底線、四phase圖例、hover／focus／touch tooltip、多碼／重疊與原文版次綁定。AI內部採精確text＋occurrence，server轉UTF-16，無效引用拒絕；公開content純回饋及原範圍metadata相容。

Public039已交易套用；舊038／037聊天、名冊及成績升級指紋保持，舊annotations NULL，metadata不可變。最終Vue256、後端310＋49條件略過，隔離DB完整359/359、Chrome20、標準VS含Vue建置成功；測試schema已刪。唯一7173正常bin/Debug PID33528、/courses200、bundle index-m5Icu7gl.js，匿名AI401，其他舊埠無listener；每次仍重查。

實際Breeze四階段合成測試已執行：先抓到照抄JSON示例、索引失敗，修正後最終12/12格式成功、9筆引用接受／22筆拒絕。**模型臨床教學品質未通過**（錯誤初始GCS、捏造遺漏、修訂判斷等）；不能以程式測試代替教授驗收。課內聊天無獨立可信案例來源，只能條件式引導；Studio沿既有caseFacts。六份[交付文件](../docs/ai-tuning/01-professor-rubric.md)、[逐項失敗與結果](../docs/ai-tuning/03-evaluation-results.md)及[UI](../docs/ai-tuning/06-annotation-ui.md)齊備。仍需教授確認少數編碼邊界及可信案例版本，不啟動新模組，保留所有既有未提交工作，未提交推送。

## 2026-11-16 練習模式驗證提醒

CaseAnalysisChat教師／助教教學練習提示已追加「各階段系統目前為 AI 提供，尚未經過專業調教與驗證。」僅文案修改，不改角色或統計規則。

## 2026-11-16 AI助教泡泡與教授SVG

依使用者要求，課內Raw／四階段／教師查閱的assistant回覆採青綠淡底、非對稱圓角泡泡，標題旁加入原創TutorAvatar inline SVG（眼鏡教授、書本、腦部徽記），沿用theme變數；僅裝飾且保留AI標示。Vue241與Chrome8流程、Vite通過，已檢視390px畫面。7173無需重啟，/courses200已提供index-DFncjnAJ.js；無後端／DB變更，既有未提交工作保留。

## 2026-11-16 回饋輪次與效能彈窗

已完成學生左側選回饋輪次／右側作答與AI配對、明確原文修訂與修改次數、任兩版學生輸入diff、逐回覆效能native dialog。使用者確認diff是學生原文；System Prompt不改。含未提交草稿確認、手動下一階段，沒有自動過關／成績。Raw效能只留暫存、不入LLM上下文或正式統計。舊metric NULL不補造，AvailableFields區別未提供與量測0。

Public038已交易套用，037舊聊天／成績升級指紋保持，revision parent與metadata不可覆寫且雙重授權。Vue241、真實DB330/330（一般283／47條件略過）、Chrome15、標準VS含Vue通過；Ollama合成2Raw+1Stage與量測確認通過。唯一7173正常bin/Debug PID26108、新bundle index-s9KPMSBt.js、/courses200，其他舊埠無listener。完整 [交付](../docs/feedback-rounds.md)。保留既有未提交變更，未提交推送；後續仍重查程序。

## 2026-11-16 個案分析五模式交付

使用者新任務要求擴充AI；Raw LangChain暫存＋四階段DB對話、去重重試、客觀統計／教師查閱已完成，無評分／過關。Public037已交易套用，兩門既有個案課啟用、115上等義轉115-1，IDs／名冊／作答／成績保留並audit；沒有平行課或假聊天。完整 [交付與工具](../docs/case-analysis-chat.md)。

Raw固定ConversationBufferMemory，不走舊40則SessionStore／Summary。成功pair才入記憶，清除／expiry取消inflight，20m閒置、每人3／全站200；刷新新ID、pagehide best-effort，429可明確確認清本人所有暫存。完成前重查授權。Stage user先存，AI保存配對才計往返，lease／attempt防晚回覆與重複。教師practice排除。三profile不改；透明ConversationAsync無appsystem／診斷，同sampling。模型本身有System，不可聲稱所有基礎設施不留資料。

Vue208、完整DB後端325/325（一般280／45條件略過）、Chrome12流程、標準VS含Vue成功；舊036升級／成績指紋與demo重跑通過，隔離schema已清理。實際Ollama合成2Raw+1Stage成功（無DB、內容不輸出）。唯一7173正常bin/Debug PID27380、頁面200、index-nrJjhDuj.js、11組API及匿名401確認，其他舊埠無listener。後續仍重查；未提交推送，正式案例／規準／PHI治理與模型品質仍待客戶。

## 2026-11-16 當學期依系統日期

最終Vue188、後端266通過／35條件式略過；另21項學期DB測試於授權隔離schema通過且已清理；Chrome4流程與標準VS含Vue建置成功。7173已更新正常bin/Debug PID21464，/courses200、新bundle index-ozAiTLef.js，未登入calendar401；其他既有舊埠無listener。無Migration／正式資料寫入／提交推送，後續仍重查執行狀態。

使用者已回答待確認項：改依系統當年日期，目的只是預設收起過往學期。現行`CurrentTerm=null`表示自動判定，取台灣UTC+8日期轉民國學年度，預設8月1日起上學期、2月1日起下學期（月份可設定）；暑修保留手動查詢，必要時仍可用明確CurrentTerm覆寫。這項取代11/13「未指定、不可依日期」的待辦。不改課程status、不刪舊課、不調整授權與歷史成績。若本學期無課但有歷年課，空狀態引導切換篩選，不誤稱未修課。

## 2026-11-13 CSMU UI／UX 與學生帳號

標準VS MSBuild（含Vue）已成功，唯一7173更新為正常bin/Debug PID31956，/courses200、CSMU title、新bundle與calendar／term API確認；5097／5107／5173／5180無listener。public完整36份Migration，最新036；後續仍重查執行狀態。

完成學生管理標籤／誤導進度移除、單元統計精簡、原始附件檔名與下載、影片專屬後端計時、分享登入返回、明確當學期設定與學年／學期篩選、小組組長更換保留成員加入紀錄、檔案繳交作業命名與CSMU品牌。`AcademicCalendar:CurrentTerm` 目前 null，已詢問使用者但未獲指定，不可自行以日期或最新課程推算。既有靜態門檻採正式點閱確認，舊達標／開始依據保留。

使用者續指示學生Email也改為csmu.edu.tw：public已交易更新19個可明確識別的純學生登入帳號，稽核0待改、再跑0更新；ID／密碼／角色／歷程保留，教師／TA／管理員不改。工具具衝突全交易拒絕、preview／apply／audit。新示範學生也採此網域。沒有新Migration，001–036隔離套用及30項DB整合通過，另Email衝突／密碼保留檢查通過；Vue187、Chrome9流程通過。交付與後續環境核對見 [本次交付](../docs/ux-corrections-2026-11-13.md)。保留本來未提交checkbox修正，未提交／推送Git，不延伸AI或個案功能。

## 2026-11-13 全站選項控制項對齊

統一12個Vue檔案、21處checkbox／radio宣告的choice-label＋choice-text，原生控制項固定18px、文字在右且多行首行對齊、整列可點、手機44px點擊列及鍵盤focus。修正全寬文字輸入CSS誤套checkbox（實測1034px）根因，移除互相衝突display:block；同分意願radio補原生同組name支援方向鍵。舊MVC思考開關同步樣板／CSS可存取性。Vue165/165、7個Chrome流程及.NET隔離build通過，既有7173提供新bundle，未新增埠／Migration／資料變更，未提交Git。

## 2026-11-13 展示評語呈現整理

已按使用者要求清理 public 的固定 Codex／展示批改前後綴、PDF 檔名及互評／意願標籤，共 419 列（線上評語125、PDF72、互評216、意願6），只處理有原種子事件證據者。分數、公開值／時間、評分者、PDF bytes、不可變答案與原始稽核保留；既有交卷快照中的展示尾註未回寫。工具提供 preview／apply／audit，可重入且逐筆記錄修訂雜湊；未來種子已去固定文案。見 [維護方式](../docs/demo-presentation-maintenance.md)。後端234通過／28條件式略過，隔離schema保護測試通過；資料即時供7173讀取，不需Migration／重啟，未提交Git。

## 2026-11-13 成績總覽評量 × 學員

依使用者要求，教師成績總覽由五欄分類摘要改為評量項目列、學員欄的對照表，固定左側評量／滿分及上方學號／姓名。每格原始得分與發布狀態，下方篩選範圍分類平均及全課程暫算／已發布總分，單元／類型／活動篩選沿用。學生畫面、計分及發布 API 不改。Vue 165/165、Chrome 多學員矩陣與師生共 3 流程、Vite 通過；既有 7173 PID 17604 提供新版，未提交 Git。

## 2026-11-13 Portal 每頁筆數

所有首頁資訊面板新增每頁 5／10／20／50 筆選單，預設 5，選單在收合時仍可操作且不觸發展開。換筆數回第一頁；訊息與學生待辦真正送出 page/pageSize，其餘對已載入清單分頁，移除教師討論／最近繳交固定六筆截斷。成績仍是最近 12 筆學習活動內的公開成績摘要、行程仍最多讀取 100 項，完整入口保留。每頁設定本次瀏覽有效，收合偏好仍跨次保存。Vue 164/164、師生 Chrome 2 流程與 Vite 通過。原 7173 已停止，重啟既有 bin/Debug PID 17604，未新增埠／後端／Migration；未提交 Git。

## 2026-11-13 Portal 面板收合

我的首頁資訊面板（跨課訊息、行程、待辦／待批改、成績／討論、課程、最近繳交）使用共用 PortalPanel 原生 details／summary，預設展開、標題／數量常駐，支援鍵盤。localStorage 只保存按帳號與教師／學生視角分開的開關值，儲存受限仍能操作。摘要捷徑及錨點會展開目標。Vue 160/160、師生瀏覽器 2 流程（含 reload／鍵盤／手機）通過，Vite 已更新既有 7173；無後端、Migration 或程序變更，尚未提交 Git。

## 2026-11-13 會議差距落實

個人首頁新增跨課活動流／考試行程，課程側欄新增持久化公告；公告沿用助教 materials 能力。正式考試預設同一成績簿的紙本期中／期末全班登分，原線上考試收合保留。小組 PDF 採選組側欄、進階個別調分／回饋，PDF 成績改為完整度與版本檢查後整班交易發布，單份發布明確拒絕。課程首頁新增本課進度摘要。詳見 [會議後續交付](../docs/meeting-followup-2026-11-13.md)。

Migration 036 新增公告、選填考試時間與分數發布時間 trigger，防止新草稿誤成公開成績事件。歷史發布時間以 updated_at 近似回填；不更動既有分數與發布快照。這項取代下方「公告尚無後端」及「正式考試只用 exams」的舊交接，原線上 exams 資料與流程仍保留。

036 已套用 public。最終 Vue 157/157、後端 231 通過／28 條件式略過，另獨立 schema 真跑 13 項 DB 整合與舊資料升級全過；Chrome 4 個合成 API journey 通過。新版 bin/Debug PID 28360 僅綁 7173，Visual Studio 保持開啟，其他舊埠無 listener；下次仍須重查。尚未提交 Git。

## 2026-11-13 評分項目改等比平均

使用者確認移除課程評分項目權重：各項先換算百分制後等比平均，空白不計、0 分計入。成績簿／分類摘要／個人預覽採同一原則；已發布課程總成績保持原快照，重新發布才套用等比平均與既有全班／個人調分。課程權重 UI 移除；相容 API 欄位固定 1、舊資料權重欄位保留但不參與算法。測驗題目配分與題內評分規準不變。這項取代下方保留課程加權算法的早期記錄。

## 2026-11-13 英文課討論示範

原數學課已由使用者改名「英文課」。該課原 3 串示範提問與 9 則教師／同學／助教回覆已原地改成 do/does、did + 原形及咖啡店禮貌對話；IDs、作者、讚數與關閉狀態保持。新增 Development-only `--english-discussion-demo-preview`、`--english-discussion-demo --course=<id>` 及唯讀 `--english-discussion-demo-audit --course=<id>`；只更新該課確定性 demo IDs，不執行全課程 seed。

## 2026-11-13 我的授課影音標示

授課課程卡片在 YouTube 教材數量大於零時，顯示 Video 圖示及「YouTube 影音教材・N 部」。課程清單／詳情既有 CourseSummary 新增 `youTubeCount`，同一個授權查詢按 course_id 與 resource_type 計數，不額外逐課請求、不改資料庫。重新 F5 才會讓執行中的舊後端提供新增欄位。

## 2026-11-13 UX／導覽整合

登入首頁改為教師／學生個人儀表板，重用真實授權 API；全域側欄與右上帳號選單加入我的首頁（Portal），側欄另有跨課／跨學期我的學習歷程。課內使用課程學習進度，隱藏跨課篩選。教師成績預設固定摘要，支援單元／類型／活動與明細；學生合併為一張我的成績表。小組 PDF 顯示完整指定名冊與未交數，分開成績分配／組員回饋，強調個別最終分數與調整原因。後端 API／Migration／授權與計分沒有修改。

操作、限制、11/15 驗收與驗證見 [UX 整合紀錄](../docs/ux-sweep-2026-11-13.md)。公告、獨立同分調查及獨立正式考試跨成績簿整合尚需後端支持。Demo 成績 audit 通過；討論 audit 因課名已變更而未通過。保留 Visual Studio 執行中的程序，不另開 port；狀態需每次重查。

## 2026-11-12 Visual Studio 自動建置 Vue

一般 Visual Studio Build／F5 與 `dotnet build` 現會先檢查 `ClientApp/package-lock.json`：npm 安裝標記缺少或較舊時執行 `npm ci`，並在每次 Build 執行 Vite，避免 Git 忽略的 `wwwroot/portal` 殘留舊 bundle。`DisableFastUpToDateCheck=true` 防止 Visual Studio 在只改 Vue 時略過整個 MSBuild。Publish 復用同一 `BuildVue` target，只收集一次產物；`SkipFrontendBuild=true` 僅供前端已另行建置驗證的隔離流程。Node/npm 缺少或 Vue 失敗會讓 .NET build 明確失敗。

Visual Studio 隨附的 Node/npm 可能不在 PATH；csproj 現由版本／edition 無關的 `$(MSBuildExtensionsPath)` 探測 `Microsoft\VisualStudio\NodeJs\npm.cmd`，存在時優先使用，並只對 frontend Exec 將同目錄置於 PATH 前端，讓 Vite shim 也能找到 bundled `node.exe`；否則退回 PATH 上的 npm。`NpmExecutable`／`NpmNodeDirectory` 可由 CI 明確覆寫。回歸測試移除 PATH 上的 Node/npm，並以含空白的 Visual Studio NodeJs 路徑重現及防止 9009 與後續 code 1。

行為測試涵蓋乾淨 restore、增量跳過、lockfile 更新、skip、publish 單次 build、失敗傳播、Visual Studio npm fallback 及 Fast Up-to-Date policy。一般 build 與 publish 實測 Vite，publish 計數 1 並含 portal index；Vue 100/100、.NET 214 通過／19 條件式略過。7173 已依使用者要求停止，供 Visual Studio F5 使用；5097／5107／5173／5180 無 listener。CLI／CI 若沒有 Visual Studio bundled npm，仍須依 README 安裝 Node/npm 並加入 PATH。

## 2026-11-12 測驗題目排序圖示

課程測驗／作業共用編輯器的「拖曳、上移、下移」文字控制已改為既有 Lucide `GripVertical`、`ArrowUp`、`ArrowDown` 圖示按鈕；沒有新增 Font Awesome。中文 `aria-label`／tooltip、首題上移與末題下移的停用狀態、拖放與儲存順位均保留。TDD 專項先失敗後通過，完整 Vue 33 檔案 100/100，Vite 與標準 .NET build 通過。最新版 PID 30864 只在 `https://localhost:7173` 運行，`/courses` 回 200 並載入 `index-BnAXc7hJ.js`；5097／5107／5180 無 listener。

## 2026-11-12 分層課程工作區

已依核准設計完成平台 → 課程內容 → 單元工作區 → 活動的導覽。課程首頁即單元總覽；單元 canonical URL 為 `/courses/{courseId}/units/{unitId}`，可建立教材、線上／課後測驗與 PDF 作業並跨類型排序。課程層仍保留正式考試、學員、分組、討論、成績、統計與設定；舊 `section=units` 及未歸類活動保留相容。教材、assignment／quiz、PDF 詳情以持久化 `unitId` 返回單元。

Migration 035 已套用 public：三種一般活動新增 `unit_sort_order` 並依既有建立順序回填；正式 `exams` 移出單元但資料未刪。最終審查另補齊未歸類活動的學生相容入口、單元內完整教材管理、授權助教建立活動、重排共用鎖及 PDF 同單元編輯不覆寫並行排序。前端 99/99、後端 214 通過／19 條件式略過、Vite 與標準 .NET build 通過；登入 E2E 因未提供 `PORTAL_DEMO_PASSWORD` 條件式略過。035 已套用 public。最新版 PID 31080 只在 `https://localhost:7173` 運行，`/courses` 回 200 並載入 `index-B7eGRHUa.js`；5097／5107／5180 無 listener。下次仍需重新核對 listener，不要另開其他 port。設計與執行計畫位於 `docs/superpowers/`。

## 2026-11-12 Demo 學員常見姓名

原 6 位仍名為「教學展示學員 1–6」的既有 Demo 帳號已原地改名為陳怡君、林志明、王美玲、張家豪、李雅婷、黃建宏。User ID、學號 DEMO115003–008、展示 Email、登入憑證、修課、交卷、分組與已發布成績均未改；PDF `report_snapshot.display_name` 已同步，避免評閱頁仍顯示舊名。另兩位本來已有姓名的展示同學未更動。

Development-only 入口為 `--rename-demo-classmates`，可安全重跑；audit 已確認 6 個帳號與 PDF 快照一致且沒有舊名。

## 2026-11-12 Demo 成績已全部發布

使用者明確授權後，所有具 Demo 標記且全員已評分的評量已按「同一作業全班一次發布」交易式發布。線上評量共 14 份、151/151 位學生；PDF 共 12 份、72/72 份繳交；小組快照 55 位成員皆有 published score。既有 `course_grade_scores.published_score` 已同步，發布工具留下作業／報告層級 audit 與 PDF grade event。此次沒有替未繳交者建立假 submission，也沒有略過缺分者；完整度不足會讓整筆交易回滾。

入口為 Development-only `tests/PortalSmoke --publish-demo-grades`，唯讀驗證為 `--publish-demo-grades-audit`。發布後 audit：線上 151/151、成績簿 151/151、PDF 72/72、未發布小組成員 0。下方「0 筆發布」紀錄是發布前歷史狀態，已由本節取代。

## 2026-11-12 單元評量與全班批改工作區

一般線上作業、隨堂／課後測驗及 PDF 作業由課程單元統一進入；教師在單元活動旁可直接選「批改作答／批改 PDF」。原外層「測驗與作業」已改為「正式考試」，使用既有 `exams` 功能承接期中、期末等獨立考試；一般評量建立與舊深層連結仍留在相容收合區，資料未搬移或重建。

線上作業批改改為討論區式雙欄：左側已交卷在上、未交卷在下，右側沿用真正 `TeachingPanel` 批改所選交卷；未交學生顯示題目與唯讀空白卷，不建立 submission、不可評分。Development-only `--submitted-assignment-demo` 已為 Demo 課程 14 份評量補齊 151 組真實帳號交卷與未發布 Codex 評分草稿；第二次執行 0 新增，audit 151/151。沒有發布成績。

前端 89/89、後端 210 通過／19 條件式略過、Vite 與 PortalSmoke build 成功；public migration 001–034 全部已套用。實際 Chrome 驗證單元批改入口、10 人交卷 sidebar、答案／草稿評分及正式考試頁。唯一 7173 為 PID 9788；5097／5107／5180 未啟動，下次仍須重新核對程序。

## 2026-11-12 靜態教材點閱與影片觀看統計

教材教師統計已依媒體分流：影片／YouTube 保留學生累積實際與內容觀看時間；其他靜態教材只顯示正式學生點閱名冊、每人點閱次數、最近點閱、總瀏覽與讚數。Migration 034 新增冪等 `resource_views`；教師、助教、學生視角預覽、封存與跨課不寫入。教材卡的靜態入口顯示「點閱紀錄」，即使格式不支援學習計時也可查看。留言數已與讚按鈕同高度同列對齊。

獨立 `group_test_*` schema 已驗證 001–034 舊資料升級與點閱統計整合並刪除。Vue 86/86、.NET 210 通過／19 條件式略過、Vite build 與 .NET build 通過。034 已套用 public；新版只在 7173 運行，app PID 31860，session／Swagger 200，5097／5107／5180 無 listener。實際 Chrome 已檢視靜態／影片入口、點閱名冊及讚／留言對齊；下次仍須重新核對程序。

## 2026-11-11 課程資源改以單元收納

教材管理已改為依課程單元排列的緊湊條列：圖片使用授權 preview API 延遲載入縮圖，其他格式顯示清楚文件／影音圖示；名稱、原檔名、大小、授課日、上傳日、下載數與摘要整齊排列。教師可就地開啟、下載、查看參與、編輯及移動單元；窄版操作移至下一列。新增檔案與 YouTube 教材必須選擇本課單元，既有未歸類資料仍顯示於相容區。

教材 tag 功能已完整退役：前後端 tag UI、搜尋、API 與 policy 移除，Migration 033 實體刪除 `course_resources.tags`／`tags_version`。獨立 `group_test_*` schema 已驗證 028→033 升級，舊教材內容雜湊與繳交紀錄保持不變，測試 schema 已刪除。後端 209 通過／19 個條件式略過，前端 82/82、.NET 與 Vite production build 通過。033 已套用 public；新版只在 `https://localhost:7173` 運行，session／Swagger 200，5097／5107／5180 無 listener。7173 app PID 32148、父 dotnet PID 14144；下次仍須重新核對，不要另開其他 port。

## 2026-11-11 課程討論區

既有「課程問答」已增量改為雙欄課程討論區：左側列出全部討論串，右側閱讀及回覆，頂部與底部皆可前後篇瀏覽。學生、教師與助教都能發起、回覆及按讚；教師／助教顯示 Moderator badge，並可關閉或重新開放討論。Migration 031–032 已套用 7173 public，保留舊問題、答覆、投票與附件並回填主題；舊版服務重啟前仍可相容寫入。後端 228、前端 78 及建置驗證見歷程。

左側入口原為不醒目的 `＋` 圖示，現改為藍色「＋ 新增討論」文字按鈕；展開後改顯示「取消」。只有可寫的正式課程成員顯示，封存與教授唯讀學生預覽仍隱藏。

6 門 active 示範課程各有 3 個可重跑的示範討論，作者與回覆者使用該課實際教師／助教／學生帳號；每課含按讚及 1 個關閉討論。內容依各課主題區分：個案一三五談事實／證據／資料缺口，個案二四六談方案比較／利害關係人／備案，數學談百分比／平均數／估算，科學談變因／對照組／誤差，兩門測試課分別以校園觀察與學習策略為情境。入口為 `tests/PortalSmoke --discussion-demo`，逐課唯讀巡檢為 `--discussion-demo-audit`，兩者僅允許 Development。2026-11-11 public 核對為 18 串、39 回覆；第二次 seed 新增 0。

## 2026-11-11 示範作業批改狀態

6 門示範課程共 12 份已發布 PDF 作業、72 份正式繳交，皆有 Codex 分數、文字回饋、評分者與評分時間，並保留 `codexDemo` 評分草稿事件；0 筆分數發布。唯讀巡檢入口為 `tests/PortalSmoke --assessed-demo-audit`，不依賴已清除的產檔目錄，也不會發布成績。

## 7173 單一開發環境

使用者指定只保留 `https://localhost:7173`，不得因輸出檔或連接埠占用另開 5097／5107／5180 等站台；必要時可先精確確認並結束 7173 的程序。2026-11-11 已確認 001–032 全部套用、正常 Debug 輸出建置成功，並以只綁定 7173 的 Development 程序啟動新版；session 與 Swagger 皆 200。

清理時發現舊 `.codex-build/course-portal-delivery` 的 5180 程序，已精確終止；整個 `.codex-build`（約 1.76 GB）、`test-results`、`output`、`tmp` 已刪除。現只保留專案 `bin/Debug` 的 7173 程序；不要重建其他埠的預覽環境。

教師／助教在課程首頁標題右側新增課程設定圖示，直接進入同課程 `?section=settings`；學生與唯讀學生預覽不顯示。

`/courses` 的擁有者課程卡片已加入快速管理：所有非封存擁有者卡可修改名稱，草稿另可公開及二次確認刪除；公開不可退回草稿，公開課程不可直接刪除。助教、學生入口與學生預覽不顯示。

## 2026-11-11 課程單元與課程生命週期

Migration 030 已套用 7173 public；現有 6 門有活動課程各建立一個相容單元，教材、測驗、PDF 作業及舊考試均無未歸類資料。課程擁有者可版本化編輯、封存及僅刪除完全空白草稿；教師可管理 `course_units` 與活動歸屬，學生唯讀並沿用原發布／小組 audience。操作、API、設計決策與 Demo 流程見 [課程編輯與教學單元](../docs/course-units.md)。

## 2026-11-11 展示同學平台帳號

既有 8 位「教學展示」學員已原地補成可登入的平台 Member 帳號，具有唯一展示 Email、學號與學生 profile；38 筆修課、48 份 PDF 繳交、分組、互評及批改紀錄仍綁定原 user id。可重跑入口為 `tests/PortalSmoke --activate-demo-classmates`，只在 Development 且提供 `PORTAL_DEMO_PASSWORD` 時執行，不把密碼寫入持久文件。

## 2026-11-11 課程問答範圍

作業與測驗頁已移除提問面板及 API。所有提問使用課程層級的「課程問答」。資料庫舊表保留供既有資料相容與後續遷移。

## 2026-11-11 課程側欄

「我的修課」列出本人正式修課及本人擔任教師的所有課程（含草稿）。從此處點選授課課程會直接進入唯讀學生視角，左箭頭返回我的修課。尚未開放的課程僅供任課教師預覽，正式修課學生仍須等課程開放。「我的授課」保留教師及助教管理入口。

## 2026-11-11 分組與 PDF 交接

Migration 029 和小組作業頁面、合成情境及驗收入口見 [課程分組與 PDF 作業](../docs/group-pdf.md)。已完成本機舊資料升級與角色端到端驗收。7173 的共用 public 已套用 029，教師與學生清單 SQL 已驗證。


## 2026-11-10 師生入口交接

課程導覽、唯讀學生預覽、逐人批改、成績發布、角色矩陣與開發示範以[師生入口指南](../docs/course-portals.md)為準。舊學生練習及直接顯示平均的說明保留作歷史紀錄。

最後更新：2026-11-11。此檔是交接摘要，不是完整變更日誌。

## 最新介面：前景播放才計時（2026-11-10）

- 使用者要求在計時說明強調背景播放不算；YouTube卡新增Eye＋「前景播放才計時」醒目區塊，明示切分頁／最小化，即使仍有聲音也不計時；示範與學習時間用詞分開。依既有visibilityState可見性，不宣稱鍵盤focus或注意力偵測，未改計時邏輯。
- LearningResourceView3項測試及build通過，5097合成1440／1024／390px位置／無溢出／無JS錯誤檢查，桌面截圖已檢視。只更新p0-publish前端與文件，不啟停服務／連DB／模型推論；未跑真實YouTube或全套回歸，未commit／push。

## 最新介面：摘要／播放／學生紀錄圖示（2026-11-10）

- 使用者反映摘要收合像播放鍵；CourseTools改FileText＋ChevronDown（展開向上），隱藏原生三角marker且保留原生鍵盤收合。播放入口加Play，師／助教標「播放課程回放（示範）」；原統計按鈕改UsersRound＋「查看學生參與紀錄」，面板明示正式學習時間。原API／權限／計時不變。
- CourseTools既有2測試與Vite build通過；5097合成教材驗證收合／Enter、播放href、紀錄面板及1440／390px無溢出，截圖已檢視。更新p0-publish前端，不重啟服務、不連DB或真實YouTube；未重跑全套回歸，未commit／push。

## 最新介面：教師／助教本地示範計時（2026-11-10）

- 使用者後續改採教師／助教也顯示運行中計時，但明示僅供示範不計入；此節取代下方隱藏數字方案。YouTube教學預覽改本地每秒計算實際／內容時間，播放／暫停／改速／背景／快轉處理，僅當頁記憶體，離頁清理、重新整理歸零，不寫session／正式或練習紀錄。
- 明示本課教授／TA身分、示範、不儲存或計入學習／成績／門檻，倍速提示也標為示範。正式學生與學生體驗沿用原後端session，不受影響。前端69與build通過；5097合成教授／TA動態示範與零session寫入檢查通過，截圖已檢視。已更新p0-publish前端；最終5097原程序已不存在且拒連，重新啟動PID27352後重驗通過，session200。未連DB／改模型或commit／push。

## 最新介面：教師／助教不計時提示（2026-11-10）

- 依使用者要求，YouTube標題／計時卡明示教師／助教預覽不記錄觀看時間，隱藏預覽累積數字／暫停狀態及計時控制／規則。按本課角色辨別；正式學生保留計時、學生體驗另提示本人練習時間，封存學生不誤標教師。
- 僅LearningResourceView前端條件呈現，後端與計時JS未變。Vite build及5097合成教授／TA／學生／練習／封存五情境檢查通過，教師截圖已檢視；已更新p0-publish前端，不重啟服務、未連DB或真實YouTube播放、未重跑全套測試、未commit／push。

## 最新介面：YouTube計時卡片位置（2026-11-10）

- 使用者要求計時移至YouTube播放器右側或下方；LearningResourceView調整wrapper／CSS，≥1280px右側300px欄、較窄視窗下方，新增學習計時標題，非YouTube仍計時在閱讀器上方。播放／計時JS、API與DB均未改。
- 前端66及Vite build通過；5097合成API／播放器在1440／1024／390px確認位置與無溢出，桌面／手機截圖已檢視。未重跑真實YouTube或後端整合。已更新p0-publish前端成品，不重啟服務或改模型；未commit／push。

## 最新實作：通知徽章與複製按鈕（2026-11-09）

- 使用者要求新作業／學生提問紅色數字提醒，續要求複製按鈕搭配重疊圖示。新增025通知已讀表與 /api/notifications、/read，個人逐項持久保存；學生看已發布測驗／PDF，師／助教看本課未讀問題，平台權限不繞過。正式／練習、封存／撤權隔離，初次既有未讀合格項目也計入。
- 導覽／課程卡片／課內頁籤／手機選單紅底白字徽章，0隱藏、99+上限。清單渲染後只標當次顯示項目（問題20筆分頁），同時重送穩定、其他人已讀不受影響；前景30秒／路由／聚焦刷新、帳號切換忽略舊回應。複製連結固定secondary按鈕＋Lucide Copy。詳見docs/notification-badges-spec.md。
- 025已套用，後端223（DB啟用0略過）、前端66、build／publish通過；5107與最終5097各3個browser情境皆通過（新徽章、原互動／複製、原評量給分）。已檢視桌面與390px手機徽章及Copy按鈕截圖；合成QA／state已清理，5107 PID41144停止。
- 5097更新p0-publish，PID30996，contentRoot根／webroot發布wwwroot，session及Swagger JSON200且含通知API；下次重查服務。未修改模型、推論、commit或push。新需求擴充站內通知，前批「無通知」保留歷史範圍，仍無email／瀏覽器推播。

## 最新實作：資源互動與課程問答（2026-11-09）

- 使用者續要求依規格書實作；新增 024_resource_interactions.sql，資源讚／留言／課程問題三表已套用，舊資料／成績／時間未搬移或重算。實作依 docs/resource-interactions-course-qa-spec.md 第九節；本節取代下方「下一批核定／未實作」現行狀態。
- 教材固定網址可分享／複製，一般不可計時檔案也有互動入口；每人一讚可取消，留言 4000 字純文字／20筆分頁，UUID 重送去重、作者由登入者決定。檔案按資源＋類型彙整正式閱讀／觀看、讚及最近留言，本人跨課、師／助教限指定本課，取消讚即移除。無全域管理權繞過。
- 課程問答獨立頁籤，學生問題立即同課可見、師／助教回覆；8000字問題、16000字單份可修訂答覆、版本409保留未存文字。舊作業私人問答完全保留。封存／練習不寫正式互動，檔案僅正式模式；無留言／問題編刪、通知或巢狀討論。
- 已完成測驗與作業／作業評閱與給分／課程成績命名、帳號與課程設定圖示、手機Menu／X及桌機強制隱藏手機選單。後端220（DB啟用0略過）、前端63通過，build／publish成功；5107新功能1項及既有7項browser通過，最終5097新功能／評量給分／課程導覽3個情境分別通過；重跑原因見history。合成QA及state已清理，5107 PID43712已停止，保留5097預覽。
- 5097已啟動新版p0-publish，PID53232，contentRoot專案根／webroot發布wwwroot，session與Swagger JSON200；下次重新查服務。未改模型設定／有效負載或推論，未提交／推送。

## 下一批核定：資源互動與課程問答（2026-11-09）

- 使用者已確認名稱／圖示／配置，要求執行並寫入 Markdown；依 docs/resource-interactions-course-qa-spec.md 開發與驗收。含分享、資源按讚留言統計、學員檔案互動、獨立公開課程問答、評量命名及設定／RWD 圖示。
- 本次先完成規格落地，產品尚未變更／migration 未建立／未部署；後續仍須實作與驗證，不把文件當完工。原作業私人提問保留，分享不開匿名權限，教師只看本課互動，練習不得污染正式資料。
## 最新實作：會議第一批教材與匿名觀摩（2026-11-09）

- 使用者要求依會議規格開發，並明確確認D1–D3：每份教材日期／摘要並依日期分組；TA準備觀摩、教師發布、兩者可撤下；首批申論及另備匿名PDF。真實作答使用授權仍逐項由教師確認，不能把功能完成當全體同意。
- 021新增teaching_date／summary／metadata_version，UI新增／編輯／清除、日期起迄／未指定與tag及q交集、排序分組、閱讀頁教師摘要；空日期不回填，日期不做時區轉換，摘要10,000字純文字，409保留草稿與原版本。名稱／逐字稿／時數／下載不重算。
- 022／023新增course_showcases／versions／publications；一項一個正式已評申論版本或PDF提交來源，來源最新有效學生及正式公布評分驗證；教師審查去識別副本與三確認／授權識別後發布，需來源closed且不可再作答。學生DTO只含匿名快照；原始PDF不公開，只接受另傳20MB內展示副本。
- 草稿編輯及換PDF皆新增版本，不變已發布內容；每次發布保留版本／教師／確認及授權識別，DB拒絕UPDATE舊快照或審查記錄。撤下及來源重新開放／失效後，學生列表／詳情／附件拒讀；封存仍能撤下。原提交與成績不變，practice／跨課／全域管理權不繞過。來源選單目前每類最多檢查200筆候選，無完整分頁。
- 021–023已套用，001–023重跑略過；後端214（DB啟用0略過）、前端60通過，build／publish成功。5107新功能2項及既有5項通過；最終5097共7個browser情境通過（批次6通過，首項啟動前拒連線，HTTP200後補跑通過）。檢視教師桌面／390px手機；只用合成內容，無真人匿名審查／教授驗收或模型推論。
- 5097更新p0-publish、PID51068，contentRoot專案根／webroot發布wwwroot，session及Swagger JSON200；下次重查。新版QA helper補觀摩來源／版本／審查guard及刪除順序，合成QA與state已清理；只停止本次5107 PID32100，使用者開發程序不動。使用者後續已明確要求提交並推送本批程式／文件至兩個repo；本次交接隨實作一併納入，Ollama保存分支不移動。
- D4保留原觀看／倍速門檻，D5通過／完課及第二階段AI仍後續，一般評量AI仍403。工作依據docs/meeting-2026-11-06-gap-spec.md及兩功能實作文件，本節取代下方原僅文件／待確認的現行狀態。

## 會議規格提交交接（2026-11-09）

- 使用者後續明確要求將會議gap／分功能規格一併提交並推送兩個repo，作為後續實作依據；取代下方先前「仍未提交」的交接狀態。本次僅文件提交，未開發或部署。
- 後續依docs/meeting-2026-11-06-gap-spec.md及分功能規格進行；D1–D5仍是待定細節，推送文件不代表教授已逐項核定，也不自動啟用一般作業AI。既有Ollama保存分支不變。

## 開發連線設定交接（2026-11-09）

- 使用者明確要求將實際資料庫連線字串與初始化設定秘密提交至兩個repo，以減少團隊設定負擔。appsettings.Local.json改納Git，移除該檔忽略規則；此決策取代下方「本地設定必須忽略／不含追蹤密碼」的舊描述，不代表可把值複製到Markdown、日誌或瀏覽器。
- 本次只納入連線設定及本決策說明，會議gap草案保留為未提交工作；不更動Ollama分支、模型、DB或服務。環境變數覆寫及publish排除本機設定的行為保留。

## 最新規格：11/6會議差距分析（2026-11-09，僅文件）

- 使用者要求一起讀會議整理、分析現況與目標並先寫規格；本輪不開發。依據是使用者貼上的會議摘要，未取得原始逐字稿／簽核或原12/15驗收清單，不把建議當教授已核定。
- 入口docs/meeting-2026-11-06-gap-spec.md，分出exemplary-work-spec（匿名觀摩）、teaching-material-metadata-spec（授課日期／教師摘要）、ai-draft-feedback-spec（第二階段）。目前缺觀摩、授課日期及教師摘要；標籤／YouTube／逐字稿／Swagger已在程式；歷程已有修課提交成績，正式通過／完課規則未有。
- 觀摩提案為正式已評且成績公布的申論／另備去識別PDF副本、教師最終發布、助教準備／緊急撤下、獨立匿名發布快照、來源及授權私有、關閉評量後發布防洩題。D1–D5（發布權、來源及授權、單元或日期欄、觀看展示、通過定義）均待確認；不自動開學生分享或公開原附件。
- 會議偏好暫緩複雜觀看監控，但使用者後續已核定並完成倍速內容門檻；現況保留，不刪功能／改時間／取消原門檻。AI一般作業端點仍403，獨立案例文字回饋保留，不能宣稱一般申論規準AI已完成。
- 建議先驗收基礎、補日期摘要、再定觀摩政策與範例；僅11/15候選，12/15待原計畫，年底為雛形，無新排程承諾。無全校／社團／視訊串流／新監控擴充。
- 本輪讀程式／schema及文件、核對Markdown連結／差異；未連即時DB／Notion、未分享文件、未跑產品測試／模型推論、未改產品或migration／啟停服務、未commit／push。兩remote雙推是本地Git設定，本輪文件仍待明確提交。

## 最新實作：逐字稿與倍速內容時間（2026-11-06）

- 本課YouTube新增表單可貼逐字稿，回放頁教師／助教可新增、修改、清除，學生唯讀；最多100,000字、純文字保留換行，獨立版本／409及未存離頁提醒。無自動轉錄或字幕擷取／時間同步跳播，封存／學生／跨課拒寫。
- 使用者明確選「影片內容時間計入門檻」：2倍速實看10分鐘計20分鐘，0.5倍速計5分鐘。0.25–4倍速按session分段，實際與內容時間分開，教師逐人與各速度統計、學生雙時間顯示。伺服器wall-clock×速度與位置上限，快轉／暫停／緩衝／背景不計，重看可累積，正式／練習隔離。講義仍實際時間；本人歷程原實際時間保持，不混入倍速內容。取代下方原1倍速限制。
- 020已套用，重跑略過；舊1倍速內容時間等量承接，不造觀看事件／不改成績或舊測驗開始依據。後端199通過（DB啟用0略過），前端55通過，Vite／publish成功；最終5097 browser/API 3項通過，含PDF／WebM、歷程／門檻與逐字稿／1x切2x／內容時間提前達標／跳轉排除／教師統計。手機與統計截圖已檢視，播放器計時採合成IFrame測試；本輪未重跑真實YouTube播放或AI模型品質。
- 5097更新p0-publish，PID41988，contentRoot專案根／webroot發布wwwroot，session200（下次重查）。QA／state清理成功，只停止本次核對的5107 PID39940，保留使用者Visual Studio程序；未提交推送。操作與API見docs/course-materials.md。

## 最新實作：教材標籤與 YouTube 回放（2026-11-06）

- 課內檔案／YouTube回放共用教材清單，教師／助教指定名稱與週次／主題標籤，學生點標籤／未標記或文字搜尋。每份10標籤、各30字，週次正規化，獨立版本／409與未存提醒；資源仍各課獨立。
- YouTube白名單影片ID、官方IFrame API／nocookie嵌入、播放自動計時，沿用伺服器時間／位置上限、背景／暫停／緩衝／倍速與跳轉規則。有效尾段close保存、整秒floor，重看可累積。換影片新增教材；站外觀看／不可嵌入不計。教師可看本課有效學生逐人正式時間／合計，不含練習；可設既有測驗門檻。詳見docs/course-materials.md。
- 018／019已套用，001–019重跑全略過；無真實教材搬移、歷史時數回填／成績重算。後端193通過（DB啟用、0略過）、前端53通過，Vite／publish成功。5107新教材＋舊計時3項通過，最終5097加檔案／改名共5項通過；檢視手機及教師統計。真實YouTube官方示例可載入嵌入與預覽；有效播放／快轉測試採合成IFrame事件，不宣稱真實班級或注意力驗收。
- 當次5097原未啟動，已更新p0-publish並啟動PID9188，contentRoot專案根／webroot發布wwwroot；session與Swagger JSON均200。下次須重查。QA帳號／課程／教材／時間／提交等及state已清理，只停止本次核對的5107 PID49724，未停止使用者Visual Studio。無模型變更／推論，未提交推送。
- 本輪工具可正常提高權限進行DB／本機瀏覽器驗證，取代下方Swagger當時受限的現行環境描述；保留其歷史紀錄。Swagger一併載入新版，原授權政策不變。

## 最新實作：Swagger（2026-11-06）

- 已加入 Swashbuckle.AspNetCore 9.0.6，Development 提供 /swagger 與 /swagger/v1/swagger.json，Production 該路徑404。登入沿用 Cookie，寫入自動取得 CSRF token；平台／課程授權不變，未加入 JWT。
- 產生92個API路徑；新增文件測試已以同程序執行通過，另驗證Dev UI／初始化JS／JSON、Prod404及Node interceptor。build／publish成功，成品swagger-release。VSTest受本機testhost連線限制未完成，不可報完整回歸。
- 本環境無法管理既有服務程序或連線既有預覽，未重啟5097／Visual Studio，也未做真實瀏覽器登入Try it out。開發服務重啟後即可載入；詳見docs/swagger.md。無DB／模型變更，舊服務狀態需下次重新核對。

## 最新實作：學習歷程與教材時間門檻（2026-11-04）

- 使用者已要求「先照規格做下去」，取代本日早先僅文件狀態。完成第一批：本人跨課／學期歷程、評量進度／待辦／課內加權平均，測驗／PDF／案例／線下成績／教材時間來源及分頁篩選。入口 /learning-history，教師／管理員不因新入口取得全校學生內容。
- 教材資源支援PDF／圖片／MP4／WebM學習計時；測驗編輯可逐份設定1–86400秒、全部達標並按開始才傳題目／接受作答。017已套用且001–017重跑略過，三新表保存門檻／計時／開始依據；無歷史回填、真實資料／成績未搬移。
- 實作政策：伺服器時間上限、10秒心跳、超25秒斷點不計、文件60秒互動時限、背景暫停、影片1x／快轉不計／重看可累積。單人跨分頁／裝置只一個計時，正式／練習分開；同資源發布前有效時間可沿用，檔案ID為現行內容版本，改名不重算。PDF內互動不可可靠捕捉，頁上提供繼續閱讀；不宣稱專注度或正式認證時數。
- 後端176通過（PostgreSQL啟用、0略過），前端45通過，build／publish成功；最終5097 browser/API 5通過，含新歷程／門檻、實際PDF／WebM計時、原批改／PDF連動／練習。已檢視歷程桌面／手機及閱讀器截圖，未做真實模型推論。
- 5097已部署p0-publish，PID31220（下次需重查），contentRoot專案根、webroot發布wwwroot；未停止使用者開發程序。合成QA／state已清理，本次5107 PID18380經命令列核對後停止。未提交／推送。
- 後續仍為：校務全修課／可靠身分、退選歷程、完整單元進度／完課／證書、影片已看比例／續播、跨修課匯出、公告與擴充教學報表。AI不擴充。操作／API／採用政策見docs/learning-history-spec.md第一批實作；舊候選表保留規劃脈絡，不等於全部已完成。

## 新增規格：測驗的教材時間門檻（2026-11-04，原規劃）

- 使用者要求先記錄：線上測驗可設定影片／講義等教材有效閱讀／觀看時間，達標才可開始。已記 elearning、learning-history-spec 與 learning-assessments；僅規格，未實作、未新增 11/15 承諾。
- 需個人有效活動追蹤與伺服器門檻檢查；下載不算達標，背景／閒置及重疊不累計，正式／練習隔離，原期限與課程權限仍有效。多教材預設逐份全達標、版本與發布前時數沿用等細節標為設計建議／待確認，勿當已核定細項。

## 最新規劃：全修課學習歷程與 11/15 候選（2026-11-04）

- 使用者要求先規劃納入本校學生所有修課的學習歷程，評估非 AI 功能可否加入 11/15；本次僅文件，未實作。主規格 docs/learning-history-spec.md，含使用者補貼參考的 11 類歷程及 LMS 亮點取捨。
- 建議近期 A 包：平台內本人跨課／學期總覽、正式提交／可見成績時間軸、評量提交進度、站內待辦與基本儀表板；約 4–5.5 人日＋1–2 人日驗收的初估，CSV 視餘裕追加。候選不是核定交付承諾。
- 全校完整修課需可靠校務身分與修課資料；現有 T2 只涵蓋課內案例，不能直接宣稱跨課完整歷程。有效活動時間、影片、完整完課／證明及校級權限分階段，不以下載當已讀或提交率當完課率。
- 管理員不因跨課歷程取得全校學生內容；學生本人跨課、教師限本課，正式／練習／草稿分離。AI 助教／推薦／出題延後優先，未改既有模型功能或設定。下一步按候選範圍排程及校方政策確認，不自行實作全部表列功能。

## 最新介面調整：作業／測驗（2026-11-03）

- 已移除編輯器分析階段選單、四階段快捷出題及閱讀頁階段標籤；保留既有資料與獨立案例／AI功能。前端45測試及build通過，輸出wwwroot/portal；本次未啟動或更新5097發布目錄。

## 最新實作：課程獨立與平台權限分離（2026-11-03）

- 已依使用者新規格實作並部署 5097，取消班別表／欄位及所有分班／調班／限班資源 API／介面。學員、小組、資源直接屬於課程；課內組名唯一，每人每課最多一有效組，資源無跨課共用。
- 使用者明確選擇拆課並各自複製內容：原 LEARN-101 保留 ID，名稱「個案分析課程－一三五」，5 位原班學生＋1 位未分班學生；新增 LEARN-101-246「個案分析課程－二四六」，6 位學生。各有 3 測驗／27 題、1 PDF 作業、3 資源與獨立規準／成績來源；原小組與紀錄保留。映射／班別時間存系統稽核，無私人答案複製。
- 平台僅 Administrator／Member，can_create_courses 獨立。原 Professor 轉管理員＋可建課，原 TA／Student 轉一般成員，原課程角色／帳號不變；015 留逐帳號映射稽核。管理員不繞過課程內容權限；一般成員可任課程教師／助教，擁有者可從最小資料的 eligible-accounts 安排成員。
- Cookie 每次查 DB，角色或建課資格變更舊 session 拒絕；can_teach 從有效課程關係更新。學生體驗按目標課程判斷，跨課混合角色的正式學生不落入練習。全域 A2／A5／A6 與課程案例／私密答案授權已拆分；實際學生課程不顯示體驗切換。
- 015／016 已套用，001–016 重跑略過；168 後端（DB 啟用、0 略過）、45 前端、9 項非模型 browser/API 回歸通過，最終 UI 微調後 focused 4 項再通過。QA／state 全清理、5107 停止。5097 PID 40968、p0-publish（下次須重新核對）；未停止 Visual Studio 程序，未改模型設定／推論、未提交推送。
- 以下「待實作／僅規格」保留前一階段交接脈絡，已由本節完成狀態取代。詳見 docs/course-only-spec.md、docs/role-permissions-spec.md 的實作紀錄；存量課程保留，勿將真實教學內容當 QA 清理。

## 最新決策：平台管理權與課程角色分離（2026-11-03，待實作）

- 使用者採用建議：平台僅「管理員／一般成員」，建課資格獨立授予；課程內才有「授課教師／助教／學生」，各課角色獨立。一般成員可由課程擁有者安排任一課程角色，不再受平台教授／助教／學生限制。擁有者仍須保留本課教師資格。
- 管理員不自動取得各課教學內容權；全域帳號／設定與課程案例／私密答案須分開驗證。practice、首頁與學生資料要求須按目標課程情境，不能只替換 Professor 字串。
- 規格 docs/role-permissions-spec.md 優先於舊「所有教授兼任管理者」。本次僅文件，實際程式仍原三類平台角色；遷移前需列帳號管理權／建課資格映射，保留各課關係與提交成績，不修改真實權限。

## 最新決策：取消班別，只用課程（2026-11-03）

- 使用者要求修改規格並評估異動：一課程＝一實際授課群體，課程直接包含學員、小組、資源、評量與成績；取消課程下班別、未分班、調班與班別限定資源。小組改本課有效學生、每人每課至多一組，組名課內唯一。
- 本次僅規格／影響評估，產品仍有分班實作，尚未變更程式／DB／部署。實作主規格 docs/course-only-spec.md；優先於下方及舊 course-sections 的現行需求效力，不可宣稱已移除分班。
- 實作前須盤點多班、限定資源、同名小組與提交關聯；禁止直接清空 section_id 擴大資源權限或呼叫刪班 API 清掉小組。先定映射，新向前 migration 不改已套用 SQL。每個課程獨立，不提供跨課資源分享或共用檔案庫；不自動拆課／搬移作答或成績。

## 評量內容整理（2026-11-02）

- 使用者確認永久刪除「1029隨堂考試」與「測試題型功能」及關聯紀錄，已交易完成；保留主題案例作業與基礎測驗。勿重新匯入這兩個舊測試測驗。
- 新「課後案例分析：作業內容理解測驗」ID 515b76f9-8953-476c-bd3e-a017af0e71ab，draft、10題100分、5單選／3多選／2是非，全客觀自動批改；依現有課後作業四面向／證據要求編寫，來源 docs/examples/homework-comprehension-quiz.json，待教授審核發布。

## 測驗批改呈現（2026-11-02）

- 教師查看提交統一於 TeachingPanel 顯示題目、勾選／誤選／漏選／正解與逐題得分；移除重複純文字列表。申論顯示已存分數／待評，學生公布前保密政策不變。45 前端測試通過；無後端／SQL變更。

## 課程文件名稱（2026-10-30）

- 課程資源上傳可填文件名稱，卡片「編輯文件名稱」可事後修改；留白回原檔名。原始檔名／內容／下載檔名不變，學生只讀，老師／助教維護，practice／封存拒寫。
- 014_resource_titles.sql 已套用；course_resources.display_title 與 title_version，舊檔預設空字串／1。PUT resources/{id}/title 使用 expectedVersion、交易／稽核，過期409；多列編輯保留最初版本與未存文字，離頁提醒。
- 167後端（DB啟用）、43前端、build／publish；最終5097 titles／files Chrome/API共2通過。001–014重跑略過，QA/state清理、5107停止，實際課程／文件／測驗保留。


## 最新：臨床心理學基礎測驗與配分組成（2026-10-30）

- 使用者提供的「臨床心理學案例分析基礎測驗」已加入 LEARN-101 個案分析課程，ID f99b5828-640f-4eb7-bb2a-9b1ae5204d4a，draft 未發布、13 題、滿分100：單選25／多選15／是非20／申論40。無期限、課程權重暫1（與試卷100分分開），需教授審核／發布。這是使用者要保留的教學內容，不是 QA，勿清理。
- 來源 docs/examples/clinical-psychology-basics-quiz.json，保留提供的客觀答案與申論五段評分參考。013_question_grading_guidance.sql 已套用，參考欄僅有效教學模式讀，學生／practice 學生視角回 null。
- 出題與閱讀頁有即時題型配分表，留白均分、餘分與伺服器一致；合計不符阻止 UI 儲存，後端原驗證保留。教師可修改逐題配分，試卷內配分不等於課程成績權重。
- 167 後端（DB 啟用）、43 前端、build／publish，scoring-plan／autograde／practice／assessments 在最終 5097 共4通過。001–013 重跑略過，QA/state 清理、5107停止，真實草稿保留，無模型推論。


## 最新：教師錯誤對照與確認正式成績（2026-10-30）

- 教師在測驗提交檢閱可看實際勾選、誤選、漏選與正解，申論輸入即時暫算總分。測驗提供「確認成績並寫入正式成績簿」，確認後先保存最新申論／評語（若有修改），再透過原發布交易寫正式成績與公開；取消不寫。仍可另存評分草稿。
- 作答、客觀判定與草稿仍存原 DB 供追溯；「正式 DB」對應正式成績簿／已發布成績，不另造 staging DB。純客觀也用同一確認按鈕。練習仍不入正式成績。
- TeachingPanel 載入申論分項須複製物件，避免編輯時污染原 grade 造成誤判未修改而發布舊分；已有單元及真實 API 回歸。練習教學端提供標準答案對照，練習學生視角不傳實值。
- 167 後端（DB 啟用）、40 前端、build／publish，最終 5097 autograde／practice／assessments 共 3 通過；QA 清理、5107 停止，無遷移。


## 最新：客觀題自動批改與申論題人工評分（2026-10-30）

- 目前 /quizzes 測驗的新提交，系統以伺服器標準答案批改是非／單選／多選；多選集合完全一致才得該題滿分，漏選／多選錯項／未答 0 分，順序無關。可設逐題配分，空白分配剩餘滿分，分幣餘數放最後未指定題。
- 提交交易保存 quiz_grading（綁不可改寫提交快照）。純客觀題產生系統成績，grader NULL 不冒用學生／老師；正式待教師「發布自動評分成績」，練習直接可見。混合題型只讓教師填申論題，所有申論填完才存完整總分，客觀分數不能人工改寫。
- 012 已套用，001–012 重跑略過；新增 quiz_grading、grades.essay_scores、允許系統 grader NULL。舊人工成績不追溯重算。正式發布前不給學生正誤／自動分數，發布才同步正式成績簿；練習與正式仍隔離。沒有模型呼叫。
- 167 後端（PostgreSQL 啟用、0 略過）、35 前端、build／publish 成功；最終 5097 的 autograde／practice／assessments Chrome/API 共 3 通過。QA／state 清理、5107 停止。操作與限制 docs/quiz-autograding.md。


## 最新政策：學生體驗可提交與本人練習評分（2026-10-30）

- 使用者明確改為允許本人學生視角提交測驗、PDF 作業與案例，取代先前禁止 practice 提交。原身份與課程資格不變，期限／封存／提交鎖定仍適用。案例僅文字回饋、不計分。
- 課程成績頁：教學視角「我的練習提交與人工評分」只評閱本人練習，儲存後學生視角「我的練習成績」可讀分數／評語／練習加權平均。不能藉此評閱他人的私人練習；正式成績與統計不受影響。
- 011_practice_reports.sql 已套用且 001–011 重跑略過，PDF 增 is_practice 與 namespace 唯一鍵，既有 false 保留。練習測驗沿用 submissions／submission_revisions／grades；PDF 沿用原提交表，practice 不呼叫正式 grade sync。
- 新 PracticeAssessmentsController 與 PracticeRecords 提供本人獨立檢閱／評分／預覽；教授／TA 真實課程 teacher／ta 資格、學生視角只讀不可評分、來源版本衝突、分數範圍檢查。實際授權仍在伺服器。
- 本輪 157 後端（DB 啟用、0 略過）、35 前端、Vite build／publish 通過。focused practice 與正式 assessments Chrome/API 在最終 5097 共 2 通過；無模型推論。QA/state 清理、5107 停止，5097 已更新。細節 docs/practice-assessments.md。


## 最新補充：分班與資源範圍（2026-10-30）

- 同課程依上課時間建立班別，每位學生每課至多一班；小組限定同班且每人至多一個有效小組。教師／助教安排，調班時結束原有效小組關係。舊學生／小組保留未分班。
- 資源可全課共用或限指定班別，上傳後可調整；列表、計數、下載後端均限制班別。未分班／教學練習僅共用；一般教師／助教可管理本課所有資源。010 已套用，001–010 重跑略過。
- 後端 147（DB 啟用、0 略過）、前端 28、最終 5097 browser/API 4 項通過；QA 與 state 已清理、5107 停止。5097 沿用 p0-publish；保留使用者 Visual Studio 程序。無 AI 推論或設定變更。詳見 docs/course-sections.md；評量與成績仍課程層級，未新增班別派題。

## 開始工作

依根目錄 `AGENTS.md`，讀取本檔、`elearning.md` 及 `history.md`，再檢查實際程式碼與 Git 狀態。需要啟動、部署或測試時，先確認現有程序及設定；不可沿用舊的程序識別碼當成有效狀態。

## 最新補充：恢復課程管理（2026-10-29）

- LEARN-101 現行名稱為「個案分析課程」（使用者要求由「測試課」更名）；原課程識別、draft 狀態與所有關聯保留，無須重新建立課程。
- 使用者要求課程管理加回，資源、學員與測驗必須連結課程。主導覽恢復 /courses，課程工作區有資源／講義、學員／小組、測驗與 PDF 作業、成績、案例五頁籤；維持 S／T／A 案例與最新測驗／PDF 定位，不恢復舊式作業 UI。
- 課程內評量建立固定綁該 courseId；測驗、PDF、案例、歷程與成績有回課程連結。既有實際 FK 沿用，無新增遷移。CourseSummary 新計數不含學生不可見草稿；isOwner 後端推導，成員寫入仍限擁有教授。
- .NET 147（DB 已啟用、0 略過）、前端 28、最終 5097 browser/API 3 項通過；手機與跨課隔離已檢視，無模型推論。QA／state 已清理、5107 停止；詳見 docs/course-hub.md。此政策取代下方「原課程導覽隱藏」的現行效力。

## 目前主站：S／T／A 案例學習＋測驗／PDF 作業（2026-10-29，優先於下方舊導覽）

- 使用者確認新 S1–S3a、T1–T4、A1–A6 取代原導覽；舊資料保留。所有教授兼任管理者，學生先填姓名／學號（系級選填）。教師仍只讀所屬課程正式提交，私人草稿不開放。
- 新 S3 案例三區四階段提供文字 AI 回饋，不提供 AI 分數；008 已套用，案例／答案／模板版本分開。原作業 ai-feedback API 繼續 403，舊 AI 入口不在新導覽。不可直接復原舊 score 契約當新案例回饋。
- 使用者再澄清原命題作答定位為線上／課後測驗；作業是 PDF 報告、教師閱讀後給分。新增 /assessments、/quizzes/{id}、/reports/{id}、/assessment-grades，009 已套用；測驗人工成績發布、PDF 人工分數儲存同交易連動 gradebook，來源只計一次，線下仍可手動登分。
- 來源 grade item 有實際 FK，成績管理只調權重、不手動改連動分數；不要破壞發布前保密、PDF 所有權或將 AI 回饋轉成正式成績。
- 現行完整規格見 docs/case-studio.md、docs/learning-assessments.md。舊五入口／獨立考試文字只保留歷史現況，不代表目前主導覽。

## 最新課程結構與線下評分（2026-10-29）

- 課程改五入口：教學資源／講義、同學（含小組）、作業管理（含提問）、考試管理（含排行榜）、評分管理。預設 resources，query section 可直接連結。原作業／規準歸入 assignments，不可因預設頁不同移除原流程。
- 使用者確認線下評分需要項目權重：各項得分先換算百分制後按相對權重平均，空白不計入、0 分計入；全班平均按有得分學生的個人平均取平均。只登錄線下得分，不自動合併作業／線上考試。
- 007_offline_gradebook.sql 新增項目與得分表；score 可空代表清除，必須保留版本避免 stale resurrection。學生只讀本人得分及匿名統計；staff practice 不寫、不讀真學生分數。詳見 docs/gradebook.md。

## 本次新需求（2026-10-29，優先於下方 AI／舊範圍記載）

- 使用者確認：作業全部停用 AI（含私人形成性回饋），獨立 AI 入口保留；申論 textarea 必須保留，新增是非／單選／多選及私密標準答案。
- 新增課程檔案上傳／下載計數、教師／助教分配小組、學生私人提問與教學端公開控制、獨立考試（四題型／作答／人工成績／排名或學號排序），帳戶頁登出入口及教授學號維護。
- 006_course_tools.sql 已套用；既有題目預設 essay，舊 AI 資料保留。FormativeFeedbackController 一律 403；不要因下方舊交接重新開啟。模型 profile／提示未修改。
- 完整邊界、最新測試與預覽狀態見 docs/course-tools.md；本次新增檔案多為未追蹤，Git 不可重設或任意清理。

## 目前方向

- 2026-10-29 已整理當日進度快照：`docs/progress-2026-10-29.md`，區分功能完成、合成測試、教授驗收與模型可靠度；資料庫查詢確認示範仍為 draft、尚無正式提交／留存 AI 評閱。後續操作仍須重新核對即時狀態。

- P0 的帳號、權限、課程名單、個人作業建立／發布、學生草稿／提交及教師檢閱已完成主要流程。
- 使用者於 2026-10-27 明確授權 P1：提交修訂／比較、人工評分草稿／發布、基本規準管理；優先於原先 P0 範圍凍結。11/15 展示含本次 P1，12/15 AI 整合，12/31 第一階段交付。教授仍需確認教學設計。
- 人工評分已接入指定提交版本；獨立 AI 入口與學生私人形成性回饋分開。不得宣稱已有正式作業 AI 成績或可靠模型校準。詳見 `docs/p1-teaching.md`。
- 待討論功能詳見 `elearning.md` 第十節；沒有新授權時，不自行擴充 P1／P2。

## 架構與重要位置

- Vue 3／Vue Router／Vite：`ClientApp/`；API：`Controllers/`；核心邏輯：`Services/`。
- ASP.NET Core 8 同源提供 SPA 與 JSON API；資料庫採 PostgreSQL／Npgsql。
- `Database/001_elearning.sql`、`002_portal_auth.sql` 及 `003_teaching_workflows.sql` 已在指定資料庫套用。啟動網站不會自動遷移。
- 帳號本地設定在被 Git 忽略的 `appsettings.Local.json`，環境變數可覆寫。讀取時避免將內容印到日誌。
- 舊 `Views/` 與原始 JS／CSS 留作參考；目前使用中的前端為 Vue。
- 本機預覽網址為 `http://127.0.0.1:5097`；最後部署內容位於 `.codex-build/p0-publish`。網址與資料夾存在不代表程序存活。

## 教師／助教學生體驗（2026-10-29）

- 使用者已選擇完整可儲存練習模式。頁首可切換，保留本人與原角色，不冒用學生。詳見 docs/student-experience.md。
- 005 已套用；submissions.is_practice 分開正式／練習，所有 own、AI、snapshot 查詢須保留 namespace。正式名單、數量、Review 及人工評分／發布必須排除 practice，不能只靠 Vue 隱藏。
- X-Portal-View: student 只讓已登入 Professor／TeachingAssistant 宣告練習視角；仍須有效課程資格，不能藉此讀真學生草稿。LearnerExperience 必須 request scoped。
- 一般對話／摘要／設定另外以 practice scope 隔離；OllamaRequestInspection 在 practice 不擷取或回傳。正常教學模式既有行為保留。
- 模式偏好存在每帳號的 localStorage，但權限只以後端驗證為準；auth API 不帶 view header，避免登入／登出角色切換殘留。
- 測試環境缺少 npm；可用既有 Node 直接執行 vitest、vite 及 Playwright CLI。不要為此重裝使用者環境。

## 學生形成性回饋（2026-10-28）

- 使用者已明確授權在作業頁提供私人 AI 練習，詳見 docs/formative-feedback.md；不是正式 AI 成績。
- 004 SQL 遷移已套用；使用 evaluation_runs，rubric 可空，不建立假外鍵。只對本人精確已存答案版本評閱，未存內容需先儲存。
- 直接用 AssessAsync／Schema／嚴格 parser，不得加入 chat memory 或先前作答。各 profile 不變。
- 形成性路由限正式 Student 或已驗證的 staff practice，並核對有效課程與本人 namespace；教授／助教不能讀正式學生私人回饋。學生回應不得包含 requestInspection、raw response、thinking。
- 模型呼叫期間不持有作業列鎖，不阻擋儲存／提交；新答案使結果過期。失敗不更動草稿或人工成績。
- 目前只有首次提交前的私人草稿；不要悄悄改動 P1 提交後修訂政策。未設定階段題目不做預設推論；整份人工 rubric 不換算為單題 AI 分數。
- QA 清理需先刪 evaluation_runs。PostgreSQL .NET 回歸以 FORMATIVE_TEST_CONNECTION 明確啟用，未提供時有 7 項略過；不可把略過當通過。

## 模型現況與限制

- AI 入口提供教授／助教的「系統詞與實際請求（唯讀）」；展開後亦顯示該請求對應的 Ollama 原始 JSON 回應與 HTTP 狀態碼，但後端會遞迴移除所有 `thinking` 欄位。只顯示自己當次操作，學生在後端即排除。`OllamaRequestInspection` 必須維持 scoped，不得改成全域快取或記錄真實作答至檔案。模型或解析失敗時亦回傳當次檢視供教學討論；目前不編輯 prompt。

- 切換方式、相容性與合成封包：`docs/ollama/README.md`。以 `Ollama__Profile=breeze|llama3|qwen` 啟動；設定各自保存於 `Configuration/Ollama/`，目前預設仍為 Breeze。
- `docs/ollama/payloads/` 保存 48 份離線合成範本；不是各模型即時驗收的成功證據。評估工具支援 `--profile` 及 `--export-payloads`。

- 目前設定：`http://192.168.1.195:11434`，`ycchen/breeze-7b-instruct-v1_0`；以 `appsettings.json` 與環境覆寫後的實值為準。
- 提示版本 `rubric-evidence-v3`：評閱合併單一 system 訊息、可讀中文作答 JSON、依案例有無約束 Schema。
- 保持嚴格回應驗證；不得為了畫面成功而硬編碼分數、植入測試答案或放寬證據原則。
- Breeze 格式相容性已有少量成功測試，但獨立強弱作答的分數不可靠。全部四階段及比較模式的 Breeze 即時驗收仍未完成。
- 最新測試範圍與限制見 `history.md`；不可把歷史測試通過數當成當前版本已重跑的結果。

## P1 交接重點

- 2026-10-28 使用者要求建立的虛構案例作業已存入 LEARN-101（測試課），作業 ID `c7b059d4-40bb-4a9d-861f-48ee866a4255`，未發布、四題／四項等權重規準、滿分 100。來源 `docs/examples/clinical-case-assignment.json`。這是使用者要求保存的真實平台作業，不屬於可清理的 QA fixture；勿因課程名稱為測試課而刪除。

- 沿用 Npgsql SQL 遷移，未另建 EF Core；理由與任務工具要求差異見 P1 文件。
- 舊作業 `allow_revisions=false`；開啟修訂後，已提交作答必須直接提交新版本，不以草稿覆蓋。
- `submission_revisions` 是整份提交快照；`grades` 的人工評分綁定快照，未發布草稿不給學生讀取，新版不繼承舊成績。
- 同課程規準可重用；已關聯規準的修改會新增版本，發布後作業關聯鎖定。
- 合成示範：`tests/PortalSmoke --demo`（用 dotnet 執行專案），狀態保存於忽略的 `.codex-build/portal-smoke.json`；清理須用新版 helper 處理 grades／snapshots／rubrics。

## 修改時必須保留

- 平台角色與課程角色分離；課程授權以實際有效成員為準。
- 學生草稿隱私、舊作業預設提交鎖定；允許修訂的新作業保留不可改寫快照、版本衝突保護及伺服器截止判定。
- AI 各帳號／階段隔離；缺少原始案例時不得假裝核實。
- Cookie、CSRF、密碼雜湊與正式環境 HTTPS 要求。
- 合成評估資料不進入正式提示；人工評分一致性與軟體測試分開報告。
- 儲存庫有大量既有未提交變更，禁止重設或清除他人工作。未經要求不提交或推送 Git。

## 建置、測試與部署注意事項

- 後端：`dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj`。
- 前端：在 `ClientApp` 目錄執行 `npm test`、`npm run build`；不要從根目錄假設存在 package.json。
- 瀏覽器測試使用真實 API／資料庫，須先依 `docs/vue-api-migration.md` 準備合成 QA 帳號。清理只能處理已核對的 QA 資料，不可刪除真實課程或帳號。
- `dotnet publish` 預設建置 Vue；`SkipFrontendBuild=true` 僅適用於資源已建置的情況。
- 執行中的 Windows 程序可能鎖定組件；使用隔離輸出目錄，再確認程序後部署。預覽使用發布資料夾，修改原始碼不會自動更新預覽。
- 避免更動內容根目錄造成驗證 Cookie 失效；重新啟動亦會清除程序內 AI 對話。
- API 採自訂 `ApiAntiforgeryFilter`。不可直接改回依賴 MVC ViewFeatures 的防偽 Filter，先前曾因此產生 500。
- 不同建置不要同時寫入同一輸出目錄；`npm ci` 時不要並行執行依賴同一 node_modules 的測試。

## 完成工作後

將日期、需求、變更、理由、實際驗證結果與未完成事項寫入 `history.md`。更新本檔的交接狀態及受影響的產品規格。只紀錄實際執行的驗證，不保留憑證、個資、程序密碼或真實案例全文。

## 最近一次驗證（2026-10-27）

原始模型回應檢視變更後，後端 99、前端 10 項通過，Vue production build 成功；本輪未重跑瀏覽器／真實 API。先前 P1 最終發布版瀏覽器／真實 API 6 項結果仍只代表當時版本。P1 預覽曾在 5097 啟動，不得假設目前程序仍存活；合成 QA／demo 資料及狀態檔已清理。完整 P1 命令、權限驗證、NuGet 警告及限制見 docs/p1-teaching.md。需示範時重新執行 helper --demo，不依賴舊 QA 帳密。

## 最新驗證與部署（2026-10-28）

學生形成性回饋交付：後端 108 項通過（含實際啟用的 7 項 PostgreSQL 測試）、前端 18 項、focused synthetic Ollama 瀏覽器／API 2 項，以及 P0／P1／入口回歸 6 項通過。004 遷移已套用並驗證重跑略過。合成 QA、DB fixture 及狀態檔已清理；5107／11535 測試程序停止，5097 已更新，仍使用 Breeze。未執行真實模型測試；正式成績未接 AI。細節見 docs/formative-feedback.md，舊驗證條目保留歷史範圍。

## 最新交付（2026-10-29）

教師／助教完整學生體驗已部署至 5097，005 已套用。後端 115、前端 21、focused 3 及既有 browser/API 6 項通過，手機深色另驗證。測試全用合成模型與合成資料，QA 已清理；5107／11535 停止。現有真實示範作業仍未發布。操作與 namespace 規則見 docs/student-experience.md；不得把 localStorage 偏好當作授權或移除正式查詢的 NOT is_practice 限制。

## 學生視角提交限制（2026-10-29，取代先前可練習提交政策）

依使用者修正，老師／助教學生視角可儲存私人草稿與取得 AI 回饋，但不能提交；按鈕可見停用，後端 Practice + Submit 拒絕。真正學生可正常提交。既有練習快照不改寫；沒有新增遷移。本次 115 後端、22 前端、focused browser/API 2 項通過，網站更新至 5097。

## 最新交付：課程工具與獨立考試（2026-10-29）

006 已套用並驗證重跑略過；5097 已更新，原發布內容根目錄保留。125 後端（含 DB、0 略過）、25 前端、最終發布版 focused 2 項通過；原作業／人工評分 browser/API 4 項另在 5107 通過。QA 合成資料及 state 已清理，5107 已停止。細節與精確界線見 docs/course-tools.md；舊 AI／可提交練習敘述只保留時間脈絡，現行以本次停用政策為準。未提交／推送 Git。

## 最新交付：課程五入口與線下加權評分（2026-10-29）

007 已套用且重跑略過；5097 已更新並保留原內容根目錄。135 後端（含啟用 DB、0 略過）、25 前端、最終 5097 focused 2 項通過；原作業／人工評閱 browser/API 4 項在 5107 通過。已檢視教師桌面與學生手機。線下分數欄可空以清除，但須保留版本；前端每筆草稿保留開始編輯時的版本，不可在刷新其他列後重綁新版。QA 與 state 已清理、5107 停止，無模型推論。docs/gradebook.md 有公式、權限及限制；舊同頁堆疊課程工具呈現已被五入口取代。未提交／推送 Git。

## 最新驗證／交付（2026-10-29）

001–009 已套用並重跑略過。後端 147（PostgreSQL 已啟用，0 略過）、前端 28、現行新站台 browser/API 4 項通過；最終 5097 focused 2 項通過（測驗／PDF／成績連動與導覽）。S3 AI 僅合成 fixture，未做真實模型品質驗收；三個 profile 序列化驗證保留相容性。QA／state 已清理，5107／11535 停止，5097 更新且保留 p0-publish 內容根目錄及 Breeze 設定。未提交／推送 Git。

## 帳號管理更新（2026-10-30）

- 帳號清單學號唯讀，空值顯示「(空)」；新增教授限定平台角色編輯，課程角色不自動同步。使用者自行設定目標帳號，本輪未更動真實帳號。
- PUT /api/accounts/{id}/role 使用 expectedRole、交易鎖、稽核；禁止自行降權及移除最後有效教授。既有 Cookie 角色驗證使變更後須重新登入。
- 後端 148（DB 啟用、0 略過）、前端 30、Vite build／publish 通過。5097 更新；本輪未執行瀏覽器或模型測試，無 SQL 遷移。

## 假學生名單匯入（2026-10-30）

- 依使用者要求匯入 11 位持續保留的假學生，學號 E2023001–E2023011、平台 Student、測試 Email 現為學號小寫加 @nsysu.edu.tw（僅測試識別，不代表真實信箱）。不是測試清理用 QA，勿自動刪除。未分配課程／班別／小組。
- 本機初始登入資料在 Git 忽略目錄 .codex-build/dummy-students-2026-10-30.secrets.json；勿複製密碼到文件。交易匯入、逐筆比對既有學號／Email，重跑跳過且不重設密碼。

## 圖片與 PDF 預覽（2026-10-30）

- 課程資源 PNG／JPEG／GIF／WebP 顯示縮圖並可放大；PDF 與已繳交 PDF 報告提供按需頁內預覽及下載備援。GET preview 沿用課程／班別／報告本人授權與 practice 限制，不增加資源下載次數。
- 僅允許副檔名與檔頭一致的格式，HTML／SVG 不預覽；不建立公開檔案網址，使用 API fetch 及元件生命週期 blob URL。瀏覽器原生 PDF 已在 Chrome 顯示頁面內容，不新增轉檔／遷移。
- 157 後端（DB 啟用、0 略過）、33 前端、build／publish，focused Chrome/API 1 項在 5107 與最終 5097 各通過；合成 QA／state 清理、5107 停止。限制見 docs/file-previews.md。

## 成員角色操作外觀（2026-10-30）

成員清單角色選單縮為 110px，與藍色主要「儲存角色」按鈕並排；僅前端樣式變更，Vite build 通過並更新 5097 靜態資源，不重啟服務。

## 學生名單直接調班（2026-10-30）

- CourseTools 學生分班名單逐列選班別／未分班，藍色儲存調班；班別表單只改名稱與時間，省略 Members 不再覆寫名單。舊 API 明確 Members 仍相容。
- PUT classmates/{student}/section 帶 expectedSectionId 核對原班別，課程鎖與交易、同課有效學生／班別檢查、稽核；調班結束原組關係，其他學生不改。課程教師／助教限定、封存／practice 禁止。
- 157 後端（DB 啟用）、33 前端、build／publish 通過，focused sections browser/API 於 5107 及最終 5097 各 1 通過；QA 清理、5107 停止，無遷移。

## 刪除班別（2026-10-30）

- 班別卡片垃圾桶，先 GET deletion 範圍，再確認學生改未分班／刪班內小組與其關係紀錄；DELETE 帶 snapshot hash，內容變更 409。保留帳號／課程資格／作答／成績。
- 專用資源未移轉或小組有作業紀錄時禁止刪除；不自動將資源變共用。教師／助教限定，封存與 practice 禁止。課程／班別鎖與交易及稽核，無遷移。
- 157 後端（DB 啟用）、33 前端、build／publish 通過；focused sections Chrome/API 在 5107／最終 5097 各 1 通過，QA 清理、5107 停止。

## 合成帳號登入交接更新（2026-10-30）

- 使用者要求簡化既有匯入測試帳號登入。已核對匯入稽核／姓名／學號／Email，重設該批 11 個合成帳號，保留目前角色（含已被使用者改為助教者）。最新憑證僅在 Git 忽略的原交接檔，勿複製至文件或恢復成舊隨機值。
- 11 個皆以 PortalAccountStore.AuthenticateAsync 實際驗證成功；其他帳號、角色、課程與作答不改。無 schema／產品密碼規則變更，無須重啟。

## 課程資源多欄流式排列（2026-10-30）

- CourseTools 資源改自動換行卡片網格，寬螢幕多欄、600px以下單欄；圖片／PDF 展開預覽時卡片跨整列，保留原授權／班別範圍與下載。FilePreview 僅新增 expanded-change 事件，未改模型／資料。
- 前端43、Vite build通過；最終5097 studio-files Chrome/API 1通過（桌面多欄、預覽跨列、手機單欄／無水平溢位）。靜態檔更新、未重啟。合成QA/state已清理，無遷移或後端測試重跑。

## PDF 收合文件圖示（2026-10-30）

FilePreview 的 PDF 未展開時顯示既有 Lucide FileText 文件圖示與 PDF 標籤，點擊仍開原預覽，收合恢復圖示。43前端／Vite build通過，5097靜態資源更新，未重啟、未改後端／DB。

5180 已依原示範 schema 重新啟動並重驗；未來先檢查埠號。共用 public 仍為 025。

帳號操作已集中右上角選單。5180 已用教師、學生從登入表單實測進入課程；前次重啟嘗試因埠已佔用失敗。

7173 共用開發資料庫 public 已依授權更新至 028，取代先前 025 紀錄。7173 再啟動後師生登入及課程清單／明細 API 通過；下次請重查程序狀態。

7173 示範資料現況（2026-11-11）：既有六門課程皆已開放，各有個人與小組作業、測驗和示範分組。

7173 的 12 份 PDF 作業已補齊 72 份模擬繳交。Codex 評分與評語存為教師草稿，個人作業成績尚未發布到學生與正式成績簿。
