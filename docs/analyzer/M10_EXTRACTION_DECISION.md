# M10 Domain/Analysis extraction decision

Status: **M10-D decision — KEEP SINGLE ASSEMBLY**

Parent: #118  
Work package: #122  
Depends on: M10-A / #119 and M10-B / #120  
Reviewed main baseline: `e082e143fe56c40bcc14ba2dda42491398479ad2`

## Decision

**KEEP SINGLE ASSEMBLY for v1.**

The current `Domain/**` and `Analysis/**` code is already sufficiently pure that a later extraction is mechanically plausible, but M10 evidence does not show a current runtime, test-isolation, reuse, or packaging problem that is solved by introducing a second production assembly now.

Extraction would therefore add project/API/package complexity without a demonstrated v1 benefit. The folder and namespace boundaries remain the architectural boundary for v1. A separate pure assembly is deferred until a real consumer or measured build/package/test problem justifies it.

This is not a decision that the pure layer should remain in the plugin assembly forever. It is the Technical Design v0.2 evidence rule applied to the current repository state.

## Audit scope

The review covered the dependency surfaces required by #122:

- `BetterDeaths/Domain/**`;
- Analyzer Engine and indexes under `BetterDeaths/Analysis/Engine/**` and `Analysis/Index/**`;
- Generic analyzers under `Analysis/Generic/**`;
- Job definitions and Dancer analyzers under `Analysis/Jobs/**`;
- Encounter definitions and Dancing Mad Ultimate/Forsaken analysis under `Analysis/Encounters/**`;
- Session contracts and Session Intelligence under `Analysis/Sessions/**`;
- `tests/BetterDeaths.Tests/BetterDeaths.Tests.csproj` linked-source wiring;
- `BetterDeaths/BetterDeaths.csproj` and Dalamud package validation;
- current canonical serialization contracts;
- M10-A performance evidence and M10-B architecture decision evidence.

No source files are moved by M10-D.

## Dependency findings

### Domain is already a pure canonical contract layer

`BetterDeaths.Domain` contains canonical identity, pull/actor/event/provenance and analysis-result contracts. Its implementation depends on BCL types and `System.Text.Json.Serialization` attributes for the canonical event polymorphism boundary.

The canonical event hierarchy uses explicit `$eventType` discriminators such as `damage`, `heal`, `cast-start`, `status-apply`, `mechanic-signal`, etc. Persisted canonical semantics therefore do not rely on assembly-qualified CLR type names.

Existing boundary tests also protect the Domain surface from integration types. `CanonicalDomainBoundaryTests` rejects Dalamud, ImGui, FFLogs client/DTO and `HttpClient` types from canonical public contracts.

**Finding:** Domain is mechanically extractable from a dependency perspective. No forbidden Domain -> Dalamud/UI/FFLogs client dependency was found.

### Analyzer Engine and indexes are pure computation

`AnalyzerContracts` depends on:

- canonical `BetterDeaths.Domain` contracts;
- pure `BetterDeaths.Analysis.Index` indexes;
- BCL collections/cancellation/tasks.

`AnalyzerEngine` builds `EventIndex`, `ActorIndex`, `TargetabilityIndex` and `StatusIntervalIndex` from a supplied `RecordedPull`, resolves module dependencies, runs modules, and returns structured results/failures/skips. It contains no persistence calls, FFLogs transport, Dalamud service, ImGui rendering, or file/network I/O.

The Analysis index implementations are canonical in-memory indexes over Domain facts. They are not persistence indexes and do not depend on `IPullStore` or the file-backed store.

**Finding:** Engine/Index are mechanically extractable together with Domain.

### Generic analyzers remain source and UI agnostic

The Generic analyzer folder contains death/raise context, healing activity, mitigation coverage, targetability-aware uptime and generic timeline logic plus their pure definitions. These modules are registered through `IAnalyzerModule` and consume canonical `AnalyzerContext` evidence.

No Generic module needs FFLogs DTOs, Dalamud services, ImGui, workspace panels, or a concrete persistence implementation.

**Finding:** no Generic blocker to a future pure assembly.

### Job analysis remains isolated

`JobDefinition` is BCL-only job/action/status metadata. Dancer modules live under `Analysis/Jobs/Dancer` and consume canonical/Analyzer Engine evidence plus job definitions.

The existing M7 architecture already established source parity: local and FFLogs inputs normalize before job analysis, and the Jobs panel renders structured output downstream rather than entering Dancer logic.

**Finding:** no Job-analysis integration dependency was found that requires the Dalamud plugin assembly.

### Encounter analysis remains isolated

Encounter definitions/party-role semantics and Dancing Mad Ultimate/Forsaken analysis live under `Analysis/Encounters`. WTFDiG-derived encounter knowledge is data/analysis logic with retained attribution; source acquisition and rendering stay elsewhere.

The M8 sign-off fixtures already require Forsaken analysis to consume canonical actor/job/status/index evidence only and keep the generic Mechanics panel downstream.

