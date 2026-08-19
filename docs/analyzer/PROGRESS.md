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

### M10 — Hardening and Extraction Review

**Status:** `AUTHORIZED AFTER M9 SIGN-OFF MERGE`

**Gate opened by:** M9 Session Intelligence combined implementation and performance review. Final authorization takes effect when the M9 sign-off/ledger PR merges.

M10 must be evidence-based rather than a cleanup rewrite. Profile storage, capture, analysis and realistic long-session behavior first; improve migrations, export/anonymization and operational hardening where measured need exists; then evaluate whether pure Domain/Analysis extraction into a separate assembly has demonstrated value. Do not split projects solely to make the repository look cleaner.

The v1 gate remains the Technical Design Definition of Done: complete local/FFLogs canonical analysis, generic/job/encounter/session extension points, synchronized review navigation, versioned persistence/migration behavior, acceptable measured long-pull/session performance, source-agnostic analysis boundaries, privacy, and retained third-party attribution.

## Completed milestone: M9 — Session Intelligence

**Status:** `APPROVED FOR MERGE`

**Parent issue:** #104 — completion pending final sign-off merge  
**Integration/sign-off issue:** #109 — completion pending final sign-off merge  
**Dancer recurrence blocker:** #114 — completed by PR #115  
**Performance/combined fixture PR:** #116 — merged as `44b20c67f8adcda1efdbaa7f5833b2e89fa10a70`  
**Combined fixture CI:** `32257711062`  
**Detailed review:** `docs/analyzer/M9_SESSION_SIGNOFF.md`

M9 establishes the cross-pull Session Intelligence layer over structured canonical analysis. Recurrence uses producer-owned `AnalyzerId + RuleKey` identity rather than rendered prose, opportunity denominators preserve unknown evidence, wipe causes require explicit structured cause evidence, progression/trends remain deterministic and evidence-aware, and session loading/drill-down stays asynchronous and separated from rendering. A 500-pull combined fixture guards progression-night scale behavior.

### M9 work packages

#### M9-A — Stable recurrence identity and pure session contracts

**Status:** `APPROVED`  
**Issue / PR:** #105 / #110  
**Merged commit:** `614c58ab552fd868dd7b51590145eea4332e6104`  
**CI:** `32242548828`

Evidence:
- optional producer-owned `AnalysisResult.RuleKey` establishes stable cross-pull semantic identity;
- `SessionFindingKey` is `AnalyzerId + RuleKey`, never localized title/summary, result ID, actor ID or timestamp;
- cross-pull `SessionEvidenceReference` carries explicit `PullId + AnalysisResultId` plus pull-local actor/time context;
- opportunity states are explicit `Unknown / Evaluable / NotApplicable`;
- optional stable participant identity is distinct from pull-local `ActorId`;
- pure Session contracts contain no source, persistence, UI, network or single-pull Analyzer Engine dependency.

#### M9-B — Recurrence, wipe causes, progression and trends

**Status:** `APPROVED`  
**Issue / PR:** #106 / #111  
**Merged commit:** `9a99446754cd0d6101b131e9d959e1164613d451`  
**CI:** `32243347621`

Evidence:
- actionable recurrence begins at `Optimization` severity and reports finding/evaluable/unknown counts plus nullable rate;
- unknown opportunities never enter the denominator;
- a finding can prove only its own opportunity and cannot fabricate successful opportunities;
- unkeyed actionable results remain excluded and visible through diagnostics;
- wipe-cause aggregation accepts only an explicit wipe outcome plus explicitly referenced keyed Warning-or-higher structured cause;
- missing/invalid cause evidence remains unknown and death chronology is never promoted to blame;
- progression consumes explicit phase observations and keeps pulls without evidence unknown;
- recent-vs-prior trends use opportunity-normalized rates, minimum sample gates and deterministic pull ordering.

#### M9-C — Async session orchestration

**Status:** `APPROVED`  
**Issue / PR:** #107 / #112  
**Merged commit:** `c4b9cf6c1ea1ddcf71aa679242c2b16784871b12`  
**CI:** `32244501334`

