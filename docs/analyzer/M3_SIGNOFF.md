# M3 Analyzer Engine — Integration Sign-off

Status date: 2026-08-18
Governing design: Technical Design v0.2
Parent issue: #32
Integration issue: #36

## Decision

M3 is **APPROVED** once this sign-off branch passes the repository CI and is merged. M4 — New workspace shell — is then authorized.

## M3-A — Canonical indexes

Issue #33 / PR #37 — APPROVED.

- `ActorIndex` validates unique pull-local actor IDs and provides stable lookups.
- `EventIndex` preserves canonical input order and rejects non-increasing sequence or duplicate event IDs rather than silently repairing them.
- Core type/source/target/involved/action/status queries are pre-indexed for analyzer use.
- Analysis indexes remain pure and source-agnostic.

## M3-B — Registry, dependencies, execution, failure isolation

Issue #34 / PR #38 — APPROVED.

- `AnalyzerRegistry` composes modules by ID; engine dispatch does not hard-code module types.
- Dependency resolution is deterministic and rejects missing, self, and cyclic dependencies explicitly.
- Modules receive read-only access only to declared dependency results.
- Actor/event indexes are built once per analysis run.
- Analyzer output is buffered per module and committed only after successful completion.
- One analyzer failure does not discard independent analyzer results.
- Dependents of failed/unsupported modules are skipped rather than executed with invalid assumptions.
- Requested cancellation propagates instead of being reported as an analyzer failure.
- Result ownership and result-ID uniqueness are validated.

## M3-C — First generic analyzer / golden fixture

Issue #35 / PR #39 — APPROVED.

- `DeathEventAnalyzer` is the deliberately small M3 vertical slice, not the later M5 causal death-analysis rewrite.
- It consumes canonical `DeathEvent` facts and emits structured `AnalysisResult` values with stable event/actor/time evidence.
- Result identity is deterministic from pull ID + analyzer ID + event ID.
- Titles identify the observed target where available, while the summary explicitly avoids classifying blame/causality prematurely.
- Provenance confidence is carried into the result.
- No-death pulls cleanly report the analyzer as unsupported.
- The golden canonical fixture locks down result ordering, actor/evidence links, time ranges, confidence/metrics, repeat-run serialization stability, and event-linked identity.

## Combined architecture review

- [x] Modules can be added through registry composition without editing engine dispatch logic.
- [x] Dependency execution order is deterministic and dependency errors are explicit.
- [x] Indexes expose canonical data without requiring repeated whole-stream scans for core paths.
- [x] Failure isolation preserves independent module results.
- [x] Failed/unsupported dependencies prevent dependent execution.
- [x] Cancellation semantics are preserved.
- [x] Analyzer output remains structured/evidence-backed and source-agnostic.
- [x] No Dalamud services, ImGui, FFLogs DTO/client, network client, or persistence implementation leaked into analyzer contracts/modules.
- [x] No M4 workspace, M5 broad analysis suite, M6 FFLogs, M7 job, or M8 encounter/WTFDiG implementation leaked into M3.
- [x] Each implementation PR passed repository CI before merge.

## M4 authorization gate

This sign-off file is intentionally separate from implementation. The sign-off PR must pass restore, formatting, all automated tests, and plugin/package build on the combined M3 state before #32/#36 are closed and M4 implementation begins.
