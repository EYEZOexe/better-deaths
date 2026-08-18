# FFXIV Static Analyzer — Progress & Review Ledger

Status date: 2026-08-18
Governing design: Technical Design v0.2
Primary implementation repository: `EYEZOexe/better-deaths`
Encounter knowledge/reference repository: `EYEZOexe/wtfdig`

## Status legend

- `NOT STARTED`
- `AUTHORIZED`
- `IN PROGRESS`
- `READY FOR REVIEW`
- `CHANGES REQUESTED`
- `APPROVED`
- `BLOCKED`

## Current milestone

### M4 — New Workspace Shell

**Status:** `APPROVED`

**Parent issue:** #41
**Integration/sign-off issue:** #45
**Combined-state CI:** `32178240346`

### M4-A — Shared analyzer workspace selection state

**Status:** `APPROVED`

**Issue / PR:** #42 / #46
**Merged commit:** `b6ab9d996ee0025477e2abf669ca90069b53009b`

Evidence:
- one shared state owns selected `PullId`, `ActorId`, `TimeRange`, `AnalysisResultId`, optional mechanic occurrence, and monotonic change/version notification;
- selecting an `AnalysisResult` synchronizes result/actor/time in one transition;
- changing pull clears stale cross-pull selection;
- no ImGui, Dalamud, persistence, or analyzer execution is embedded in the state model.

### M4-B — Focused panel contracts and shell panels

**Status:** `APPROVED`

**Issue / PR:** #43 / #48
**Merged commit:** `1264d90ccb79f3260c24b0def3df849150e53250`
**Final CI:** `32176263964`

Evidence:
- added focused shared panel context/interface plus Overview/Timeline/Deaths/Replay shells;
- Overview result selection, Timeline timestamp selection, Deaths evidence selection, and Replay focus all use the same shared state;
- panels do not call one another or execute analyzers/access persistence directly;
- no analyzer workspace logic was added to `RecapWindow`;
- review caught and fixed a render-path scalability problem: the Deaths shell now consumes precomputed `DeathEvents` rather than rescanning the entire full-pull stream every frame;
- repository CI passed restore, format, tests, and plugin/package build.

### M4-C — AnalyzerWindow plugin integration

**Status:** `APPROVED`

**Issue / PR:** #44 / #49
**Merged commit:** `72ec307d0189b0591a81b177450f30274285c0af`
**Final CI:** `32177896535`

Evidence:
- `AnalyzerWorkspaceDataController` owns pure `IPullStore` + `AnalyzerEngine` orchestration;
- `AnalyzerWindow` loads canonical summaries/details and runs the current analyzer registry away from ImGui `Draw`;
- rapid pull changes cancel stale detail loads and generation checks prevent stale async completions from replacing current state;
- selected pull/results/precomputed death events flow into one shared panel context;
- analyzer module failures are surfaced while independent results remain usable;
- a dedicated plugin partial registers the analyzer window through the existing `WindowSystem` without adding analyzer sections to the monolithic `Plugin.cs`/`RecapWindow.cs` bodies;
- the existing current-pull widget exposes an additive `Analyzer Workspace` launcher while existing `/bd` behavior remains unchanged;
- legacy Deaths/Replay bridges open the existing pull review rather than reaching into private death-centric replay internals;
- review corrected an attempted direct replay bridge that was not part of the public legacy review surface;
- final CI passed restore, formatting, 272 tests, and plugin/package build.

### M4-D — Combined workspace review

**Status:** `APPROVED`

Combined review:
- [x] one shared state owns pull/actor/time/result/mechanic selection;
- [x] result selection synchronizes actor/time for Timeline + Deaths/Replay consumers;
- [x] panels communicate through shared state/context only;
- [x] `AnalyzerWindow` remains separate from `RecapWindow`;
- [x] existing death recap/replay workflows remain available and were not rewritten wholesale;
- [x] canonical persistence/analysis work is queued away from the ImGui render path;
- [x] UI consumes `IPullStore`/analyzer contracts rather than concrete persistence from panels;
- [x] no M5 broad analysis, M6 FFLogs, M7 job, or M8 encounter/WTFDiG implementation leaked into M4;
- [x] M4-A/B/C are merged on `main`;
- [x] M4-D combined-state CI `32178240346` passed restore, formatting, tests, and plugin/package build.

Detailed review: `docs/analyzer/M4_SIGNOFF.md`.

**Runtime validation note:** CI cannot launch FFXIV/Dalamud. In-game confirmation of workspace opening, pull population, panel rendering, and legacy review navigation remains a manual smoke item and is not claimed as automated evidence.

**Decision:** M4 is approved. M5 — Generic Hardcore Analysis — is authorized after this sign-off PR is merged.

## Completed milestone: M3 — Analyzer Engine

**Status:** `APPROVED`

- M3-A / #33 / PR #37 — canonical actor/event indexes; merge `607b2c9530c7a94f24c7248df79e4317f6c9fbd7`.
- M3-B / #34 / PR #38 — analyzer registry/dependency execution/failure isolation; merge `d0b026df60213fb1aa453b2f06637c2c53d73368`; CI `32174557211`.
- M3-C / #35 / PR #39 — first generic `DeathEventAnalyzer` + deterministic golden fixture; merge `924f57a517a85d8ede108192aae4d8a205c542b9`; CI `32174764060`.
- corrected M3-D / #36 / PR #47 — combined state revalidated after the earlier premature sign-off; CI `32175231795`; merge `9c99abe899284a08bdfd323672c1e381d72a5d03`.

Lead review during M3 caught and fixed non-atomic global result-ID reservation and incorrect hard-coded golden result IDs before final approval.

