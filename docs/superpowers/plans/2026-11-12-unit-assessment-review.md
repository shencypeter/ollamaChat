# Unit Assessment Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Place ordinary assessment review inside course units, provide a roster-sidebar grading workspace, and seed real demo submissions for every active demo student.

**Architecture:** Compose the existing class-roster API and `TeachingPanel` in a focused Vue component; unit activity links carry a review intent for staff. Add an idempotent Development-only PortalSmoke seed that writes existing assignment submission tables and audits coverage.

**Tech Stack:** Vue 3, Vitest, ASP.NET Core 8, Npgsql/PostgreSQL, xUnit

**Spec:** `docs/superpowers/specs/2026-11-12-unit-assessment-review-design.md`

## Global Constraints

- Preserve existing assessment, grade, permission, and historical data.
- Do not create submissions for unsubmitted roster display.
- Do not release demo grades.
- Use only the existing 7173 environment.

## Review Focus

- A roster containing both submitted and unsubmitted students orders submitted students first.
- Selecting an unsubmitted student never calls submission or grade endpoints.
- Cross-course assignment IDs remain rejected by existing backend authorization.
- Re-running the demo seed does not duplicate submissions or answers.
- Courses without published individual assignments are skipped safely.

---

### Task 1: Grading workspace

**Files:**
- Create: `ClientApp/src/components/AssignmentReviewWorkspace.vue`
- Create: `ClientApp/src/components/AssignmentReviewWorkspace.test.js`
- Modify: `ClientApp/src/views/AssignmentView.vue`

- [ ] Write a failing component test for grouping, selection, and the blank read-only paper.
- [ ] Run the focused Vitest test and confirm it fails.
- [ ] Implement the workspace and replace the table/modal review section.
- [ ] Run the focused and full frontend tests.

### Task 2: Unit and formal-exam navigation

**Files:**
- Modify: `ClientApp/src/components/CourseUnits.vue`
- Modify: `ClientApp/src/components/CourseUnits.test.js`
- Modify: `ClientApp/src/views/AssessmentsView.vue`

- [ ] Add failing assertions for staff grading actions inside units.
- [ ] Implement role-aware unit actions and clarify the outer formal-exam purpose.
- [ ] Run focused and full frontend tests.

### Task 3: Idempotent submitted-work demo

**Files:**
- Create: `tests/PortalSmoke/SubmittedAssignmentDemo.cs`
- Modify: `tests/PortalSmoke/Program.cs`

- [ ] Add a Development-only `--submitted-assignment-demo` and audit entry point.
- [ ] Insert missing submissions through existing tables in one transaction, capture quiz grading, and leave grades unpublished.
- [ ] Run the seed twice and run its coverage audit against the authorized development database.

### Task 4: Verification and handoff

**Files:**
- Modify: `.ai/agents.md`
- Modify: `.ai/elearning.md`
- Modify: `.ai/history.md`

- [ ] Run Vue tests/build and .NET tests/build.
- [ ] Verify migrations and the 7173 runtime without opening another port.
- [ ] Update persistent project documentation with behavior, tests, data changes, and limitations.
