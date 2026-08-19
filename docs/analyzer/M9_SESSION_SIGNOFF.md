# M9 Session Intelligence — sign-off

Status: **APPROVED FOR MERGE**

Parent: #104  
Integration/sign-off package: #109  
Dancer recurrence blocker: #114 / PR #115  
Performance/combined fixture PR: #116  
Governing design: Technical Design v0.2, Sections 11, 12, 15 and 16

## Scope reviewed

M9 adds the first cross-pull Session Intelligence vertical slice over structured canonical pull analysis. The implementation was reviewed from M8-complete baseline `9bd7f6dd29a81753869a7230a90d1514727f7866` through M9 combined main `44b20c67f8adcda1efdbaa7f5833b2e89fa10a70`.

Reviewed M9 implementation packages:

- M9-A — #105 / PR #110 / merge `614c58ab552fd868dd7b51590145eea4332e6104` / CI `32242548828`.
- M9-B — #106 / PR #111 / merge `9a99446754cd0d6101b131e9d959e1164613d451` / CI `32243347621`.
- M9-C — #107 / PR #112 / merge `c4b9cf6c1ea1ddcf71aa679242c2b16784871b12` / CI `32244501334`.
- M9-D — #108 / PR #113 / merge `520a5cdce6a2af69d2527387ec8323eb109fc790` / CI `32245290450`.
- Dancer RuleKey blocker — #114 / PR #115 / merge `9851b90e694b395e0fed1ccc09d0f007d3776fa4` / CI `32245996656`.
- M9-E performance/combined fixture — #109 / PR #116 / merge `44b20c67f8adcda1efdbaa7f5833b2e89fa10a70` / CI `32257711062`.

## Architecture decision

**APPROVED.** M9 follows the Technical Design rule that Session analysis consumes structured pull-level results instead of rendered UI prose.

The single-pull `IAnalyzerModule`/`AnalyzerContext` contract remains single-pull. Multi-pull state was not forced into the Analyzer Engine. Session analysis instead receives compact `SessionPullAnalysis` records containing canonical pull identity/metadata, structured `AnalysisResult` values, explicit opportunity/progression/outcome evidence and optional stable participant identity.

## Stable recurrence identity

Cross-pull recurrence uses producer-owned semantic identity:

`AnalyzerId + AnalysisResult.RuleKey`

The review confirms that recurrence identity does **not** use localized `Title`/`Summary`, pull-specific `AnalysisResultId`, pull-local `ActorId`, timestamps, sequence IDs or observed metric values.

`SessionEvidenceReference` carries the contributing `PullId + AnalysisResultId` plus pull-local actor/time context, so drill-down evidence remains explicit across pulls rather than pretending event/result identities are globally scoped.

The M9 review found one legitimate prerequisite gap: M7 Dancer actionable results predated `RuleKey`. #114/PR #115 corrected this at the producer boundary. Stable Dancer keys are now explicit semantic constants or stable prefixes combined with immutable definition/status identity; the Session algorithm was not changed to reverse-engineer Dancer prose.

## Recurrence and opportunity semantics

The pure Session Intelligence analyzer distinguishes:

- finding count;
- evaluable opportunity count;
- unknown opportunity count;
- nullable opportunity-normalized rate.

`Unknown` and `NotApplicable` opportunities do not become successful denominators. A concrete structured finding may prove only that its own opportunity occurred; it cannot fabricate additional successful opportunities.

Actionable recurrence begins at `Optimization` severity. Informational/observational results are not silently promoted into failure counts. Actionable results without an explicit stable rule key remain excluded and are surfaced through diagnostics.

## Wipe-cause review

Wipe-cause aggregation requires an explicit `SessionPullOutcomeKind.Wipe` plus an explicitly referenced structured `CauseResultId` that resolves to a keyed Warning-or-higher result.

Missing, invalid, unkeyed or insufficient cause evidence remains unknown. The implementation does not substitute the last damage event, last death or chronologically last finding as wipe blame.

This satisfies the design's evidence-first/no-silent-certainty requirement.

## Progression and trend review

Session progression consumes explicit phase-reach observations and produces deterministic phase reach counts/rates, average reach timing and furthest observed phase. Pulls with no explicit progression evidence stay unknown.

Recent-vs-prior trend classification uses opportunity-normalized rates, configurable sample minimums and deterministic pull ordering. `Improving`, `Stable` and `Worsening` are emitted only when both comparison windows contain sufficient evaluable opportunities; otherwise the result remains `InsufficientEvidence`.

## Persistence/orchestration review

`AnalyzerSessionDataController` keeps persistence and application orchestration outside the pure `Analysis/Sessions` layer.