Evidence:
- compact `PullSummary` rows are queried before full canonical pull loads;
- territory/time filtering happens before full-load work;
- full pulls load/analyze sequentially and asynchronously, then publish compact `SessionPullAnalysis` rather than retained `RecordedPull` event streams;
- missing pulls, load failures, analyzer failures and enrichment failures are isolated into structured diagnostics;
- cancellation and generation invalidation prevent stale loads from replacing newer session state;
- default Forsaken opportunities become evaluable only from complete exact evidence; sampled/incomplete evidence stays unknown;
- stable participant identity and wipe/kill outcome are not fabricated by the default enricher.

#### M9-D — Generic Session workspace panel and drill-down

**Status:** `APPROVED`  
**Issue / PR:** #108 / #113  
**Merged commit:** `520a5cdce6a2af69d2527387ec8323eb109fc790`  
**CI:** `32245290450`

Evidence:
- one generic `AnalyzerSessionPanel` renders progression, recurrence rates, unknown counts, wipe causes, recent trends and diagnostics;
- recurrence/wipe rows are bounded for per-frame rendering;
- panel performs no session analysis, persistence query, FFLogs access or encounter/job-specific interpretation;
- explicit Load/Refresh session work runs asynchronously outside `Draw` with separate cancellation/generation state;
- evidence drill-down uses explicit `PullId + AnalysisResultId`, asynchronously loads the contributing pull and reuses `AnalyzerWorkspaceSelection.SelectResult` for synchronized actor/time/result navigation;
- `RecapWindow.cs` remains outside the new Session architecture.

#### M9 blocker — Dancer stable recurrence keys

**Status:** `APPROVED`  
**Issue / PR:** #114 / #115  
**Merged commit:** `9851b90e694b395e0fed1ccc09d0f007d3776fa4`  
**CI:** `32245996656`

Lead review identified that M7 Dancer actionable results predated `RuleKey` and therefore could not legally participate in M9 recurrence. The fix stayed at the producer boundary: explicit semantic constants or stable prefixes plus immutable action/status/definition identity were added without changing M7 severities, thresholds, evidence or UI. Equivalent local/FFLogs facts use the same recurrence keys; Session logic does not reverse-engineer Dancer prose.

#### M9-E — Performance fixture, combined review and sign-off

**Status:** `APPROVED FOR MERGE`  
**Issue / PR:** #109 / #116  
**Merged fixture commit:** `44b20c67f8adcda1efdbaa7f5833b2e89fa10a70`  
**CI:** `32257711062`

Combined evidence:
- 500-pull pure Session fixture reports 100 Forsaken findings / 450 known opportunities / 50 unknown opportunities with evidence for every counted finding;
- representative per-player Dancer recurrence uses an explicit stable participant identity and producer-owned rule key;
- 100 known / 400 unknown wipe causes prove unknown cause handling rather than chronology-derived blame;
- P1/P2/P3/P4 phase-reach variation and recent-vs-prior improvement are exercised;
- deterministic input reordering produces stable recurrence/wipe/progression/trend outputs;
- 500-pull orchestration queries compact summaries first, loads 500 canonical pulls with 20 events each sequentially, keeps max concurrent full-pull loads at one and does not publish retained `RecordedPull` objects;
- generous 5-second pure-aggregation and 10-second orchestration CI guards passed;
- combined CI passed restore, formatting, all tests and plugin/package build;
- no M10 extraction/storage rewrite leaked into M9.

**Decision:** M9 satisfies the Technical Design v0.2 Session Intelligence milestone and is approved for final sign-off merge. After this reconciliation lands on `main`, complete #109/#104 and authorize M10 — Hardening and Extraction Review.

## Completed milestone: M8 — First Encounter Pack: Dancing Mad Ultimate / Forsaken

**Status:** `APPROVED`

