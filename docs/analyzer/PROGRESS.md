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

### M2 — Full-pull live recorder

**Status:** `APPROVED`

**Parent issue:** #21

**Integration/sign-off issue:** #26

**Purpose**

Introduce a separate append-only canonical full-pull path that can preserve meaningful zero-death pulls without changing Better Deaths' existing short death-recap buffers or legacy `PullDeathSnapshot` review workflow.

### M2 work packages

#### M2-A — FullPullRecorder and finalization policy

**Status:** `APPROVED`

**Issue / PR:** #22 / #27

**Merged commit:** `d69c4864a131147cd3f15149bdf550a8f57d4608`

Evidence:
- Added append-only `FullPullRecorder` with explicit begin/combat/register/append/finalize/reset lifecycle.
- One strict sequence boundary covers events, positions, and world-marker samples.
- Pull-local actor registration rejects conflicting identity reuse.
- `PullFinalizationPolicy` requires duty-active + combat-observed + at least one relevant canonical event + at least one second duration.
- Meaningful zero-death pulls can finalize; trivial/no-event pulls are discarded.
- Reset/finalize tests prove no cross-pull contamination.
- Existing lead-up/death buffers were not modified.
- PR CI run `32162633593` passed restore, format, tests, and plugin/package build.

#### M2-B — Canonical pull persistence

**Status:** `APPROVED`

**Issue / PR:** #23 / #28

**Merged commit:** `2b0b3e757c3ae2f549db11e7d1c29a5f631eceb1`

Evidence:
- Added `IPullStore`, canonical `PullSummary`/`PullQuery`, `CanonicalPullSerializer`, and file-backed canonical pull store.
- Canonical file/pull/index schemas start at version 1 and are explicitly separate from legacy history schema 3 / index schema 7.
- Compact index and per-pull details are stored separately.
- Writes use temp + flush-to-disk + backup + replace behavior.
- Corrupt compatible data can recover through detail backup or index backup/detail rebuild.
- Unsupported schema versions fail explicitly instead of being silently reinterpreted or hidden behind older backups.
- Save/Delete validate an existing index before mutating canonical detail storage.
- Zero-death canonical pulls save, load, query, and delete normally.
- Legacy `PullDeathSnapshot` persistence was not replaced or modified.
- Final PR CI run `32163926522` passed 226 tests, format, and plugin/package build with 0 warnings/errors.

#### M2-C — Live canonical normalization boundary

**Status:** `APPROVED`

**Issue / PR:** #24 / #29

**Merged commit:** `ea9b07a74818571d4c92d4473ed126eabd5aeb8b`

Evidence:
- Added source-boundary live fact records outside `BetterDeaths.Domain`.
- Added `DalamudLiveEventNormalizer` translating reconstructed live facts into typed canonical events/spatial samples.
- Stable adapter-supplied source-instance keys resolve deterministic pull-local `ActorId` values and preserve owner/pet relationships.
- Conflicting reuse of one source-instance key is rejected rather than silently merging actor instances.
- Events and spatial samples share one explicit sequence allocator; `EventId` is stable from that sequence.
- Pull-relative time is primary; source wall-clock remains metadata.
- Dalamud source provenance, fidelity, and confidence are attached to normalized facts.
- No analyzer interpretation or plugin-hook wiring was introduced in this package.
- Final PR CI run `32164499951` passed 231 tests, format, and plugin/package build with 0 warnings/errors.

#### M2-D — Additive live lifecycle integration

**Status:** `APPROVED`

**Issue / PR:** #25 / #30

**Merged commit:** `67c839cb450d40c849ceefa62c949d8326bdc32d`

Evidence:
- Existing pull start/combat lifecycle starts and marks the separate `FullPullRecorder`.
- Existing resolved raw action-effect packets additionally feed canonical action/damage/heal events and sampled positions; old replay and death-resolution calls still run.
- Party actor identity uses existing stable member keys; other live objects prefer Dalamud game-object identity with explicit fallback behavior.
- `ArchiveCurrentPullForReview` finalizes the canonical pull before the old death-gated archive/reset decision, so a meaningful zero-death pull can survive while legacy zero-death behavior remains unchanged.
- Canonical storage is isolated under a separate `analyzer-pulls` configuration directory and queued away from the immediate finalization path.
- Canonical normalization/persistence failures are caught separately from the legacy death recap path.
- `ResetCurrentPull` / `PrepareCurrentPullForReview` also clear the canonical live recorder after finalization/reset boundaries.
- Automated end-to-end coverage proves live canonical facts -> zero-death `RecordedPull` -> canonical file store -> reload.
- Source-contract coverage verifies canonical capture is additive rather than a replacement for existing replay/death processing.
- Existing legacy snapshot death gate and 70/75-second short-buffer limits remain explicitly covered.
- Review caught an invalid amount-helper call; it was corrected to reuse the existing raw action-effect amount reconstruction before merge.
- Final PR CI run `32166053444` passed 236 tests, format, and plugin/package build with 0 warnings/errors.

### M2-E — Integration review

**Status:** `APPROVED`

Combined review findings:
- [x] A meaningful clean zero-death pull can be finalized into `RecordedPull`, saved through `IPullStore`, reloaded, and inspected in automated integration coverage.
- [x] The legacy `PullDeathSnapshot` capture path remains death-gated and is still executed for death-containing pulls.
- [x] Canonical finalization happens before legacy zero-death reset.
- [x] Existing `LeadUpTimingPolicy` display/capture/live-retention values remain unchanged and bounded.
- [x] Full-pull recorder reset/finalize semantics prevent pull-to-pull state leakage.
- [x] Canonical event IDs/order/time/provenance are explicit and deterministic within one pull.
- [x] Canonical persistence schema is distinct from legacy history v3/index v7 and has corruption/compatibility recovery coverage.
- [x] M2 contains no analyzer engine, new workspace UI, FFLogs integration, job analyzer, encounter pack, or WTFDiG port.
- [x] Each implementation PR passed repository CI before merge.
- [x] Combined M2 sign-off CI run `32166422121` passed restore, formatting, all tests, and plugin/package build.

