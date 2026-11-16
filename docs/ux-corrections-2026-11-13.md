# CSMU UI／UX 修正交付（2026-11-13）

> 2026-11-16 使用者已確認當學期改依系統日期。下列「待校方指定／null顯示全部」是11/13交付當時狀態，已由本次規則取代：null或空白自動取台灣日期、轉民國學年度，預設8月1日上學期、2月1日下學期，月份可設定；明確值仍可覆寫，暑修手動查詢。僅預設篩選過往學期，沒有刪除／自動封存或改成績，歷年課仍可查詢。若只有舊課，空狀態會說明篩選無結果。實作與驗證續記於.ai/history.md的11/16紀錄。

## 現況盤點與完成內容

| 項目 | 盤點結果 | 本次結果／入口 |
| --- | --- | --- |
| 學生公開狀態 | 課程、作業與考試混有管理標籤 | 學生課程清單、詳情、檔案作業、考試隱藏管理狀態；成績發布狀態保留 |
| 學生課程進度 | 繳交比例誤稱學習進度 | 移除比例條與課程卡重複待辦；保留首頁待辦、本課學習紀錄與歷程查詢 |
| 單元活動統計 | 類型數量再加總一次 | 保留教材／評量分類數，移除重複活動總數 |
| 原始附件 | 已有上傳、預覽、下載 | 教材標題與安全原始檔名分列，格式圖示沿用；Office等不支援格式明示下載，不產生空白預覽 |
| 計時 | PDF／圖片仍經共用計時服務 | 共用 MediaType 僅接受 YouTube、簽名正確的MP4／WebM；靜態session拒絕415；前後台不顯示靜態計時 |
| 分享 | 已有具體教材網址與原生分享／複製 | 保留並補登入next返回與本機路徑白名單，禁止外站／API／登入循環；所有教材存取仍須課程授權 |
| 學期 | 有term_code，無可靠當學期 | 新增AcademicCalendar設定與授權讀取API；首頁、我的修課／授課加學年度／上／下／暑修篩選，待辦／訊息／行程同步 |
| 小組編輯 | 重建所有成員；更換組長有唯一限制順序風險 | 先清舊組長再指定、只改實際增減成員；可取消組長，未分組名單排除其他組；解散確認、錯誤與更新保留 |
| 作業命名 | PDF作業命名誤導 | 建立／列表／繳交／評閱改檔案繳交作業，明示目前僅PDF、20MB；內部識別與API相容 |
| 品牌 | NSYSU識別錯誤 | Vue、MVC、登入、瀏覽器標題、README與現行規格改中山醫學大學／CSMU Learn；未下載Logo |
| 學生Email | 既有學生非校域登入 | 19個明確純學生登入原地改csmu.edu.tw，衝突檢查、交易稽核、可重跑，新示範學生採校域 |

Checkbox／radio已存在的全站對齊修改完整保留並回歸桌面1440與手機390；未新增框架。

## 設計決策與相容性

- 正式系統名採使用者本次指定「中山醫學大學個案分析 AI 助教系統」，品牌短名CSMU Learn。舊規格歷史決策保留日期脈絡，現行規則以本文件為準；確定性種子的內部hash前綴不改，避免重建舊ID。
- `AcademicCalendar:CurrentTerm`（可用環境變數`AcademicCalendar__CurrentTerm`）接受`115-1`／`115-2`／`115-S`這類校內學年，不從西元日期推算。預設null，畫面明示未設定並可選所有歷年資料；目前仍待校方指定當學期。設定後需重啟。
- 舊靜態測驗門檻保留原秒數與已達標證據，未達標以正式`resource_views`點閱確認；不把靜態停留時間算進影音，影片不能用點閱替代時間。原測驗開始快照、截止、封存及練習隔離不變。既有草稿靜態門檻只能原樣保留或取消。
- 舊靜態教材歷程仍可查原事件與歷史值，API新增相容欄位`isTimedMaterial`，使用相同簽名判斷；畫面不將該歷史值顯示成觀看秒數。原資料不刪除，影片歷程照常顯示時間。
- 可暫時取消組長以方便編輯；發布小組作業仍必須符合既有指定有效組長規則。舊提交成員快照、組長歷史事件與成績不搬移。
- 不新增Migration；001–036仍是目前完整schema。當學期用設定，舊靜態確認沿用034點閱表，學生更名是一次性可重入維護，不改表結構。
- Email維護僅處理Member、無建立課程權限且具學號／學生修課關係的登入帳號，排除教師／TA／課程擁有者。保留local-part；先檢查學生間及全站衝突，有衝突整筆拒絕。更新app_users.email及portal_credentials.normalized_email，保留密碼雜湊／ID／狀態／角色指紋，逐筆audit。這是平台登入名稱，不會建立校方信箱。

