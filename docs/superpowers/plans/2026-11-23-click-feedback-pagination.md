# Click-to-load feedback history implementation plan

> **For agentic workers:** Use superpowers:executing-plans in this existing workspace. Keep unrelated dirty work; only the 7173 runtime is authorized. Review the complete change before completion.

**Goal:** Initially fetch five feedback pairs and append at most five more only when the user clicks load more; never infinite scroll.

**Architecture:** Extend the existing stage-history envelope with optional paging metadata. Opt-in `paged=true` preserves legacy full-history callers. SQL scopes every query by authenticated owner/course/stage/practice, numbers all owned attempts before model/trash filtering, and reads only five answer/reply bodies plus a lightweight lookahead. Existing fetch wrapper, authorization and immutable history remain.

**Tech stack:** ASP.NET Core, Npgsql/PostgreSQL, Vue, Vitest, Playwright Chrome.

**Spec:** User request 2026-11-23: JavaScript fetch, five appended rows per click, no infinite scroll; .ai/elearning.md role/history behavior.

## Constraints and review focus

- No new persistence or migration; Raw Chat is temporary and has no database history to page.
- Preserve global version numbers, selected answer, draft, model filter, trash/restore, diagnostics, formal snapshots, and retry protection.
- Fetch metadata without loading all answer bodies; no intersection/scroll observers or background auto-pagination.
- Model/stage/identity changes invalidate pending requests; double clicks cannot overlap; failures retain current rows and allow retry.
- Empty/final pages, equal timestamps, retired/unknown models, cross-course/student IDs, and concurrent newly added attempts must remain safe.

## Task 1: Backend page contract and owner-scoped reads

- [x] Add failing database tests for 5/5/remainder, pairing, stable numbers, filters, authorization and mutation responses.
- [x] Extend stage DTOs/query and implement bounded SQL reads in a focused service partial; keep legacy behavior when paging is omitted.
- [x] Propagate opt-in paging through existing send/retry/trash routes without returning the complete history.
- [x] Run backend tests in an isolated authorized test schema, then remove it.

## Task 2: Fetch-on-click UI and verification

- [x] Add failing Vue tests for initial page, explicit append, selection/draft preservation, failure/retry and stale/duplicate requests.
- [x] Use existing api(fetch) wrapper for first/next pages; server numbering and metadata drive the list; button is the only continuation trigger.
- [x] Add desktop/mobile Chrome coverage for 5→10→remainder and no requests on scrolling; preserve model/trash/role flows.
- [x] Run relevant/full tests and build, review, update product/history docs, and deploy only 7173.

## Implementation ledger

- Initial audit: stage GET and all mutations currently materialize complete history; frontend filters locally. Paging must cover mutations as well as first-load GET. Comparison choices can use explicitly loaded attempts; loading more makes older choices available.

- Ruling: use a bounded owner-scoped target lookup for an explicitly clicked old completion/restore and failure refresh; it is not automatic continuation paging. A focused or disjoint newest-page mutation resets the bounded list rather than merging a gap.
- Review: independent read-only review findings were reproduced and addressed with regressions; post-await generation guard and restore model preservation were also fixed.
- Final verification: 515 backend tests with isolated PostgreSQL and nonempty migration upgrade/rerun, 460 Vue tests, 81 Chrome regressions plus final paging 2/2, Visual Studio build including Vite, and diff check passed. Test schemas removed. 7173 PID 28444 serves the new bundle; anonymous history remains 401. No migration/model/prompt changes, commit or push.
