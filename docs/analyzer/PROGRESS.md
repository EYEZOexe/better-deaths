# FFXIV Static Analyzer — Progress & Review Ledger

Status date: 2026-08-19
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

### M5 — Generic Hardcore Analysis

**Status:** `APPROVED`

**Parent issue:** #51
**Integration/sign-off issue:** #58
**Combined-state CI:** `32216593552`
**Detailed review:** `docs/analyzer/M5_SIGNOFF.md`

M5 establishes source-agnostic generic raid analysis over canonical `RecordedPull` data while preserving evidence-first uncertainty and keeping FFLogs/job/encounter semantics out of the generic layer.

### M5 work packages

#### M5-A — Targetability/status interval indexes

**Status:** `APPROVED`
**Issue / PR:** #52 / #59
**Merged commit:** `6cb116415aaeb9cc7ff5b2eb2a1cd9f4df2e297f`

Evidence:
- targetability intervals retain unknown pre-first-observation state rather than assuming targetable;
- status intervals are keyed by target/status/source and distinguish known vs uncertain endings;
- interval indexes are built once per analyzer run and exposed read-only through `AnalyzerContext`.

#### M5-B — Live generic evidence enrichment

**Status:** `APPROVED`
**Issue / PR:** #53 / #60
**Merged commit:** `0ea7b9a25a1a714328a8b1068783db2fea3ccae7`

Evidence:
- existing live object/action snapshots additionally produce sampled canonical status and targetability facts;
- fidelity/confidence remain explicit and repeated/no-op observations are suppressed;
- pull reset clears delta-tracker state;
- no raise fact is guessed where deterministic source/target/action evidence is unavailable.

#### M5-C — Targetability-aware uptime/activity

**Status:** `APPROVED`
**Issue / PR:** #54 / #61
**Merged commit:** `f1d943da2b5f5888b763708b22d7d52457066867`

Evidence:
- activity gaps are reported only inside evidence-supported targetable windows;
- forced untargetable time is excluded;
- unknown boundary time is not treated as active time;
- death downtime is deferred to death/raise analysis rather than charged as ordinary execution loss.

#### M5-D — Death and raise context

**Status:** `APPROVED`
**Issue / PR:** #55 / #62
**Merged commit:** `c02193cbd569c5bf26f1c40840c3b4abb14b2278`

Evidence:
- bounded fatal-context windows preserve damage/status evidence without treating chronology as lethal attribution;
- current lack of target post-event HP/shield evidence is explicit;
- even very large last damage remains context, not automatic cause/blame;
- bounded canonical raise evidence is linked as a downstream observation without claiming resurrection completion.

#### M5-E — Mitigation coverage / what-if

**Status:** `APPROVED`
**Issue / PR:** #56 / #64
**Merged commit:** `1145ad3ee5678499d32b77d7f25223634f62df0c`
**Corrected-head CI:** `32215987673`

Evidence:
- mitigation semantics are explicit configuration rather than invented FFXIV IDs;
- evidence application (`TargetStatus` / `DamageSourceStatus`) is distinct from semantic scope (`Personal`, `Targeted`, `PartyWide`, `DamageSourceDebuff`, `Other`);
- effect kind remains distinct (`DamageReduction`, `Shield`, `Invulnerability`, `Other`);
- only positive evidence-supported coverage is reported; absence is not called a missed use;
- overlap is neutral coverage evidence, not automatically waste;
- configured reduction estimates are explicit counterfactuals rather than reconstructed server damage or survival proof;
- lead review caught and corrected the original scope collapse before merge.

#### M5-F — Healing + explicit buff/cooldown timelines

**Status:** `APPROVED`
**Issue:** #57
**Healing PR / merge:** #63 / `10677289e609d640285d31beb0ad43f06e8fbfc7`
**Timeline PR / merge:** #65 / `2e3c65fe298d2dd4d13ba481d79a58dfcffd30cb`
**Corrected timeline CI:** `32216083596`

Evidence:
- healing analysis reports raw captured activity neutrally because effective healing/overheal/HP deficit/resource/opportunity cost are not in the current canonical contract;
- raw/high healing is not automatically a warning;
- cooldown/buff references must be explicitly configured;
- ActionUse is preferred per source actor, with CastStart as that actor's fallback only;
- damage/heal packets are never reinterpreted as extra cooldown uses;
- unknown status ends remain uncertain and are not converted into full uptime/missed-refresh claims;
- lead review caught both an invalid `CastTime` test fixture and global-rather-than-per-actor fallback behavior before merge.

### M5-G — Combined fixture pack, workspace integration, and sign-off

**Status:** `APPROVED`

Combined-review evidence:
- [x] representative fixture runs death/raise, healing, targetability-aware uptime, mitigation, and explicit timelines together;
- [x] clean no-evidence fixture produces no invented findings;
- [x] deterministic result identities are checked across repeated runs;
- [x] forced untargetable time is excluded from uptime findings;
- [x] mitigation overlap/scope/uncertainty rules are exercised together;
- [x] high raw healing remains neutral;
- [x] death context remains distinct from blame and links raise evidence downstream;
- [x] default Analyzer Workspace replaces the M3 vertical-slice `DeathEventAnalyzer` with `DeathRaiseContextAnalyzer` and also registers `HealingActivityAnalyzer` + `TargetabilityAwareUptimeAnalyzer`;
- [x] configured mitigation and generic buff/cooldown analyzers remain definition-driven rather than silently inventing a global FFXIV semantic catalog;
- [x] combined M5-G branch CI `32216593552` passed restore, formatting, all tests, and plugin/package build.