## Completed milestone: M2 — Full-pull live recorder

**Status:** `APPROVED`

- M2-A / #22 / PR #27 — separate append-only `FullPullRecorder`, meaningful-pull finalization, zero-death support.
- M2-B / #23 / PR #28 — independent versioned canonical `IPullStore` persistence/recovery.
- M2-C / #24 / PR #29 — live normalization into canonical events/spatial facts.
- M2-D / #25 / PR #30 — additive runtime integration preserving legacy recap behavior.
- M2-E / #26 / PR #31 — combined sign-off.

## Completed milestone: M1 — Canonical domain skeleton

**Status:** `APPROVED`

Issue #14 / implementation PR #19 / sign-off PR #20. Established source-agnostic `RecordedPull`, typed `NormalizedEvent` records, stable IDs, deterministic pull-relative ordering/time, structured evidence-backed `AnalysisResult`, provenance/fidelity, serialization, and dependency-boundary tests.

## Completed milestone: M0 — Baseline and characterization

**Status:** `APPROVED`

M0 characterized lifecycle/archive/reset, death-gated legacy snapshots, persistence/schema behavior, replay round trips, and 10/30/60 display with 70-second capture / 75-second live retention before additive migration work began.

## Contracts later milestones must preserve or intentionally replace

1. Existing Better Deaths death recap remains available and its optimized short buffers remain bounded.
2. Full-pull history uses the separate canonical recorder; legacy recap lists are never made unbounded.
3. Legacy `PullDeathSnapshot` persistence remains supported during additive migration.
4. Canonical data has independent versioning/recovery behavior and unsupported versions fail explicitly.
5. All future sources normalize into canonical `RecordedPull` / `NormalizedEvent` contracts before analyzer interpretation.
6. Stable event IDs, explicit sequence, and pull-relative time remain the evidence/time-ordering basis; wall-clock timestamps are metadata.
7. Domain/analyzer contracts remain free of Dalamud services, FFLogs DTOs, ImGui types, network clients, and persistence implementations.
8. Analyzer findings remain structured and evidence-backed; rendered prose is not the source of truth.
9. Analyzer modules do not make network calls, render UI, mutate pulls, or depend on hidden global state.
10. New analyzer UI remains outside the monolithic legacy `RecapWindow`.
11. WTFDiG code/data is not ported until M8 and requires exact-path/commit provenance plus MIT attribution.

## Milestone roadmap

| Milestone | Status | Gate to start |
|---|---|---|
| M0 Baseline & characterization | APPROVED | Complete |
| M1 Canonical domain skeleton | APPROVED | Complete |
| M2 Full-pull live recorder | APPROVED | Complete |
| M3 Analyzer engine | APPROVED | Complete |
| M4 New workspace shell | APPROVED | Complete after CI `32178240346` |
| M5 Generic hardcore analysis | AUTHORIZED | M4 approved |
| M6 FFLogs integration | NOT STARTED | M5 approved |
| M7 First job analyzer | NOT STARTED | M6 approved |
| M8 First encounter pack | NOT STARTED | M7 approved |
| M9 Session intelligence | NOT STARTED | M8 approved |
| M10 Hardening/extraction review | NOT STARTED | M9 approved |

## WTFDiG provenance baseline

Fork inspected: `EYEZOexe/wtfdig`
Baseline commit observed: `73a2ffa959b8f57bfbe7a1a75d5e43383ae2ea81`
License: MIT, copyright 2024 Matthew Czubakowski.

Verified later-use surfaces:
- `src/lib/arena.ts` — role/group matching, waymarks, arena/player/boss/AoE/tether/arrow/polygon concepts;
- `src/routes/ultimates/umad/data.ts` — phase/mechanic scaffolding and role/light-party strategy assignment data.

Do not port WTFDiG until M8. Record exact upstream path + commit and update `THIRD_PARTY_NOTICES.md` when direct reuse first lands.

## Review ledger

| Date | Package/PR | Review result | Evidence / notes |
|---|---|---|---|
| 2026-08-18 | Foundation PR #1 | APPROVED | Execution contract/progress ledger foundation. |
| 2026-08-18 | M0 PRs #8–#13 | APPROVED | Baseline characterization and combined CI complete. |
| 2026-08-18 | M1 PR #19 / #20 | APPROVED | Canonical domain/results boundary established. |
| 2026-08-18 | M2 PRs #27–#31 | APPROVED | Full-pull recorder/persistence/live normalization/lifecycle integration. |
| 2026-08-18 | Original M3 sign-off PR #40 | SUPERSEDED | Premature sign-off corrected before M4-B. |
| 2026-08-18 | Corrected M3-D / PR #47 | APPROVED | Combined-state CI `32175231795`; M4 authorized. |
| 2026-08-18 | M4-A PR #46 | APPROVED | Shared workspace selection state. |
| 2026-08-18 | M4-B PR #48 | APPROVED | Focused shell panels; Deaths render-rescan fixed; CI `32176263964`. |
| 2026-08-18 | M4-C PR #49 | APPROVED | Async AnalyzerWindow + IPullStore/engine integration; CI `32177896535`. |
| 2026-08-18 | M4-D / PR #50 | APPROVED | Combined-state CI `32178240346`; M5 authorized after merge. |

## Agent return format

Every implementation package returns:
1. scope completed;
2. files changed;
3. behavior implemented/characterized;
4. commands/CI results;
5. acceptance criteria pass/fail;
6. risks/unknowns;
7. branch + PR;
8. requested review state (`READY FOR REVIEW`, never self-approved).
