# Clause-level classification tags

**Goal:** Distinct facts within one sentence get their own exact-source annotations and inline badges, using Chinese/English clause punctuation rather than treating every sentence as one classification unit.

**Finding:** PhaseAnnotatedAnswer already renders badges at each validated excerpt's end and has no Chinese-period-only grouping rule. Coarse annotation spans from the model cause sentence-sized code bundles. Splitting a historical multi-code span heuristically would invent unsupported code-to-clause mappings.

**Approach:** Preserve the existing renderer, source offsets, validation, schema and saved history. Clarify the formal annotation output instructions: shortest complete meaningful excerpt, separate distinct facts at commas/enumeration commas/semicolons/colons/sentence punctuation/newlines; do not mechanically split decimals, dates, abbreviations or parentheses; keep genuine same-excerpt multi-code cases. Version new formal prompts. Raw and Studio teaching instructions, provider profiles, sampling, schema limits and authorization stay unchanged.

- [x] Add renderer regressions for Chinese/English punctuation and preserved original/UTF-16 text; add failing wire tests for clause instructions and versioned formal requests.
- [x] Add the small shared annotation-granularity instruction and update formal prompt versions while retaining all professor rules.
- [x] Run synthetic actual-provider punctuation checks without real answers or DB writes; report any model compliance limits honestly.
- [x] Verify backend isolated-schema regression/upgrade, frontend tests and browser badge behavior; build/deploy only 7173.
- [x] Update project handoff/spec/history and annotation/Ollama docs, noting saved broad annotations are not silently rewritten. Preserve dirty work and do not commit/push.

## Ruling after live evidence

Prompt wording alone still produced whole-answer excerpts in all six short Omni checks. The formal response schema therefore constrains text to a bounded list of exact punctuation-delimited source clauses when >=2 unique choices, <=128 clauses and every clause fits the existing quote limit. Original answerText remains intact. Protected brackets/abbreviations/numbers and short label:value forms stay together. If these bounds cannot be met, retain the existing free-excerpt schema instead of truncating or inventing text. The model still chooses all codes; no code-to-clause guesswork or historical rewriting. Clinical classification remains imperfect (including excessive codes on age clauses); this is formatting/source granularity, not a clinical correctness gate. No new fields or database migration.

## Completed verification

Backend433/433 in a disposable schema plus upgrade fingerprints, Vue331, annotation browser8, VS build passed. Synthetic Omni6/Llama2 enforced-granularity results were complete but still semantically imperfect; no classification correctness claim. Existing limits retained (A6284/B7717/C6497/D6219 bytes). Read-only review found no actionable issues. Unique7173 PID41924 defaultOmni, prompt versions reflected from deployed DLL, /courses200/anonymousAI401 and old ports checked. No migration/production seeding/commit/push; old annotations remain unchanged.
