# M10 v1 hardening and Definition-of-Done sign-off

Status: **APPROVED — v1 architecture complete**

Governing design: Technical Design v0.2  
Parent milestone: #118  
Sign-off work package: #123  
Reviewed combined baseline: `4faee8bed44223633f62fcdc3e1463d9af2be19b`  
Combined validation CI: `32270674469`

## Review objective

M10-E is the final evidence review for the Technical Design v0.2 v1 architecture. Green CI alone is not treated as sufficient. Each Definition-of-Done statement is mapped below to concrete implementation/test evidence, and the M10 hardening decisions are checked for forbidden coupling, privacy regressions, speculative infrastructure changes, and loss of legacy Better Deaths behavior.

Technical Design v0.2 defines M10 as profiling storage/capture/analysis, improving migration behavior, adding export/anonymization, and evaluating whether pure Domain/Analysis should become a separate assembly. Its acceptance criteria are that extraction is evidence-based rather than speculative and that the v1 Definition of Done is met.

## M10 work-package state entering sign-off

| Package | Result | Merge | Validation |
|---|---|---|---|
| M10-A / #119 / PR #124 | realistic performance baseline established; no speculative optimization/backend rewrite | `38c9a85a3dd075ce7b7cc6f0bc074eb70b0762f0` | CI `32259455682` |
| M10-B / #120 / PR #125 | persistence/recovery hardened; file-backed `IPullStore` retained; explicit migration policy recorded | `3ae3a6e821f5958a09115ad4f766c288b8679793` | final-head CI `32265402021` |
| M10-C / #121 / PR #127 | canonical export + true anonymized export/privacy boundary | `e082e143fe56c40bcc14ba2dda42491398479ad2` | final-head CI `32268302871` |
| M10-D / #122 / PR #128 | evidence-based **KEEP SINGLE ASSEMBLY** decision | `4faee8bed44223633f62fcdc3e1463d9af2be19b` | final-head CI `32269832664` |

All four prerequisite packages were merged before this combined sign-off branch. Combined validation CI `32270674469` then reran restore, formatting, the complete automated suite including all retained M9/M10 performance fixtures, and plugin/package validation with the final M10-E guards present.

## v1 Definition of Done — item-by-item evidence

### 1. Full pulls, including zero-death pulls, can be captured locally, saved, reloaded, and analyzed

**Status: SATISFIED by automated architecture fixtures.**

Evidence:

- `FullPullLifecycleIntegrationTests.MeaningfulZeroDeathLivePipelineFinalizesPersistsAndReloads` constructs the actual `FullPullRecorder -> DalamudLiveEventNormalizer -> RecordedPull -> FileCanonicalPullStore -> reload` path with meaningful combat and no `DeathEvent`.
- `FullPullLifecycleIntegrationTests.PluginArchiveFinalizesCanonicalPullBeforeLegacyDeathGatedDecision` guards the production ordering: canonical full-pull finalization happens before the legacy death-only archive decision.
- M10-E adds `M10V1DefinitionOfDoneTests.MeaningfulZeroDeathLocalPullPersistsReloadsAndRunsDefaultAnalyzerEngine`, extending that representative zero-death flow through the default Analyzer Engine after reload and requiring zero analyzer failures.
- M10-A separately exercises a 20-minute / 50,000-event canonical full pull through recorder finalization and the default Analyzer Engine performance boundary.

This intentionally coexists with the legacy death-recap store remaining death-gated; canonical full-pull persistence is the new zero-death-capable path rather than a silent rewrite of legacy storage semantics.

### 2. FFLogs pulls import through an adapter and normalize into the same `RecordedPull`

**Status: SATISFIED.**

Evidence:

- M6 sign-off reviewed the full `FFLogs OAuth/client -> report/fight/events -> normalization -> RecordedPull -> IPullStore -> Analyzer Engine -> Workspace` path.
- `FFLogsAnalyzerParityTests.EquivalentLocalAndFFLogsFactsProduceEquivalentGenericAnalysisSemantics` proves equivalent live/FFLogs facts pass through the same Analyzer Engine semantics while retaining different provenance source kinds.
- `FFLogsAnalyzerParityTests.SameCanonicalPullAnalyzesIdenticallyAfterPersistenceRoundTripRegardlessOfFFLogsOrigin` proves imported canonical data does not acquire source-specific analysis behavior after persistence.
- Dancer and Forsaken combined fixtures separately verify local/FFLogs semantic parity at Job and Encounter extension points.

