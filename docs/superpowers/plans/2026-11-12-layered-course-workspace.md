# Layered Course Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將現有課程導覽改為平台 → 課程內容 → 單元工作區 → 教學活動的分層瀏覽，同時保留既有深層連結、權限、正式考試與未歸類資料。

**Architecture:** 後端以既有 `CourseUnit` 與各活動的 `unit_id` 為基礎，新增單元詳情及跨活動類型排序 API；正式考試沿用獨立 `exams` 模型並移出單元。前端新增 canonical 單元路由與工作區元件，課程首頁直接成為單元總覽，活動詳細頁依持久化 `unitId` 返回所屬單元。

**Tech Stack:** ASP.NET Core / C# / Npgsql / PostgreSQL migrations；Vue 3 / Vue Router / Vitest / Vue Test Utils / Playwright。

**Spec:** `docs/superpowers/specs/2026-11-12-layered-course-workspace-design.md`

## Global Constraints

- 參考 HTML 只作資訊架構參考；不複製固定週次、教材標籤、假資料、先教材後測驗限制或連鎖刪除。
- 不建立第二套課程、單元、評量或成績模型。
- 一般教材、線上作業、PDF 作業與隨堂／課後測驗屬於單元；`exams` 是課程層正式考試。
- 保留舊活動 URL；`?section=units` 相容導向課程內容工作區。
- 未歸類舊資料不得自動視為正式考試，也不得對學生顯示未發布內容。
- 單元含活動時不可刪除；課程刪除／封存規則不變。
- 所有 `courseId`、`unitId`、活動 ID 組合都必須在後端驗證同課歸屬。
- 只使用既有 7173 環境；不得另開其他連接埠或工作樹。
- 不提交 Git commit，除非使用者另行明確要求；每個 task 以測試與 `git diff --check` 作為檢查點。

## Review Focus

- 跨課 `unitId`：單元詳情與排序 API 必須回 404/400，且不洩漏其他課程名稱或活動。
- 舊資料：`unit_id IS NULL` 的活動仍可由教師整理，學生看不到草稿，舊深層 URL 仍能開啟。
- 正式考試：既有 `exams.unit_id` 升級後清空，但考試、作答及成績資料不變，且不再阻擋單元刪除。
- 返回脈絡：直接貼上教材／作業／PDF URL 時，能以 API 回傳的 `unitId` 返回正確單元；無單元時安全回課程內容。
- 排序一致性：跨教材、測驗與 PDF 作業的完整排序必須防重複、防漏項並在多人操作時以 transaction 保持連續。

---

## File Structure

### Create

- `Database/035_layered_unit_workspace.sql`：活動共同排序欄位、既有順序回填、正式考試移出單元及索引。
- `Services/CourseUnitActivityOrder.cs`：鎖定單元、計算下一順位及跨資料表重排的唯一後端實作。
- `ClientApp/src/views/UnitWorkspaceView.vue`：單元工作區頁面、活動清單、排序與教師新增入口。
- `ClientApp/src/views/UnitWorkspaceView.test.js`：單元載入、權限、排序、返回與活動路由測試。
- `ClientApp/src/components/UnitAssessmentCreator.vue`：在既有單元內建立線上／課後測驗及 PDF 作業。
- `ClientApp/src/components/UnitAssessmentCreator.test.js`：建立 payload 必含目前 `unitId` 的測試。
- `ClientApp/src/utils/courseActivityNavigation.js`：活動 URL 與返回所屬單元的純函式。
- `ClientApp/src/utils/courseActivityNavigation.test.js`：有／無單元的路由相容測試。
- `ClientApp/tests/e2e/layered-course-workspace.spec.js`：教師與學生主要分層流程。

### Modify

