> 2026-11-09 規格草案：[11/6會議差距與驗收入口](docs/meeting-2026-11-06-gap-spec.md)，分為匿名觀摩、教材授課日期／摘要及第二階段AI。僅規格，新增功能尚未實作；既有倍速內容時間政策保留。

> YouTube 回放可編輯逐字稿；影片門檻依播放速度換算有效內容時間（2倍速實看10分鐘計20分鐘），並保留實際時間與逐速度統計。020已套用本機。

> 2026-11-06 教材支援週次／主題標籤搜尋，以及 YouTube 課程回放嵌入、有效觀看時間與課內逐人統計；回放亦可設為測驗時間門檻。018–019已套用本機。詳見 [教材操作與限制](docs/course-materials.md)。

> 2026-11-03 implementation: courses are independent teaching groups with direct membership, groups and resources; sections and cross-course resource sharing are removed. Platform Administrator/Member and independent course-creation permission replace global teaching titles; teaching/student access follows each course membership. Migrations 015–016 are applied locally; pre-existing section data must be mapped before 016 (it fails closed). See [course implementation](docs/course-only-spec.md) and [permission implementation](docs/role-permissions-spec.md). Existing sessions must sign in again.

Development API documentation is available at `/swagger` after restarting the rebuilt service. It uses the existing login Cookie and automatically fetches CSRF tokens for writes; Swagger is disabled in Production. See [Swagger usage and verification](docs/swagger.md).


> 2026-10-29 最新補充：[課程管理與課程工作區](docs/course-hub.md) 已加回主導覽。課程資源、學員、小組、線上測驗、PDF 作業、成績與案例均以所屬課程串接，維持目前 S／T／A 與人工成績／文字 AI 的區分。

> 2026-10-29 現行站台：S1–S3a 學生案例學習、T1–T4 教師歷程／統計／匯出、A1–A6 管理；教授兼任管理者。新案例提供純文字 AI 回饋、不給分，舊資料保留但舊導覽不再顯示。另有線上／課後命題測驗與 PDF 報告作業，人工成績連動到加權成績管理。請先讀 [案例站台](docs/case-studio.md) 與 [測驗／PDF／成績管理](docs/learning-assessments.md)，下方原課程平台說明保留其歷史範圍。資料庫需明確套用 001–009；目前本機預覽仍為 http://127.0.0.1:5097 。

# NSYSU Learn — Vue + ASP.NET Core Web API

An e-learning POC with a Teams-inspired collaboration workspace, light/dark themes, PostgreSQL-backed accounts and courses, individual assignment publishing/submission, and an AI teaching portal.

## Run locally

Requirements: .NET 8 SDK, Node 22.22+ LTS (or a supported newer LTS), PostgreSQL 16+, and the configured on-prem Ollama service for AI requests. Vue assets are bundled locally; the UI does not require a CDN.

As explicitly requested on 2026-11-09, `appsettings.Local.json` is tracked and includes the shared development database connection string and setup secret. Cloning either repository provides these local settings. Environment variables still override configuration (`ConnectionStrings__Portal`, `Portal__BootstrapToken`). Keep actual values out of Markdown, browser code, and command output. The redacted template remains in `docs/portal-config.example.json`. Publish still excludes this local file; configure the deployed application separately.

```powershell
# Build Vue assets first, then run the API and SPA on the same origin.
cd ClientApp
npm ci
npm run build
cd ..
dotnet run --launch-profile http
```

Open http://localhost:5084 . On an empty account database, the login page offers one-time setup for the first professor. Use the initialization code from `Portal:BootstrapToken` in your local configuration, choose your email and a password of at least 12 characters. The token is never sent to the browser automatically. Setup is locked transactionally after the first account exists. No demo passwords or automatic role-switching are enabled.

Professors can create Professor / TeachingAssistant / Student accounts, create courses, and manage membership for courses they own. Course teachers and TAs can create/edit draft individual assignments, publish or close them, and review submitted answers. Students can save drafts, reload them, and submit once before the deadline. Both have access to their enrolled courses and the standalone AI portal. Course teacher/TA/student membership is distinct from platform account permissions. Students cannot create courses, enumerate platform accounts or view the staff roster API. Accounts are password-hashed; cookies are HttpOnly; API writes require an antiforgery header. Password changes sign out the current session.

