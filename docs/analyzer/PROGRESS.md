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

**Status:** `IN PROGRESS`

**Purpose**

Lock down the current Better Deaths behavior that later M1/M2 changes must preserve or intentionally replace. No new analyzer architecture belongs in M0.

**Required characterization surface**

- [ ] Pull/duty reset and archive behavior.
- [ ] Death-gated current snapshot behavior.
- [ ] Saved pull serialization/loading and current schema/version assumptions.
- [ ] Replay data survives saved-pull round trip.
- [ ] `LeadUpTimingPolicy` retention/display behavior.
- [ ] Existing test suite remains green.
- [ ] Plugin build remains green.
- [ ] Concise implementation note records exact preserved/current behavior.

**Observed baseline facts from repository inspection**

- `ArchiveCurrentPullForReview` persists only when `currentDeaths.Count > 0`; a started pull without deaths is reset rather than archived.
- `CaptureCurrentPullSnapshot` explicitly refuses capture if the snapshot was already captured or the current death list is empty.
- Existing snapshots already contain substantial replay context: positions, markers, mechanics, world markers, mitigation, and debuffs.
- The current root persisted detail type is `PullDeathSnapshot`, confirming the death-centric storage boundary M2 must eventually replace/add alongside.
- Recorded pull storage already uses an index + detail-file model with backup/temp recovery and background saving, so M2 should build on this behavior rather than replacing persistence wholesale.
- Legacy normalization/loading currently filters entries to positive death counts; zero-death pull support therefore requires an intentional later migration rather than only changing finalization.
- `LeadUpTimingPolicy` currently defines 10/30/60 second display choices, 70 seconds capture history, and 75 seconds live retention.
- CI already verifies formatting, tests, and plugin build on .NET 10 with Dalamud downloaded during workflow execution.

## Work packages

### M0-A — Pull lifecycle characterization

**Status:** `NOT STARTED`

**Agent brief**

Characterize current lifecycle behavior without introducing M1/M2 structures. Focus on `Plugin.PullLifecycle.cs` and any minimal pure policy seam needed to test archive/reset decisions deterministically.

**Acceptance evidence required**

- Tests demonstrate that a death-containing pull archives for review.
- Tests demonstrate that a started zero-death pull is reset/not persisted under current behavior.
- Tests demonstrate relevant duty reset and combat-end close behavior or the smallest deterministic policy seam underlying them.
- Existing runtime code behavior remains unchanged.

### M0-B — Persistence/schema characterization

**Status:** `NOT STARTED`

**Agent brief**

Characterize the current saved-pull format and migration assumptions in `Plugin.RecordedPulls.cs`/`Models.cs`. Prefer extracting only pure serialization/normalization helpers if direct plugin testing is impractical.

**Acceptance evidence required**

- Current legacy array and wrapped-history behavior is documented/tested where feasible.
- Death-count filtering behavior is locked down.
- Pull numbering normalization assumptions are covered.
- Index/detail model and schema version assumptions are documented.
- No new canonical `RecordedPull` architecture yet.

### M0-C — Replay persistence round-trip characterization

**Status:** `NOT STARTED`

**Agent brief**

Add a fixture or pure serialization test proving representative replay positions, markers, mechanics, world markers, mitigation, and debuff state survive the current saved-detail round trip.

**Acceptance evidence required**

- Representative `PullDeathSnapshot` round-trip retains replay collections and key fields.
- Existing replay-specific tests remain green.

### M0-D — Timing-policy characterization

**Status:** `NOT STARTED`

**Agent brief**

Confirm `LeadUpTimingPolicy` behavior with direct unit tests if not already fully covered. Do not change timing values during M0.

**Acceptance evidence required**

- 10/30/60 normalization behavior covered.
- Capture/live-retention constants and relationship covered.
- No production behavior change.

### M0-E — Baseline report and sign-off

**Status:** `NOT STARTED`

**Owner:** Lead reviewer / integrator

**Deliverable**

Review all M0 PRs together, verify repository gates, record the exact baseline contracts M1/M2 must preserve or intentionally replace, then mark M0 `APPROVED` before any M1 implementation starts.

## Milestone roadmap

| Milestone | Status | Gate to start |
|---|---|---|
| M0 Baseline & characterization | IN PROGRESS | Current |
| M1 Canonical domain skeleton | NOT STARTED | M0 approved |
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

Do not port these during M0. When M8 begins, record exact upstream path + commit per ported subsystem and update `THIRD_PARTY_NOTICES.md` with the MIT notice before/with the first direct reuse.

## Review ledger

| Date | Package/PR | Agent | Review result | Evidence / notes |
|---|---|---|---|---|
| 2026-08-18 | Foundation docs | Lead integrator | IN PROGRESS | Added execution contract and progress ledger on `analyzer/foundation`. |

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
