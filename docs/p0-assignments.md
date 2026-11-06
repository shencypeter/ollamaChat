# P0 — individual assignments and answers

The P0 path is now implemented: **course staff creates a draft → edits and publishes it → student saves/reloads answers → student submits → course staff reviews the submitted snapshot**. This uses the existing PostgreSQL schema; no additional migration or answer-key/AI changes are required.

## Use the workflow

1. Open **我的課程 → 課程作業**. Only actual active course members can access it.
2. A course teacher or TA chooses **建立個人作業**, supplies a title, optional case/instructions, optional opening/deadline dates and 1–20 text questions. Each question can optionally reference one of the four analysis stages. The **加入四階段題目** shortcut adds four editable prompts; it does not invoke AI.
3. Save/edit the draft, then publish. Drafts are hidden from students. Published questions, instructions and dates are locked. Staff may close a published assignment; there is no P0 reopen/unsubmit workflow.
4. A course student opens a published assignment, saves drafts to PostgreSQL, reloads them and submits all answers. Blank answers are allowed in drafts, not in final submissions. Each answer has a 16,000-character limit.
5. Submitted work is read-only. Staff sees only submitted work, including the submitter's full account name, server submission timestamp, current answers and saved revisions. Student drafts are not exposed by staff review APIs.

P0 is individual text work with one submission attempt per student per assignment. Group assignments, file attachments, late-submission exceptions, resubmission, human grading and AI evaluation tied to submissions remain later phases. Existing standalone AI tools are unchanged. The supplied screenshot prioritizes AI as P1; there is no automatic grading in this path.

## Persistence and consistency

- `assignments` / `assignment_questions`: course-scoped draft, published and closed assignments.
- `submissions`: exactly one individual attempt (`attempt_no=1`); student identity comes from the authenticated cookie, never a request-supplied owner ID.
- `submission_answers` / `answer_revisions`: a saved current pointer and historical versions per question. Unchanged answers do not create redundant revisions. Current revisions are marked submitted atomically with the final submission timestamp.
- `audit_events`: assignment create/update/publish/close and submission save/submit actions. Audit metadata does not copy answer text.
- Transactions serialize assignment mutation and submission saves with a row lock. A draft's expected version rejects stale-tab or simultaneous overwrite attempts with 409. Staff edits also check an edit checksum. Published/submitted records cannot be rewritten through these APIs.
- Opening time is inclusive; deadline is exclusive. Dates are stored as UTC and displayed in the device's local timezone. The server clock is authoritative. Saving and submitting are both blocked before opening, at/after the deadline, for closed assignments and for archived courses.
- Students can still read their own previously saved/submitted content after closing/deadline. Unsaved local text remains selectable if the page becomes read-only; leaving/reloading warns before discarding unsaved changes.
- Course-specific membership controls staff permissions. A platform Professor who is not course staff has no special access. Students cannot enumerate classmates' submissions or impersonate another student by supplying IDs.

## API

| Method / path | Purpose |
| --- | --- |
| `GET /api/courses/{courseId}` | Current member's course metadata |
| `GET /api/courses/{courseId}/assignments` | Staff sees all individual assignments; students see published/closed |
| `POST /api/courses/{courseId}/assignments` | Course teacher/TA creates a draft |
| `GET /api/assignments/{id}` | Authorized assignment and questions |
| `PUT /api/assignments/{id}` | Edit a draft with `expectedEditVersion` |
| `POST /api/assignments/{id}/publish` | Publish a draft |
| `POST /api/assignments/{id}/close` | Close a published assignment |
| `GET /api/assignments/{id}/my-submission` | Student's own saved/submitted work |
| `POST /api/assignments/{id}/my-submission` | `{expectedVersion, submit, answers:[{questionId,text}]}` |
| `GET /api/assignments/{id}/submissions` | Staff sees submitted roster only |
| `GET /api/assignments/{id}/submissions/{submissionId}` | Staff reads a submitted snapshot/history |

All writes require existing cookie authentication and CSRF validation. Cross-assignment question IDs, duplicate/missing question IDs and incomplete final submissions are rejected before saving. Stale versions/state conflicts return 409 without overwriting content. Draft validation failures roll back the complete transaction.

## Verification

- Backend regression/policy suite: **72 passed**.
- Frontend/component/date tests: **8 passed**.
- Chrome real-API/browser suite: **4 passed** (includes previous portal tests).
- Real database probes verified draft privacy, course access, TA authoring/review, cross-assignment rejection, revision persistence, stale/concurrent save rejection, one-way submission locking, deadline/open-time enforcement and staff-only submitted review.
- The browser completed staff publishing, student draft save/reload, final submission and staff review. Desktop light/dark and mobile screenshots were visually reviewed. Assignment tests do not mock the assignment API or database and make no LLM inference calls.
- Temporary QA records are scoped by verified run-specific UUID/email, then cleaned up in a transaction, including answer revision pointers and their dependent rows. Existing real users/courses are not removed.

## Files

`Models/AssignmentModels.cs`, `Services/AssignmentPolicy.cs`, `Services/AssignmentWorkflow.cs`, `Controllers/AssignmentsController.cs`, course metadata/count changes in `CoursesController.cs`, DI in `Program.cs`; Vue assignment editor, course detail and assignment views, routes/navigation/course links/styles; date utilities, policy/date tests and `ClientApp/tests/e2e/assignments.spec.js`; scoped QA cleanup changes in `tests/PortalSmoke`; root README and these delivery notes.


## Local release verification

The full publish (including Vue install/build) succeeded. The same four Chrome/API tests also passed against the published artifact at `http://127.0.0.1:5097`, not just the development build. Local connection settings and QA state were confirmed absent from publish output. The preview was restarted on that URL; existing sessions may need to sign in again. The temporary test API on port 5107 has been stopped.
