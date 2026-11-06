# Independent rubric evaluation

Production prompt version: `rubric-evidence-v2`.

The old answer-specific calibration examples and grades were removed. The production prompt contains only rubric definitions, evidence handling rules, and stage scope. No fixture or target is loaded by the web app. The `fixtures.json` content is synthetic, and **all target scores are provisional until a qualified clinical assessor reviews them**. The historical original is explicitly marked previously exposed and is excluded from generalization comparisons.

## Software checks

```powershell
dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --no-restore -p:UseSharedCompilation=false --output .codex-build/calibration-tests
```

These check transport/schema contracts, parsing, score bounds, missing-case handling, comparison mapping, prompt contamination, diagnostics, and stage memory isolation. They do not establish clinical grading accuracy.

## Opt-in model evaluation

```powershell
dotnet run --project tests/AssessmentEvaluation/AssessmentEvaluation.csproj -- --base-url http://IAN02:11434 --model llama3.1:8b --repeats 2
```

Options: `--only premorbid-1,premorbid-2,premorbid-3,premorbid-4,premorbid-4-paraphrase,historical-original-no-case`, `--max-cases 6`, `--output tests/AssessmentEvaluation/results`.

The fixture has 1–4 target responses for each of the four stages, a paraphrase of the strong premorbid response, and the historical answer without case facts. Each fixture sends **only** its stage, synthetic case facts and answer; fixture IDs, target grades, grouping and evaluation expectations are never sent. The same production client, schema, system prompt and parser are used. Each sample makes one model request. Repeats measure consistency and are not retries to obtain a desired grade. Transport failures stop the run; unexecuted samples are reported separately.

JSONL records include model ID/digest, server version, all explicit sampling settings, prompt version/hash, exact assembled payload, submitted answer, structured findings, score, parse/transport errors, provisional target and blank human-review fields. Results are gitignored and must contain synthetic information only. No private `thinking` channel is recorded. Reports give weak/strong ordering probes, provisional target differences, repeated-score spreads, paraphrase differences and unsupported-claim counts. None proves clinical correctness. Review each claim/evidence pair and fill `evidenceCorrect`, `feedbackGradeAgreement`, `assessorGrade` and notes before making accuracy claims. A qualified reviewer should also approve or revise all targets. A score difference of more than one point across a paraphrase or repeat is a useful investigation flag, not an automatic clinical verdict.

## Request contract

`POST /api/chat` sends `model`, `stream:false`, `options`, `format` (the JSON schema object), and these messages in order:

1. Configured zh-TW language system instruction.
2. General evidence/rubric instructions + exactly one selected stage's scope.
3. The JSON schema as a system instruction.
4. A user JSON object: `{ "caseFacts": "... or null", "answers": [{ "version": "current", "text": "..." }] }`. Comparison uses `before` and `after` in one request with the same case reference.

Default explicit assessment sampling: `temperature:0`, `seed:42`, `top_p:0.9`, `top_k:40`, `num_ctx:16384`, `num_predict:4096`. These can be changed through `Ollama:AssessmentSampling`; record settings for every experiment. Fixed seed/temperature do not guarantee identical results across hardware or server/model changes.

Each evaluation returns `version`, `caseEvidenceAvailable`, `supportedClaims`, `partiallySupportedClaims`, `unsupportedClaims`, `unverifiedClaims`, `importantOmissions`, `reasoningQuality`, `score`, `justification`. Every finding has `claim`, `evidence`, `explanation`. The root also has `comparisonFeedback`. Findings are public evidence summaries, not private chain of thought. Application rendering uses validated findings; the model's private thinking is not shown for assessments.

Missing case facts mean factual claims cannot be verified, supported/partially-supported findings and omissions must be empty, and the displayed score is labeled provisional. Unsupported internal inferences may still be identified. No stage gets another stage's case reference, answers or history. Cases are provided anew in each request; no reference is silently inferred from prior conversations. User-supplied references are classroom reference material, not independently authenticated clinical truth.

Server parsing rejects missing/extra/duplicate fields, invalid types, empty finding strings, noninteger/out-of-range scores, wrong version count/labels, missing comparison explanations and case-availability contradictions. It does not rewrite grades or infer clinical correctness from valid JSON. Unsupported-schema servers fail explicitly; there is no silent switch to unconstrained JSON or second inference call.

## Development payload diagnostics

Off by default. Enable only in a local Development environment:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Diagnostics__CaptureOllamaPayload = 'true'
```

Inspect the registered `OllamaPayloadDiagnostics.LatestRedactedPayload` in the local debugger. It is a single process-memory snapshot, replaced by the next request. There is no HTTP endpoint, disk log, or production capture even if the flag is set. The capture occurs at the serialization point immediately before the same bytes are posted to Ollama. Application-owned system instructions, schema, model and options are retained; case/answer text and conversation/summary content are redacted, with case availability and answer versions preserved. Full unredacted payload capture exists only in the synthetic evaluation harness. General chat and memory summaries retain their prior behavior and do not acquire assessment schema/sampling.

Ollama documents JSON schema objects in the `format` parameter: https://docs.ollama.com/capabilities/structured-outputs . Compatibility must be verified against the actual on-prem server. Schema success is independent of semantic grading quality.
