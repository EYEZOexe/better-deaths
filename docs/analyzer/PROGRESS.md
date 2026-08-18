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

### M3 — Analyzer Engine

**Status:** `READY FOR REVIEW`

**Parent issue:** #32
**Integration/sign-off issue:** #36

M3 was previously marked approved too early: the documentation sign-off landed while implementation PRs #38 and #39 were still open. The inconsistency was discovered before M4-B began. #32/#36 were reopened, both missing implementation PRs were independently reviewed/fixed/merged, and a fresh combined-state sign-off is now the only remaining gate.

### M3 work packages

#### M3-A — Canonical Actor/Event indexes

**Status:** `APPROVED`

**Issue / PR:** #33 / #37
**Merged commit:** `607b2c9530c7a94f24c7248df79e4317f6c9fbd7`

Evidence:
- validated unique actor IDs, event IDs, and strictly increasing canonical sequence;
- deterministic typed/source/target/involved/action/status lookup paths;
- pure source-agnostic analysis boundary;
- repository CI passed before merge.

#### M3-B — Analyzer engine, registry, dependencies, failure isolation

**Status:** `APPROVED`

**Issue / PR:** #34 / #38
**Merged commit:** `d0b026df60213fb1aa453b2f06637c2c53d73368`
**Final CI:** `32174557211`

Evidence:
- registry composition with no hard-coded dispatch;
- deterministic dependency resolution with explicit missing/self/cycle failures;
- declared dependency-result access;
- event/actor indexes built once per run;
- per-module buffered output, failure isolation, dependent skip behavior, cancellation propagation;
- result ownership/global identity validation.

Lead review caught and fixed a correctness bug before merge: global result IDs were being reserved while a module was still being validated. A module that later failed could therefore poison result IDs for subsequent independent modules. The final implementation validates the complete module result set first and commits global ID reservations only on success.

#### M3-C — First generic analyzer / golden fixture

**Status:** `APPROVED`

**Issue / PR:** #35 / #39
**Merged commit:** `924f57a517a85d8ede108192aae4d8a205c542b9`
**Final CI:** `32174764060`

Evidence:
- `DeathEventAnalyzer` consumes canonical `DeathEvent` facts through the real engine;
- structured result links stable event/actor/time evidence and provenance confidence;
- deterministic result identity derives from pull + analyzer + event identity;
- no-death pulls are cleanly unsupported;
- no M5 causal/blame logic was pulled forward.

Lead review caught and fixed an invalid golden fixture: two hard-coded result GUIDs did not match the actual stable identity algorithm. The corrected fixture derives expected IDs from the identity contract and verifies repeated runs serialize to identical structured results.

#### M3-D — Corrected integration review

**Status:** `READY FOR REVIEW`

Combined review:
- [x] M3-A/B/C are now actually merged on `main`.
- [x] Analyzer contracts remain source-agnostic and independent of Dalamud/ImGui/FFLogs/network/persistence implementation types.
- [x] Module output commit semantics are atomic after the M3-B lead-review fix.
- [x] Generic analyzer output is deterministic and evidence-backed after the M3-C fixture correction.
- [x] No M4+ implementation leaked into the analyzer engine packages.
- [ ] Corrected combined-state sign-off branch CI must pass restore, format, tests, and plugin/package build.

See `docs/analyzer/M3_SIGNOFF.md` for the detailed correction record.

## M4 — New Workspace Shell

**Status:** `BLOCKED` pending corrected M3 sign-off, except already-merged M4-A.

**Parent issue:** #41

### M4-A — Shared analyzer workspace selection state

**Status:** `APPROVED / ALREADY MERGED`

**Issue / PR:** #42 / #46
**Merged commit:** `b6ab9d996ee0025477e2abf669ca90069b53009b`

This package landed while the M3 ledger was inconsistent. It remains because it is pure UI selection-state infrastructure and does not depend on unmerged analyzer-engine behavior.

It owns one synchronized selection model for:
- selected `PullId`;
- selected `ActorId`;
- selected `TimeRange`;
- selected `AnalysisResultId`;
- optional mechanic occurrence key;
- monotonic change/version notification.

Changing pull clears stale cross-pull context; selecting an `AnalysisResult` synchronizes its deterministic actor/time selection. No ImGui, Dalamud, persistence, or analyzer execution is embedded in the state model.

