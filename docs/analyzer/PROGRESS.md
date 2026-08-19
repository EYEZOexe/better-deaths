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

### M7 — First Job Analyzer

**Status:** `AUTHORIZED`

**Gate opened by:** M6 FFLogs Integration approval and merge `1f600fc6f91800f2b64ce9e035d05014d4c0cd2b`.

M7 is now permitted to begin. The first job implementation must remain a source-agnostic analyzer over canonical data, with explicit job definitions/rules and evidence-backed findings. FFLogs, Dalamud, ImGui, networking and persistence details must not enter the job analyzer contract.

Before implementation, create bounded M7 work packages and choose the first job using the Technical Design v0.2 milestone scope. Do not pull M8 encounter/WTFDiG work forward.

## Completed milestone: M6 — FFLogs Integration

**Status:** `APPROVED`

**Parent issue:** #67 — completed  
**Integration/sign-off issue:** #73 — completed  
**Actor-fidelity blocker:** #79 — completed  
**Sign-off PR:** #80 — merged as `1f600fc6f91800f2b64ce9e035d05014d4c0cd2b`  
**Reviewed implementation/sign-off CI:** `32225159132`  
**Final PR-head CI:** `32225505916`  
**Detailed review:** `docs/analyzer/M6_FFLOGS_SIGNOFF.md`

M6 establishes an FFLogs-specific OAuth/GraphQL/import boundary that normalizes report/fight data into the same canonical `RecordedPull` model used by live capture. FFLogs DTOs, credentials, pagination and source identity remain outside Domain/Analysis. Imported pulls persist through `IPullStore` and execute through the same Analyzer Engine/Workspace as local pulls.

### M6 work packages

#### M6-A — Source/auth/security boundary

**Status:** `APPROVED`  
**Issue / PR:** #68 / #74

Evidence:
- `IPullDataSource`/structured integration-error boundary established before networking;
- FFLogs report/fight/event DTOs remain source-local;
- public-client vs user-authorized access modes are explicit;
- access tokens are opaque/redacted;
- sanitized report/fight provenance references are generated without spreading FFLogs fields into Domain.

#### M6-B — OAuth, GraphQL, pagination and cache

**Status:** `APPROVED`  
**Issue / PR:** #69 / #75

Evidence:
- client-credentials OAuth and public `/api/v2/client` path implemented behind the FFLogs boundary;
- private/user-authorized access requires a separate token provider rather than silently reusing client credentials;
- report/fight metadata and selected-fight events load asynchronously;
- pagination requires finite strictly advancing cursors;
- report metadata and event pages use credential-free cache keys, with event-page keys including report revision;
- HTTP/auth/rate-limit/network/protocol failures map to safe structured integration errors.

#### M6-C — Canonical FFLogs normalization

**Status:** `APPROVED`  
**Issue / PR:** #70 / #76

Evidence:
- FFLogs damage/heal/cast/action/status/death/raise/targetability facts translate to typed canonical events only when evidence exists;
- unsupported or insufficient events are explicitly skipped rather than guessed;
- deterministic pull-relative ordering and stable per-pull event identity are enforced;
- imported pull identity includes report code, fight ID and report revision;
- pull/event provenance is FFLogs-tagged without adding source-specific fields to canonical contracts.

#### M6-D — Local/FFLogs Analyzer Engine parity

**Status:** `APPROVED`  
**Issue / PR:** #71 / #77

Evidence:
- equivalent local and FFLogs canonical facts execute through the same analyzer registry/engine;
- structured analyzer meaning is parity-tested while source provenance remains different;
- unsupported FFLogs source facts remain source-normalization diagnostics rather than analyzer failures;
- FFLogs canonical persistence/reload preserves analyzer semantics.

#### M6-E — Analyzer Workspace import flow

**Status:** `APPROVED`  
**Issue / PR:** #72 / #78

Evidence:
- report/fight loading and fight import are asynchronous and never synchronously awaited from ImGui `Draw`;
- generation/cancellation guards prevent stale requests from replacing current workspace state;
- imported pulls save through `IPullStore`, refresh the canonical pull browser and select/load through the existing workspace path;
- FFLogs source/auth errors remain separate from analyzer-module failures;
- client-secret input is password-masked, kept out of canonical data, and cleared from the UI field after session construction;
- `RecapWindow` remains untouched by FFLogs workspace logic.

#### M6-F — Combined review and actor-fidelity correction

**Status:** `APPROVED`  
**Issues / PR:** #73 + blocker #79 / #80

Lead review found and rejected a fidelity gap before milestone sign-off: production report master actors were not reaching normalization, and report actor ID alone could merge distinct NPC/pet instances.

Correction evidence:
- production GraphQL report metadata now carries `masterData.actors` into fight import;
- report actor name/type/subType/petOwner metadata reaches normalization;
- non-player canonical identity uses report actor ID plus source/target instance evidence when present;
- player identity intentionally remains stable across instance-field noise;
- pet owners resolve through report master actor data;
- only selected-fight referenced actors plus required owners enter the canonical pull;
- missing source master data remains explicit unknown placeholders;
- deterministic fixtures prove same-report-ID/different-instance NPCs remain distinct;
- PR #80 changed-file review touched only `BetterDeaths/Sources/FFLogs/**`, FFLogs tests and M6/progress documentation; Domain, Analysis and `RecapWindow` were unchanged;
- CI `32225159132` passed the reviewed implementation/sign-off state and final PR-head CI `32225505916` passed before merge.

