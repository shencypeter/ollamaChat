# Feedback Rounds and Performance Dialog

User requested direct implementation; clarification confirmed diff means student answers/questions, not System Prompt versions.

- [x] Persist per-assistant performance metrics and explicit immutable revisesMessageId links (038), preserve legacy nulls; real DB tests for ownership/stage/role/retry/upgrade.
- [x] Show four-stage round sidebar, paired answer/feedback, explicit revise/submit/manual next-stage; preserve Raw lifecycle and teacher readonly review, protect unsent drafts.
- [x] Native performance dialog and bounded Unicode answer diff for any two authorized revisions; metrics units/availability, XSS, whitespace and large input tests.
- [x] FullVue241/DB330/Chrome15, standard VS build, apply038 to existing7173 only, update spec/history/report. No grading, prompt-editor or Raw persistence.