- `Controllers/CourseUnitsController.cs`：單元詳情、活動重排、正式考試排除與跨課驗證。
- `Controllers/CourseToolsController.cs`、`Controllers/YouTubeResourcesController.cs`：新教材配置單元內下一順位。
- `Controllers/LearningAssessmentsController.cs`：新測驗、線上作業及 PDF 作業配置單元內下一順位。
- `tests/AiTeachingAssistant.Tests/CourseUnitTests.cs`：migration 後行為、單元詳情、排序與權限測試。
- `ClientApp/src/main.js`：加入 `/courses/:courseId/units/:unitId` 並置於課程 catch-all 路由之前。
- `ClientApp/src/App.vue`：載入單元脈絡、課程／單元側欄、麵包屑與左箭頭。
- `ClientApp/src/utils/courseNavigation.js`：移除重複的教材／單元入口並將首頁命名為課程內容。
- `ClientApp/src/views/CourseDetailView.vue`：首頁與單元總覽合併，`section=units` 相容呈現首頁。
- `ClientApp/src/components/CourseUnits.vue`：單元卡片總覽、進入單元與摘要；移除在卡片內直接平鋪全部活動。
- `ClientApp/src/components/CourseUnits.test.js`：卡片與 canonical 單元連結測試。
- `ClientApp/src/components/CourseTools.vue`：支援鎖定單元與只呈現教材建立器。
- `ClientApp/src/views/AssessmentsView.vue`：正式考試頁只保留 `ExamManager`，一般評量改由單元建立。
- `ClientApp/src/views/LearningResourceView.vue`、`ClientApp/src/views/AssignmentView.vue`、`ClientApp/src/views/ReportAssignmentView.vue`：活動返回位置改由持久化 `unitId` 決定。
- `.ai/elearning.md`、`.ai/agents.md`、`.ai/history.md`：更新現行行為、驗證與交接。

---

### Task 1: 資料庫活動排序與正式考試歸位

**Files:**
- Create: `Database/035_layered_unit_workspace.sql`
- Create: `Services/CourseUnitActivityOrder.cs`
- Modify: `Controllers/CourseToolsController.cs`
- Modify: `Controllers/YouTubeResourcesController.cs`
- Modify: `Controllers/LearningAssessmentsController.cs`
- Test: `tests/AiTeachingAssistant.Tests/CourseUnitTests.cs`

**Interfaces:**
- Produces: `CourseUnitActivityOrder.NextAsync(NpgsqlConnection connection, Guid courseId, Guid unitId, CancellationToken ct) -> Task<int>`。
- Produces: `CourseUnitActivityOrder.ReorderAsync(NpgsqlConnection connection, Guid courseId, Guid unitId, IReadOnlyList<ActivityKey> items, CancellationToken ct) -> Task`。
- Produces: 三個一般活動表的 `unit_sort_order integer NOT NULL`；`exams.unit_id` 全部設為 `NULL`。

- [ ] **Step 1: 寫 migration 升級測試**

在 `CourseUnitTests` 增加測試，建立同一單元中的教材、測驗、PDF 作業與正式考試，斷言 migration／測試資料狀態下前三者有正整數順位、正式考試 `unit_id IS NULL`，且原考試 ID、作答與分數未改變。

- [ ] **Step 2: 執行測試並確認失敗**

Run: `dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore --filter CourseUnitTests`

Expected: FAIL，資料表尚無 `unit_sort_order` 或正式考試仍有 `unit_id`。

- [ ] **Step 3: 新增 migration**

`035_layered_unit_workspace.sql` 對 `course_resources`、`assignments`、`report_assignments` 新增 `unit_sort_order`；以 `created_at,id` 跨表穩定回填每個單元的 1..N；新增 `(course_id,unit_id,unit_sort_order)` 索引；將 `exams.unit_id` 設為 `NULL`，不刪除欄位或考試資料。

- [ ] **Step 4: 實作共同排序服務**

建立 `ActivityKey(string Kind, Guid Id)` 與上述兩個方法。`NextAsync` 先 `SELECT ... FOR UPDATE` 鎖定同課單元，再從三表取最大順位加一；`ReorderAsync` 必須在呼叫端 transaction 內更新三表，並拒絕未知 kind。

- [ ] **Step 5: 將新活動建立流程接到 `NextAsync`**