The reviewed flow:

1. queries compact `PullSummary` rows first;
2. applies requested territory/time filtering before full loads;
3. loads and analyzes full canonical pulls asynchronously and sequentially;
4. converts each pull into compact `SessionPullAnalysis` data;
5. publishes the session result without retaining the full `RecordedPull` event streams;
6. isolates missing pulls, load failures, analyzer failures and enrichment failures into structured diagnostics;
7. propagates cancellation and generation invalidation so stale loads cannot replace newer selections.

The default enrichment remains deliberately conservative. Forsaken failure opportunities become evaluable only with complete exact canonical evidence. Sampled/incomplete evidence remains unknown. Pull kill/wipe outcome and stable participant identity are not fabricated when the canonical source cannot establish them.

## Workspace/UI review

One generic `AnalyzerSessionPanel` renders structured Session Intelligence output. It displays:

- progression;
- recurring finding rates with finding/opportunity/sample counts;
- explicit unknown counts;
- evidence-backed wipe causes;
- recent trend directions;
- session diagnostics.

Rendering rows are bounded. The panel does not perform session analysis, query persistence, call FFLogs, or contain Dancer/Forsaken-specific analysis logic.

Evidence drill-down passes `SessionEvidenceReference` to outer workspace navigation. The workspace asynchronously loads the contributing `PullId`, resolves the exact `AnalysisResultId`, then reuses `AnalyzerWorkspaceSelection.SelectResult`, preserving synchronized actor/time/result navigation with Timeline, Mechanics, Jobs, Deaths and Replay.

`RecapWindow.cs` was not used as a new Session implementation surface.

## Performance / combined fixture gate

PR #116 adds a progression-night scale test fixture with **500 pulls**.

Pure aggregation fixture proves:

- 100 Forsaken findings / 450 known opportunities / 50 unknown opportunities;
- explicit evidence for every counted recurrence;
- stable per-player Dancer recurrence with an explicit stable participant key;
- 100 known and 400 unknown wipe causes;
- P1/P2/P3/P4 phase-reach variation;
- recent-vs-prior improving Dancer trend;
- deterministic results under input reordering;
- a generous 5-second CI guard for 500-pull pure aggregation.

Application orchestration fixture proves:

- 500 compact summaries are queried before full loads;
- 500 canonical pulls containing 20 events each are loaded/analyzed sequentially;
- max concurrent full-pull loads remains one;
- clean-fixture diagnostics remain empty;
- published Session objects do not retain `RecordedPull` objects;
- a generous 10-second CI guard covers the 500-pull orchestration path.

CI `32257711062` passed restore, formatting, all tests and plugin/package build on the current-main combined state.

## Technical Design acceptance review

- [x] Repeated findings group by stable analyzer/rule identity, never rendered prose.
- [x] Recurrence exposes findings/opportunities/rate and cross-pull evidence references.
- [x] Unknown opportunities remain outside the denominator.
- [x] Wipe causes require explicit structured evidence and do not infer blame from death chronology.
- [x] Phase reach and recent-vs-prior trends are deterministic and evidence-aware.
- [x] Session algorithms are pure and source-agnostic.
- [x] Persistence orchestration is async/cancellation-safe with partial failure isolation.
- [x] Session UI is generic and drill-down reuses shared workspace selection.
- [x] 500-pull scale fixture guards pure aggregation and orchestration responsiveness.
- [x] `RecapWindow.cs` remains outside the new Session architecture.
- [x] No M10 extraction/storage rewrite was pulled into M9.
- [x] Combined CI is green.

## Known limits carried forward

These are explicit evidence limits, not M9 failures:

- default stable participant identity is not inferred from player name or pull-local ActorId; a future static/config/account identity source may provide it when justified;
- default wipe/kill outcome remains unknown unless an upstream source or deterministic analyzer explicitly establishes it;
- current default progression enrichment is intentionally narrow and encounter-evidence-driven rather than pretending every pull has complete phase telemetry;
- Session Intelligence currently operates over the existing `IPullStore` abstraction; long-term SQLite/chunked-storage decisions remain an M10 evidence-based hardening question.

## Lead-integrator decision

**APPROVED FOR MERGE.**

M9 satisfies the Technical Design v0.2 Session Intelligence milestone and proves the cross-pull extension point at progression-night scale while preserving source-agnostic, evidence-first analysis boundaries.

After this sign-off/ledger reconciliation lands on `main`, complete #109 and #104 and authorize M10 — Hardening and Extraction Review. M10 must remain evidence-based: profile capture/storage/analysis first, improve migration/export/anonymization where needed, and evaluate assembly extraction only after the current Domain/Analysis boundaries demonstrate measured value.