### 3. Core generic death, mitigation, healing, and damage/uptime analysis produces structured evidence-backed results

**Status: SATISFIED.**

`M5GenericAnalysisIntegrationTests` runs the representative generic suite together and verifies:

- death/raise context has event evidence and explicitly avoids naive last-hit blame;
- healing remains evidence-backed and neutral where overheal/resource evidence is unavailable;
- mitigation distinguishes personal/source-debuff scope, preserves active mitigation evidence, and does not invent missed-use/waste claims;
- targetability-aware uptime never turns forced untargetable windows into execution gaps;
- configured cooldown/buff timelines remain evidence-driven.

Mitigation and buff/cooldown analyzers remain definition-driven by design. The default workspace registers only the catalog-free generic modules rather than silently inventing a global FFXIV semantic catalog; this was explicitly accepted in M5 sign-off.

### 4. At least one Job analyzer is a proven extension point

**Status: SATISFIED — Dancer.**

`M7DancerIntegrationTests` exercises Dancer core execution, burst/cooldown, targetable GCD uptime, proc expiration, partner evidence, deterministic structured findings, clean-pull behavior, and local/FFLogs parity through the generic Analyzer Engine. Warnings/errors are required to carry actors, time, confidence and event/actor evidence. The Jobs panel remains generic rather than Dancer-specific.

### 5. At least one Encounter/Mechanic analyzer is a proven extension point

**Status: SATISFIED — Dancing Mad Ultimate / Forsaken.**

`M8ForsakenCombinedFixtureTests` verifies known compatible exact openings, known incompatible exact openings, ambiguous/incomplete/sampled evidence handling, explicit evidence, and local/FFLogs semantic parity. Actionable failure requires complete exact evidence and remains non-blaming.

The encounter definition/rule boundary is isolated from capture/engine/UI. `THIRD_PARTY_NOTICES.md` retains WTFDiG MIT provenance for the selected Kroxy-Rinon Forsaken strategy data and exact fork revision/path references.

### 6. Raid Session aggregation identifies recurring findings with opportunity counts

**Status: SATISFIED.**

`M9SessionPerformanceAndCombinedFixtureTests.FiveHundredPullCombinedSessionProducesOpportunityNormalizedEvidenceBackedIntelligenceWithinBudget` verifies, at 500 pulls:

- structured recurrence keys;
- finding/evaluable/unknown opportunity counts and normalized rates;
- evidence for counted findings;
- explicit known/unknown wipe-cause accounting;
- progression phase reach;
- recent-vs-prior trends;
- no chronology-derived or prose-derived recurrence identity.

The companion orchestration fixture requires compact-summary query first, sequential full-pull loading with max concurrency one, no publication of retained `RecordedPull` event streams, and a generous 10-second 500-pull CI gate.

### 7. Timeline, findings, deaths, and replay share synchronized selection/time navigation

**Status: SATISFIED at the architecture/contract level.**

`AnalyzerWorkspaceSelectionContractTests` guards one shared selection state for pull, actor, time range, analysis result and mechanic occurrence. Selecting a result synchronizes actor/time in that state, changing pulls clears stale cross-pull context, and one shared version/change notification drives consumers.

M4 sign-off verifies the Overview/Timeline/Deaths/Replay shell consumes this shared state without direct panel-to-panel calls, and legacy replay remains bridged rather than rewritten into the new architecture.

### 8. Existing Better Deaths death recap functionality has not regressed materially

**Status: SATISFIED by characterization/architecture evidence; runtime smoke remains a release check.**

Evidence:

- `FullPullLifecycleIntegrationTests` guards canonical capture as additive to the existing replay/death-resolution path and preserves bounded legacy lead-up retention.
- `RecordedPullPersistenceContractTests` preserves legacy raw-array and wrapped-history loading behavior and current characterized legacy schema versions.
- the same persistence characterization tests preserve the existing death-only filters instead of silently changing the old recap file contract.
- M4/M5 integration explicitly keeps Analyzer features out of monolithic `RecapWindow` and retains the legacy pull-review/replay route.

