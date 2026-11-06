# Calibration implementation report

Production prompt version: `rubric-evidence-v2`.

This change removes the previously embedded historical answer and its score from production prompts. Grading remains a single model call, with evidence findings before the score. There is no keyword classifier, score override, second grading call or answer-key lookup. Optional case facts are explicitly separated from the submitted answer. The original application had no separate case-facts input; no stored reference case was found in its request path.

## Files changed for this task

| File | Change |
| --- | --- |
| `.gitignore` | Ignore synthetic evaluation run outputs. |
| `README.md` | Link the rubric evaluation workflow. |
| `Controllers/HomeController.cs` | Send per-request case/answer context, invoke schema assessment, validate and render findings; suppress assessment private thinking. |
| `Models/ChatModels.cs` | Add optional case input, structured assessment response, Ollama format/options fields. |
| `Models/AssessmentModels.cs` | Findings, evaluation result and explicit sampling models. |
| `Options/OllamaOptions.cs` | Assessment sampling configuration. |
| `Program.cs` | Register development diagnostic capture. |
| `Services/AnalysisStageInstructions.cs` | Remove answer-specific calibration; operational rubric/evidence rules and version. Keep stage scopes. |
| `Services/AssessmentContract.cs` | JSON input envelope and schema generator shared by production and evaluation. |
| `Services/AssessmentResultParser.cs` | Strict contract validation, score mapping and evidence rendering. No semantic grade correction. |
| `Services/OllamaClient.cs` | Send Ollama JSON schema and explicit sampling; capture serialized request; safely handle malformed API JSON and avoid logging clinical error bodies. |
| `Services/OllamaPayloadDiagnostics.cs` | Opt-in Development-only redacted, debugger-only, process-local snapshot. |
| `Views/Home/Index.cshtml` | Optional case reference fields on review and comparison forms. |
| `wwwroot/js/site.js` | Include case references with the submitted assessment request. Existing before/after score cards remain. |
| `tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj` | Copy independent fixture for contamination/coverage tests. |
| `tests/AiTeachingAssistant.Tests/LangChainMemoryOrchestratorTests.cs` | Verify all-stage case/history isolation and generic prompt contract; update client fake. |
| `tests/AiTeachingAssistant.Tests/AssessmentPayloadTests.cs` | Capture actual HTTP payload; test schema, sampling, diagnostic restrictions/redaction and absence of answer key. |
| `tests/AiTeachingAssistant.Tests/AssessmentResultParserTests.cs` | Valid/invalid contract, scores, comparisons, missing-case and parse-failure tests. |
| `tests/AiTeachingAssistant.Tests/EvaluationFixtureTests.cs` | Four-stage/level coverage and fixture/target exclusion from runtime context. |
| `tests/AssessmentEvaluation/AssessmentEvaluation.csproj` | Separate opt-in evaluation executable. |
| `tests/AssessmentEvaluation/Program.cs` | Live synthetic evaluation, exact payload/metadata/findings capture and provisional consistency/ranking reports. |
| `tests/AssessmentEvaluation/fixtures.json` | Independent synthetic 1–4 targets across four stages, paraphrase, separately marked historical answer. All targets await qualified review. |
| `tests/AssessmentEvaluation/README.md` | Usage, boundaries, sampling, diagnostics and human evaluation workflow. |
| `tests/AssessmentEvaluation/assembled-request.example.json` | Exact payload captured by the HTTP handler for the historical no-case request; synthetic content only. No expected score supplied. |
| `tests/AssessmentEvaluation/IMPLEMENTATION.md` | This report. |

The worktree also contains earlier scoring UI changes; this task did not modify `wwwroot/css/site.css` or `Views/Shared/_AssessmentRubric.cshtml`.

## Final request structure

```text
POST /api/chat
model: llama3.1:8b
stream: false
options: temperature=0, seed=42, top_p=0.9, top_k=40, num_ctx=16384, num_predict=4096
format: generated JSON schema object
messages:
  system: configured zh-TW language instruction
  system: generic evidence/rubric instructions + selected stage scope
  system: JSON schema text
  user: JSON { caseFacts: string|null, answers: [{version: current, text: answer}] }
```

Comparisons use two answer objects (`before`, `after`) with one shared case reference. There are no prior answers, prior feedback or another stage's history. The schema contains an `evaluations` array; each evaluation includes supported, partially supported, unsupported, unverified and omitted claims, then reasoning quality, score and justification. Findings contain `claim`, `evidence`, `explanation`. The root also contains `comparisonFeedback`. The complete captured JSON is in `assembled-request.example.json`.

## Verification

- Local software regression suite: **57 passed, 0 failed, 0 skipped**.
- Web app and evaluation executable build successfully. Evaluation build reported NU1900 because the NuGet vulnerability feed was unavailable; this is not a completed vulnerability audit.
- JavaScript syntax check and `git diff --check`: passed (Git emits existing line-ending normalization notices).
- On-prem endpoint reachable; `/api/version` reports **0.40.4**.
- Model: **llama3.1:8b**, Q4_K_M, digest `46e0c10c039e019119339687c3c1757cc81b9da49709a3b3924863ba87ca666e`.
- Initial unseen `premorbid-1` attempt timed out at 180 seconds; that initial batch was stopped. Its JSONL payload/error record is retained locally.
- Historical no-case answer completed: **2/4**, contract valid. This does **not** establish grading accuracy: evidence fields contained only commas, reasoning quality was garbled, and justification did not explain the grade. The historical answer is not an independent unseen test and had no answer key in its request.

- Final independent run (`20261021T132638Z`) planned 10 samples (five unseen premorbid responses including a paraphrase, two repeats each). The first sample timed out at 180 seconds; fail-fast recorded **1 attempted, 0 valid contracts, 9 unexecuted**. No weak/strong ranking, repeat consistency, paraphrase stability or clinical feedback/grade agreement result was obtained. Other stages are covered by fixtures and software isolation checks, not completed live evaluation.
- Local run artifacts: `results/20261021T132058Z.jsonl` (initial timeout), `results/20261021T132450Z.jsonl` and matching report (historical response), `results/20261021T132638Z.jsonl` and matching report (independent timeout and unexecuted count). These files are intentionally gitignored; the complete historical request without an answer key is also provided as the tracked `assembled-request.example.json`.

All automated target scores are provisional. Human review fields remain blank. No claim of improved grading accuracy is supported by the completed results above.