**Finding:** encounter analysis remains suitable for a future pure layer and does not justify extraction by itself.

### Session Intelligence is pure, but session orchestration is intentionally not part of the pure layer

`Analysis/Sessions/SessionContracts.cs` and `SessionIntelligenceAnalyzer.cs` contain cross-pull aggregation contracts and algorithms. They are separate from `AnalyzerSessionDataController`, which lives under `Windows/Analyzer` and owns persistence-driven async orchestration/drill-down.

This is the desired boundary: pure Session Intelligence can move with Domain/Analysis later, while store/query/workspace orchestration remains in the plugin/application assembly.

**Finding:** Session Intelligence is mechanically compatible with a pure assembly; the existing folder boundary already expresses the split without creating another binary.

## Cross-source and integration boundary evidence

The test suite already contains several important boundary guards:

- `FFLogsSourceBoundaryTests.DomainAndAnalysisContractsDoNotReferenceFFLogsIntegrationTypes` scans Domain/Analysis contract member types for `BetterDeaths.Sources.FFLogs` coupling;
- `CanonicalDomainBoundaryTests` rejects common integration types from canonical public contracts;
- Analyzer workspace/panel tests separately protect rendering boundaries;
- M7/M8 parity fixtures verify Job/Encounter analyzers operate on canonical facts rather than source DTOs.

M10-C additionally introduced an export-core boundary that accepts canonical `RecordedPull` data and remains separate from FFLogs credentials/UI. That work does not require changing the Domain/Analysis extraction decision.

No forbidden coupling discovered by this audit requires a blocker/fix PR.

## Test-project wiring audit

`tests/BetterDeaths.Tests/BetterDeaths.Tests.csproj` targets plain `net10.0` with only test/coverage package references. It directly links:

- `../../BetterDeaths/Domain/**/*.cs`;
- `../../BetterDeaths/Analysis/**/*.cs`;
- and the other non-Dalamud source boundaries needed by repository tests.

It does **not** reference `Dalamud.NET.Sdk` or the plugin project.

This is significant evidence in both directions:

1. all current Domain/Analysis source already compiles and runs in a non-Dalamud `net10.0` test environment, so a future pure assembly is technically plausible;
2. the repository already obtains the main test-isolation benefit that extraction would otherwise provide. Current pure logic can be tested without loading/building the Dalamud plugin assembly as a test dependency.

The linked-source strategy is not ideal as a permanent reusable-library distribution model, but no present external consumer requires one. Replacing it with a production project solely for aesthetics would not improve the current analyzer behavior or evidence quality.

## API-surface implications of extracting now

This is the largest non-packaging cost.

Canonical Domain models are intentionally public because they form the serialized/analysis data contract. In contrast, the Analyzer Engine, analyzer modules, job/encounter/session definitions and most analysis implementation contracts are currently `internal`.

The plugin application layer composes those internal types directly, for example `AnalyzerWorkspaceEngineComposition` constructs `AnalyzerRegistry`, registers Generic/Dancer/Forsaken modules, and returns `AnalyzerEngine`.

Moving those internal types into a second assembly would require one of the following:

- make a substantial Analysis surface public;
- add `InternalsVisibleTo` from the pure assembly to the plugin (and likely tests);
- or add a new public composition/facade boundary.

All three are real API/maintenance decisions. None is required by current behavior, tests, performance, or reuse needs. Creating that API churn during v1 hardening would be contrary to the design rule against speculative cleanup.

## Canonical serialization implications

Moving Domain types to another assembly should not inherently change the current JSON wire format because canonical event polymorphism uses explicit discriminator strings and the serializer owns explicit file/pull schema versions.

However, extraction would still be a persistence-sensitive refactor. It would require re-running and retaining all canonical round-trip, typed-event, migration/compatibility, export, and legacy saved-data fixtures on the new project graph before being considered behavior-neutral.

There is no serialization problem today that extraction solves.

## Dalamud package/build implications

The production plugin currently uses one `BetterDeaths.csproj` with `Dalamud.NET.Sdk/15.0.0` and produces the single plugin assembly.

The repository's package validator deliberately asserts an exact packaged file set:

- `BetterDeaths.deps.json`;
- `BetterDeaths.dll`;
- `BetterDeaths.json`;
- `THIRD_PARTY_NOTICES.md`.

A separate Domain/Analysis assembly would add at least one production DLL dependency. Therefore extraction is **not** just a folder/project move: the Dalamud package composition and exact-package validator would have to be changed and revalidated. The plugin deployment must prove the dependency DLL is shipped and loaded correctly for both debug/release package paths.

That is manageable when a concrete benefit exists, but today it creates a new release/package failure mode while the existing package is green.

## Measured M10 evidence

M10-A provides the relevant runtime evidence:

