# Per-submission model selection implementation plan

> Use superpowers:executing-plans inline in the existing workspace. Preserve current uncommitted documentation and use only port 7173.

**Goal:** Professors, authorized TAs and enrolled students choose one of the four existing models before each AI submission.

**Design:** Reuse the whitelist resolver and its IAN02/Taipei endpoints. Formal rounds persist the actual model on both messages; new selection affects only the next submission, idempotency and retry remain bound to the original model. The composer owns the selector. Raw sends an optional modelProfile per message, preserving its existing LangChain conversation and draft across model switches. Raw keeps per-pair model labels in process memory and never enters DB/statistics. Existing legacy session defaults stay compatible. Model limits apply per chosen request. A student's choice must not expose staff request payloads, diagnostics or others' history. Role changes must not transfer staff practice conversations into student statistics.

**Stack:** Existing ASP.NET Core/Npgsql, Vue JS, LangChain.Core 0.17.0; no new packages, schema or provider implementations.

## Tasks

- [x] Add failing DB/service tests for student choices, stored tags, same-ID/retry isolation, role/withdrawal changes, and Raw mixed-model full history with accurate per-message metadata and selected limits.
- [x] Extend CaseAnalysisChatController/Turns authorization and RawChatInput/RawChatSessions request snapshots, keeping staff inspection separate and all existing course checks.
- [x] Add failing Vue tests, then move the native model selector into the composer, enable actual students, keep draft/history across switches and send the selected model for Raw and formal requests. Preserve original-model retry and in-flight isolation.
- [x] Extend desktop/mobile student/teacher browser tests, run backend isolated-schema regression and old-data upgrade, Vue suite and normal VS build. Inspect final UI and review authorization/metadata boundaries.
- [x] Deploy only 7173, confirm actual runtime/bundle and login protection. Update project specs, handoff, history and model-selection documentation. Do not rebuild deleted course B or write real student answers in tests/docs. No commit/push without instruction.

## Review focus

Student choices affect only their own accessible course and requests; invalid profiles never become arbitrary endpoints. Completion after role changes must not mix practice/formal history. Raw memory carries only user/assistant content, while model labels remain correct for each historic pair. Different selected budgets reject without silently trimming memory. A failed retry must retain original-model identity when the selector has moved.

## Verification and rulings

- One model per submission was explicitly confirmed. Raw selection keeps the same conversation; explicit clear/restart remains the memory reset action. Legacy create-session profile defaults remain compatible.
- Final database411/411 plus migration upgrade fingerprints, Vue301/301, Chrome34/34 and normal Visual Studio build passed. A role-demotion capture cleanup regression was found and fixed before final checks; inspection is reauthorized for all current errors.
- Fresh-context read-only review found no actionable issues. 7173 deployedPID46904, bundle index-CjJH7E-c.js. Both configured hosts and all four installed tags checked via the same resolver, no new inference-quality claims.
- No schema/model-profile/global-default changes or production data seeding; previously deleted course B remains out of scope. Existing dirty course-deletion docs preserved. No commit/push.
