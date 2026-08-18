# M3 Analyzer Engine — Corrected Integration Sign-off

Status date: 2026-08-18
Governing design: Technical Design v0.2
Parent issue: #32
Integration issue: #36

## Decision

M3 is **APPROVED**.

The corrected combined-state CI run `32175231795` passed restore, formatting, all automated tests, and plugin/package build with M3-A, M3-B, and M3-C simultaneously present.

The earlier M3 sign-off was merged prematurely while implementation PRs #38 and #39 were still open. That bookkeeping error was discovered before continuing M4-B. Issues #32 and #36 were reopened, the missing implementation work was independently reviewed, defects were fixed, both PRs were actually merged, and this corrected sign-off revalidated the real combined repository state before authorizing further workspace work.

M4-A had already landed during the inconsistent ledger state. It is limited to pure shared workspace selection state and does not depend on unfinished analyzer behavior, so it remains. M4-B may resume after this corrected sign-off change is merged.

## M3-A — Canonical indexes

Issue #33 / PR #37 — **APPROVED**.

Merged commit: `607b2c9530c7a94f24c7248df79e4317f6c9fbd7`.

- `ActorIndex` validates unique pull-local actor IDs and provides stable lookups.
- `EventIndex` preserves canonical input order and rejects non-increasing sequence or duplicate event IDs rather than silently repairing them.
- Core type/source/target/involved/action/status queries are pre-indexed for analyzer use.
- Analysis indexes remain pure and source-agnostic.

## M3-B — Registry, dependencies, execution, failure isolation

Issue #34 / PR #38 — **APPROVED** after corrective lead review.

Merged commit: `d0b026df60213fb1aa453b2f06637c2c53d73368`.
Final implementation CI: `32174557211` — restore, formatting, tests, and plugin/package build passed.

Implemented:
- `IAnalyzerModule`, `AnalyzerContext`, result sink, scope/configuration, and declared dependency-result access;
- `AnalyzerRegistry` composition without hard-coded engine dispatch;
- deterministic topological dependency ordering;
- explicit missing/self/cyclic dependency failures;
- actor/event indexes built once per analysis run;
- per-module result buffering, failure isolation, dependent skip behavior, and cancellation propagation;
- analyzer result ownership and globally unique result IDs.

### Lead-review defect caught and fixed

The initial PR mutated the global `AnalysisResultId` reservation set while validating a module. If a later result from that same module conflicted, the module failed but IDs from its earlier uncommitted results remained reserved, allowing a failed analyzer to poison independent analyzers executed later.

The final implementation validates the complete buffered module result set against committed global IDs first and only reserves those IDs after validation succeeds. Existing coverage proving that a failed module does not reserve otherwise-valid result IDs now matches the implementation contract.

## M3-C — First generic analyzer / golden fixture

Issue #35 / PR #39 — **APPROVED** after corrective lead review.

Merged commit: `924f57a517a85d8ede108192aae4d8a205c542b9`.
Final implementation CI: `32174764060` — restore, formatting, tests, and plugin/package build passed.

Implemented:
- `DeathEventAnalyzer` as a deliberately small generic vertical slice, not the later M5 causal death-analysis rewrite;
- structured `AnalysisResult` output from canonical `DeathEvent` facts;
- stable event/actor/time evidence and provenance confidence;
- deterministic result identity from pull ID + analyzer ID + event ID;
- clean unsupported behavior for pulls with no death events;
- golden canonical fixture covering structured ordering, evidence links, time ranges, confidence/metrics, identity, serialization, and repeat-run determinism.

### Lead-review defect caught and fixed

The original golden fixture hard-coded two expected result GUIDs that did not match the actual `StableAnalysisResultIdentity` algorithm. The fixture now derives expected IDs through the analysis identity contract and independently verifies repeated runs produce identical structured serialization. This tests determinism without encoding incorrect hand-calculated constants.

## Combined architecture review

- [x] Modules can be added through registry composition without editing engine dispatch logic.
- [x] Dependency execution order is deterministic and dependency errors are explicit.
- [x] Indexes expose canonical data without repeated whole-stream scans for core indexed paths.
- [x] Module output is committed atomically only after successful validation.
- [x] One analyzer failure does not discard independent analyzer results.
- [x] Failed/unsupported dependencies prevent dependent execution.
- [x] Requested cancellation propagates rather than becoming a module failure.
- [x] The first generic analyzer emits structured evidence tied to stable canonical event/actor/time identities.
- [x] Analyzer contracts/modules remain free of Dalamud services, ImGui, FFLogs DTO/client types, network clients, and persistence implementations.
- [x] No M4 workspace rendering, M5 broad analysis, M6 FFLogs, M7 job analyzer, or M8 encounter/WTFDiG implementation leaked into M3.
- [x] M3-A, M3-B, and M3-C are actually merged on `main`.
- [x] Corrected combined-state CI run `32175231795` passed restore, formatting, tests, and plugin/package build.

## M4 authorization

M3 is now truthfully complete. Once this correction PR is merged, issues #32/#36 may be reclosed and M4-B — focused analyzer workspace panel contracts/shells — is authorized.
