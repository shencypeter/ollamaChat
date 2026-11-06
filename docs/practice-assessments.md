# 本人學生體驗提交與練習成績

2026-10-30 新政策取代先前禁止練習提交規定。

## 操作

1. 教師／助教切換學生視角，在所屬課程已發布測驗填答提交，或上傳 PDF 作業。案例也可提交本人練習，但只文字 AI 回饋、不計分。
2. 返回教學視角，至課程的「測驗／作業成績」→「我的練習提交與人工評分」。閱讀本人的練習答案／PDF，輸入練習得分、評語並儲存。
3. 切回學生視角，同一課程成績頁顯示「我的練習成績」與百分制相對權重平均。未評分顯示待評分，不以零計入。

練習為本人獨立紀錄，不冒用學生、不出現在正式提交名單，不寫正式成績簿／班級平均／案例統計。已提交測驗／PDF 預設鎖定，沒有重設或重交練習入口；仍遵守開放、截止、關閉與封存。舊式隱藏 exams 模組未擴充，此處測驗指目前 /quizzes 的線上／課後測驗。

## 後端與保存

- 測驗沿用 submissions.is_practice、不可改寫 submission_revisions 與 grades；私人練習分數即儲存即對本人練習可見，不同步正式成績。
- PDF 的 011_practice_reports.sql 新增 is_practice default false，唯一鍵包含模式；原正式提交仍 false。沿用原 report_submissions 分數／評語，不建立平行表。
- 案例沿用 studio_attempts.is_practice，正式教學端的 NOT is_practice 篩選保留。
- GET /api/courses/{course}/practice：僅本人最新已提交測驗快照與 PDF 練習、分數、評語、平均。
- PUT /api/courses/{course}/practice/{quiz|report}/{id}/grade：教學模式限定本人，課程 teacher／ta 授權、封存拒寫、快照／提交歸屬、分數精度範圍、前 grade ID／version 衝突與稽核。
- GET /api/courses/{course}/practice/reports/{id}/preview：只本人練習，教學與練習模式都可讀。
- 普通 Student 無此專用端點權限；其他 staff 不能讀／評分本人以外練習；practice 模式不能評分。沒有指定學生 ID 的功能。正式報告評分端點排除 practice；正式 GradeSync 不受練習影響。

## 驗證

157 後端（實際啟用 PostgreSQL，0 略過）、35 前端、Vite build／publish 通過。studio-practice.spec.js 與 studio-assessments.spec.js 在最終 5097 共 2 通過；含 UI 練習提交、UI 人工評分、學生視角本人結果、另一 TA 隔離、角色／版本／分數拒絕、正式成績不受影響與真正學生回歸。只用合成資料，無模型推論；合成資料已清理。011 已套用、001–011 重跑略過。

## 2026-10-30 客觀題自動批改更新

新提交的目前測驗由系統批改客觀題，多選採全對得分；教師只評申論，純客觀正式成績僅需發布，混合題待申論完成。練習沿用相同規則但與正式成績隔離。012 已套用，舊人工提交不追溯重算。詳見 quiz-autograding.md。