- 20-minute / 50,000-event canonical recorder fixture: 5-second regression ceiling;
- canonical serializer round-trip for 50,000 events + 4,000 positions: 10-second ceiling;
- default Analyzer Engine over the 50,000-event pull: 10-second ceiling;
- real file-store 20 x 1,000-event save/query/reload: 20-second ceiling;
- retained M9 500-pull Session Intelligence gates: 5-second pure / 10-second orchestration ceilings.

CI `32259455682` passed those gates. These are deliberately generous regression ceilings rather than precision benchmarks, but they show no order-of-magnitude analyzer/build/runtime problem that an assembly boundary would fix.

M10-B reached the same evidence-first conclusion for storage: retain the replaceable current boundary instead of introducing infrastructure without a measured blocker. The same rule applies here.

No M10 measurement demonstrates that assembly extraction would improve analyzer runtime, allocation behavior, persistence, or user-visible responsiveness.

## Benefit/cost comparison

### Keep the single production assembly — selected

Benefits:

- preserves the current green Dalamud package shape and exact-file validator;
- preserves internal Analysis APIs instead of publishing/friending them prematurely;
- keeps canonical persistence assembly movement out of v1 hardening;
- retains current plain-net10 linked-source test isolation;
- no change to source adapters, persistence, UI, or plugin loading;
- no behavior change and no migration concern.

Costs:

- Domain/Analysis cannot yet be referenced as a ready-made reusable NuGet/project assembly by an external offline tool;
- tests continue using linked source rather than a production pure-project reference;
- folder/namespace boundaries, rather than the CLR assembly loader, enforce the architecture.

Those costs are acceptable for v1 because no external consumer currently exists and the architecture is already guarded by tests/review conventions.

### Extract pure Domain/Analysis now — deferred

Potential benefits:

- stronger compile-time dependency direction between pure and plugin layers;
- reusable library boundary for a future CLI/offline analyzer/service;
- tests could reference the production pure assembly rather than link its source;
- plugin-specific code could not accidentally be referenced from the pure project without a project-reference violation.

Current costs:

- new project and project-reference graph;
- Analysis internal-access decision (`public` surface vs `InternalsVisibleTo` vs facade);
- package validator and Dalamud distribution changes for an additional DLL;
- canonical persistence/serialization regression surface during a non-behavioral refactor;
- test-project rewiring across a very large existing fixture set;
- no measured runtime/performance problem solved;
- no actual external consumer receiving value from the reusable binary today.

The potential benefits are real, but not yet concrete enough to justify the v1 risk/cost.

## Smallest future extraction plan

If a revisit trigger is met, the smallest coherent extraction should be approximately:

1. Add one plain `net10.0` project for canonical Domain + pure Analysis/Index/Generic/Jobs/Encounters/Sessions.
2. Keep capture, persistence implementations, source adapters, exports/application I/O, Windows/ImGui and Dalamud plugin code in `BetterDeaths`.
3. Choose and document the minimum Analysis visibility policy; prefer a narrow facade/public contract over making every implementation public.
4. Change the plugin to a `ProjectReference` on the pure project without changing canonical JSON schema/version.
5. Change tests from linked Domain/Analysis source to the production pure-project reference while retaining integration-source fixtures as needed.
6. Update the Dalamud package validator/package layout to deliberately include the new dependency DLL.
7. Re-run canonical/legacy persistence, local/FFLogs parity, Generic, Dancer, Forsaken, Session, export/privacy, M10 performance and package fixtures.
8. Keep the extraction PR behavior-neutral; do not combine it with file moves, API redesign, storage changes or analyzer feature work.

## Revisit triggers

Re-open extraction when at least one concrete trigger exists:

- a real CLI/offline analyzer/service needs to reference Domain/Analysis as a reusable binary;
- another production repository needs the same canonical/analyzer contracts;
- the linked-source test strategy creates a measured build/test maintenance or correctness problem;
- a forbidden plugin/source/UI dependency actually enters Domain/Analysis and a project boundary is the smallest robust prevention mechanism;
- packaging changes for an additional dependency DLL are required for another reason, materially reducing the incremental extraction cost;
- measured build/package/runtime behavior demonstrates a problem that a separate assembly can credibly address.

Do not revisit solely because a multi-project solution appears cleaner.

## M10-D acceptance mapping

- [x] Domain dependency surface audited.
- [x] Analyzer Engine and Index dependency surfaces audited.
- [x] Generic analyzers audited.
- [x] Job/Dancer analysis audited.
- [x] Encounter/Forsaken analysis audited.
- [x] Session Intelligence vs application orchestration boundary audited.
- [x] Current linked-source test strategy and plain `net10.0` test target reviewed.
- [x] Dalamud package and exact-file validation implications reviewed.
- [x] internal/public API implications documented.
- [x] canonical serialization implications documented.
- [x] decision cites M10-A/M10-B evidence.
- [x] explicit decision recorded: **KEEP SINGLE ASSEMBLY**.
- [x] concrete revisit conditions recorded.
- [x] no unrelated file moves/style cleanup or speculative extraction introduced.

M10-D requires full branch CI before merge. If CI remains green, the decision is ready for lead-integrator review/sign-off.