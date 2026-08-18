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

### M1 — Canonical domain skeleton

**Status:** `APPROVED`

**Issue / implementation PR:** #14 / #19

**Merged implementation commit:** `db4882cf8f781faf85a5ef14c0c9bd9040bdff90`

**Purpose**

Establish the source-agnostic canonical contracts that later live capture, FFLogs imports, analyzers, persistence, and UI will share, without switching any existing Better Deaths runtime path yet.

**Acceptance evidence**

- [x] Added canonical `RecordedPull` root aggregate with versioned schema, metadata, actors, typed events, positions, world markers, and pull provenance.
- [x] Added stable `PullId`, `EventId`, pull-local `ActorId`, `AnalysisResultId`, and `PullSchemaVersion` value types.
- [x] Added `NormalizedEvent` with explicit deterministic `Sequence`, pull-relative `PullTime`, optional wall-clock metadata, optional source/target actor references, and required provenance.
- [x] Added typed damage, healing, cast, action-use, status, death, raise, targetability, gauge, tether, marker, and mechanic-signal event records rather than one giant nullable event object.
- [x] Added structured `AnalysisResult` / `AnalysisEvidence` contracts with severity/category, actors, event evidence, time ranges, confidence, and metrics.
- [x] Added source/fidelity provenance so local and imported sources can retain differing evidence quality without leaking source DTOs into analyzers.
- [x] Added JSON polymorphic round-trip coverage proving stable mixed event identities/types/order survive reload.
- [x] Added actor ownership, position/world-marker, and structured result/evidence round-trip coverage.
- [x] The lightweight `net10.0` test project compiles `BetterDeaths/Domain/**/*.cs` directly, proving the domain does not require the Dalamud SDK to compile.
- [x] Added a dependency-boundary test rejecting obvious Dalamud, ImGui, FFLogs client/DTO, and network-client leakage from public domain contracts.
- [x] Existing Better Deaths capture, death recap, saved-history, and UI paths were not switched or changed.
- [x] GitHub Actions run `32161772862` on the final PR #19 head passed restore, formatting, 201 automated tests, and plugin/package build.

**Review notes**

- The first implementation CI exposed missing explicit `System`/collections imports because the Dalamud project does not use the same implicit-usings behavior as the lightweight test project. This was corrected before merge and the final head passed the full plugin build.
- A test-analyzer warning (`xUnit2031`) was also removed before the final green run.
- Engineering fields that Technical Design v0.2 intentionally leaves as sketches are kept conservative and documented in `docs/analyzer/M1_DOMAIN_CONTRACTS.md`.
- `PullDataSourceKind` includes both `SavedFile` and `ImportedFile` to preserve the design's saved-pull and generic-import concepts without embedding source-specific DTOs.
- No `FullPullRecorder`, zero-death persistence, analyzer engine, FFLogs integration, new workspace UI, or WTFDiG port was introduced early.

**Decision:** M1 is approved. M2 — Full-pull live recorder — is authorized after this sign-off change is merged.

## Completed milestone baseline

### M0 — Baseline and characterization

**Status:** `APPROVED`

M0-A through M0-D are implemented, reviewed, and merged. The combined M0 state was validated by GitHub Actions after Actions/billing was enabled: checkout, .NET setup, Dalamud download, restore, formatting verification, unit tests, and plugin build completed successfully.

**Required characterization surface**

- [x] Pull/duty reset and archive behavior.
- [x] Death-gated current snapshot behavior.
- [x] Saved pull serialization/loading and current schema/version assumptions.
- [x] Replay data survives saved-pull round trip.
- [x] `LeadUpTimingPolicy` retention/display behavior.
- [x] Existing test suite verified green after the combined M0 changes.
- [x] Plugin build/format checks verified green after the combined M0 changes.
- [x] Concise implementation notes record the exact preserved/current behavior.

### M0 work packages

