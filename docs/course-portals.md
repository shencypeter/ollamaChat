# 11 月 10 日師生入口與示範指南

## 2026-11-10 課程小組 PDF 更新

既有 PDF 作業的建立、批改和成績頁支援小組模式。操作與示範資料詳見 [課程分組與 PDF 作業](group-pdf.md)。


## 立即示範

本機示範站位於 http://127.0.0.1:5180 。兩位教授帳號為 `faculty1`、`faculty2`，助教為 `ta`，學生為 `student01` 至 `student10`。帳號均加上 `@portal-demo.example.invalid`。同一批合成帳號使用建立示範時指定的密碼。目前這台 Windows 電腦由建立站台的使用者在專案根目錄執行下列命令即可複製密碼。

```powershell
(Import-Clixml .codex-build/course-portal-delivery/demo-login.xml).GetNetworkCredential().Password | Set-Clipboard
```

1. 用 faculty1 進入「研究方法（示範）」並開啟「批改與評分 → 單元測驗」。全班十人，前三位已交卷。第一位已有公布成績，第二位有評分草稿，第三位待人工批改。
2. 開啟第三位的考卷，檢視選擇題與申論原文，填寫申論分數與回饋，先儲存草稿，再修改並確認發布。到課程總成績可查看各學生的項目分數，設定調分並另行發布總成績。
3. 點頁首「學生視角（預覽）」檢視同一門課的學生畫面。使用獨立瀏覽器工作階段登入 student10，可實際讀教材、開始定時測驗、繳交 PDF 及查閱本人已發布成績。
4. 教授二負責第三門課。助教支援三門課，三門課各有不同名單；示範資料與測試使用的 schema 分開。

## 站台地圖與權限

平台首頁是「我的課程」，右上角姓名選單提供個人資料、更改密碼與登出；平台管理員另有管理入口。桌面與手機共用側欄。選課後網址為 `/courses/:courseId`，各功能由 section 指定；教材、測驗及報告明細另有含課程 ID 的網址。

| 課程功能 | 教師 | 助教 | 修課學生 |
|---|---|---|---|
| 課程首頁、教材、測驗與作業、課程問答 | 教學檢視 | 依本課身分 | 已開放課程及可見內容 |
| 學員管理／分組 | 擁有者開課、配置成員及助教權限；教師可分組 | 查看名單、分組 | 無管理入口 |
| 教材與評量管理 | 建立、設定與發布 | 擁有者可分別限制教材編輯與評量設定 | 讀教材、下載、正式作答 |
| 批改與評分 | 全班名單與逐人考卷、草稿及發布 | 登分可按課限制 | 本人已發布結果 |
| 課程總成績／我的成績 | 加權試算、全班及個人調分、公布總分 | 可檢視；項目給分依權限 | 本人已發布分數及總分 |
| 學習統計／我的學習歷程 | 有效學員、提交、教材參與與逐教材明細 | 統計可按課限制 | 本課正式活動、期限與結果 |
| 討論 | 發文、公開回覆、鎖定 | 同課教學權限 | 發文、回覆、投票；新討論可附一份 5 MB 內 PDF |
| 學生視角預覽 | 任教且開放的課程可切換 | 同左 | 使用正式學生身分 |

預覽不建立考試開始、交卷、草稿或計時紀錄。個別作業的私人提問沿用原權限。案例與觀摩保留既有資料與程式，退出主要導覽。YouTube 分頁隱藏時要求播放器暫停，回到前景由使用者播放。站外可轉傳影片連結，平台只驗證本站教材權限。

## 結構與遷移

Vue Router 與 App.vue 管理頁面及側欄，CourseDetailView 組合教材、名單、問答、統計、評量及成績元件。AssignmentsController 與 AssignmentWorkflow 沿用提交快照、自動批改與人工發布。LearningAssessmentsController 處理 PDF，GradebookController 管理項目和總成績。ASP.NET Core Cookie、防偽權杖、有效課程成員及 PostgreSQL/Npgsql 是授權與儲存基礎。

- 026 新增限時／大題、評量通知、公布分快照、課程調分、公開回覆與討論鎖定。既有已對學生可見的 PDF 及項目分數保留。新測驗明確開始，伺服器檢查開放、截止及限時，多選仍採全對計分。
- 027 新增教材通知、組長、討論投票及附件、逐課助教限制。既有教材預設不補發通知。
- 028 為標準學期代碼產生學年度、學期欄位，舊自訂代碼原樣保留；新增學系與年級欄位。學號、學系、年級與修課狀態由機構維護，學生檔案唯讀。

## 在其他開發環境啟動

準備 .NET 8、Node、PostgreSQL 與專用開發資料庫。在專案根目錄的 PowerShell 7 設定環境變數，再建置、遷移及手動載入示範資料。

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Portal = Read-Host '專用開發資料庫連線字串' -MaskInput
$env:PORTAL_DEMO_PASSWORD = Read-Host '示範密碼，至少 12 字元' -MaskInput
Push-Location ClientApp
npm ci
npm run build
Pop-Location
dotnet run --no-launch-profile -- --migrate
dotnet run --project tests/PortalSmoke -- . --course-demo
Remove-Item Env:PORTAL_DEMO_PASSWORD
dotnet run --no-launch-profile -- --urls http://127.0.0.1:5180
```

種子一次交易建立固定識別的資料，以交易鎖防止並行重入，完成標記防止重跑覆寫。測試 `studio-course-portals.spec.js` 會建立測試資料，須另用可拋棄的示範資料庫，設定 PORTAL_TEST_URL、PORTAL_DEMO_PASSWORD 與 PORTAL_TEST_DISPOSABLE=1。

## 邊界與待確認事項

目前教材加入課程即對有效學員可見。成績單以已發布項目計算可公布總分，全班與個別調分可使總分超過 100。舊測驗缺開始事件時，耗時留白。大題採有序題目的標題，討論附件限新主題的單份 PDF。站內徽章依發布者選擇更新。校務同步尚待校方提供識別鍵、欄位來源、轉退選與更正規則。作業專屬分組須先決定提交歸屬、變更成員與成績分攤。