教材檔案、YouTube、assignment/quiz 及 PDF report 在確認同課單元後取得下一順位，INSERT／UPDATE 同時寫入 `unit_sort_order`；沒有單元的相容資料使用 0。

- [ ] **Step 6: 執行 migration 與單元測試**

Run: `dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore --filter CourseUnitTests`

Expected: PASS。

- [ ] **Step 7: 檢查點**

Run: `git diff --check`

Expected: 無 whitespace error；不建立 commit。

---

### Task 2: 安全的單元詳情與跨類型排序 API

**Files:**
- Modify: `Controllers/CourseUnitsController.cs`
- Modify: `tests/AiTeachingAssistant.Tests/CourseUnitTests.cs`

**Interfaces:**
- Produces: `GET /api/courses/{courseId}/units/{unitId}` → `{ id, name, description, sortOrder, version, canManage, activities[] }`。
- Produces: `PUT /api/courses/{courseId}/units/{unitId}/activities/reorder`，body `{ items: [{ kind, id }] }`，成功回 204。
- `activities[]` 欄位固定為 `{ id, unitId, kind, title, status, createdAt, dueAt, sortOrder }`，kind 僅 `material|quiz|assignment|report`。

- [ ] **Step 1: 寫失敗的 controller 測試**

新增測試斷言：教師與學生可讀同課單元；學生看不到 draft；其他課的 `unitId` 回 NotFound；回應不包含 exam；排序依 `sortOrder`；reorder 對重複、漏項、外課 ID、未知 kind 回 BadRequest/NotFound，完整清單回 NoContent。

- [ ] **Step 2: 執行測試並確認失敗**

Run: `dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore --filter CourseUnitTests`

Expected: FAIL，endpoint 或 DTO 尚不存在。

- [ ] **Step 3: 抽出單元活動查詢**

在 `CourseUnitsController` 建立私有查詢方法，`Curriculum` 與單元詳情共用；SQL union 只含三類一般活動資料表，依 `unit_sort_order,created_at,id` 排序，不再 union `exams`。

- [ ] **Step 4: 實作單元詳情 endpoint**

先用既有 `Role` 驗證課程成員，再以 `WHERE id=$unit AND course_id=$course` 查單元；不存在一律 NotFound。學生／預覽沿用 draft 過濾與小組 PDF audience 規則。

- [ ] **Step 5: 實作 reorder endpoint**

在 transaction 內鎖定單元，讀出該單元完整可管理活動鍵集合，要求 body 恰好一次包含每筆，再呼叫 `CourseUnitActivityOrder.ReorderAsync`、寫 audit `course.unit.activities_reordered` 後提交。

- [ ] **Step 6: 修正 Move 行為**

活動移入單元時配置該單元下一順位；移出時 `unit_sort_order=0`。移除 exam kind 支援；正式考試不能經此 API 移入單元。單元刪除檢查不再包含 exams。

- [ ] **Step 7: 執行後端測試**

Run: `dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore --filter CourseUnitTests`

Expected: PASS。

- [ ] **Step 8: 檢查點**

Run: `git diff --check`

Expected: 無 whitespace error。

---

### Task 3: 前端 canonical 路由與課程／單元導覽脈絡

**Files:**
- Create: `ClientApp/src/utils/courseActivityNavigation.js`
- Create: `ClientApp/src/utils/courseActivityNavigation.test.js`
- Modify: `ClientApp/src/main.js`
- Modify: `ClientApp/src/App.vue`
- Modify: `ClientApp/src/utils/courseNavigation.js`
- Modify: `ClientApp/src/views/CourseDetailView.vue`

**Interfaces:**
- Produces: `unitLocation(courseId, unitId) -> string`，格式 `/courses/{courseId}/units/{unitId}`。
- Produces: `activityReturnLocation(courseId, unitId) -> RouteLocationRaw`；有單元回 canonical 路由，無單元回 `{ path: '/courses/{courseId}', query: { legacy: 'activities' } }`。
- Consumes: Task 2 的單元詳情 API。