**Parent issue:** #94 — completed  
**Integration/sign-off issue:** #98 — completed  
**Sign-off PR:** #102 — merged as `9b47183fe5fbaf03bd45ac73dfe31af6739f79db`  
**Combined fixture CI:** `32240800389`  
**Final PR-head CI:** `32241136029`  
**Detailed review:** `docs/analyzer/M8_FORSAKEN_SIGNOFF.md`

M8 proves the first encounter-analysis vertical slice using Dancing Mad Ultimate P2 Forsaken opening partner/group assignment. Encounter definitions and rules remain isolated from capture/engine/UI code, the analyzer consumes canonical actor/job/status evidence only, uncertainty never becomes a fabricated failure, and one generic Mechanics panel renders Encounter results through the shared workspace selection model.

### M8 work packages

#### M8-A — WTFDiG audit, encounter-definition boundary and Forsaken data

**Status:** `APPROVED`  
**Issue / PR:** #95 / #99  
**Merged commit:** `5ad919105ab35c5b206983fb89ab175e76c41b94`

Evidence:
- reusable pure `EncounterDefinition`, `EncounterPhaseDefinition`, `AssignmentRule`, `ArenaGeometry`, and party-role classification contracts;
- deterministic canonical job -> Tank/Healer/Melee/Ranged resolution;
- DMU territory/arena/phase and Forsaken status semantics required by the slice;
- WTFDiG strategy/role semantics pinned to `EYEZOexe/wtfdig@73a2ffa959b8f57bfbe7a1a75d5e43383ae2ea81` and exact upstream paths;
- `docs/analyzer/M8_WTFDIG_AUDIT.md` records reused versus deferred data;
- WTFDiG MIT attribution added to `THIRD_PARTY_NOTICES.md` at first direct/derived reuse;
- later tower/South Adjust/Future-Past geometry deliberately deferred until canonical evidence can support it.

#### M8-B — Forsaken opening partner/group analyzer

**Status:** `APPROVED`  
**Issue / PR:** #96 / #100  
**Merged commit:** `a3c6f81c541f1b328107cd0c5aeda037f66d4d80`

Evidence:
- analyzer is `AnalyzerScope.Encounter` and consumes canonical actor/job/status/index evidence only;
- bounded opening-status batch and complete participant/status requirements prevent absence-based false certainty;
- all Tank↔Healer and Melee↔Ranged full-party layouts are evaluated without manufacturing MT/OT/H1/H2/M1/M2/R1/R2 identities from ActorId ordering;
- unique compatible layouts emit neutral Group A/B assignments with actor/event/time evidence;
- multiple compatible layouts remain explicitly ambiguous;
- incomplete, duplicate or sampled evidence cannot become an actionable failure;
- complete exact incompatible evidence emits one structured non-blaming Warning with expected/observed/cause/consequence/confidence;
- equivalent local and FFLogs canonical facts produce equivalent encounter semantics.

#### M8-C — Generic Mechanics panel and workspace composition

**Status:** `APPROVED`  
**Issue / PR:** #97 / #101  
**Merged commit:** `0f3d27d3b5ce23b4cca808c639e9a42956b29f25`  
**CI:** `32240327553`

Evidence:
- one generic `AnalyzerMechanicsPanel` renders structured `AnalysisCategory.Mechanic` results;
- selecting a mechanic result uses `AnalyzerWorkspaceSelection.SelectResult` and therefore shared actor/time/result navigation;
- Forsaken registers through the existing default Analyzer Engine registry rather than rendering code;
- workspace integration fixture surfaces Forsaken findings through the real default engine path;
- panel boundary tests reject Analyzer Engine, persistence, FFLogs, Dancer, Forsaken and legacy replay coupling;
- `RecapWindow.cs` remains untouched.

#### M8-D — Combined fixtures/review/sign-off

**Status:** `APPROVED`  
**Issue / PR:** #98 / #102  
**Merged commit:** `9b47183fe5fbaf03bd45ac73dfe31af6739f79db`  
**Combined fixture CI:** `32240800389`  
**Final PR-head CI:** `32241136029`