GitHub CI cannot launch FFXIV/Dalamud, so actual `/bd`, recap rendering and in-game replay navigation remain manual release-smoke items. They are not represented as automated evidence.

### 9. Persisted data is versioned and migration behavior is tested

**Status: SATISFIED.**

Canonical persistence has explicit file/pull/index schema versions and compatibility exceptions. M10-B adds deterministic hardening fixtures for:

- unsupported pull schema during deserialization;
- corrupt primary index with valid backup fallback;
- stale temp file isolation;
- last-known-good detail preservation;
- mismatched detail identity fallback;
- detail-driven index rebuild rejecting filename/payload identity mismatch.

Existing tests retain explicit unsupported canonical/index behavior and mutation safety. `M10_STORAGE_DECISION.md` requires an exact source->target migration policy and fixtures before any future schema bump, forbids silent reinterpretation/startup bulk migration, and retains legacy Better Deaths saved-data compatibility as a separate intentional path.

### 10. Long Ultimate pull and progression-session performance is acceptable under measured fixtures

**Status: SATISFIED within documented automated measurement scope.**

M10-A regression gates use deliberately generous hosted-CI upper bounds:

- 20-minute / 50,000-event recorder append + finalize: 5 s;
- 50,000-event + 4,000-position canonical serializer round-trip: 10 s;
- default Analyzer Engine over 50,000 events: 10 s;
- real file store over 20 x 1,000-event pulls: 20 s;
- retained M9 500-pull Session gates: 5 s pure aggregation / 10 s orchestration.

CI `32259455682` passed the baseline suite. Subsequent M10-B/C/D final-head CI runs and M10-E combined CI `32270674469` also reran the retained performance tests successfully.

These are regression ceilings, not invented precision benchmarks. Hosted tests cannot measure FFXIV/Dalamud frame scheduling; manual in-game frame-time validation remains a release QA item rather than a claimed CI metric.

### 11. No source-specific DTO/service/UI types leaked into analyzer/domain contracts

**Status: SATISFIED and strengthened in M10-E.**

Existing guards include:

- `CanonicalDomainBoundaryTests` rejecting Dalamud, ImGui, FFLogs client/DTO and `HttpClient` types from canonical Domain contracts;
- `FFLogsSourceBoundaryTests.DomainAndAnalysisContractsDoNotReferenceFFLogsIntegrationTypes` rejecting FFLogs integration types from Domain/Analysis contracts.

M10-D manually audited Domain, Engine/Index, Generic, Jobs, Encounters and Sessions and found no forbidden coupling. M10-E adds a broader reflection guard over all `BetterDeaths.Domain*` and `BetterDeaths.Analysis*` implementation types, rejecting dependencies whose types come from `BetterDeaths.Sources`, `BetterDeaths.Persistence`, `BetterDeaths.Windows`, Dalamud or ImGui. Combined CI `32270674469` passed with that guard active.

### 12. Privacy remains local-first and FFLogs credentials are handled safely

**Status: SATISFIED.**

M6 keeps OAuth/client/token handling inside the FFLogs integration boundary and excludes credentials from canonical pulls/analyzer contracts. M10-C adds the explicit sharing boundary required by Technical Design v0.2:

- canonical export is clearly labeled as lossless/local, not privacy-safe;
- anonymized export replaces player/pet/unknown display names;
- original pull identity is replaced from sanitized content rather than source identity;
- external `SourceReference` values are removed from pull/event/position/marker provenance;
- absolute pull/event wall-clock timestamps are removed;
- free-form marker labels are removed;
- actor relationships, event IDs/order, pull-relative timing, mechanics and positions remain analyzable;
- representative client ID, client secret, access token and Authorization-header strings injected into sensitive canonical fields are required not to survive anonymized bytes.

The export core receives only `RecordedPull` + options; it accepts no FFLogs credential/config/client, Dalamud or ImGui type. No automatic upload/publish path was introduced.

