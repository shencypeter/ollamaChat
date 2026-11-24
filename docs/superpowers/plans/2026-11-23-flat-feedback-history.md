# Flat feedback history and model filter

**Goal:** One numbered draft/feedback pair per history entry, across legacy conversations, with a model filter replacing the conversation selector.

**Architecture:** Flatten the authorized stage history already returned by the API. Keep original session/message IDs, ancestry, formal submission snapshots, model records and backend permissions. Sessions remain internal compatibility metadata; no database rewrite or migration. Raw Chat remains a multi-turn chat. Model filtering uses saved reply model (request model fallback, unknown for legacy NULL), independently from the next-request model selector.

**Implementation:**

- [x] Add failing Vue regressions for multiple sessions, model changes within one session, old models/NULL, one feedback pair, stable numbering, drafts and request isolation.
- [x] Add a small pure history projection utility and connect the unified list/filter; preserve exact pair/session ownership for diagnostics, trash, copy and latest-result navigation.
- [x] Remove the legacy restart/session navigation; allow current composing independently from viewing closed historical attempts. Keep retry restrictions for closed/older failures and copy ancestry supported by the existing API.
- [x] Adapt session-navigation tests to unified history semantics, retaining authorization, formal-source protection, retry, restore, draft and accessibility coverage.
- [x] Build and run applicable Vue/Chrome checks, inspect desktop/mobile screenshots and verify the single 7173 deployment. Do not restart the backend for this frontend-only change.
- [x] Update persistent specifications, handoff/history and feedback documentation with actual verification results.

**Acceptance:** All old attempts remain visible through All models; filters never renumber entries or change submission model/content; only selected draft and paired reply are shown; a new result hidden by the current filter can be revealed from its completion link; review remains formal-only and Raw remains isolated.

**Verification:** Vue 443/443, full case-chat Chrome 76/76, Vite build and diff checks passed. Read-only review found no actionable issue. Desktop teacher/mobile student dark screenshots inspected. Existing 7173 serves index-0PXAQKoZ.js/index-DYWbBA_F.css; process kept running, no backend/database changes or commit/push.
