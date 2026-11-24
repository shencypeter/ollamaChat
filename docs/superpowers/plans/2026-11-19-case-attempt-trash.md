# Recoverable case-analysis attempt trash

User request: students and professors can trash bad attempts. Implementation assumes ownership: students manage their own formal rounds; teachers/authorized TAs manage their own practice rounds. Viewing another learner remains read-only.

## Design

- Add nullable `case_analysis_messages.trashed_at` for user messages. Paired assistant DTO inherits that timestamp; content remains immutable.
- Full authorized history retains all messages and revision links, with `trashedAt` metadata. Normal UI and comparison hide trashed pairs; a trash toggle permits viewing/restoring them. Formal learning statistics remain unchanged and the UI explains this.
- POST own stage message `/trash` or `/restore` returns the existing history envelope. Require active authorized course and matching owner/stage/practice. Reject assistant IDs, cross-scope IDs, live pending rounds, retries and new revisions from trashed sources. Closed sessions can be organized within active courses.
- Serialize mutations with the existing stage lock and session-before-message locks. Audit only identifiers/action, never answer content. Operations are idempotent. No permanent deletion endpoint.
- Preserve typed drafts; clear a trashed draft's revision source and request identity. Preserve selected session and choose another visible round. Review-only and student POV cannot mutate. Raw continues using its existing ephemeral clear operation.

## Verification and delivery

1. Backend owner/scope, paired state, restore, immutable history/statistics, pending, retry/revision, audit and controller-preview tests.
2. Upgrade existing 039 data to 040 with fingerprints and NULL defaults; rerun migrations.
3. UI tests and synthetic browser flows for student/teacher, confirmation/cancel, refresh, restore, drafts, closed sessions and readonly review.
4. Run isolated database regressions, frontend checks and normal Visual Studio build. Apply validated migration to the existing environment and restart only 7173. Update persistent specs/history; do not commit or push.

## Outcome

Implemented and deployed on the existing 7173 environment. Backend 449/449, frontend 351/351, Chrome 48/48 and standard Visual Studio build passed. 036-to-040 upgrade/rerun retained existing fingerprints; temporary schema removed. Production migration 040 was the sole pending migration and preserved message/session/course/grade fingerprints. Independent security/concurrency review found no actionable issues. No real attempt was trashed during testing. Raw process memory cleared by the same-port restart; saved stage history remains. No commit or push.