The privacy policy explicitly documents residual correlation risk from distinctive preserved combat/position evidence rather than misrepresenting v1 anonymization as formal k-anonymity or differential privacy.

### 13. Required third-party notices are present

**Status: SATISFIED.**

`THIRD_PARTY_NOTICES.md` currently contains:

- BossMod BSD 3-Clause attribution for generated Ultimate replay catalog data;
- xivanalysis MIT attribution and pinned Dawntrail revision/paths for Dancer definition/burst semantic references;
- WTFDiG MIT attribution and pinned fork revision/paths for Dancing Mad Ultimate / Forsaken strategy/role semantics.

The package validator requires `THIRD_PARTY_NOTICES.md` in the produced Dalamud package, and M10-E combined CI `32270674469` passed package validation.

## M10 hardening decisions reviewed together

### Storage

**Retain file-backed `FileCanonicalPullStore` behind `IPullStore` for v1.**

M10-A does not show a measured storage/query blocker. M10-B closes deterministic recovery/identity gaps and documents the residual detail/index non-transactional crash window. SQLite and compressed/chunked storage are deferred behind concrete revisit triggers rather than introduced speculatively.

### Export/privacy

**Keep canonical local interchange separate from true anonymized sharing output.**

Anonymization is a data transformation at an explicit export boundary, not a UI-only name-redaction switch. The policy is versioned and tested, and no source credential state enters the export contract.

### Domain/Analysis extraction

**KEEP SINGLE ASSEMBLY for v1.**

M10-D confirms Domain/Analysis are mechanically pure enough for a future extraction, but current tests already compile them in a plain `net10.0` environment, current Analysis implementations are predominantly internal, an extra production DLL would require deliberate Dalamud package changes, no current external binary consumer exists, and no measured M10 runtime/build issue is solved by splitting the assembly.

The ADR records a smallest future extraction plan and concrete revisit triggers.

## Final combined fixture/audit coverage

Combined validation CI `32270674469` reran, together:

- zero-death local canonical lifecycle: `FullPullLifecycleIntegrationTests` + M10-E zero-death analyze guard;
- local/FFLogs canonical/analyzer parity: `FFLogsAnalyzerParityTests`;
- generic death/mitigation/healing/damage-uptime: `M5GenericAnalysisIntegrationTests`;
- Dancer extension point/parity: `M7DancerIntegrationTests` + Dancer unit/rule-key suites;
- Forsaken extension point/parity: `M8ForsakenCombinedFixtureTests` + Mechanics workspace integration;
- Session Intelligence and 500-pull responsiveness: `M9SessionPerformanceAndCombinedFixtureTests`;
- shared selection/navigation: `AnalyzerWorkspaceSelectionContractTests` and workspace integration suites;
- canonical version/recovery/migration safety: canonical store/serialization + M10 persistence hardening suites;
- true anonymized export/privacy: `M10CanonicalExportTests` + export-envelope tests;
- long-pull/storage/default-engine performance: `M10PerformanceBaselineTests`;
- architecture source/storage/UI purity: existing boundary tests + M10-E broad Domain/Analysis guard;
- plugin/package integrity including third-party notice packaging: repository package validation step.

The run passed restore, formatting verification, all tests/performance guards, and plugin/package validation. The M10-E changed-file review remains intentionally small: final sign-off documentation plus the two bounded DoD guards. No analyzer feature, storage backend, source adapter, UI rewrite, project split or broad cleanup was introduced in the sign-off package.

## Final decision

**APPROVED — v1 architecture complete.**

Technical Design v0.2 architecture and automated Definition-of-Done gates are satisfied on the reviewed combined M10 state. M10-A measured the performance-sensitive boundaries, M10-B hardened persistence and recorded explicit migration/storage policy, M10-C established the tested canonical/anonymized export privacy boundary, M10-D made the deferred extraction decision from evidence, and M10-E reran the complete repository suite with additional zero-death/default-engine and Domain/Analysis purity guards.

This approval does not pretend GitHub CI executed FFXIV/Dalamud itself. In-game UI/capture/frame-time smoke testing remains normal release QA and should be performed before distributing a release build.