Combined evidence:
- known compatible exact opening produces four evidence-backed neutral assignments;
- known incompatible complete exact opening produces one non-blaming Warning with complete party evidence;
- ambiguous, incomplete and sampled fixtures do not invent actionable failures;
- local/FFLogs canonical parity is preserved;
- WTFDiG exact-path/commit provenance and MIT notice are retained;
- no M9 session logic leaked into M8;
- final CI passed restore, formatting, all tests and plugin/package build.

**Decision:** M8 is approved and complete. M9 — Session Intelligence — is authorized from current `main`.

## Completed milestone: M7 — First Job Analyzer: Dancer

**Status:** `APPROVED`

**Parent issue:** #82 — completed  
**Integration/sign-off issue:** #87 — completed  
**Sign-off PR:** #92 — merged as `eae228476c1683b7a267c9391aa79484263cb0ef`  
**Combined CI:** `32230435195`  
**Final PR-head CI:** `32230698017`  
**Detailed review:** `docs/analyzer/M7_DANCER_SIGNOFF.md`

M7 proves the first xivanalysis-style Job analyzer vertical slice using Dancer. Job modules consume only canonical pull/event/index contracts plus explicit job definitions, emit structured evidence-backed results, and render through one generic Jobs panel. No DNC-specific UI architecture or source-specific analyzer dependency was introduced.

### M7 work packages

#### M7-A — Job-definition boundary and DNC data/provenance

**Status:** `APPROVED`  
**Issue / PR:** #83 / #88  
**Merged commit:** `f0c60d322600fd5ce9c84371d44eaf33226b56a7`  
**CI:** `32227679821`

Evidence:
- reusable source-agnostic `JobDefinition` / action / status definition contracts;
- deterministic validation for duplicate/invalid action, status, cooldown, charge and cooldown-group data;
- DNC action/status/cooldown data required by the vertical slice;
- Standard Step / Finishing Move shared cooldown group represented explicitly;
- xivanalysis Dawntrail provenance pinned to commit `f90bfac9ad9984354437b83e529f5dd709346413` and exact upstream paths;
- MIT attribution recorded in `THIRD_PARTY_NOTICES.md`.

#### M7-B — DNC dance/core/proc/partner analysis

**Status:** `APPROVED`  
**Issue / PR:** #84 / #89  
**Merged commit:** `7f6988b7fc4cdbcdbda38dae1d2075f8524446b1`  
**CI:** `32228178813`

Evidence:
- DNC actors are detected from canonical `ActorRecord.JobAbbreviation`;
- explicit under-stepped Standard/Technical finish variants produce evidence-backed warnings;
- missing finish evidence is not inferred as a mistake;
- unused-proc warnings require known duration expiry plus exact source evidence;
- sampled/unknown status evidence remains unknown;
- Dance Partner assignment is recorded neutrally from canonical source->target status evidence;
- contradictory assignment is reported only for known overlapping intervals and never as an optimal-partner DPS ranking judgment.

#### M7-C — DNC burst/cooldown cadence and targetability-aware GCD execution

**Status:** `APPROVED`  
**Issue / PR:** #85 / #90  
**Merged commit:** `e1ff044611574cb7d021fb20193b3817066caf90`  
**Corrected CI:** `32229549949`

Evidence:
- Devilment/Technical Finish alignment uses explicit canonical action ordering and pinned xivanalysis behavioral provenance;
- no preceding Technical Finish means unknown/silent rather than a fabricated alignment error;
- late-window, cooldown-drift and GCD-gap absence judgments require exact action coverage;
- cooldown readiness starts from an observed prior use rather than pull start/prepull assumptions;
- forced untargetable and unknown targetability time are excluded from drift/opportunity calculations;
- death-containing execution windows are deferred rather than charged as ordinary execution loss;
- no Skill Speed simulation or synthetic expected-GCD count is used;
- Esprit/feather overcap/underuse remains unreported because verified canonical gauge semantics are not yet available.

Lead review caught an initial compile issue and used the correction to further tighten the missing-Technical evidence rule before merge.

#### M7-D — Generic Jobs panel and workspace composition

