# FFXIV Static Analyzer — Progress & Review Ledger

Status date: 2026-08-21
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

### M12 — FFLogs Semantic Fidelity

**Status:** `AUTHORIZED / IN PROGRESS`

**Parent issue:** #150

M12 is authorized as explicitly approved work packages. The v1 dependency, privacy, evidence,
persistence and compatibility contracts remain unchanged; fixes stay at source-owned normalization
boundaries and do not teach analyzers to accept source-specific identities.

### M12 work packages

#### M12-A — Canonical FFLogs actor/job identity mapping

**Status:** `APPROVED / COMPLETED / MERGED`

**Issue:** #151

**Implementation PR / merged commit:** #154 / `db6d654256cec4c00e3b2bf5e5ce2c05ca29605e`

**Reviewed implementation head / CI:** `cc9b818905ba72cb9fac80024b800bb1247ad726` / `32415273467`

FFLogs player job names, normalized name/slug variants and known canonical abbreviations now pass
through one source-owned allowlist mapper before `ActorRecord.JobAbbreviation` is constructed.
Missing, blank, non-player and unrecognized subtype values remain unmapped instead of becoming
invented canonical identities. The mapper covers the current combat-job set, including the eight
designated acceptance jobs: DNC, WHM, PCT, VPR, GNB, PLD, MNK and SGE.

Focused source-boundary, normalizer, default-engine and parity coverage proves that source
`Dancer` becomes canonical `DNC`, both Dancer modules activate without analyzer-side aliases or
post-normalization actor mutation, and equivalent local/FFLogs facts retain Dancer result
semantics. The independent 1001825/1825 exact-status mismatch remains locked as an M12-B
sentinel. The M11 anonymized fixture, truth manifest and persisted bytes are unchanged.

Local validation passed the 113-test focused source-boundary slice and 602 tests after excluding the existing
Windows newline-sensitive selection-contract check, production formatting verification, Release
plugin build and package-invariant validation. The unexcluded local run passed 602 of 603 tests;
the sole failure was the existing CRLF-sensitive source-text marker check. Exact-head Linux CI
passed restore, formatting, the full automated test suite, Release plugin build/package validation
and package artifact verification. Independent review approved the exact implementation head.

#### M12-B — FFLogs masterData ability catalog and source-ID classification

**Status:** `APPROVED / COMPLETED / MERGED`

**Issue:** #152

**Implementation PR / merged commit:** #156 / `a36fd7b9f2bd4a2f7cb79bd9d2dc0814d7d8bd28`

**Reviewed implementation head / CI:** `18b89d56a30a6329967fb4816b2c2f506a5aff8e` / `32418901802`

The FFLogs report metadata query and source-local contracts now retain `masterData.abilities`
`gameID`, name, icon and type fields alongside actors. A source-owned semantic decoder classifies
catalogued pass-through identities, exact verified status mappings and uncatalogued preserved
identities before canonical event creation. Only the explicitly evidenced status IDs `1001825`,
`1005084`, `1005085` and `1005086` map to canonical `1825`, `5084`, `5085` and `5086`, and only
when the exact source ID is present in that report's ability catalog. No name matching or arithmetic
family transform is used. Action identities remain unchanged.

Uncatalogued unknown or synthetic IDs remain in the canonical event with a source-local diagnostic
instead of being coerced or skipped. Unmapped catalogued identities likewise retain their source ID
and emit a truthful diagnostic that no verified canonical mapping exists; they are not labeled
synthetic. FFLogs status durations treat only negative finite values and the exact `9,999,000ms`
indefinite sentinel as unavailable, with separate typed duration diagnostics for both cases. Missing,
zero, ordinary, nearby and other long finite durations remain unchanged without a duration
diagnostic. Domain, Analysis, UI, persistence/schema, resource enrichment and the M11 anonymized
fixture/manifest remain unchanged.

Focused decoder/client/normalizer/integration coverage passed 43 tests; the broader FFLogs,
Dancer, Forsaken and immutable M11 slice passed 219 tests. The Release suite passed all 622 tests
after excluding the existing Windows newline-sensitive selection-contract check; the unexcluded
run passed 622 of 623 tests with only that existing CRLF-sensitive failure. Production formatting,
Release plugin build/package invariants and package artifact verification passed locally. The M11
fixture SHA-256 remains `D370AE57ECA46CFE97863D7F1E44D6E254A349DA74F228AC6838C7D7B80BB5FA`.
Exact-head Linux CI `32418901802` passed restore, formatting, the full automated suite, Release
plugin build/package validation and package artifact verification. Independent technical and
exact-head repository-state reviews approved the reviewed implementation head before merge.

