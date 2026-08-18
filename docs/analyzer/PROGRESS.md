# FFXIV Static Analyzer — Progress & Review Ledger

Status date: 2026-08-18
Governing design: Technical Design v0.2
Primary implementation repository: `EYEZOexe/better-deaths`
Encounter knowledge/reference repository: `EYEZOexe/wtfdig`

## Status legend

- `NOT STARTED`
- `IN PROGRESS`
- `READY FOR REVIEW`
- `CHANGES REQUESTED`
- `APPROVED`
- `BLOCKED`

## Current milestone

### M0 — Baseline and characterization

**Status:** `APPROVED`

M0-A through M0-D are implemented, reviewed, and merged. The combined M0 state was validated by GitHub Actions run `32142414713` (attempt 4 after the account billing lock was cleared): checkout, .NET setup, Dalamud download, restore, formatting verification, unit tests, and plugin build all completed successfully.

**Purpose**

Lock down the current Better Deaths behavior that later M1/M2 changes must preserve or intentionally replace. M0 adds no new analyzer product behavior.

**Required characterization surface**

- [x] Pull/duty reset and archive behavior.
- [x] Death-gated current snapshot behavior.
- [x] Saved pull serialization/loading and current schema/version assumptions.
- [x] Replay data survives saved-pull round trip.
- [x] `LeadUpTimingPolicy` retention/display behavior.
- [x] Existing test suite verified green after the combined M0 changes.
- [x] Plugin build/format checks verified green after the combined M0 changes.
- [x] Concise implementation notes record the exact preserved/current behavior.

**Observed baseline facts from repository inspection**

- `ArchiveCurrentPullForReview` persists only when `currentDeaths.Count > 0`; a started pull without deaths is reset rather than archived.
- `CaptureCurrentPullSnapshot` refuses capture if the snapshot was already captured or the current death list is empty.
- Existing snapshots already contain replay context: positions, markers, mechanics, world markers, mitigation, and debuffs.
- The current root persisted detail type is `PullDeathSnapshot`, confirming the death-centric storage boundary M2 must intentionally extend/replace additively.
- Recorded pull storage uses an index + detail-file model with backup/temp recovery and background saving.
- Legacy normalization/loading filters entries to positive death counts; zero-death pull support therefore requires an intentional later persistence change rather than only changing finalization.
- Current saved-pull schema constants are history version 3 and split-index version 7.
- `LeadUpTimingPolicy` defines 10/30/60 second display choices, 70 seconds capture history, 75 seconds live retention, and a 10 second late-fatal-cause lookback.

## Work packages

### M0-A — Pull lifecycle characterization

**Status:** `APPROVED`

**Issue / PR:** #3 / #10

**Merged commit:** `79709671cb260b394c5fad790e883f9cf3e2e61a`

**Evidence**

- Added the minimal pure `PullLifecyclePolicy` seam for the existing archive/reset and single-snapshot guards.
- Existing `Plugin.PullLifecycle.cs` routes the same current conditions through that seam; no zero-death persistence was introduced.
- Tests characterize death-containing archive, zero-death reset, inactive no-op, and duplicate/empty snapshot guards.
- Existing `CaptureTimingPolicyTests` characterize post-combat grace/close behavior.
- Initial diff review found unrelated import churn; it was corrected before merge.
- No `RecordedPull`, `FullPullRecorder`, analyzer engine, FFLogs, or analyzer UI implementation was introduced.

### M0-B — Persistence/schema characterization

**Status:** `APPROVED`

**Issue / PR:** #4 / #11

**Merged commit:** `22ead99dcd44f3dfa4a36fca1f32604c70e0abef`

**Evidence**

- Added public-model serialization tests for legacy raw-array and wrapped-history shapes.
- Added characterization coverage for death-only persistence and pull-number normalization assumptions.
- Added source-contract assertions tying schema versions and current death filters to the actual runtime source.
- Added `docs/analyzer/M0_PERSISTENCE_BASELINE.md` documenting schema versions, split index/detail storage, load/recovery order, background-save revision safety, compressed-detail migration, and M1/M2 compatibility constraints.
- Private persistence methods remain inside the Dalamud-coupled plugin partial; M0 intentionally avoided a broad persistence refactor only for test exposure.
- No canonical persistence implementation or legacy-filter change was introduced.

### M0-C — Replay persistence round-trip characterization

**Status:** `APPROVED`

**Issue / PR:** #5 / #9

**Merged commit:** `fe803523d0fd207b6508c4c7440449cc37ee6d33`

**Evidence**

- Added a representative `PullDeathSnapshot` serialization round-trip fixture.
- Verifies replay positions, markers, mechanics, world markers, mitigation, debuffs, `ReplayDebuffsCaptured`, and representative identity/timing/state fields.
- No replay redesign, canonical event migration, or encounter analyzer implementation was introduced.

### M0-D — Timing-policy characterization

**Status:** `APPROVED`

**Issue / PR:** #6 / #8

