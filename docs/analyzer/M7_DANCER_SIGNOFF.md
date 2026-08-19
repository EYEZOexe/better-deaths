# M7 Dancer job-analyzer sign-off

Status: **APPROVED**

Parent: #82 — completed  
Sign-off package: #87 — completed  
Sign-off PR: #92 — merged as `eae228476c1683b7a267c9391aa79484263cb0ef`  
Combined CI: `32230435195`  
Final PR-head CI: `32230698017`

## Selected vertical slice

M7 uses **Dancer (DNC)** as the first Job analyzer. This follows Technical Design v0.2's job-analysis architecture: job modules consume canonical events plus explicit job definitions, emit structured evidence-backed findings, and render through one generic Jobs panel rather than job-specific UI.

DNC also exercises the design's representative `DNC partner/burst` Job scope while testing an important uncertainty boundary: Esprit/feather resource state is not reconstructed unless canonical source evidence has a verified semantic contract.

## Implementation packages

### M7-A — job-definition boundary / DNC data

Issue #83 / PR #88 / merge `f0c60d322600fd5ce9c84371d44eaf33226b56a7`  
CI: `32227679821`

Evidence:
- reusable source-agnostic job/action/status definitions with validation;
- DNC action/status/cooldown data required by the vertical slice;
- explicit shared Standard Step / Finishing Move cooldown group;
- xivanalysis Dawntrail data provenance pinned to commit `f90bfac9ad9984354437b83e529f5dd709346413` and exact paths;
- MIT attribution recorded in `THIRD_PARTY_NOTICES.md`.

### M7-B — DNC dance/core/proc/partner analysis

Issue #84 / PR #89 / merge `7f6988b7fc4cdbcdbda38dae1d2075f8524446b1`  
CI: `32228178813`

Evidence:
- explicit under-stepped finish variants produce evidence-backed warnings;
- missing finish evidence is not converted into a mistake;
- unused-proc warnings require known duration expiry plus exact source evidence;
- sampled/unknown proc intervals remain unknown;
- Dance Partner evidence is recorded neutrally and no optimal partner is inferred from damage;
- contradictory partner assignment is reported only for evidence-supported known overlap.

### M7-C — burst/cooldown cadence / targetability-aware GCD execution

Issue #85 / PR #90 / merge `e1ff044611574cb7d021fb20193b3817066caf90`  
Corrected CI: `32229549949`

Evidence:
- Devilment alignment uses explicit Technical Finish evidence and pinned xivanalysis behavioral provenance;
- no preceding Technical Finish means unknown/silent rather than an inferred mistake;
- late-window, cooldown-drift and GCD-gap absence judgments require exact action coverage;
- cooldown readiness starts only from an observed prior use;
- targetable-time calculations exclude forced untargetable and unknown time;
- death-containing execution windows are deferred rather than charged as ordinary job loss;
- no Skill Speed simulation or fabricated Esprit/feather verdict is introduced.

Lead review corrected an initial local-name compile failure and tightened the missing-Technical evidence rule before merge.

### M7-D — generic Jobs panel / workspace composition

Issue #86 / PR #91 / merge `13072911fab1254e294f42904db75fdf1092c3c0`  
CI: `32230088925`

Evidence:
- one generic `AnalyzerJobsPanel` renders structured Job-category results;
- no DNC-specific window/panel architecture exists;
- selecting a job result reuses shared `AnalyzerWorkspaceSelection.SelectResult`, synchronizing primary actor and result time;
- DNC analyzers are registered through the existing default Analyzer Engine composition seam;
- panel source-contract tests reject analyzer execution, persistence, FFLogs, Dancer and `RecapWindow` coupling;
- `RecapWindow.cs` remains untouched.

## Combined M7 review checklist

- [x] DNC detection uses canonical `ActorRecord.JobAbbreviation` only.
- [x] Job definitions/data are explicit and validated.
- [x] Third-party DNC data/rule provenance is pinned to exact xivanalysis paths/commit with MIT notice.
- [x] Both DNC analyzers declare `AnalyzerScope.Job` and consume canonical Domain/index contracts only.
- [x] Dance/core/proc/partner findings preserve unknown evidence rather than manufacturing mistakes.
- [x] Cooldown drift/missed-use logic starts from observed readiness evidence and excludes forced targetability downtime.
- [x] Missing Technical Finish boundary evidence stays unknown.
- [x] No Esprit/feather resource verdict is fabricated without verified canonical gauge semantics.
- [x] Every Warning/Error is structured with actors, time, event evidence and confidence.
- [x] Equivalent local and FFLogs canonical facts are fixture-tested for equivalent Job semantics.
- [x] Generic Jobs panel renders results without DNC-specific UI architecture.
- [x] Shared result selection synchronizes player/time through the existing workspace state.
- [x] `RecapWindow.cs` remains outside M7 feature implementation.
- [x] No M8 encounter/WTFDiG implementation was pulled into M7.
- [x] Combined M7-E fixture pack passed.
- [x] Combined CI `32230435195` passed restore, formatting, all tests, and plugin/package build.
- [x] Final PR-head CI `32230698017` passed the complete validation pipeline before merge.

## Combined fixture evidence

The M7-E integration fixture runs both DNC modules together and exercises under-stepped dance, exact proc expiry, Dance Partner assignment, Devilment timing, Flourish cadence and targetable GCD-gap analysis. The fixture asserts evidence/time/actor/confidence on every Warning/Error, includes gauge events without generating unsupported resource conclusions, includes a clean no-finding DNC case, and proves local-vs-FFLogs canonical semantic parity.

## Runtime/manual validation note

Automated CI validates deterministic canonical analysis, workspace composition contracts, formatting and plugin/package build. CI cannot launch FFXIV/Dalamud, so visual Jobs-panel smoke validation in-game remains a manual runtime item rather than being misrepresented as automated evidence.

## Lead-integrator decision

**APPROVED.**

M7 satisfies the first-job vertical-slice acceptance criteria without introducing job-specific UI architecture or source-specific analyzer dependencies. The remaining resource limitation is explicit: Esprit/feather overcap/underuse cannot be judged until canonical source evidence defines those gauge semantics reliably.

PR #92 merged to `main`; #87 and #82 are completed. M8 — First Encounter Pack — is authorized from merge commit `eae228476c1683b7a267c9391aa79484263cb0ef`.
