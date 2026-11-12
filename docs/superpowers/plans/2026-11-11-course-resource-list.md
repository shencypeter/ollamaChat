# Course Resource List Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the long tag/date-grouped material cards with unit-grouped compact resource rows that show real image thumbnails or clear document icons while preserving existing metadata, authorization, preview, download, learning-time and statistics behavior.

**Architecture:** Retire resource tags with migration 033 and make `unitId` required for all new file/YouTube resources. Return `unitId` in the existing course resources API, derive ordered unit sections in a focused frontend utility, and render each item through a new `ResourceVisual` component plus a compact row in `CourseTools.vue`.

**Tech Stack:** ASP.NET Core 8, PostgreSQL/Npgsql, Vue 3, Vitest, Lucide Vue, existing authenticated preview API.

**Spec:** `docs/superpowers/specs/2026-11-11-materials-by-unit-design.md`

## Global Constraints

- Work only in `D:\AI_TeachingAssistant`; do not create a worktree, alternate build directory, preview environment or port.
- Use only `https://localhost:7173` for final runtime verification.
- Preserve existing user changes in the dirty worktree and do not commit or stage them.
- New resources require a same-course unit; existing null `unit_id` rows remain readable under a compatibility section.
- Remove historical resource tags physically; preserve audit history and all non-tag resource data.
- Thumbnail access must use the authenticated preview endpoint; no public URLs or third-party YouTube thumbnail fetches.

## Review Focus

- A cross-course or missing `unitId` must be rejected for both file and YouTube creation.
- Image preview failure must fall back to an icon without breaking the row.
- Null-unit legacy resources must remain visible and movable into a real unit.
- Student and restricted-TA views must not gain editing or statistics controls.
- Long titles, filenames and summaries must wrap without horizontal overflow on narrow screens.

---

### Task 1: Retire tags and enforce unit-scoped resource creation

**Files:**
- Create: `Database/033_resource_units_retire_tags.sql`
- Modify: `Controllers/CourseToolsController.cs`
- Modify: `Controllers/YouTubeResourcesController.cs`
- Delete: `Controllers/ResourceTagsController.cs`
- Delete: `Services/ResourceTagPolicy.cs`
- Modify/Test: `tests/AiTeachingAssistant.Tests/ResourceMaterialPolicyTests.cs`
- Modify/Test: `tests/AiTeachingAssistant.Tests/CourseUnitTests.cs`

**Interfaces:**
- Produces resource list objects with `unitId` and without `tags`/`tagsVersion`.
- File upload and `YouTubeResourceInput` require non-null `UnitId` belonging to the same course.

- [x] Add failing tests proving missing/cross-course units are rejected and the resource list includes `unitId` without tag fields.
- [x] Run targeted .NET tests and verify the new assertions fail for the current nullable/tagged behavior.
- [x] Add migration 033 dropping `course_resources.tags` and `tags_version`; remove tag controller/policy and tag request/query fields.
- [x] Update file and YouTube creation SQL to require and validate a same-course unit.
- [x] Run targeted .NET tests and the backend suite; expect all enabled tests to pass.

### Task 2: Build unit-grouped resource presentation helpers

**Files:**
- Create: `ClientApp/src/utils/courseResourceList.js`
- Create/Test: `ClientApp/src/utils/courseResourceList.test.js`
- Create: `ClientApp/src/components/ResourceVisual.vue`
- Create/Test: `ClientApp/src/components/ResourceVisual.test.js`
- Modify: `ClientApp/src/utils/materialMetadata.js`
- Delete: `ClientApp/src/utils/resourceTags.js`
- Delete: `ClientApp/src/utils/resourceTags.test.js`

**Interfaces:**
- `groupResourcesByUnit(resources, units)` returns ordered `{key,name,description,legacy,items}` sections.
- `searchResources(resources, query)` searches title, filename and summary only.
- `ResourceVisual` consumes `{item, endpoint}` and renders lazy authenticated image preview with icon fallback, or the existing format icon for non-images.

- [x] Write failing utility tests for unit order, legacy null-unit section and tag-free search.
- [x] Write failing component tests for image thumbnail loading/fallback and document/YouTube icon behavior.
- [x] Run the targeted Vitest files and verify failures reflect missing helpers/component.
- [x] Implement the helpers and `ResourceVisual` with object URL cleanup and accessible labels.
- [x] Run targeted tests and expect all to pass.

### Task 3: Replace long material cards with compact unit rows

**Files:**
- Modify/Test: `ClientApp/src/components/CourseTools.vue`
- Modify/Test: `ClientApp/src/components/CourseTools.test.js`
- Modify: `ClientApp/src/components/FilePreview.vue` only if needed to keep expanded PDF/image preview behavior separate from the list thumbnail.

**Interfaces:**
- Consumes Task 1 `unitId` and Task 2 grouping/visual components.
- Uses existing `PUT /courses/{course}/units/activities/material/{id}` to move materials.

- [x] Replace tag tests with failing tests for unit sections, required unit creation, legacy section, move-unit control, metadata alignment and role-gated actions.
- [x] Run `CourseTools.test.js` and verify the current card/tag UI fails the new expectations.
- [x] Remove tag/date-grouping UI and wire search, unit grouping, required upload unit and move-unit calls.
- [x] Render compact rows with visual, two-line summary, aligned metadata/actions, inline editors and responsive no-overflow styles.
- [x] Run component tests and the full frontend suite; expect all to pass.

### Task 4: Migration, runtime verification and handoff

**Files:**
- Modify: `.ai/agents.md`
- Modify: `.ai/elearning.md`
- Modify: `.ai/history.md`
- Modify: `docs/superpowers/specs/2026-11-11-materials-by-unit-design.md` status to implemented.

**Interfaces:**
- Applies migration 033 to the authorized 7173 public database after a clean application build.

- [x] Stop only the exact 7173 process, build .NET and Vue in normal project outputs, and run backend/frontend suites.
- [x] Apply migrations through the existing migrator; verify 001–033 and confirm resource counts/content hashes survive while tag columns are absent.
- [x] Start only `https://localhost:7173`; verify session and Swagger return 200 and no alternate project ports are started.
- [x] Run `git diff --check`, review the complete diff against the spec, and update persistent handoff documents with exact test/migration results.