For separate frontend development, run `dotnet run --launch-profile http` and `npm run dev` in `ClientApp`. Open http://127.0.0.1:5173 . Vite proxies `/api` to the backend; set `API_PROXY_TARGET` to override the backend address. Frontend routes use Vue Router history; ASP.NET serves the built SPA for page routes and keeps unknown `/api` routes as JSON 404 responses.

## Database

`Database/001_elearning.sql` contains the supplied domain DDL unchanged in meaning. Its outer transaction is handled by the migration runner. `002_portal_auth.sql` adds local credentials linked to `app_users`; course role definitions remain in `course_members`. The migration ledger tracks checksums and uses an advisory lock. Migrations do not run automatically when the website starts.

```powershell
dotnet run --no-launch-profile -- --inspect-database
dotnet run --no-launch-profile -- --migrate
```

The original two migrations were applied and committed to the supplied database on 2026-10-22; the server reported PostgreSQL 18.6. Re-running the migration command safely skipped both applied versions. No production user or course is seeded automatically. The connection string is read from configuration and is never echoed by the migration runner.

## Structure

- `ClientApp/`: Vue 3 single-file components, Vue Router, Vite, component/API unit tests and Playwright browser tests.
- `Controllers/`: authenticated JSON Web API for accounts, authentication, courses and AI.
- `Services/PortalAccountStore.cs`: PostgreSQL-backed accounts and password verification.
- `Services/ChatSessionStore.cs`: server-side AI conversation state, scoped to authenticated user and analysis stage.
- `Services/AnalysisStageInstructions.cs`, `AssessmentContract.cs`, `AssessmentResultParser.cs`: evidence-first rubric prompts, Ollama schema and validation.
- `Database/`: explicit transactional SQL migrations.
- `Views/` and the old `wwwroot/js/site.js` / `wwwroot/css/site.css`: retained legacy MVC reference; no controller routes render these views. The active frontend is Vue.

## API

| Route | Access / purpose |
| --- | --- |
| `GET /api/auth/csrf` | Establish request antiforgery token |
| `GET /api/auth/session` | Current authenticated user and setup availability |
| `POST /api/auth/bootstrap` | First professor only; requires configured setup token |
| `POST /api/auth/login`, `/logout`, `/password` | Sign in/out and change password |
| `GET/POST /api/accounts` | Professor-only account list/create |
| `GET /api/courses` | Current user's active course memberships |
| `POST /api/courses` | Professor creates a draft course and owner membership |
| `GET /api/courses/{id}/members` | Course teachers/TAs can read roster |
| `POST /api/courses/{id}/members` | Owning professor assigns or updates membership |
| `GET/POST /api/courses/{id}/assignments` | Course members list assignments; course staff creates drafts |
| `GET/PUT /api/assignments/{id}` | Member reads; course staff edits a draft |
| `POST /api/assignments/{id}/publish`, `/close` | Course staff changes publication status |
| `GET/POST /api/assignments/{id}/my-submission` | Student reads, saves or submits own answers |
| `GET /api/assignments/{id}/submissions` | Course staff lists submitted work only |
| `GET /api/assignments/{id}/submissions/{submissionId}` | Course staff reads submission/revision history |
| `GET /api/ai/configuration` | Model, stages, account-scoped chat state and memory setting |
| `POST /api/ai/send` | Review, compare or general chat |
| `POST /api/ai/memory`, `/clear`, `/stage` | Existing AI workspace controls |
| `GET /api/ai/health` | Check configured Ollama availability |

The AI portal preserves all four assessment stages, optional case reference, independent before/after scores, structured findings, general chat, the four memory modes, sanitized Markdown for chat and request telemetry. Drafts are held in the Vue component by stage and review/compare mode. Assessments do not import previous submissions; conversations are additionally isolated by authenticated account.

Professors and teaching assistants can expand the read-only Ollama inspection panel after a request to review the exact assembled request, HTTP status and corresponding raw Ollama JSON response. The backend removes every `thinking` field before returning this inspection, does not expose unparsed non-JSON response text, and never includes the inspection for students. Inspection data remains request/page scoped and is not written to chat history, logs or the database.

AI portal requests remain standalone practice and do not yet create submissions, answer revisions, evaluation runs or official grades in PostgreSQL. Those workflows need course assignments/questions and rubric records first; no fake foreign-key records are inserted. Individual assignment authoring, publication, draft saving, revision history, immutable submission and staff review are now implemented for P0. P1 now adds optional submission revisions and comparison, reusable course rubrics, manual grade drafts and explicit feedback release. Group work and AI integration into actual submissions remain later phases. See [P1 teaching workflows](docs/p1-teaching.md). See [P0 delivery notes](docs/p0-assignments.md).