**Authorization boundary:** M12-A and M12-B are approved and complete. M12-C (#153) is now the
next eligible authorized implementation slice. M13 and later work remain unauthorized until
explicitly approved. Overall M12 remains in progress.

## Completed milestone: M11 — Reality Baseline & Golden Pull

**Status:** `APPROVED / COMPLETED`

**Parent issue:** #145

**Implementation PR / merged commit:** #148 / `82a08e738be5fdfcef934672f87b46a96152b5a8`

**Reviewed implementation head / CI:** `c293145f06e47970bd05f8fb6856be7cb091cdfb` / `32412966379`

**Post-v1 design basis:** supplied Technical Design v0.3 DOCX

Technical Design v0.3 governs the approved post-v1 productization refinements while the dependency, privacy, licensing, evidence, persistence and compatibility invariants from Technical Design v0.2 remain in force.

### M11 work packages

#### M11-A — Sanitized real-pull golden fixture and truth manifest

**Status:** `APPROVED / MERGED`

**Issue:** #146

The supplied real fight-3 canonical export was transformed through anonymized export policy v1 into a deterministic 14,912-event golden fixture. The raw source remains outside the repository. Privacy, event-order, evidence-identity, semantic-mismatch, capability and default-engine truths are locked by `M11GoldenPullFixtureTests`; detailed evidence is recorded in `docs/analyzer/M11_FFLOGS_GOLDEN_PULL.md`.

#### M11-B — FFLogs semantic-fidelity characterization

**Status:** `APPROVED / MERGED`

**Issue:** #147

This slice is characterization-only: reproduce and lock the observed Dancer job-identity and source status-ID mismatches without implementing the later normalization fixes or broadening into M12 scope.

Combined M11 evidence passed exact-head restore, formatting, the full automated test suite, plugin build/package validation and package existence checks. Independent technical and privacy/design reviews approved the exact six-file diff. The anonymized fixture contains no detected direct actor identity, report/source linkage, wall-clock linkage, original pull identity, credential material, URL/email, raw-source path or raw-source hash; the documented residual combat-sequence correlation risk remains governed by anonymized export policy v1.

**Decision:** M11 is approved and complete. At M11 sign-off, the real-pull baseline reproduced
the then-current Dancer and Forsaken semantic failures without implementing their fixes.
Pre-normalization skipped-event diagnostics, effective-healing/resource fidelity and spatial
evidence remain explicitly unavailable from the supplied canonical artifact.

**Historical authorization boundary at M11 completion:** M12 and later milestones were
unauthorized until the required M11 evidence, review and explicit authorization were recorded.
That prerequisite is now satisfied for the explicitly authorized M12 work packages recorded above;
current work-package eligibility is recorded in the M12 authorization boundary above.

## Completed milestone: M10 — Hardening and Extraction Review

**Status:** `APPROVED — V1 ARCHITECTURE COMPLETE`

**Parent issue:** #118 — completion pending M10-E sign-off merge  
**Integration/sign-off issue:** #123 — PR #129  
**Detailed review:** `docs/analyzer/M10_V1_SIGNOFF.md`  
**Combined M10-E validation CI:** `32270674469`

M10 closed the v1 architectural loop with measured hardening rather than a cleanup rewrite. Capture, serialization, storage, Analyzer Engine and Session behavior were measured first; deterministic persistence/recovery gaps were hardened without a speculative backend rewrite; canonical and true anonymized export boundaries were added; and Domain/Analysis extraction was reviewed from dependency, test, API and package evidence rather than repository aesthetics.

### M10 work packages

#### M10-A — Performance baseline

**Status:** `APPROVED / MERGED`  
**Issue / PR:** #119 / #124  
**Merged commit:** `38c9a85a3dd075ce7b7cc6f0bc074eb70b0762f0`  
**CI:** `32259455682`  
**Detailed evidence:** `docs/analyzer/M10_PERFORMANCE_BASELINE.md`

Evidence:
- realistic 20-minute / 50,000-event canonical recorder fixture;
- serializer fixture includes 50,000 events + 4,000 positions;
- default Analyzer Engine runs over the long-pull fixture;
- real file-store fixture covers 20 x 1,000-event pulls;
- M9 500-pull Session Intelligence and orchestration gates remain active;
- generous regression ceilings are used instead of brittle hosted-CI microbenchmarks;
- no optimization, storage-backend change or project split was justified by the measurements.

Documented CI ceilings:
- recorder append + finalize: 5 s;
- serializer round-trip: 10 s and <= 100 MiB payload;
- default Analyzer Engine: 10 s;
- file store save/query/reload fixture: 20 s;
- 500-pull Session: 5 s pure aggregation / 10 s orchestration.

#### M10-B — Persistence/recovery/migration hardening

**Status:** `APPROVED / MERGED`  
**Issue / PR:** #120 / #125  
**Merged commit:** `3ae3a6e821f5958a09115ad4f766c288b8679793`  
**Final-head CI:** `32265402021`  
**Decision record:** `docs/analyzer/M10_STORAGE_DECISION.md`

Evidence:
- unsupported canonical pull schema fails explicitly during deserialization;
- corrupt primary index can use a valid backup deterministically;
- stale `.tmp` files cannot replace last-known-good canonical data;
- mismatched detail payload identity falls back only to a matching backup;
- detail-driven index rebuild validates canonical N-format filename identity against embedded `PullId`;
- unsupported index/file versions remain fail-closed rather than being hidden by recovery;
- future schema bumps require an explicit source->target migration policy and deterministic migration fixtures before the version changes;
- legacy Better Deaths saved-data support remains intentionally separate and retained.

**Storage decision:** retain the replaceable file-backed `FileCanonicalPullStore` behind `IPullStore` for v1. SQLite and compressed/chunked storage are deferred until measured pull-count/query/disk/crash requirements justify them.

#### M10-C — Canonical export and true anonymized export/privacy boundary

**Status:** `APPROVED / MERGED`  
**Issue / PR:** #121 / #127  
**Merged commit:** `e082e143fe56c40bcc14ba2dda42491398479ad2`  
**Final-head CI:** `32268302871`  
**Policy:** `docs/analyzer/M10_EXPORT_PRIVACY.md`

Evidence:
- canonical export remains lossless local interchange and is explicitly not presented as privacy-safe sharing output;
- anonymized export replaces player/pet/unknown actor display names while preserving canonical actor/owner relationships;
- original pull identity is replaced from fully sanitized canonical content rather than source identity;
- pull/event/position/marker `SourceReference` values are removed;
- pull `StartedAt` and event `ObservedAt` wall-clock timestamps are removed while pull-relative evidence remains;
- free-form world-marker labels are removed;
- event IDs/order, actor references, jobs, mechanics and position evidence remain analyzable;
- representative client ID, client secret, access token and Authorization-header strings are fixture-tested not to survive sensitive anonymized fields;
- export core accepts only canonical `RecordedPull` + options and has no FFLogs credential/client, Dalamud or ImGui contract dependency;
- Analyzer workspace exposes explicit canonical vs anonymized actions with serialization/file I/O outside `Draw` and no automatic upload/publish path.

#### M10-D — Domain/Analysis extraction decision

**Status:** `APPROVED / MERGED`  
**Issue / PR:** #122 / #128  
**Merged commit:** `4faee8bed44223633f62fcdc3e1463d9af2be19b`  
**Final-head CI:** `32269832664`  
**Decision record:** `docs/analyzer/M10_EXTRACTION_DECISION.md`

**Decision:** `KEEP SINGLE ASSEMBLY` for v1.

Evidence:
- Domain, Engine/Index, Generic, Jobs/Dancer, Encounters/Forsaken and Session Intelligence are already sufficiently pure for a future mechanical extraction;
- the plain `net10.0` test project already compiles/runs Domain/Analysis linked source without `Dalamud.NET.Sdk`, providing current test isolation;
- most Analysis implementation contracts remain `internal`, so extraction would force a deliberate public/friend/facade API decision;
- current Dalamud package validation expects one plugin DLL, so extraction would add package/deployment work and a new release failure surface;
- no current external binary consumer needs the pure layer;
- M10 measurements show no runtime/build problem solved by an assembly split.

The ADR records the smallest future extraction plan and concrete revisit triggers; extraction is not rejected permanently.

#### M10-E — Combined v1 Definition-of-Done review

**Status:** `APPROVED — MERGE PENDING`  
**Issue / PR:** #123 / #129  
**Combined validation CI:** `32270674469`  
**Detailed review:** `docs/analyzer/M10_V1_SIGNOFF.md`

M10-E adds only two bounded final guards plus the sign-off documentation:
- a meaningful zero-death live canonical pull must finalize, persist, reload and run the **default Analyzer Engine** without module failures;
- all Domain/Analysis implementation contract types are reflection-guarded against `BetterDeaths.Sources`, `BetterDeaths.Persistence`, `BetterDeaths.Windows`, Dalamud and ImGui dependencies.

CI `32270674469` passed restore, formatting, the complete automated suite including the retained M9/M10 performance fixtures, and plugin/package validation with those guards present. The final PR-head documentation reconciliation CI is recorded on PR #129 before merge.

### Technical Design v0.2 Definition of Done

| Requirement | Status | Primary evidence |
|---|---|---|
| Full pulls including zero-death pulls capture/save/reload/analyze locally | `SATISFIED` | `FullPullLifecycleIntegrationTests`; M10-E zero-death/default-engine guard |
| FFLogs imports normalize into the same canonical `RecordedPull` | `SATISFIED` | M6 sign-off; `FFLogsAnalyzerParityTests` |
| Generic death/mitigation/healing/damage-uptime is structured/evidence-backed | `SATISFIED` | `M5GenericAnalysisIntegrationTests`; M5 sign-off |
| At least one Job analyzer extension point | `SATISFIED` | Dancer M7 integration/parity fixtures |
| At least one Encounter/Mechanic extension point | `SATISFIED` | Forsaken M8 combined/parity fixtures |
| Raid Session recurrence uses opportunity counts across multiple pulls | `SATISFIED` | M9 500-pull combined fixture |
| Timeline/findings/deaths/replay share synchronized selection/time | `SATISFIED` | `AnalyzerWorkspaceSelectionContractTests`; M4 sign-off |
| Existing Better Deaths death recap not materially regressed | `SATISFIED` automated characterization | legacy persistence/lifecycle characterization; in-game smoke remains release QA |
| Persisted data versioned and migration behavior tested | `SATISFIED` | canonical compatibility suites; M10-B hardening + migration policy |
| Long Ultimate pull / progression-session performance acceptable | `SATISFIED` within measured CI scope | M10-A 50k-event / file-store gates; M9 500-pull gates |
| Source-specific DTO/service/UI types stay out of Domain/analyzer contracts | `SATISFIED` | existing boundary tests + M10-E broad implementation reflection guard |
| Privacy remains local-first; FFLogs credentials handled safely | `SATISFIED` | M6 credential boundary; M10-C anonymized export/privacy fixtures |
| Required third-party notices present | `SATISFIED` | `THIRD_PARTY_NOTICES.md`; package validator |

**Decision:** the Technical Design v0.2 architecture and automated v1 Definition-of-Done gates are satisfied. GitHub CI cannot launch FFXIV/Dalamud itself, so in-game UI/capture/frame-time smoke remains normal release QA before distributing a build and is not misrepresented as automated evidence.

## Completed milestone: M9 — Session Intelligence

**Status:** `APPROVED / COMPLETED`

**Parent issue:** #104 — completed  
**Integration/sign-off issue:** #109 — completed  
**Dancer recurrence blocker:** #114 — completed by PR #115  
**Performance/combined fixture PR:** #116 — merged as `44b20c67f8adcda1efdbaa7f5833b2e89fa10a70`  
**Final sign-off PR:** #117 — merged as `ae7420d30c200a4ee1251ba62c45d728cb1d90e5`  
**Combined fixture CI:** `32257711062`  
**Final sign-off CI:** `32258653853`  
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

**Status:** `APPROVED / MERGED`  
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

**Decision:** M9 satisfies the Technical Design v0.2 Session Intelligence milestone and is complete after final sign-off PR #117 merged to `main`.

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
