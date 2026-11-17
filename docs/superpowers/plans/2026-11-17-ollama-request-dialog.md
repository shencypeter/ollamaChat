# Ollama request in performance dialog

**Goal:** Teachers and course TAs can inspect the actual constructed request for their own newly generated Raw/Stage reply in the existing performance dialog.

**Architecture:** Reuse request-scoped OllamaRequestInspection with explicit course-authorized conversation capture. Capture the serialized transport body, never reconstruct history using today's prompt/model. Successful send responses add optional requestInspection; no DB/message/history schema changes. Vue binds snapshots to the exact reply in page memory, clears them with scope/lifecycle changes, and shows an unavailable state for older replies.

**Constraints:** Raw remains no application system/no persistence/no content logs or formal statistics. Students and student-view requests receive no snapshots. Teacher review of student records does not expose this request view. Recheck staff course access after inference. Exclude URL credentials, headers and private thinking; do not capture model response bodies for conversation inspection. Preserve model profiles, endpoint routes and existing response fields. Only7173.

- [x] Add failing transport/capture tests for explicit opt-in, all profiles, exact serialized body and privacy (root).
- [x] Implement scoped capture, course-role guards and optional successful-send envelopes; preserve default conversation isolation (root).
- [x] Add failing dialog/page-memory tests; implement stats/request tabs, readable messages and JSON, copy, focus handling and unavailable state (UI agent).
- [x] Test actual HTTP-captured requests with mocked model and real isolatedDB authorization: teacher/TA/student/preview, withdrawal/demotion, no history leakage or DB schema change (root).
- [x] Build and verify desktop/mobile light/dark, exact round selection, Raw clear and student/review isolation; review and update only7173 (root).
- [x] Update .ai, Ollama and feedback-dialog docs with measured results and limits (root).

Final: isolatedDB371, Vue273, Chrome22 and standardVS build passed. Current7173 normalbin PID43760, newbundle index-CFfvnS5W.js, anonymous401. Only newly captured staff-owned replies expose outbound payload; no reconstruction/persistence/model-response body. Independent review found no actionable security/correctness issues. Tests use modelHTTP mock, no realLLM-quality claim. Temporary task builds cleaned, logs and synthetic screenshots retained. No commit/push.

User explicitly requested this feature; request display is authorized. This changes the previous blanket conversation capture exclusion only for explicitly authorized staff requests, retaining privacy/persistence limits. No separate planning approval, no commit/push requested.