## Tests

```powershell
dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj
cd ClientApp
npm test
```

Browser tests use a real local API and the PostgreSQL database. They create clearly identified temporary QA users and courses, and mock **only** AI generation responses to verify UI behavior without making inference calls. See `docs/vue-api-migration.md` for reproducible fixture preparation and cleanup. This UI verification does not establish LLM grading accuracy.

## Publish

```powershell
dotnet publish -c Release -o artifacts/portal
```

Publish runs `npm ci` and `npm run build`, and includes generated Vue assets. `appsettings.Local.json`, frontend source/dependencies and synthetic test data are excluded from publish. Configure the connection string and bootstrap token at the destination via environment variables or managed secrets. `-p:SkipFrontendBuild=true` can reuse already built assets. Authentication cookies require HTTPS outside Development. The current in-process session/cache setup targets a single-instance POC.

## Independent LLM calibration evaluation

See [the evaluation workflow](tests/AssessmentEvaluation/README.md) for synthetic fixtures, schema/payload verification and human grading review. Production prompt `rubric-evidence-v2` contains no answer-specific key. The [calibration implementation report](tests/AssessmentEvaluation/IMPLEMENTATION.md) records the earlier live-model limitations; the Vue migration does not change that evidence.

## P1 assessment and teaching workflows

See [P1 policies, database changes, APIs and November 15 demo](docs/p1-teaching.md). Apply `Database/003_teaching_workflows.sql` with the existing migration command before running the updated API. Existing assignments retain their locked-after-submit behavior by default. New draft assignments can explicitly enable revisions and select a course rubric. Grades belong to a submitted snapshot and require explicit release before students can see feedback. This phase does not invoke AI grading.

## Student pre-submission formative AI feedback

The Vue assignment page now offers Save Draft, Ask AI, and Submit to Teacher/TA as distinct actions. AI evaluates a saved answer revision only, marks older feedback stale, and remains optional. Results are private to the owning student and never create or release official grades. Apply `Database/004_formative_feedback.sql` using the existing `--migrate` command before running the updated API. See [workflow, privacy, persistence and tests](docs/formative-feedback.md).

This currently covers private drafts before the first official submission. Existing post-submission P1 revisions remain direct immutable submissions. Questions need a configured analysis stage; no fallback to general chat is used. Standalone AI memory and staff-only request/response inspection remain separate.

## 2026-10-29：教師／助教學生體驗

教師與助教可切換學生視角，使用本人隔離的 practice 草稿、形成性回饋與提交，不變更帳號／課程角色，不計正式提交或成績。005 遷移新增 submissions.is_practice；原學生流程保留，staff 仍不能讀正式學生私人回饋。一般對話記憶另分 namespace，practice 不擷取或顯示原始 Ollama 診斷。完整政策、限制及驗證見學生體驗文件。

[學生體驗文件](docs/student-experience.md)

## 2026-10-30 學生體驗提交更新

本人學生視角現可提交目前線上／課後測驗、PDF 作業及案例練習；教學視角可從獨立本人練習紀錄人工評分測驗／PDF，再於學生視角查看練習成績。正式成績／統計隔離。此政策取代先前禁止練習提交敘述；操作、011 遷移與驗證見 docs/practice-assessments.md。

## 2026-10-30 客觀題自動批改更新

新提交的目前測驗由系統批改客觀題，多選採全對得分；教師只評申論，純客觀正式成績僅需發布，混合題待申論完成。練習沿用相同規則但與正式成績隔離。012 已套用，舊人工提交不追溯重算。詳見 docs/quiz-autograding.md。

測驗出題與閱讀頁提供題型配分組成表，申論題可保存教師限定的評分參考；013 遷移及操作見 docs/quiz-autograding.md（2026-10-30）。

## 2026-11-04 學習歷程與測驗教材門檻

學生可由「我的學習歷程」查看本人平台內跨課／學期修課、評量進度、待辦、正式提交及已公布成績。課程資源新增 PDF／圖片／MP4／WebM 閱讀器計時，測驗可逐教材設定最低有效秒數，全部達標並開始測驗後才取得題目／寫入作答。需套用新增017遷移，沿用001–016；不回填舊時間或改動歷史成績。正式／練習與跨課權限保留。計時政策、量測限制與後續範圍見 [學習歷程規格](docs/learning-history-spec.md)。