### Remaining M4 work

- **M4-B / #43 — BLOCKED:** focused panel contracts and Overview/Timeline/Deaths/Replay shells.
- **M4-C / #44 — BLOCKED:** `AnalyzerWindow` plugin integration, canonical pull/result loading and navigation.
- **M4-D / #45 — BLOCKED:** combined workspace review/sign-off.

M4-B resumes only after corrected M3-D approval.

## Completed milestone: M2 — Full-pull live recorder

**Status:** `APPROVED`

- M2-A / #22 / PR #27 — separate append-only `FullPullRecorder`, meaningful-pull finalization, zero-death support.
- M2-B / #23 / PR #28 — independent versioned canonical `IPullStore` persistence and recovery.
- M2-C / #24 / PR #29 — source-boundary Dalamud live normalization into canonical events/spatial facts.
- M2-D / #25 / PR #30 — additive live lifecycle integration and zero-death persistence while preserving legacy recap behavior.
- M2-E / #26 / PR #31 — combined sign-off.

Important preserved contracts:
- legacy death recap remains available and death-gated;
- short recap buffers remain bounded at their characterized limits;
- canonical full-pull storage is separate from legacy history;
- runtime in-game FFXIV/Dalamud smoke validation remains a manual item because CI cannot launch the game.

## Completed milestone: M1 — Canonical domain skeleton

**Status:** `APPROVED`

Issue #14 / implementation PR #19 / sign-off PR #20.

Established source-agnostic canonical `RecordedPull`, typed `NormalizedEvent` records, stable pull/event/actor/result IDs, pull-relative deterministic sequence/time, structured `AnalysisResult`/`AnalysisEvidence`, provenance/fidelity, and serialization/boundary tests.

## Completed milestone: M0 — Baseline and characterization

**Status:** `APPROVED`

- M0-A: lifecycle/archive/reset characterization.
- M0-B: persistence/schema/death-filter baseline.
- M0-C: replay persistence round trip.
- M0-D: 10/30/60 display, 70-second capture and 75-second live retention characterization.
- M0-E: combined CI validation.

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
| M3 Analyzer engine | READY FOR REVIEW | Corrected combined-state CI |
| M4 New workspace shell | BLOCKED (M4-A already merged) | M3 corrected approval |
| M5 Generic hardcore analysis | NOT STARTED | M4 approved |
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
- `src/lib/arena.ts` — role/group matching, waymarks, arena/player/boss/AoE/tether/arrow/polygon concepts.
- `src/routes/ultimates/umad/data.ts` — phase/mechanic scaffolding and role/light-party strategy assignment data.

Do not port WTFDiG until M8. Record exact upstream path + commit and update `THIRD_PARTY_NOTICES.md` when direct reuse first lands.

## Review ledger

| Date | Package/PR | Review result | Evidence / notes |
|---|---|---|---|
| 2026-08-18 | Foundation PR #1 | APPROVED | Execution contract/progress ledger foundation. |
| 2026-08-18 | M0 PRs #8–#13 | APPROVED | Baseline characterization and combined CI complete. |
| 2026-08-18 | M1 PR #19 / #20 | APPROVED | Canonical domain/results boundary established. |
| 2026-08-18 | M2 PRs #27–#31 | APPROVED | Full-pull recorder/persistence/live normalization/lifecycle integration. |
| 2026-08-18 | M3-A PR #37 | APPROVED | Canonical analysis indexes. |
| 2026-08-18 | Original M3 sign-off PR #40 | SUPERSEDED | Sign-off was premature because PRs #38/#39 were still open; correction initiated before M4-B. |
| 2026-08-18 | M4-A PR #46 | APPROVED / retained | Pure selection-state package landed during ledger inconsistency; no analyzer-engine dependency. |
| 2026-08-18 | M3-B PR #38 | APPROVED | Atomic result-ID defect caught/fixed; CI `32174557211`; merge `d0b026df...`. |
| 2026-08-18 | M3-C PR #39 | APPROVED | Invalid golden IDs caught/fixed; CI `32174764060`; merge `924f57a5...`. |
| 2026-08-18 | Corrected M3-D | READY FOR REVIEW | Awaiting fresh combined-state CI. |

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