**Decision:** M6 is approved and complete. M7 — First Job Analyzer — is authorized from current `main`.

## Earlier completed milestone summaries

### M5 — Generic Hardcore Analysis

**Status:** `APPROVED`  
**Parent/sign-off:** #51 / #58  
**Combined CI:** `32216593552`  
**Detailed review:** `docs/analyzer/M5_SIGNOFF.md`

Established evidence-first generic targetability/status indexes, live sampled status/targetability enrichment, targetability-aware activity, death/raise context, mitigation coverage/counterfactuals, neutral healing context and explicit definition-driven buff/cooldown timelines. Generic analysis does not turn unknown evidence into certainty, last-hit chronology into blame, raw healing into waste, mitigation overlap into waste, or forced downtime into inactivity.

### M4 — New Workspace Shell

**Status:** `APPROVED`  
**Combined CI:** `32178240346`

Established one shared pull/actor/time/result/mechanic selection state, focused Overview/Timeline/Deaths/Replay panels outside `RecapWindow`, and asynchronous canonical loading/analysis away from ImGui `Draw`.

### M3 — Analyzer Engine

**Status:** `APPROVED`  
**Corrected combined CI:** `32175231795`

Established canonical actor/event indexes, registry/dependency execution with failure isolation, deterministic structured results and the first generic analyzer vertical slice. The original premature sign-off was explicitly superseded and corrected before later milestone work continued.

### M2 — Full-pull Live Recorder

**Status:** `APPROVED`

Added a separate append-only `FullPullRecorder`, canonical persistence, live normalization and additive runtime integration while preserving the bounded legacy death-recap path and enabling meaningful zero-death canonical pulls.

### M1 — Canonical Domain Skeleton

**Status:** `APPROVED`

Established source-agnostic `RecordedPull`, typed `NormalizedEvent` records, stable IDs, deterministic pull-relative ordering/time, structured evidence-backed `AnalysisResult`, provenance/fidelity and versioned canonical serialization boundaries.

### M0 — Baseline and Characterization

**Status:** `APPROVED`

Characterized lifecycle/archive/reset, death-gated legacy snapshots, persistence/schema behavior, replay round trips, and 10/30/60 display with 70-second capture / 75-second live retention before migration work began.

## Contracts later milestones must preserve or intentionally replace

1. Existing Better Deaths death recap remains available and its optimized short buffers remain bounded.
2. Full-pull history uses the separate canonical recorder; legacy recap lists are never made unbounded.
3. Legacy `PullDeathSnapshot` persistence remains supported during additive migration.
4. Canonical data has independent versioning/recovery behavior and unsupported versions fail explicitly.
5. Every future source normalizes into canonical `RecordedPull` / `NormalizedEvent` contracts before analyzer interpretation.
6. Stable event IDs, explicit sequence and pull-relative time remain the evidence/time-ordering basis; wall-clock timestamps are metadata.
7. Domain/analyzer contracts remain free of Dalamud services, FFLogs DTOs, ImGui types, network clients and persistence implementations.
8. Analyzer findings remain structured and evidence-backed; rendered prose is not the source of truth.
9. Analyzer modules do not make network calls, render UI, mutate pulls or depend on hidden global state.
10. New analyzer UI remains outside the monolithic legacy `RecapWindow`.
11. Heuristic/source-limited conclusions expose confidence/fidelity and remain unknown when evidence is insufficient.
12. WTFDiG code/data is not ported until M8 and requires exact-path/commit provenance plus MIT attribution.

## Milestone roadmap

| Milestone | Status | Gate to start |
|---|---|---|
| M0 Baseline & characterization | APPROVED | Complete |
| M1 Canonical domain skeleton | APPROVED | Complete |
| M2 Full-pull live recorder | APPROVED | Complete |
| M3 Analyzer engine | APPROVED | Complete |
| M4 New workspace shell | APPROVED | Complete |
| M5 Generic hardcore analysis | APPROVED | Complete |
| M6 FFLogs integration | APPROVED | Complete |
| M7 First job analyzer | AUTHORIZED | M6 approved |
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
| 2026-08-19 | M5 PRs #59–#66 | APPROVED | Generic analysis combined CI `32216593552`. |
| 2026-08-19 | M6-A / PR #74 | APPROVED | Source/auth/security boundary. |
| 2026-08-19 | M6-B / PR #75 | APPROVED | OAuth/GraphQL/pagination/cache. |
| 2026-08-19 | M6-C / PR #76 | APPROVED | Canonical FFLogs normalization. |
| 2026-08-19 | M6-D / PR #77 | APPROVED | Local/FFLogs analyzer parity. |
| 2026-08-19 | M6-E / PR #78 | APPROVED | Async Analyzer Workspace import flow. |
| 2026-08-19 | M6-F / PR #80 | APPROVED | #79 fidelity correction; CI `32225159132` and final-head CI `32225505916`; merge `1f600fc6f91800f2b64ce9e035d05014d4c0cd2b`. |

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