- [ ] **Step 1: 寫路由純函式測試**

斷言 GUID／字串 ID 正確 URL encode；`unitId=null|undefined|''` 回課程相容位置；有單元不夾帶 `section=units`。

- [ ] **Step 2: 執行測試並確認失敗**

Run: `npm test -- --run src/utils/courseActivityNavigation.test.js`

Workdir: `ClientApp`

Expected: FAIL，module 尚不存在。

- [ ] **Step 3: 實作純函式與 Vue Router 路由**

新增工具；在 `/courses/:courseId` 之前註冊 `/courses/:courseId/units/:unitId` 對應 `UnitWorkspaceView`，meta title 為「單元工作區」。

- [ ] **Step 4: 調整課程導覽定義**

`courseSections(course)` 將 `home` 顯示為「課程內容」，移除 `units` 與 `resources`；保留正式考試、學員、分組、討論、成績、統計、設定及既有角色條件。

- [ ] **Step 5: 調整 `App.vue` 的單元脈絡**

由 `route.params.unitId` 載入單元詳情。單元路由時側欄上方依序呈現「返回課程內容」、課程名稱、單元序號／名稱；下方仍渲染課程層 sections。麵包屑為入口／課程／單元，載入失敗安全回課程內容且不顯示外課資訊。

- [ ] **Step 6: 合併課程首頁與 units 相容入口**

`CourseDetailView` 將 `section=units` 視同 `home`；首頁只渲染一次可管理的 `CourseUnits`。設定頁的單元／教材入口改回課程內容；舊 `?section=units` 不白屏。

- [ ] **Step 7: 執行前端單元測試與 build**

Run: `npm test -- --run src/utils/courseActivityNavigation.test.js src/components/CourseUnits.test.js`

Run: `npm run build`

Workdir: `ClientApp`

Expected: PASS 且 Vite build 成功。

- [ ] **Step 8: 檢查點**

Run: `git diff --check`

Expected: 無 whitespace error。

---

### Task 4: 課程單元總覽與聚焦單元工作區

**Files:**
- Create: `ClientApp/src/views/UnitWorkspaceView.vue`
- Create: `ClientApp/src/views/UnitWorkspaceView.test.js`
- Modify: `ClientApp/src/components/CourseUnits.vue`
- Modify: `ClientApp/src/components/CourseUnits.test.js`

**Interfaces:**
- Consumes: Task 2 的單元詳情與 reorder API。
- Consumes: Task 3 的 `unitLocation` 與活動 URL helper。
- Produces: `UnitWorkspaceView`，教師可排序／進入新增，學生唯讀瀏覽。

- [ ] **Step 1: 寫課程總覽測試**

更新 `CourseUnits.test.js`：單元卡片顯示名稱、說明與各類活動數量；「進入單元」指向 canonical URL；教師有新增／編輯／排序／刪除空單元，學生沒有管理按鈕；未歸類區塊只對可管理者提供整理控制。

- [ ] **Step 2: 寫單元工作區失敗測試**

`UnitWorkspaceView.test.js` 斷言：依 API 順序混合顯示教材、測驗、作業、PDF；教師有上／下移和新增入口；學生無管理控制且看得到截止／狀態；API 404 顯示安全錯誤與返回課程；reorder body 送出完整 `{kind,id}` 清單。

- [ ] **Step 3: 執行測試並確認失敗**

Run: `npm test -- --run src/components/CourseUnits.test.js src/views/UnitWorkspaceView.test.js`

Workdir: `ClientApp`

Expected: FAIL，canonical 卡片與新 view 尚未完成。

- [ ] **Step 4: 將 `CourseUnits` 改成總覽卡片**

每張卡片只顯示摘要、計數與「進入單元」，不再平鋪完整活動。保留單元 CRUD/reorder；未歸類活動顯示在獨立相容性區塊，教師可指派，學生只顯示伺服器允許的可見資料且不提供管理。

- [ ] **Step 5: 實作 `UnitWorkspaceView`**