**Decision:** M2 is approved. M3 — Analyzer Engine — is authorized after this sign-off change is merged.

**Runtime validation note:** automated CI cannot launch FFXIV/Dalamud in-game. An in-game smoke test of combat start/end, wipe/duty reset, plugin reload, and generated canonical files remains a manual runtime validation item. No current automated/static review evidence indicates a regression, but this is kept explicit rather than claiming unexecuted runtime validation.

## Completed milestone: M1 — Canonical domain skeleton

**Status:** `APPROVED`

**Issue / implementation PR:** #14 / #19

**Merged implementation commit:** `db4882cf8f781faf85a5ef14c0c9bd9040bdff90`

Key contracts established:
- canonical `RecordedPull`, versioned schema, metadata, actors, typed normalized events, positions/world markers, and provenance;
- stable `PullId`, `EventId`, pull-local `ActorId`, `AnalysisResultId`, and explicit sequence + pull-relative `TimeSpan` ordering;
- structured `AnalysisResult` / `AnalysisEvidence` contracts;
- source/fidelity provenance and mixed-event serialization;
- pure Domain compile boundary without Dalamud/ImGui/FFLogs/network/persistence implementation dependencies.

Final implementation CI run `32161772862` passed restore, formatting, 201 tests, and plugin/package build. Sign-off PR #20 merged as `71e0149480ff92bc14c232a264028d5bd0890e47`.

## Completed milestone: M0 — Baseline and characterization

**Status:** `APPROVED`

- M0-A / #3 / PR #10: lifecycle/archive/reset characterization; merge `79709671cb260b394c5fad790e883f9cf3e2e61a`.
- M0-B / #4 / PR #11: persistence/schema baseline and compatibility assumptions; merge `22ead99dcd44f3dfa4a36fca1f32604c70e0abef`.
- M0-C / #5 / PR #9: replay persistence round-trip; merge `fe803523d0fd207b6508c4c7440449cc37ee6d33`.
- M0-D / #6 / PR #8: 10/30/60 display, 70-second capture, 75-second live retention characterization; merge `bd0359a2b90adde40debbeb0e322699f39da5a61`.
- M0-E / #7: combined integration review and executable CI validation.

## Contracts later milestones must preserve or intentionally replace

1. Existing Better Deaths death recap remains available and its short optimized buffers remain bounded.
2. Full-pull history is collected by the separate M2 recorder, never by making legacy recap lists unbounded.
3. Legacy `PullDeathSnapshot` reading/persistence remains supported while canonical storage evolves additively.
4. Existing persistence recovery/background-save safety must not be silently discarded.
5. Canonical schema versions must not silently reuse legacy history version 3 / index version 7 semantics.
6. All future data sources normalize into the M1 `RecordedPull` / `NormalizedEvent` contracts before analyzer interpretation.
7. Stable event IDs, explicit sequence, and pull-relative time remain the evidence/time-ordering basis; wall-clock timestamps are metadata.
8. Domain/analyzer contracts remain free of Dalamud service objects, FFLogs DTOs, ImGui types, network clients, and persistence implementations.
9. Analyzer findings remain structured/evidence-backed rather than UI-formatted prose as the source of truth.
10. Canonical persistence incompatibility remains explicit; unsupported schema data is not silently treated as corrupt or rebuilt into another semantic version.
11. Canonical runtime capture remains additive until later milestones intentionally migrate old death analysis onto canonical events.

## Milestone roadmap

| Milestone | Status | Gate to start |
|---|---|---|
| M0 Baseline & characterization | APPROVED | Complete |
| M1 Canonical domain skeleton | APPROVED | Complete |
| M2 Full-pull live recorder | APPROVED | Complete |
| M3 Analyzer engine | AUTHORIZED | M2 approved |
| M4 New workspace shell | NOT STARTED | M3 approved |
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

Do not port WTFDiG until the M8 encounter-pack milestone. Record exact upstream path + commit and update `THIRD_PARTY_NOTICES.md` with the MIT notice when direct reuse first lands.

## Review ledger

| Date | Package/PR | Review result | Evidence / notes |
|---|---|---|---|
| 2026-08-18 | Foundation PR #1 | APPROVED | Execution contract/progress ledger foundation. |
| 2026-08-18 | M0 PRs #8–#13 | APPROVED | Baseline characterization and combined CI complete. |
| 2026-08-18 | M1 PR #19 / sign-off #20 | APPROVED | Canonical domain/result contracts; final implementation CI `32161772862` green. |
| 2026-08-18 | M2-A PR #27 | APPROVED | Recorder/finalization policy; CI `32162633593` green. |
| 2026-08-18 | M2-B PR #28 | APPROVED | Canonical pull file store; compatibility fixes reviewed; CI `32163926522` green. |
| 2026-08-18 | M2-C PR #29 | APPROVED | Live normalization boundary; CI `32164499951` green. |
| 2026-08-18 | M2-D PR #30 | APPROVED | Additive lifecycle integration; 236 tests/build green on CI `32166053444`. |
| 2026-08-18 | M2-E / sign-off PR #31 | APPROVED | Combined M2 state green on CI `32166422121`; M3 authorized. |

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
