# Vue / Web API migration

## Delivered foundation

The active UI is Vue 3 + Vue Router + Vite in `ClientApp`. ASP.NET Core now exposes JSON Web API controllers rather than rendering the legacy MVC page. A same-origin cookie/antiforgery flow supports Professor, TeachingAssistant and Student accounts stored in PostgreSQL. The workspace has a Teams-inspired app rail, course navigation, content cards, chronological conversations and responsive light/dark themes. Theme choice persists locally.

The existing AI form capabilities are available at `/ai`: four assessment stages, current-answer review, before/after comparison, optional case context, criterion-level findings and scores, general chat, four memory modes, sanitized Markdown and telemetry. Assessment requests retain their independent context; chat state is additionally keyed by authenticated user. AI generation, prompt version, sampling parameters, JSON schema and validation rules were not tuned as part of this UI migration. Browser AI-generation responses are mocked in UI tests; no grading-accuracy claim is made.

## Database application

The user-supplied DDL was saved in `Database/001_elearning.sql`, with its outer transaction delegated to the runner. `002_portal_auth.sql` adds a separate `portal_credentials` table rather than repurposing domain/course membership tables for login. `portal_schema_migrations` records migration checksums. Both versions committed successfully on 2026-10-22 against the supplied server, which reported PostgreSQL 18.6. A subsequent run verified both were already applied. No table/data drops were performed.

Local connection credentials and a generated first-professor setup token are in the ignored `appsettings.Local.json`. They are excluded from publishing. To initialize the platform, open `/login`, use the code under `Portal:BootstrapToken`, and create the first real professor account. Initialization is one-time and protected by a transactional database lock. There are no built-in demo credentials. Professors create additional accounts, create draft courses, and enroll users in owned courses. Course roles remain `teacher`, `ta`, `student` in the supplied schema; platform roles are separate.

## Scope for the next phase

The domain schema includes assignments, group work, submissions, revisions, rubrics, model evaluation runs and official grades. This iteration implements accounts, courses and membership, and ports the existing standalone AI portal. Assignment authoring/submission/grading interfaces are not implemented yet. Standalone AI practice is not saved as official grades or evaluation runs, because no assignment/revision/rubric context has been selected.

Legacy Razor files and original assets are retained for reference but no controller renders them. The new routes are Vue routes. `/api` endpoints enforce authorization on the server; hiding a navigation link is not the security boundary. Current sessions use process memory and target a single-instance POC.

## Development and publishing

See the root README for normal startup. Build with `cd ClientApp; npm ci; npm run build`, then `dotnet run --launch-profile http` from the project root. Vite development can run separately on port 5173 and proxies `/api` to 5084. Set `API_PROXY_TARGET` to change that address.

`dotnet publish` installs/builds Vue and packages the generated assets once. `-p:SkipFrontendBuild=true` reuses already built assets. Configuration at a deployment destination must provide the connection string and setup token. The local configuration, frontend dependencies/source and QA fixture files are not included in publish output.

## Reproducing browser/API verification

Use only a development database for synthetic QA fixtures. The helper inserts synthetic accounts in a transaction and writes credentials to ignored local state without echoing them:

```powershell
dotnet build tests/PortalSmoke/PortalSmoke.csproj -o .codex-build/portal-smoke
dotnet .codex-build/portal-smoke/PortalSmoke.dll D:/AI_TeachingAssistant
# Start the built API on http://127.0.0.1:5097 in Development.
cd ClientApp
npm run test:e2e
cd ..
dotnet .codex-build/portal-smoke/PortalSmoke.dll D:/AI_TeachingAssistant --cleanup
```

The browser checks real API authentication, antiforgery rejection, Professor/TA/Student restrictions, account creation and duplicate handling, course creation/membership, unknown API JSON 404s, SPA deep links and request validation. The UI test checks both themes, persistence on reload, stage-specific drafts, before/after scores, structured findings, conversation rendering, mobile navigation and browser errors. AI responses alone are controlled test fixtures. Screenshots are saved in ignored `ClientApp/test-results`.

Cleanup validates the exact synthetic UUIDs and their run-specific `example.invalid` emails before deleting only that run's QA courses/accounts. It does not use broad table truncation or delete real users. The bootstrap path is protected by the initialization token; the test helper does not create a permanent professor or bypass production API role checks.

## Implementation map

- `ClientApp/src/App.vue`: workspace shell, role-aware navigation and global theme control.
- `ClientApp/src/views`: login/setup, overview, courses/membership, member accounts, AI portal and account settings.
- `ClientApp/src/components/AssessmentResult.vue`: structured findings rendered as escaped text.
- `ClientApp/src/api.js`, `composables`: API/CSRF/auth/theme state.
- `Controllers/AuthController.cs`, `CoursesController.cs`, `AiController.cs`: authenticated JSON routes.
- `Services/PortalAccountStore.cs`: PostgreSQL accounts, password hashing and login lockout.
- `Services/ApiAntiforgeryFilter.cs`: API-only antiforgery validation.
- `Services/DatabaseMigrator.cs`: explicit transactional SQL migration runner.
- `Services/ChatSessionStore.cs`: per-account/per-stage conversation isolation.
- `Models/PortalModels.cs`: account/course API contracts.
- `Program.cs`, `AiTeachingAssistant.csproj`: middleware, database configuration, Vue asset serving and publishing.
- `tests/PortalSmoke`, `ClientApp/tests/e2e`, frontend unit tests and `AccountSessionIsolationTests.cs`: migration verification.

The first database attempt timed out until the firewall was updated. Subsequent inspection, migration and repeat-migration verification succeeded. Full publish verification caught duplicate asset inclusion, which was corrected rather than suppressing the error. The host Node 23.5 is outside the supported engine range of two transitive development tools; builds and tests execute, but use a supported Node LTS version for repeatable development.


## Final verification results

- Full `dotnet publish` completed successfully, including `npm ci` and the Vue production build.
- Backend regression suite: **58 passed, 0 failed** (includes per-account conversation isolation).
- Vue/component/API unit tests: **6 passed, 0 failed**.
- Chrome end-to-end tests against the published artifact and real PostgreSQL database: **2 passed, 0 failed**. AI generation alone was mocked.
- Screenshots reviewed: desktop overview light/dark, AI feedback light, conversation dark, mobile dark. Browser checks reported no page errors or mobile horizontal overflow.
- Verified publish exclusions: no `appsettings.Local.json`, `ClientApp` source/dependencies, or `.codex-build/portal-smoke.json` QA credentials.
- Temporary synthetic QA accounts and their course were removed after testing. An anonymous session check confirmed `user: null` and `needsSetup: true`.
- Preview is running locally at `http://127.0.0.1:5097`, using the published build with environment-provided secrets. The regular development launch remains port 5084.
- Remaining work: assignment/group/submission/revision/official-grading workflows. This is an account/course/AI-portal foundation, not a complete LMS.
