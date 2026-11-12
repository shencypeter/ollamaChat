# Visual Studio Frontend Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make normal Visual Studio and `dotnet build` runs restore changed Vue dependencies and rebuild the SPA before ASP.NET starts.

**Architecture:** Add one reusable MSBuild dependency-restore target and one Vue build target to the web project. Normal Build invokes them, while Publish reuses the same target and only performs its existing generated-file packaging step.

**Tech Stack:** MSBuild, ASP.NET Core/.NET 8, npm, Vite, PowerShell test harness

**Spec:** `docs/superpowers/specs/2026-11-12-visual-studio-frontend-build-design.md`

## Global Constraints

- Generated `wwwroot/portal` files remain ignored and uncommitted.
- No Vite development server or additional port is introduced.
- `SkipFrontendBuild=true` disables npm restore/build for explicit isolated workflows.
- Missing npm or failed Vite compilation must fail the .NET build.
- Publish must not execute the Vue build twice.
- Do not commit or push unless the user explicitly asks.

## Review Focus

- A missing `node_modules/.package-lock.json` triggers `npm ci` before `npm run build`.
- An unchanged lock marker skips `npm ci` on the next build but still runs `npm run build`.
- A newer `package-lock.json` triggers `npm ci` again.
- `SkipFrontendBuild=true` invokes neither npm command.
- Publish gathers the generated assets without a second npm build invocation.

---

### Task 1: Reusable Visual Studio Vue build target

**Files:**
- Create: `tests/BuildPipeline.Tests.ps1`
- Modify: `AiTeachingAssistant.csproj`
- Modify: `README.md`
- Modify: `.ai/agents.md`
- Modify: `.ai/elearning.md`
- Modify: `.ai/history.md`

**Interfaces:**
- Consumes: `ClientApp/package-lock.json`, npm's `ClientApp/node_modules/.package-lock.json` installation marker, existing Vite `npm run build`, and `SkipFrontendBuild`.
- Produces: MSBuild targets `RestoreVueDependencies` and `BuildVue`; the existing publish collection target depends on `BuildVue` rather than running npm itself.

- [ ] **Step 1: Write the failing MSBuild behavior test**

Create a PowerShell harness that copies the project file into an isolated temporary fixture, supplies a fake `npm.cmd` that logs commands and creates the dependency marker, and invokes `dotnet msbuild -t:BuildVue`. Assert the five Review Focus behaviors from literal command-log counts and ordering.

- [ ] **Step 2: Run the test and verify RED**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/BuildPipeline.Tests.ps1`

Expected: FAIL because target `BuildVue` does not exist.

- [ ] **Step 3: Implement the shared MSBuild targets**

Add incremental `RestoreVueDependencies` with lockfile input/marker output, add `BuildVue` with `BeforeTargets="Build"` and dependency on restore, and change `BuildVueForPublish` to depend on `BuildVue` while retaining only `ResolvedFileToPublish` collection. Apply the same `SkipFrontendBuild` condition to restore/build.

- [ ] **Step 4: Run the behavior test and verify GREEN**

Run the PowerShell harness again.

Expected: PASS for missing marker, unchanged marker, changed lockfile, skip property, and single publish-target invocation.

- [ ] **Step 5: Verify real builds**

Precisely stop only the verified current project process if it locks standard output. Run normal `dotnet build AiTeachingAssistant.csproj --no-restore` and confirm Vite executes and the build succeeds. Run an isolated `dotnet publish` with normal frontend build and confirm the log contains one Vite build plus the published `wwwroot/portal/index.html`.

- [ ] **Step 6: Verify application and regression suites**

Run the full Vue suite and full .NET suite, restart only `https://localhost:7173`, and confirm `/courses` returns HTTP 200 with the newly generated bundle while 5097/5107/5180 remain clear.

- [ ] **Step 7: Update documentation and persistent project records**

Update startup instructions to state that Visual Studio Build/F5 now performs the frontend build automatically while direct frontend development remains optional. Record RED→GREEN evidence, real build/publish results, test counts, bundle name, and final PID in the three `.ai` files.
