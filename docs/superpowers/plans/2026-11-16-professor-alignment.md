# Professor alignment and phase annotations

> **For agentic workers:** Use superpowers:executing-plans for integration; independently owned prompt, annotation UI and existing Studio integration tasks use parallel agents.

**Goal:** Apply the supplied four-phase methodology and annotate submitted originals without changing assessment or Raw Chat behavior.

**Architecture:** Share one code catalog and reusable teaching instructions. Course chat receives a structured internal feedback envelope; existing public content stays plain feedback with optional annotations. Existing Studio retains its response fields and case-version source. Nullable metadata is immutable and attached to the assistant reply's exact answer version.

**Tech Stack:** ASP.NET Core 8, Npgsql/PostgreSQL, Vue 3, existing Ollama transport, xUnit/Vitest/Playwright.

**Spec:** User's two task attachments and four July/March/September reference documents; operational rubric recorded in docs/ai-tuning/01-professor-rubric.md. These authorize implementation without an additional planning approval.

## Global constraints

- Exactly four analysis phases; Raw remains separate without application prompts or persistence.
- Preserve all dirty work, API routes, original answers, grades, authorization, teacher analytics and profile sampling/compatibility.
- Use synthetic tests; no clinical source text in logs or durable documentation. Only port 7173.
- UTF-16 inclusive start/exclusive end; validate exact substring and scalar boundaries. Invalid annotations are discarded, never relocated by text search.

## Review focus

- Repeated phrases and astral Unicode must retain exact offsets.
- Missing or contradictory case evidence cannot become fabricated omissions.
- Old feedback without metadata stays readable and unannotated.
- Overlap renders original text once, with multiple codes and accessible explanations.
- Failures/retries/concurrent revisions cannot attach metadata to a different answer.

## Tasks

- [x] Read four references and implement catalog/prompt revision with source-located rubric, limitations and deterministic tests (prompt agent).
- [x] Add strict feedback envelope, bounded exact-span validation, isolated provider schema and nullable immutable migration; test invalid spans, UTF-16, parser failure, three profiles, version persistence and existing authorization (root).
- [x] Implement submitted-answer annotation, four-color expandable legend and keyboard/touch tooltip; helper/component/E2E tests before code (UI agent).
- [x] Reuse teaching rules in existing case Studio; preserve original structured fields and trusted case-version material, validate annotations against saved answer (Studio agent).
- [x] Apply 039 to isolated populated 038 database; fingerprint history/grades, rerun migrations and full tests. Apply public only after upgrade passes (root).
- [x] Build Vue/.NET, inspect desktop/mobile/dark UI and live synthetic model behavior. Update six tuning documents, README, Ollama notes and .ai handoff/history (root).

Final: Vue256, full isolated DB359, Chrome20 and standard VS build passed. Public039 applied; only7173 updated. Live Breeze behavior was actually inspected and remains inadequate (false omissions/GCS/revision errors); docs03 explicitly records failed scenarios. Exact-quote plus explicit occurrence now replaces unreliable model arithmetic in internal schema, public UTF-16 remains unchanged. No model/profile substitution or artificial clinical pass. Temporary extracts and task build directories removed; original source documents and existing work preserved.

No commit or push requested. No new case-management or retrieval architecture.
