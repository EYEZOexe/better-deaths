# M8 First Encounter Pack — Forsaken sign-off

Status: **APPROVED FOR MERGE**

Parent milestone: #94  
M8-A: #95 / PR #99  
M8-B: #96 / PR #100  
M8-C: #97 / PR #101  
M8-D: #98 / PR #102  
Combined PR-head CI: `32240800389`

## Selected vertical slice

Encounter: **Dancing Mad Ultimate** (`territoryId = 1363`)  
Phase/mechanic slice: **P2 Forsaken opening partner/group assignment**  
Strategy baseline: **Kroxy-Rinon**, as audited from WTFDiG.

The slice intentionally proves the encounter-pack extension boundary with evidence that the canonical model currently exposes reliably. It validates the opening role-compatible partner/group assignment and does not claim later tower occupancy, South Adjust movement, Future/Past baiting, or resolution geometry that has not yet been represented with sufficient canonical evidence.

## M8-A — encounter definitions and WTFDiG audit

**Status:** `APPROVED`  
**PR:** #99  
**Merged commit:** `5ad919105ab35c5b206983fb89ab175e76c41b94`

Evidence:
- reusable `EncounterDefinition`, `EncounterPhaseDefinition`, `AssignmentRule`, and `ArenaGeometry` contracts live under the Analysis encounter boundary;
- deterministic Tank/Healer/Melee/Ranged classification uses canonical actor job metadata rather than source/UI state;
- DMU/Forsaken definition records territory, phase, arena and the opening strategy semantics required by this slice;
- Forsaken status IDs `5084` Stack / `5085` Spread / `5086` Cone are reconciled from existing Better Deaths capture/replay knowledge rather than misattributed to WTFDiG;
- WTFDiG strategy/role semantics are pinned to `EYEZOexe/wtfdig@73a2ffa959b8f57bfbe7a1a75d5e43383ae2ea81`;
- audited paths are `src/routes/ultimates/umad/data.ts` and `src/lib/arena.ts`;
- `docs/analyzer/M8_WTFDIG_AUDIT.md` records reused versus deferred knowledge;
- `THIRD_PARTY_NOTICES.md` contains the required WTFDiG MIT copyright and permission notice.

## M8-B — Forsaken opening assignment analyzer

**Status:** `APPROVED`  
**PR:** #100  
**Merged commit:** `a3c6f81c541f1b328107cd0c5aeda037f66d4d80`

Evidence:
- analyzer scope is `AnalyzerScope.Encounter`;
- only canonical actor/job/status/index evidence is consumed;
- the opening relevant-status batch is bounded to three seconds from the first observed Forsaken status;
- deterministic classification requires exactly two Tanks, two Healers, two Melee and two Ranged players plus exactly one relevant opening status per participant;
- all four Tank↔Healer × Melee↔Ranged full-party layouts are evaluated;
- Group A means different debuffs with one Stack; Group B means matching debuffs; Cone+Spread is incompatible;
- static MT/OT/H1/H2/M1/M2/R1/R2 identities are never synthesized from ActorId ordering;
- a unique compatible layout emits neutral evidence-backed partner/group assignments;
- multiple compatible layouts remain neutral/ambiguous;
- incomplete, duplicate or sampled evidence cannot become a failure;
- zero compatible layouts produce a Warning only with complete exact pull + event evidence;
- findings distinguish expected, observed, cause and consequence and explicitly avoid automatic player blame;
- equivalent local and FFLogs canonical facts produce equivalent encounter semantics.

## M8-C — generic Mechanics panel and workspace composition

**Status:** `APPROVED`  
**PR:** #101  
**Merged commit:** `0f3d27d3b5ce23b4cca808c639e9a42956b29f25`  
**Validated CI:** `32240327553`

Evidence:
- one generic `AnalyzerMechanicsPanel` consumes `AnalysisCategory.Mechanic` results;
- the panel renders severity/title/time/confidence/summary/evidence without Forsaken-specific rendering architecture;
- selecting a finding uses `AnalyzerWorkspaceSelection.SelectResult`, preserving shared actor/time/result synchronization;
- the Forsaken analyzer is registered through `AnalyzerWorkspaceDataController.CreateDefault` and the existing Analyzer Engine registry, not from rendering code;
- the default panel catalog includes Mechanics after Timeline;
- integration coverage runs a representative Forsaken canonical fixture through the real default workspace engine path;
- panel source-contract tests reject Analyzer Engine, persistence, FFLogs, Dancer, Forsaken, legacy replay and `RecapWindow` coupling;
- `RecapWindow.cs` remains untouched;
- CI `32240327553` passed restore, formatting, all tests and plugin/package build.

## M8-D combined fixture and review gate

Combined sign-off evidence:

- [x] compatible exact Forsaken opening -> four evidence-backed neutral pair/group assignments;
- [x] incompatible complete exact opening -> one non-blaming Warning with eight actors/events and expected/observed/cause/consequence;
- [x] ambiguous opening -> neutral result rather than invented pairing;
- [x] incomplete opening -> no fabricated failure;
- [x] sampled opening -> no actionable failure from absence/uncertainty;
- [x] equivalent local/FFLogs canonical facts -> equivalent encounter meaning;
- [x] encounter contracts and analyzer remain source/UI/persistence/network/replay independent;
- [x] generic Mechanics panel uses shared selection and no encounter-specific UI class;
- [x] WTFDiG attribution/provenance is exact and retained;
- [x] no M9 session-intelligence logic is included in M8;
- [x] final M8-D PR-head CI `32240800389` passed restore, format, tests and plugin/package build;
- [x] lead-integrator final diff review completed.

## Uncertainty boundary

M8 deliberately does **not** claim that every Forsaken mechanic is now solved. The first encounter pack proves the architecture and one deterministic mechanic slice. Later encounter work may add tower occupancy, movement, resolution/cause chains and configured static assignments only when the canonical event/position evidence and rule definitions support those conclusions. Missing fidelity remains unknown instead of being reconstructed silently.

## Manual validation boundary

CI cannot launch FFXIV/Dalamud, so final in-game rendering/synchronization smoke testing remains manual. Automated tests cover the structured result path, workspace registration, shared-selection call path and source-boundary invariants.

## Lead-integrator decision

**APPROVED FOR MERGE.**

M8 satisfies the Technical Design v0.2 first-encounter-pack gate: reusable encounter definitions are isolated from core capture/engine code, one deterministic Forsaken mechanic slice has known compatible/incompatible fixtures, source uncertainty remains explicit, direct WTFDiG-derived strategy semantics retain exact provenance/MIT attribution, and the generic Mechanics panel renders structured encounter results through shared selection.

M9 — Session Intelligence — may be authorized only after PR #102 merges to `main`, #98/#94 are completed, and the progress ledger records M8 complete / M9 authorized.