**Status:** `APPROVED`  
**Issue / PR:** #86 / #91  
**Merged commit:** `13072911fab1254e294f42904db75fdf1092c3c0`  
**CI:** `32230088925`

Evidence:
- one generic `AnalyzerJobsPanel` renders structured Job-category results without DNC-specific rendering classes;
- selecting a result uses shared `AnalyzerWorkspaceSelection.SelectResult`, synchronizing primary actor + evidence time;
- both DNC analyzers register through the existing default `AnalyzerEngine` composition seam;
- panel source-contract tests reject analyzer execution, persistence, FFLogs, Dancer and `RecapWindow` coupling;
- `RecapWindow.cs` remains untouched.

#### M7-E — Combined fixtures/review/sign-off

**Status:** `APPROVED`  
**Issue / PR:** #87 / #92  
**Merged commit:** `eae228476c1683b7a267c9391aa79484263cb0ef`  
**Combined CI:** `32230435195`  
**Final PR-head CI:** `32230698017`

Combined evidence:
- both DNC analyzers execute together over the same canonical pull;
- representative fixture covers under-stepped dance, exact proc expiry, partner evidence, Devilment timing, Flourish cadence and targetable GCD-gap analysis;
- every Warning/Error has actors, time, event evidence and confidence;
- clean DNC fixture produces no invented findings;
- equivalent local/FFLogs canonical facts produce equivalent combined DNC semantics;
- explicit gauge events do not create fabricated Esprit/feather resource verdicts;
- M8 encounter/WTFDiG implementation did not leak into M7;
- combined and final-head CI both passed restore, formatting, all tests, and plugin/package build.

**Decision:** M7 is approved and complete. M8 — First Encounter Pack — is authorized from current `main`.

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

**Decision:** M6 is approved and complete.

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
12. WTFDiG direct reuse requires exact-path/commit provenance plus MIT attribution; strategy knowledge is translated behind source-agnostic encounter abstractions rather than copied into the canonical pull model.
13. Job analyzers remain generic-engine modules; missing resource/source evidence stays unknown rather than being reconstructed silently.
14. Encounter analyzers consume canonical evidence plus explicit encounter definitions, do not fabricate static assignment identities, and keep insufficient/ambiguous evidence non-actionable.
15. Mechanics UI remains generic and downstream of structured Encounter results; encounter-specific analysis never lives in rendering code or `RecapWindow`.
16. Session recurrence identity remains explicit producer-owned `AnalyzerId + RuleKey`; rendered prose, result IDs, actor IDs and timestamps are not recurrence keys.
17. Session opportunities preserve `Unknown` / `NotApplicable`; unknown evidence never becomes a pass or enters the evaluable denominator.
18. Cross-pull drill-down carries explicit `PullId + AnalysisResultId`; pull-local identities are never treated as globally scoped evidence.
19. Reliable wipe causes require explicit structured cause evidence; death/result chronology does not become blame.
20. Session loading remains asynchronous/bounded and published session results do not retain full canonical pulls unless future profiling demonstrates a real need.
21. M10 extraction remains evidence-based; no assembly split is justified solely by aesthetic repository structure.

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
| M7 First job analyzer | APPROVED | Complete |
| M8 First encounter pack | APPROVED | Complete |
| M9 Session intelligence | APPROVED FOR MERGE | Final sign-off/ledger merge |
| M10 Hardening/extraction review | AUTHORIZED AFTER M9 SIGN-OFF MERGE | M9 approved/merged |

## WTFDiG provenance baseline

Fork inspected: `EYEZOexe/wtfdig`  
Baseline commit observed: `73a2ffa959b8f57bfbe7a1a75d5e43383ae2ea81`  
License: MIT, copyright 2024 Matthew Czubakowski.

Verified reusable surfaces:
- `src/lib/arena.ts` — role/group matching, waymarks, arena/player/boss/AoE/tether/arrow/polygon concepts;
- `src/routes/ultimates/umad/data.ts` — phase/mechanic scaffolding and role/light-party strategy assignment data.