**Merged commit:** `bd0359a2b90adde40debbeb0e322699f39da5a61`

**Evidence**

- Added direct tests for 10/30/60 display normalization.
- Locked down 70 second capture, 75 second live retention, and 10 second late fatal-cause lookback constants.
- Locked down the existing `MaximumDisplay + 10` capture and `Capture + 5` retention relationships.
- Production timing values and retention behavior were not changed or expanded.

### M0-E — Baseline report and sign-off

**Status:** `APPROVED`

**Owner:** Lead reviewer / integrator

**Sign-off evidence**

- Reviewed each M0 implementation diff for milestone leakage and unrelated churn.
- Confirmed M0-A through M0-D remain characterization-focused.
- Confirmed zero-death persistence has not been implemented early.
- Confirmed legacy persistence support remains intact and its current contract is documented.
- Confirmed the short death-recap retention windows remain bounded and unchanged.
- Confirmed no canonical domain/analyzer/FFLogs/workspace implementation leaked into M0.
- GitHub Actions run `32142414713` successfully completed checkout, .NET setup, Dalamud download, restore, formatting verification, unit tests, and plugin build against the combined M0 branch state.

**Decision:** M0 is approved. M1 is authorized to begin after this sign-off PR is merged.

## Baseline contracts M1/M2 must preserve or intentionally replace

1. Existing Better Deaths death recap remains death-gated until M2 intentionally adds a separate full-pull path.
2. Existing short lead-up buffers remain bounded; full-pull capture must not be implemented by extending them indefinitely.
3. Legacy `PullDeathSnapshot` loading remains supported while canonical persistence is introduced additively.
4. Existing persistence recovery/background-save safety must not be silently discarded.
5. Zero-death pull support requires both lifecycle and persistence changes; changing only archive finalization is insufficient.
6. Replay/context fields currently persisted on death snapshots must remain reviewable during migration.
7. Existing post-combat grace and deterministic pull-close behavior must remain intact unless a later milestone explicitly changes it.
8. New canonical schema versions must not silently reuse the existing history version 3 / index version 7 semantics.

## Milestone roadmap

| Milestone | Status | Gate to start |
|---|---|---|
| M0 Baseline & characterization | APPROVED | Complete |
| M1 Canonical domain skeleton | AUTHORIZED | M0 approved |
| M2 Full-pull live recorder | NOT STARTED | M1 approved |
| M3 Analyzer engine | NOT STARTED | M2 approved |
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

Verified useful upstream surfaces for later M8 work:

- `src/lib/arena.ts` — role/group matching, waymarks, player/boss/arena elements, AoEs, tethers, arrows, polygons, visibility predicates, and declarative arena data concepts.
- `src/routes/ultimates/umad/data.ts` — phase/mechanic scaffolding and role/light-party strategy assignment data.

Do not port these during M1. When M8 begins, record exact upstream path + commit per ported subsystem and update `THIRD_PARTY_NOTICES.md` with the MIT notice before/with the first direct reuse.

## Review ledger

| Date | Package/PR | Agent | Review result | Evidence / notes |
|---|---|---|---|---|
| 2026-08-18 | Foundation PR #1 | Lead integrator | APPROVED | Execution contract and progress ledger merged as `9198ec55ae117209ed0bda349a473df7520df1b0`. |
| 2026-08-18 | M0-D PR #8 | Implementation + lead diff review | APPROVED | Timing tests only; merged as `bd0359a2b90adde40debbeb0e322699f39da5a61`. |
| 2026-08-18 | M0-C PR #9 | Implementation + lead diff review | APPROVED | Replay persistence round-trip fixture; merged as `fe803523d0fd207b6508c4c7440449cc37ee6d33`. |
| 2026-08-18 | M0-A PR #10 | Implementation + lead diff review | APPROVED | Minimal lifecycle policy seam/tests; unrelated import churn caught and removed; merged as `79709671cb260b394c5fad790e883f9cf3e2e61a`. |
| 2026-08-18 | M0-B PR #11 | Implementation + lead diff review | APPROVED | Persistence contracts/docs with explicit direct-test limitation; merged as `22ead99dcd44f3dfa4a36fca1f32604c70e0abef`. |
| 2026-08-18 | M0 validation PR #13 | GitHub Actions + lead integrator | APPROVED | Run `32142414713`: restore, format, test, and plugin build all green after billing lock was cleared. |
| 2026-08-18 | M0-E | Lead integrator | APPROVED | Static integration review plus executable CI validation complete; M1 authorized. |

## Agent return format

Every agent should return/update the ledger with:

1. **Scope completed** — exact assigned package only.
2. **Files changed** — production and tests separately.
3. **Behavior characterized/implemented** — concrete facts, not generic summary.
4. **Commands run** — format/test/build and results.
5. **Acceptance criteria** — pass/fail per item.
6. **Risks/unknowns** — especially source fidelity, persistence compatibility, and runtime-only behavior.
7. **PR** — branch + PR number.
8. **Requested review result** — `READY FOR REVIEW`, never self-approved.