- **M0-A / #3 / PR #10 — APPROVED:** lifecycle policy seam and archive/reset/snapshot characterization; merged as `79709671cb260b394c5fad790e883f9cf3e2e61a`.
- **M0-B / #4 / PR #11 — APPROVED:** persistence/schema/death-filter/normalization characterization and `M0_PERSISTENCE_BASELINE.md`; merged as `22ead99dcd44f3dfa4a36fca1f32604c70e0abef`.
- **M0-C / #5 / PR #9 — APPROVED:** representative replay persistence round-trip; merged as `fe803523d0fd207b6508c4c7440449cc37ee6d33`.
- **M0-D / #6 / PR #8 — APPROVED:** 10/30/60 display, 70-second capture, 75-second live retention, and late-fatal-cause timing characterization; merged as `bd0359a2b90adde40debbeb0e322699f39da5a61`.
- **M0-E / #7 — APPROVED:** combined diff review and executable CI validation.

## Baseline contracts M2 and later milestones must preserve or intentionally replace

1. Existing Better Deaths death recap remains death-gated while the new full-pull path is introduced additively.
2. Existing short lead-up buffers remain bounded; full-pull capture must not be implemented by extending them indefinitely.
3. Legacy `PullDeathSnapshot` loading remains supported while canonical persistence is introduced additively.
4. Existing persistence recovery/background-save safety must not be silently discarded.
5. Zero-death pull support requires both lifecycle and persistence changes; changing only archive finalization is insufficient.
6. Replay/context fields currently persisted on death snapshots must remain reviewable during migration.
7. Existing post-combat grace and deterministic pull-close behavior must remain intact unless a later milestone explicitly changes it.
8. New canonical schema versions must not silently reuse existing history version 3 / index version 7 semantics.
9. All future data sources normalize into the M1 `RecordedPull`/`NormalizedEvent` contracts before analyzer interpretation.
10. Domain/analyzer contracts remain free of Dalamud service objects, FFLogs DTOs, ImGui types, network clients, and persistence implementations.
11. Stable event IDs plus explicit sequence and pull-relative time remain the evidence/time-ordering basis; wall-clock timestamps are metadata.
12. Analyzer findings remain structured and evidence-backed rather than UI-formatted prose as the source of truth.

## Milestone roadmap

| Milestone | Status | Gate to start |
|---|---|---|
| M0 Baseline & characterization | APPROVED | Complete |
| M1 Canonical domain skeleton | APPROVED | Complete |
| M2 Full-pull live recorder | AUTHORIZED | M1 approved |
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

Do not port these during M2. When M8 begins, record exact upstream path + commit per ported subsystem and update `THIRD_PARTY_NOTICES.md` with the MIT notice before/with the first direct reuse.

## Review ledger

| Date | Package/PR | Agent | Review result | Evidence / notes |
|---|---|---|---|---|
| 2026-08-18 | Foundation PR #1 | Lead integrator | APPROVED | Execution contract and progress ledger merged as `9198ec55ae117209ed0bda349a473df7520df1b0`. |
| 2026-08-18 | M0-D PR #8 | Implementation + lead diff review | APPROVED | Timing tests only; merged as `bd0359a2b90adde40debbeb0e322699f39da5a61`. |
| 2026-08-18 | M0-C PR #9 | Implementation + lead diff review | APPROVED | Replay persistence round-trip fixture; merged as `fe803523d0fd207b6508c4c7440449cc37ee6d33`. |
| 2026-08-18 | M0-A PR #10 | Implementation + lead diff review | APPROVED | Minimal lifecycle policy seam/tests; unrelated import churn caught and removed; merged as `79709671cb260b394c5fad790e883f9cf3e2e61a`. |
| 2026-08-18 | M0-B PR #11 | Implementation + lead diff review | APPROVED | Persistence contracts/docs with explicit direct-test limitation; merged as `22ead99dcd44f3dfa4a36fca1f32604c70e0abef`. |
| 2026-08-18 | M0 validation PR #13 | GitHub Actions + lead integrator | APPROVED | Restore, format, tests, and plugin build green; M0 signed off. |
| 2026-08-18 | M1 PR #19 | Implementation + lead integrator | APPROVED | Canonical domain/results contracts and serialization/boundary tests; final head CI run `32161772862` green; merged as `db4882cf8f781faf85a5ef14c0c9bd9040bdff90`. |

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
