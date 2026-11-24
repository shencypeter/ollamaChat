# Left category and revision rail

User-approved layout: ABCD categories above revision history in the existing left rail; student answer and AI feedback remain on the right. Narrow viewports stack the rail above the answer. This supersedes the earlier right-column suggestion before implementation.

## Implementation and checks

1. Extend existing browser scenarios to check category/history order, desktop columns and narrow stacking. Reproduce the missing rail.
2. Reuse `PhaseAnnotatedAnswer` selection state through an optional Vue Teleport target supplied as a DOM element by `CaseAnalysisChat`. Keep the inline fallback for standalone use. Move only the legend and selected-category status; tabs, copy and original text stay with the answer.
3. Preserve scoped palette variables in the moved controls. Add one-column category buttons and a desktop sticky rail with bounded scrolling. Keep revision history below it, including session and comparison controls. Avoid a third content column.
4. Verify current-round categories, disabled unmentioned controls, display-mode highlighting, readonly review, round/session changes, Raw isolation, keyboard and mobile layout. Run frontend tests, build and Chrome synthetic flows; inspect screenshots.
5. Update specifications, handoff and history. Serve built assets on the existing 7173 instance, without restarting the backend or touching database/model settings. Do not commit or push without a request.

No API, migration, model prompt, stored annotation or grading changes.

## Outcome

Completed the left-rail design. Vue 335/335, Chrome 41/41 (including 1440/1024/390 layouts), and Vite passed. Desktop/light and mobile/dark screenshots inspected; read-only review found no actionable regressions. Existing 7173 serves the new bundle, without backend restart, database writes, model changes, commit, or push.
