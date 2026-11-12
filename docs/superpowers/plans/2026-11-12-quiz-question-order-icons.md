# Quiz Question Order Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the quiz question editor's drag, move-up, and move-down text controls with accessible Lucide icon buttons.

**Architecture:** Keep the existing `AssignmentEditor` sorting state and event handlers unchanged. Change only the rendered controls and their styling contract, using the icon package already shared by the Vue application.

**Tech Stack:** Vue 3, `@lucide/vue`, Vitest, Vue Test Utils

**Spec:** `docs/superpowers/specs/2026-11-12-quiz-question-order-icons-design.md`

## Global Constraints

- Do not add Font Awesome or another icon dependency.
- Preserve drag/drop, move-up, move-down, disabled-state, and saved question order behavior.
- Preserve Chinese accessible labels and add matching tooltips.
- Modify only the shared question editor controls, not unit or activity ordering.
- Do not commit or push unless the user explicitly asks.

## Review Focus

- The first question's up button remains disabled while still exposing its accessible name.
- The last question's down button remains disabled while still exposing its accessible name.
- Dragging continues to use the real draggable button and the existing drop path.
- Icon buttons contain no visible text that can reintroduce header crowding.
- Mobile wrapping continues to use the existing `question-editor-header` layout without a new dependency or global style override.

---

### Task 1: Accessible question-order icon controls

**Files:**
- Modify: `ClientApp/src/components/AssignmentEditor.vue`
- Modify: `ClientApp/src/components/AssignmentEditor.test.js`
- Modify: `.ai/elearning.md`
- Modify: `.ai/history.md`
- Modify: `.ai/agents.md`

**Interfaces:**
- Consumes: existing `move(from, to)`, `dragged`, drag/drop handlers, and global `.icon-button` styling.
- Produces: icon-only controls with `aria-label`/`title` values `拖曳題目排序`, `題目 N 上移`, and `題目 N 下移`.

- [ ] **Step 1: Write the failing component test**

Add a test that mounts two quiz questions and asserts:

- the three controls for the first question are found by accessible label;
- each control uses `.icon-button` and renders an SVG icon;
- the controls contain no visible `拖曳`, `上移`, or `下移` text;
- first-up and last-down remain disabled;
- clicking the second question's up icon changes the submitted question order.

- [ ] **Step 2: Run the focused test and verify RED**

Run: `node node_modules/vitest/vitest.mjs run src/components/AssignmentEditor.test.js`

Expected: FAIL because the current text buttons do not use `.icon-button`, do not render SVG icons, and use non-specific move labels.

- [ ] **Step 3: Implement the minimal icon control change**

Import `GripVertical`, `ArrowUp`, and `ArrowDown` from `@lucide/vue`. Render each ordering control as an `.icon-button`, retain the drag attributes/handlers, use question-number-specific accessible labels for move controls, add matching `title` attributes, and leave `move` plus all persistence code untouched.

- [ ] **Step 4: Verify GREEN and the full frontend suite**

Run focused test, then: `node node_modules/vitest/vitest.mjs run`

Expected: focused test passes; all frontend tests pass with zero failures.

- [ ] **Step 5: Build and runtime smoke test**

Run Vite production build, rebuild the standard .NET output after precisely stopping only the current project process if it locks the DLL, restart only `https://localhost:7173`, and confirm `/courses` returns HTTP 200 while 5097/5107/5180 remain clear.

- [ ] **Step 6: Update persistent project records**

Record the icon behavior, RED→GREEN evidence, complete test count, build result, bundle name, and final 7173 PID in the three `.ai` files. Preserve the working tree without a commit.
