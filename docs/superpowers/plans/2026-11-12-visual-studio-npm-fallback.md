# Visual Studio npm Fallback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Visual Studio Build/F5 use its bundled npm when npm is absent from PATH.

**Architecture:** Resolve an overridable `NpmExecutable` MSBuild property from Visual Studio's version-independent MSBuild extensions directory when that npm exists; otherwise use `npm`. For the bundled toolchain, prepend its directory to the two frontend Exec environments so package scripts resolve the adjacent Node executable. Keep the current targets and invocation order unchanged.

**Tech Stack:** MSBuild, PowerShell behavior test, npm/Vite

**Spec:** `docs/superpowers/specs/2026-11-12-visual-studio-npm-fallback-design.md`

## Global Constraints

- Do not hard-code a Visual Studio version, edition, or user path.
- Preserve `SkipFrontendBuild`, incremental restore, publish reuse, and build failure behavior.
- Work in the current main workspace because the user explicitly requested the existing single-directory workflow and direct push.

## Review Focus

- Visual Studio Node/npm path contains spaces: quote the selected executable, remove Node/npm from PATH, and prove an npm package script can resolve the adjacent Node executable.
- No Visual Studio npm exists: retain the PATH-based npm fallback already covered by the behavior suite.
- Caller overrides `NpmExecutable`: do not overwrite a non-empty value.
- `npm ci` and `npm run build` must use the same resolved executable.
- Normal publish must continue to invoke the Vue build once.

---

### Task 1: Resolve and verify Visual Studio npm

**Files:**
- Modify: `tests/BuildPipeline.Tests.ps1`
- Modify: `AiTeachingAssistant.csproj`
- Modify: `README.md`
- Modify: `.ai/agents.md`
- Modify: `.ai/elearning.md`
- Modify: `.ai/history.md`

**Interfaces:**
- Consumes: Visual Studio's `$(MSBuildExtensionsPath)` and optional caller-supplied `NpmExecutable`.
- Produces: one quoted npm executable used by both frontend MSBuild targets.

- [ ] **Step 1: Write the failing behavior test**

Move the fake npm outside PATH, pass a fake Visual Studio npm path containing spaces, invoke `BuildVue`, and assert `run build` is logged.

- [ ] **Step 2: Run the behavior test and verify RED**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\BuildPipeline.Tests.ps1`
Expected: FAIL with MSBuild error 9009 because the current project still invokes bare `npm`.

- [ ] **Step 3: Implement minimal MSBuild resolution**

Add derived Visual Studio npm and overridable `NpmExecutable` properties. Quote `$(NpmExecutable)` in both Exec commands.

- [ ] **Step 4: Run behavior and project suites**

Run the behavior test, Vue test suite, and .NET solution tests. Expected: behavior passes, Vue 100/100, .NET 214 pass with 19 conditional skips.

- [ ] **Step 5: Update documentation, review, commit, and push**

Record the fallback behavior and validation in README and persistent project documents; obtain final review, then commit and push to both configured `origin/main` URLs.