載入 `/courses/{courseId}/units/{unitId}`；呈現醒目單元標題與混合活動清單；活動連結使用既有深層 URL；教師移動按鈕重新排列本地完整清單並呼叫 reorder API，成功後重載。

- [ ] **Step 6: 加入窄螢幕樣式與可及性**

活動列在 650px 以下改為單欄；所有圖示按鈕有包含活動名稱的 `aria-label`；狀態不只靠顏色；錯誤與成功訊息使用 `role=alert/status`。

- [ ] **Step 7: 執行元件測試**

Run: `npm test -- --run src/components/CourseUnits.test.js src/views/UnitWorkspaceView.test.js`

Workdir: `ClientApp`

Expected: PASS。

- [ ] **Step 8: 檢查點**

Run: `git diff --check`

Expected: 無 whitespace error。

---

### Task 5: 單元內建立活動與返回所屬單元

**Files:**
- Create: `ClientApp/src/components/UnitAssessmentCreator.vue`
- Create: `ClientApp/src/components/UnitAssessmentCreator.test.js`
- Modify: `ClientApp/src/views/UnitWorkspaceView.vue`
- Modify: `ClientApp/src/components/CourseTools.vue`
- Modify: `ClientApp/src/views/AssessmentsView.vue`
- Modify: `ClientApp/src/views/LearningResourceView.vue`
- Modify: `ClientApp/src/views/AssignmentView.vue`
- Modify: `ClientApp/src/views/ReportAssignmentView.vue`
- Test: `ClientApp/src/views/LearningResourceView.test.js`

**Interfaces:**
- Produces: `UnitAssessmentCreator` props `{ course: Object, unitId: String }`，建立 quiz/report payload 一律包含 `unitId`。
- Produces: `CourseTools` props `lockedUnitId?: String` 與 `creatorOnly?: Boolean`；鎖定時不可選其他單元。
- Consumes: Task 3 的 `activityReturnLocation`。

- [ ] **Step 1: 寫活動建立 payload 測試**

斷言 quiz 使用 `/courses/{course}/quizzes` 且 `assignment.unitId` 為目前單元；PDF 使用 `/courses/{course}/reports` 且 body `unitId` 為目前單元；成功後導向既有活動深層 URL。

- [ ] **Step 2: 寫返回位置測試**

教材、assignment/quiz 與 PDF view 分別以 API 回傳 `unitId` 顯示「返回 {單元名稱或課程內容}」；沒有 `unitId` 時回課程相容區，而不是失效的 `section=resources/assessments`。

- [ ] **Step 3: 執行測試並確認失敗**

Run: `npm test -- --run src/components/UnitAssessmentCreator.test.js src/views/LearningResourceView.test.js`

Workdir: `ClientApp`

Expected: FAIL。

- [ ] **Step 4: 抽出單元評量建立器**

重用 `AssignmentEditor`、`ReportModeFields`、`toUtc` 與現有 API payload，不複製題目／小組 PDF 規則。建立器只負責目前單元的一般評量，不顯示 `ExamManager`。

- [ ] **Step 5: 擴充 `CourseTools` 的鎖定單元模式**

`lockedUnitId` 預設上傳單元且隱藏單元 selector；`creatorOnly` 只顯示新增檔案／YouTube 表單，不重複渲染 UnitWorkspace 的活動清單。原課程相容管理模式保持可用。

- [ ] **Step 6: 將建立器接入單元工作區**

教師的「新增教材」「新增測驗／作業」「新增 PDF 作業」在目前單元展開既有表單；助教限制與封存狀態沿用現有 disable/hidden 條件。

- [ ] **Step 7: 純化正式考試頁**

`AssessmentsView` 只顯示正式考試介紹與 `ExamManager`；移除一般 quiz/report 建立表單及列表，改為連到課程內容工作區。未歸類活動由課程首頁相容區管理。

- [ ] **Step 8: 修改三種活動詳細頁返回連結**

