# Current phase and annotation alignment

**Goal:** In a B answer, underline recognized other-phase content, highlight recognized B content, and explicitly explain a possible phase mismatch in the feedback area.

**Root cause:** The annotated answer receives classifications but no current stage. The existing model contract permits correct classification with unverified status while its prose omits the selected-stage mismatch. Classification recognition is distinct from evidence verification.

**Scope:** Keep four stages, Raw isolation, source text, persisted replies, model profiles and endpoints. No database migration. Necessary cross-phase context and valid multiple codes are not automatically errors.

- [x] Write failing tests for explicit current-phase model input and per-stage prompt direction; then implement stage-aware overload preserving existing provider contracts (root).
- [x] Put current-stage identity prominently in the prompt and shared Studio data; require the prose to explain misplaced main content rather than only praise it. Version new prompts, never regenerate old feedback (root).
- [x] Write failing frontend tests, pass current stage into the shared annotated view, highlight matching classifications and underline foreign ones, preserving both for mixed spans (UI agent).
- [x] Add an explicitly labelled application-generated phase-check note based only on validated original-text annotations; preserve original AI remarks and clinical uncertainty (UI agent).
- [x] Verify synthetic A-in-B, B-in-B, mixed/contextual answers with actual configured IAN02 provider. Keep transport/format checks distinct from teaching-quality observations (root).
- [x] Run affected/full tests, review changes, build and update only7173; update tuning and .ai docs with actual results and limits (root).

Final evidence: Vue265, isolatedDB362, Chrome17, VS build and current7173 bundle passed. No new migration. Actual final Llama test2/3 formatted responses with remaining wrong subcodes/time claims and failed mixed example; model teaching quality explicitly not passed. App note remains conditional and independently labelled, never treats unannotated content as missing or rewrites saved AI text. Independent read-only review found no actionable software/security issues.

The screenshot is evidence of a UI/model discrepancy, not material to seed into prompts or send to the model. Live checks use independent synthetic examples. User requested direct implementation; no separate design approval needed.