## 主要修改檔案

- `ClientApp/src/views/{CoursesView,OverviewView,CourseDetailView,LearningHistoryView}.vue`：學期、學生呈現與歷程。
- `ClientApp/src/components/{CourseTools,CourseUnits,FilePreview,LearningRequirementsEditor,PortalActivity,ReportModeFields}.vue`與`LearningResourceView.vue`：分組、教材、門檻、活動與作業文案。
- `ClientApp/src/{App.vue,main.js}`、`views/LoginView.vue`、`utils/{returnPath,academicCalendar,branding,materialFormat}.js`：品牌、登入返回、學期與檔名。
- `Controllers/{AcademicCalendar,PortalFeed,LearningHistory,CourseTools}Controller.cs`、`Options/AcademicCalendarOptions.cs`：授權學期API、跨課查詢、小組與安全檔名。
- `Services/{LearningTime,QuizLearningStart,ResourceFileNamePolicy,StudentEmailPolicy}.cs`與`Models/LearningModels.cs`：影片判斷、舊門檻、檔名與Email政策。
- `tests/PortalSmoke/{CsmuStudentEmails,CsmuStudentEmailVerification,CourseDemo,DemoClassmateAccounts}.cs`：學生網域preview／apply／audit、隔離檢查與未來種子。
- 對應Vue、後端、Playwright測試；README、學習歷程規格與`.ai`交接文件。其餘命名替換只改使用者可見文字，不改API路由／資料識別。

## 驗證結果

- Vue：54檔187項通過；Vite production build通過。
- .NET：253通過、35個需外部DB的條件式測試略過。無獨立TypeScript／vue-tsc設定（目前JavaScript專案）；Vue編譯由Vite驗證，未宣稱額外型別檢查。
- 另以授權`group_test_*`隔離schema完整套用001–036：30項資料庫整合通過（LearningWorkflow、GroupManagement、AcademicCalendar、PortalFeed），0略過；schema完成即刪除。涵蓋新舊靜態門檻／影片時間、正式與練習隔離、跨課／師生／TA權限、換組長／保留加入時間／解散、學期篩選與訊息發布。
- 學生Email隔離檢查：衝突全交易拒絕、preview不寫入、舊密碼雜湊可驗證、教師排除及重跑0更新通過。public實際19更新，audit0待改，再跑0更新。
- Chrome 9流程通過：師生首頁／成績、桌面／手機checkbox與radio、矩陣成績、小組唯讀未交／評閱、紙本全班發布、教材分享登入返回／靜態PPTX檔名／無計時、當學期與暑修歷史篩選。
- 標準Visual Studio MSBuild成功（包含Vue建置），既有7173已更新為正常bin/Debug的PID31956。實際/courses回200、CSMU瀏覽器標題、新bundle及academic-calendar／term API確認；5097／5107／5173／5180無監聽。之後須重新檢查程序，不假設PID永久有效。
- 未重跑全套歷史E2E；其中早期教材／學習歷程腳本仍期待已移除的tag或靜態計時，須另行維護。未實測外站YouTube實際播放或校務信箱服務；影片計時以元件與真實DB流程驗證。

## Demo驗收

1. 在7173登入學生（Email改校域，原密碼），檢查CSMU品牌及我的修課無管理標籤／進度條。
2. 用學年度／學期選歷史及暑修，再回目前選擇；當學期未設定會明示，不會遺失舊課程。
3. 開課程單元：檢查分類數量、檔案繳交作業名稱與PDF限制。開PDF／圖片／Office教材，檢查原始檔名、預覽或下載且無計時；YouTube保留觀看資訊。
4. 複製具體教材連結，登出再開、登入後回同一教材。分享不賦予其他課程存取權。
5. 教師進分組：改名、更換／取消組長、增減組員，不解散；確認其他組成員不可重複加入。重新載入確認持久化，舊繳交快照／成績仍在。

## 尚待確認與風險

- 業務確認：校方當學期值；混合教師／學生身分是否也改登入網域；是否保留舊Email別名（目前無別名，請使用新校域）。
- 後端／資料庫額外開發：本次無必要待辦；擴充非PDF作業格式、校務行事曆同步不在範圍。
- 下一階段：歷史E2E腳本維護、正式校務信箱／SSO整合與實際YouTube跨瀏覽器驗收。
- 風險：學生登入名稱有實際變更，舊Email不能再登入；密碼不變。校域名稱不代表已配置郵件投遞。靜態舊門檻由時間改正式點閱，是明示的相容假設；不刪歷史紀錄、不重新計分、不改發布／授權邊界。無新AI／個案模組；未提交／推送Git。
