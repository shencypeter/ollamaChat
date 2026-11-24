# Case workspace writing / feedback tabs

> Execute inline with superpowers:executing-plans; review once after implementation.

**Goal:** Separate writing from reading in the four-stage learner/practice workspace, as approved by the user via the two-tab choice.

**Architecture:** Local Vue state, no API or persistence changes. Keep existing editor and reading components mounted with visibility controls so drafts, snapshots, selection and pending requests survive switching. Raw chat and teacher formal-review remain unchanged.

**Spec/design:** 作答 shows editor, model selection, pending/ready message and AI send. 查看回饋 shows conversation/revision selection, formal submission, classification, original answer and feedback. Existing history opens in viewing; an empty active stage opens in writing. Both tabs retain drafts. Completion stays in writing until the result link is clicked; the link opens viewing before focus/scroll. Copy-confirm opens writing and focuses the editor. Closed/trash/readonly contexts cannot write. Switching tabs does not submit or request AI.

## Constraints / review focus

- Preserve dirty workspace, sole site7173, no backend/model/database change or restart.
- Preserve confirmation on replacing drafts and departing modes/sessions; local tabs never clear drafts.
- Preserve formal component lifetime and immutable source selection; hidden UI cannot bypass readiness rules.
- Busy disables local tab switching; retry opens writing to show pending status.
- Keyboard tablist uses independent IDs/labels, arrows/Home/End and disabled handling; mobile has no horizontal body overflow.

## Tasks

- [x] Update existing main-chat unit cases and add focused state-transition tests; observe failure.
- [x] Implement local write/view state and accessible tabs in CaseAnalysisChat.vue; retain Raw and teacher review.
- [x] Adapt Chrome helpers/workflows to visible state changes; add desktop/mobile draft/ready/copy/keyboard scenarios.
- [x] Apply additional screenshot steering: merge identical popup classification/code-set, status and explanation into one card while retaining distinct original quotes and differing interpretations.
- [x] Run targeted then full Vue, Vite build, appropriate Chrome suite, read-only review and visual QA.
- [x] Update persistent spec/history/handoff and feedback-rounds; confirm7173 serves bundle. No commit/push.

## Ledger

- Ruling: Existing history opens in viewing, empty stages in writing — supports returning to read without an empty editor; user approved tab separation, not a forced empty landing page.
- Ruling: Keep hidden panels mounted — protects the existing formal-submission envelope and chosen assignment/version while preserving the current draft string and source.
- Checks: Initial two new state tests failed on missing tabs; implementation and strengthened transition assertions pass. Full Vue416/416, focused chat77/77, Vite and read-only review pass. Browser targeted state/geometry/formal flows pass; full suite in progress. No product defects raised by review.
- Ruling: Popup deduplication uses the complete code set, status and exact explanation, not just C5 — identical descriptions should read once without collapsing different clinical caveats or reassigning a quote to another explanation. This affects only rendering, not saved annotations or the badge run.
- Final: Vue419/419, Chrome64/64 (targeted12/12), Vite, diff check and screenshots pass. Browser helper timing corrected; no product defect in review. Existing7173 was no longer running at final check; restored same Debug app PID24040 without backend build. /courses200 serves DBQYjJ3B/LJeSfA8j; anonymous case API401. No model/API/DB changes, commit or push.

## 2026-11-20 後續操作調整

使用者追加：分類accordion改水平tab，藍點只標真正階段；再追加未提交版本卡片右上 × 確認移入可還原垃圾桶。正式提交／退回來源後端及UI保護，沿既有stage鎖防submit/trash競爭，legacy trash允許restore。此授權增量取代本計畫早期不改後端的限制，不涉及schema／Prompt／模型。新元件、API與並行情境先驗失敗再修，最終測試結果記於history。


## 2026-11-20 導覽層級調整

使用者指定最上層「作答／回饋／AI 對話測試」，作答與回饋下層共用 A／B／C／D。保留既有 staff-only Raw 與正式提交／統計授權；正式審閱保留獨立教師工具入口。沿 CaseAnalysisChat 的 workspaceView／mode／tab，加入返回正式階段的記憶與明確 workspace 參數，不改 API／DB／模型。相同階段作答與回饋切換只改可見性，跨階段及 Raw 切換仍確認離開；階段變更保留所選上層工作狀態。Primary 與 phase tablist 分別連結外層／內層 panel，鍵盤手動選取，不在箭頭移焦時清 Raw。

1. 新增上層順序／下層顯示、草稿及 Raw 離開／返回階段的回歸測試，先驗失敗。
2. 重排既有 Vue nav，移除重複 local tabs，保留正式審閱工具及無障礙／busy／readonly。
3. 更新單元／Chrome helper，驗兩層 role、學生不可 Raw、草稿確認與正式審閱。
4. Vue 全套、Vite、相關 Chrome 後視需要完整回歸；review、視檢及文件／7173確認，不重啟。

使用者後續追加：Raw AI 對話訊息在上、輸入框在下。移動既有 form 到 feedback-reading 後面，正式階段仍 v-show 互斥；既有單元先證原順序不符，修正後 fullVue429過，Chrome補 Raw 兩種寬度位置驗證。

最終：Vue429/429，targeted Chrome15/15，full Chrome68/68、Vite及diff check成功；兩次read-only review無可行動問題。7173新static index-CN-zKKOx.js／index-B3qSXk2g.css；未重啟或修改DB／模型／Prompt。


## 2026-11-20 原文純文字／選分類自動徽章

使用者指定原文為純文字，任何分類選取自動切badge以避免沒有代碼的底線。保留underline內部值相容；顯式plain清篩選，showAll選全部並badge，hideAll清篩選並plain，取消最後分類保留badge但無持續底線。原文片段不再提供無代碼互動；徽章的局部hover／詳細說明及不可變原文／資料保持。先以新增整合案例重現，再調Vue／notice與既有測試，Chrome改用badge入口並驗plain不含標註；建置／相關全套Vue／Chrome及7173確認，不重啟、無後端／Prompt修改。
