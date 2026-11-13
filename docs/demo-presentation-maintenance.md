# 展示評語與檔名整理

Development-only 維護入口位於 `tests/PortalSmoke`：

```powershell
dotnet run --project tests/PortalSmoke -- <project-root> --normalize-demo-feedback-preview
dotnet run --project tests/PortalSmoke -- <project-root> --normalize-demo-feedback
dotnet run --project tests/PortalSmoke -- <project-root> --normalize-demo-feedback-audit
```

須設定 Development 與 `ConnectionStrings__Portal`，連線資訊不寫入文件或命令紀錄。執行時如站台鎖住一般輸出，使用隔離 OutDir 編譯並從該 DLL 執行，不另開服務。

只處理可由既有種子事件確認的紀錄：線上評分須連至具有 `demo.submitted_assignment.20261112` 的評量；PDF 及其檔案歷程、互評、同分意願須連至含 `codexDemo=true` 的評分事件。相同文字若沒有這些來源證據，不修改。

規則僅移除已知固定文案：`Codex 示範批改：`、展示草稿提醒，以及互評／意願開頭的展示標籤；保留實際回饋內容。僅將確定性的 `codex-demo-submission.pdf` 改成 `作業報告.pdf`，不泛改含有 Codex 的正常教材或檔名。未發布欄位的 NULL 保持 NULL。

修改在同一交易進行，以既有 seed advisory lock 串行化，報告版本遞增。操作前後對其餘欄位做完整雜湊比對：分數、發布快照與時間、作者／評分者、PDF bytes 不得改變。逐筆新增 `demo.presentation_text.normalized` 稽核，記錄修改欄位與前後雜湊，不刪除或改寫原種子／評分事件；不是新增一筆真人教師批改。

不可變 `submission_revisions`、答案版本、既有學生作答與 PDF 內容不在整理範圍，原歷史中的展示註記仍存在。未來線上種子不再在新答案加入「示範交卷」尾註，既有快照不回寫。PDF 產生器與 manifest 評語會先套用相同規則，避免重新產生固定品牌文案。

2026-11-13 已更新 public 共 419 列：grades 125、report_submissions 72、report_peer_reviews 216、report_votes 6；PDF 的草稿／公開評語及檔名在同列整理。覆核零待整理、重跑零修改。獨立測試 schema 已驗證未標記資料不變、NULL、分數／發布、檔案 bytes、不可變快照與原始稽核保留，以及可重入，完成後刪除 schema。