M8 directly/derivatively reused only the audited Forsaken strategy/role semantics required by the first slice, translated behind C# encounter definitions. Exact path + commit provenance is recorded in `docs/analyzer/M8_WTFDIG_AUDIT.md`, and `THIRD_PARTY_NOTICES.md` now retains the WTFDiG MIT notice. Later encounter reuse must continue to record its own exact upstream provenance.

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
| 2026-08-19 | M7-A / PR #88 | APPROVED | DNC job definitions/provenance; CI `32227679821`; merge `f0c60d322600fd5ce9c84371d44eaf33226b56a7`. |
| 2026-08-19 | M7-B / PR #89 | APPROVED | Evidence-first DNC dance/proc/partner analyzer; CI `32228178813`; merge `7f6988b7fc4cdbcdbda38dae1d2075f8524446b1`. |
| 2026-08-19 | M7-C / PR #90 | APPROVED | DNC burst/cooldown/targetable GCD analyzer; corrected CI `32229549949`; merge `e1ff044611574cb7d021fb20193b3817066caf90`. |
| 2026-08-19 | M7-D / PR #91 | APPROVED | Generic Jobs panel/workspace composition; CI `32230088925`; merge `13072911fab1254e294f42904db75fdf1092c3c0`. |
| 2026-08-19 | M7-E / PR #92 | APPROVED | Combined DNC fixtures/review; CI `32230435195`, final-head CI `32230698017`; merge `eae228476c1683b7a267c9391aa79484263cb0ef`. |
| 2026-08-19 | M8-A / PR #99 | APPROVED | Encounter-definition boundary, WTFDiG audit/provenance and Forsaken definition; merge `5ad919105ab35c5b206983fb89ab175e76c41b94`. |
| 2026-08-19 | M8-B / PR #100 | APPROVED | Canonical Forsaken opening assignment analyzer with explicit ambiguity/insufficient-evidence behavior; merge `a3c6f81c541f1b328107cd0c5aeda037f66d4d80`. |
| 2026-08-19 | M8-C / PR #101 | APPROVED | Generic Mechanics panel + default workspace composition; CI `32240327553`; merge `0f3d27d3b5ce23b4cca808c639e9a42956b29f25`. |
| 2026-08-19 | M8-D / PR #102 | APPROVED | Combined Forsaken fixtures/review; CI `32240800389`, final-head CI `32241136029`; merge `9b47183fe5fbaf03bd45ac73dfe31af6739f79db`. |
| 2026-08-19 | M9-A / PR #110 | APPROVED | Stable recurrence identity/session contracts; CI `32242548828`; merge `614c58ab552fd868dd7b51590145eea4332e6104`. |
| 2026-08-19 | M9-B / PR #111 | APPROVED | Recurrence/opportunity, wipe-cause, progression and trend analysis; CI `32243347621`; merge `9a99446754cd0d6101b131e9d959e1164613d451`. |
| 2026-08-19 | M9-C / PR #112 | APPROVED | Async session orchestration, conservative enrichment and partial-failure isolation; CI `32244501334`; merge `c4b9cf6c1ea1ddcf71aa679242c2b16784871b12`. |
| 2026-08-19 | M9-D / PR #113 | APPROVED | Generic Session panel, async load flow and PullId+ResultId shared-selection drill-down; CI `32245290450`; merge `520a5cdce6a2af69d2527387ec8323eb109fc790`. |
| 2026-08-19 | M9 blocker / PR #115 | APPROVED | Producer-owned Dancer recurrence RuleKeys; CI `32245996656`; merge `9851b90e694b395e0fed1ccc09d0f007d3776fa4`. |
| 2026-08-19 | M9-E / PR #116 | APPROVED | 500-pull pure/orchestration performance fixture; CI `32257711062`; merge `44b20c67f8adcda1efdbaa7f5833b2e89fa10a70`. |
| 2026-08-19 | M9 final sign-off / pending PR | APPROVED FOR MERGE | `M9_SESSION_SIGNOFF.md`; M10 gate activates after merge. |

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