**Runtime validation note:** CI cannot launch FFXIV/Dalamud. Live status/targetability enrichment and Analyzer Workspace presentation remain explicit manual smoke items.

**Decision:** M5 is approved. M6 — FFLogs Integration — is authorized after the M5 sign-off PR is merged.

## Completed milestone: M4 — New Workspace Shell

**Status:** `APPROVED`

- M4-A / #42 / PR #46 — shared workspace selection state; merge `b6ab9d996ee0025477e2abf669ca90069b53009b`.
- M4-B / #43 / PR #48 — focused Overview/Timeline/Deaths/Replay shells; merge `1264d90ccb79f3260c24b0def3df849150e53250`; CI `32176263964`.
- M4-C / #44 / PR #49 — async AnalyzerWindow + `IPullStore`/engine integration; merge `72ec307d0189b0591a81b177450f30274285c0af`; CI `32177896535`.
- M4-D / #45 / PR #50 — combined sign-off; merge `517bd93c217f98e19a8e98334ee87a21c0b39599`; combined CI `32178240346`.

M4 established one shared pull/actor/time/result/mechanic selection state, focused panels outside `RecapWindow`, and asynchronous canonical loading/analysis away from ImGui `Draw`.

## Completed milestone: M3 — Analyzer Engine

**Status:** `APPROVED`

- M3-A / #33 / PR #37 — canonical actor/event indexes; merge `607b2c9530c7a94f24c7248df79e4317f6c9fbd7`.
- M3-B / #34 / PR #38 — analyzer registry/dependency execution/failure isolation; merge `d0b026df60213fb1aa453b2f06637c2c53d73368`; CI `32174557211`.
- M3-C / #35 / PR #39 — first generic `DeathEventAnalyzer` vertical slice; merge `924f57a517a85d8ede108192aae4d8a205c542b9`; CI `32174764060`.
- corrected M3-D / #36 / PR #47 — combined state revalidated after earlier premature sign-off; merge `9c99abe899284a08bdfd323672c1e381d72a5d03`; CI `32175231795`.

## Completed milestone: M2 — Full-pull live recorder

**Status:** `APPROVED`

M2 added the separate append-only `FullPullRecorder`, independent canonical persistence, live normalization, and additive runtime integration while preserving the legacy bounded death-recap path and enabling meaningful zero-death canonical pulls.

## Completed milestone: M1 — Canonical domain skeleton

**Status:** `APPROVED`

Established source-agnostic `RecordedPull`, typed `NormalizedEvent` records, stable IDs, deterministic pull-relative ordering/time, structured evidence-backed `AnalysisResult`, provenance/fidelity, serialization, and dependency-boundary tests.

## Completed milestone: M0 — Baseline and characterization

**Status:** `APPROVED`

Characterized lifecycle/archive/reset, death-gated legacy snapshots, persistence/schema behavior, replay round trips, and 10/30/60 display with 70-second capture / 75-second live retention before migration work began.

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
11. Generic M5 analysis never turns unknown evidence into certainty, last-hit chronology into blame, raw healing into waste, mitigation overlap into waste, or forced downtime into inactivity.
12. WTFDiG code/data is not ported until M8 and requires exact-path/commit provenance plus MIT attribution.

## Milestone roadmap

| Milestone | Status | Gate to start |
|---|---|---|
| M0 Baseline & characterization | APPROVED | Complete |
| M1 Canonical domain skeleton | APPROVED | Complete |
| M2 Full-pull live recorder | APPROVED | Complete |
| M3 Analyzer engine | APPROVED | Complete |
| M4 New workspace shell | APPROVED | Complete |
| M5 Generic hardcore analysis | APPROVED | Complete after sign-off PR merge |
| M6 FFLogs integration | AUTHORIZED | M5 approved |
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
| 2026-08-18 | Corrected M3-D / PR #47 | APPROVED | Combined-state CI `32175231795`. |
| 2026-08-18 | M4 PRs #46/#48/#49/#50 | APPROVED | Workspace shell/integration; combined CI `32178240346`. |
| 2026-08-18 | M5-A PR #59 | APPROVED | Shared targetability/status interval indexes. |
| 2026-08-18 | M5-B PR #60 | APPROVED | Live sampled status/targetability enrichment. |
| 2026-08-18 | M5-C PR #61 | APPROVED | Targetability-aware uptime/activity. |
| 2026-08-18 | M5-D PR #62 | APPROVED | Evidence-first death/raise context. |
| 2026-08-18 | M5-F healing PR #63 | APPROVED | Neutral raw-healing context. |
| 2026-08-19 | M5-E PR #64 | APPROVED | Mitigation scope correction + CI `32215987673`; merge `1145ad3ee5678499d32b77d7f25223634f62df0c`. |
| 2026-08-19 | M5-F timeline PR #65 | APPROVED | Per-actor fallback + canonical CastDuration correction; CI `32216083596`; merge `2e3c65fe298d2dd4d13ba481d79a58dfcffd30cb`. |
| 2026-08-19 | M5-G / PR #66 | APPROVED | Combined fixture/workspace/sign-off CI `32216593552`; M6 authorized after merge. |

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