活動載入後以 `activityReturnLocation(courseId,item.unitId)` 建立確定性的返回路由；按鈕文字為「返回所屬單元」或「返回課程內容」。不依賴 `history.back()`。

- [ ] **Step 9: 執行相關前端測試**

Run: `npm test -- --run src/components/UnitAssessmentCreator.test.js src/views/LearningResourceView.test.js src/views/UnitWorkspaceView.test.js src/components/CourseTools.test.js`

Workdir: `ClientApp`

Expected: PASS。

- [ ] **Step 10: 檢查點**

Run: `git diff --check`

Expected: 無 whitespace error。

---

### Task 6: E2E、完整回歸、migration 與文件交付

**Files:**
- Create: `ClientApp/tests/e2e/layered-course-workspace.spec.js`
- Modify: `.ai/elearning.md`
- Modify: `.ai/agents.md`
- Modify: `.ai/history.md`

**Interfaces:**
- Consumes: Tasks 1–5 的 API、路由與 UI。
- Produces: 可在既有 7173 demo 環境重現的驗收流程。

- [ ] **Step 1: 寫 E2E 測試**

覆蓋：教師從課程卡進入課程內容、新增／進入單元、新增教材或評量、活動返回單元、進入正式考試、切學生視角；學生只看得到已發布活動；單元內仍可切討論與成績；窄螢幕可用左箭頭返回。

- [ ] **Step 2: 先執行 E2E 並記錄真實失敗**

Run: `npm run test:e2e -- layered-course-workspace.spec.js`

Workdir: `ClientApp`

Expected: 若 selector 或 demo 資料尚未對齊則 FAIL；依實際 UI 修正測試或無障礙名稱，不用 timeout 掩蓋。

- [ ] **Step 3: 執行完整前端測試與 build**

Run: `npm test`

Run: `npm run build`

Workdir: `ClientApp`

Expected: 全部 PASS，build 成功。

- [ ] **Step 4: 執行完整後端測試**

Run: `dotnet test AI_TeachingAssistant.sln --no-restore`

Workdir: repository root

Expected: 全部非條件性測試 PASS；條件性 PostgreSQL skip 數量需在回報列出。

- [ ] **Step 5: 套用 migration 到唯一 7173 環境**

先確認監聽 7173 的 process 路徑與專案相符，再以現有 migration runner 啟動／重啟同一環境；不得另開其他 port。確認 `portal_schema_migrations` 含 `035_layered_unit_workspace.sql` 且 checksum 一致。

- [ ] **Step 6: 執行 E2E 與 smoke test**

Run: `npm run test:e2e -- layered-course-workspace.spec.js`

Workdir: `ClientApp`

Expected: PASS；`https://localhost:7173/courses` 與至少一個單元 canonical URL 回應正常。

- [ ] **Step 7: 更新持久化文件**

在 `.ai/elearning.md` 記錄課程／單元瀏覽、正式考試與相容行為；`.ai/agents.md` 更新目前完成狀態與唯一 7173 注意事項；`.ai/history.md` 追加日期、migration、測試數量、已知限制與人工驗收路徑，不寫敏感帳密或真實學生答案。

- [ ] **Step 8: 最終差異與安全檢查**

Run: `git status --short`

Run: `git diff --check`

Run: `git diff --stat`

Expected: 只有本工作及使用者原有變更；無其他工作樹、環境或連接埠；不提交 commit。

---

## Self-Review Result

- Spec coverage：平台層、課程層、單元層、教師／學生流程、正式考試、舊資料、權限、排序、錯誤處理與文件均有對應 task。
- Step scan：每一步只有一項可檢查成果；實作 signature 與 payload 已固定，未抄寫完整程式本體。
- Type consistency：單元活動 kind 固定 `material|quiz|assignment|report`；正式考試不進入此 union；前後端 reorder payload 一致為 `{items:[{kind,id}]}`。
- Review focus：五項風險分別由 Task 1、2、3、5、6 的測試覆蓋。
- Proportion：計畫比設計更具體但未展開 controller、SQL 或 Vue 的完整實作。
