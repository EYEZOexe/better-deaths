# FFXIV Static Analyzer — v1 Runtime Release QA

Status: **PENDING MANUAL IN-GAME VALIDATION**  
Governing architecture: Technical Design v0.2  
Architecture sign-off: `docs/analyzer/M10_V1_SIGNOFF.md`  
Tracking issue: #134

## Purpose

Technical Design v0.2 v1 architecture is approved and the automated Definition-of-Done suite is green. That does **not** prove behavior inside a live FFXIV/Dalamud process.

This checklist is the release gate for the runtime behavior GitHub CI cannot exercise: plugin loading, live capture, ImGui interaction, real duty transitions, FFLogs workspace interaction, replay navigation, restart behavior, and qualitative frame-time responsiveness.

Do not mark a manual item `PASS` solely because an automated fixture exists. Automated evidence is listed separately so a tester knows what has already been proven and what still needs live verification.

## Result vocabulary

Use exactly one result per manual item:

- `PENDING` — not yet executed on the recorded package.
- `PASS` — expected runtime behavior was observed.
- `FAIL` — behavior is incorrect or materially regressed; create/link a blocker issue.
- `BLOCKED` — the test could not be completed because of environment, credentials, encounter/data availability, or another external prerequisite; record the reason and issue if applicable.

A release candidate is runtime-approved only when every **Required** item is `PASS`. Optional/conditional items may remain `BLOCKED` only when the release decision explicitly accepts the documented limitation.

## Test-run identity

Record this before testing so screenshots/logs/results can be tied to exact code and package bytes.

| Field | Value |
|---|---|
| Repository commit | `PENDING` |
| PR / release candidate | `PENDING` |
| Package filename / artifact ID | `PENDING` |
| Plugin version | `PENDING` |
| Dalamud API / client version | `PENDING` |
| FFXIV game version | `PENDING` |
| Tester | `PENDING` |
| Test date | `PENDING` |
| Test character/world | `PENDING` |
| Notes / environment differences | `PENDING` |

If code or package bytes change after testing begins, start a new run identity. Do not carry `PASS` results across an unreviewed binary change unless the item is explicitly judged unaffected and that decision is recorded.

## Automated evidence already established

The following is supporting evidence, not a substitute for the manual sections below.

| Area | Automated evidence | Last reviewed evidence |
|---|---|---|
| Zero-death canonical full pull | `FullPullLifecycleIntegrationTests`; `M10V1DefinitionOfDoneTests` | M10-E final PR-head CI `32271382462` |
| FFLogs canonical/source parity | `FFLogsAnalyzerParityTests` and M6 suites | M10-E full-suite rerun |
| Generic analysis | `M5GenericAnalysisIntegrationTests` | M10-E full-suite rerun |
| Dancer Job analysis | M7 Dancer integration/unit/rule-key suites | M10-E full-suite rerun |
| Forsaken Encounter analysis | M8 Forsaken combined/workspace suites | M10-E full-suite rerun |
| Session Intelligence | M9 combined 500-pull and orchestration fixtures | M10-E full-suite rerun |
| Shared workspace selection | `AnalyzerWorkspaceSelectionContractTests` and workspace integration suites | M10-E full-suite rerun |
| Canonical persistence/recovery | canonical store/serializer suites + M10 persistence hardening | M10-E full-suite rerun |
| Canonical/anonymized export | M10 export/privacy/envelope suites | M10-E full-suite rerun |
| Long-pull/session/storage regression budgets | `M10PerformanceBaselineTests` + retained M9 performance guards | M10-E full-suite rerun |
| Domain/Analysis dependency purity | canonical/source boundary tests + M10-E broad reflection guard | M10-E full-suite rerun |
| Package invariants / notices | `scripts/validate-package-manifest.ps1` | M10-E final PR-head CI `32271382462` |
| Export panel no-selection safety | `AnalyzerExportPanelContractTests` | PR #131 CI `32272087452`: 523 tests, plugin build 0 warnings / 0 errors |

M10 performance numbers are hosted-CI regression ceilings, not claims about live frame time. Live responsiveness is tested below.

# Manual runtime checklist

## A. Install, startup, and legacy Better Deaths continuity

### QA-01 — Package installs and plugin loads

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Install the exact recorded release-candidate package in the normal Dalamud plugin environment.
2. Start/reload Dalamud and enable the plugin.
3. Confirm the plugin loads without an exception, repeated error toast, or immediate disable.
4. Open the plugin UI/command entry point used by the existing Better Deaths workflow.

Pass criteria:
- plugin remains loaded and interactive;
- no startup exception attributable to the package;
- packaged manifest/API level is accepted by the tested Dalamud environment.

Evidence / notes: `PENDING`  
Blocker issue: `none`

### QA-02 — Existing Better Deaths `/bd` and recap workflow remains usable

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Open the existing Better Deaths UI through `/bd` or the normal existing entry point.
2. Verify the existing death-recap surface is present and usable.
3. Exercise the previously supported pull/death selection flow.

Pass criteria:
- legacy recap entry point still works;
- existing death-focused review is not replaced by an analyzer-only flow;
- no obvious layout/input regression prevents normal recap use.

Evidence / notes: `PENDING`  
Blocker issue: `none`

### QA-03 — Legacy death replay/navigation remains functional

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Produce or load a pull containing at least one party death.
2. Open the legacy death/replay review.
3. Select a death and move through its available replay/timeline controls.
4. Confirm HP/status/action context still corresponds to the selected death.

Pass criteria:
- death selection works;
- replay/timeline navigation works;
- analyzer additions have not broken the legacy death-review route.

Evidence / notes: `PENDING`  
Blocker issue: `none`

## B. Analyzer workspace and live canonical capture

### QA-04 — Analyzer workspace opens with and without a selected pull

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Open the Analyzer workspace before selecting/loading a canonical pull.
2. Visit its panels, including Export.
3. Confirm no exception occurs from a missing pull; Export actions should be disabled and indicate that a pull must be selected.
4. Select/load a canonical pull and revisit the workspace.

Pass criteria:
- workspace is stable in the empty-selection state;
- selecting a pull populates the workspace without reopening/restarting the plugin;
- no panel crashes because `Pull` is absent.

Evidence / notes: `PENDING`  
Blocker issue: `none`

### QA-05 — Meaningful zero-death duty pull is captured and archived

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Enter a normal PvE duty supported by the current capture path.
2. Produce meaningful combat activity but complete/end the tested pull with **zero party deaths**.
3. Trigger the normal pull/duty archive/finalization transition.
4. Open the Analyzer pull browser/history.

Pass criteria:
- the pull exists even though there were no deaths;
- duration/territory/source metadata is plausible;
- meaningful action/damage facts are present;
- the legacy death-recap history is not required for the canonical pull to exist.

Evidence / notes: `PENDING`  
Blocker issue: `none`

### QA-06 — Zero-death pull reloads and analyzes

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Select the zero-death pull from QA-05.
2. Load it into the Analyzer workspace.
3. Inspect Overview/Timeline and available findings.
4. Close/reopen the workspace and load the same pull again.

Pass criteria:
- the pull loads from persisted canonical storage;
- Analyzer Engine results render without a module failure/error state;
- timeline ordering and duration remain coherent;
- no synthetic death is invented.

Evidence / notes: `PENDING`  
Blocker issue: `none`

### QA-07 — Death-containing pull supports canonical analysis and legacy recap together

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Produce a pull containing at least one party death.
2. Verify the legacy Better Deaths recap/archive entry exists.
3. Verify the canonical Analyzer pull also exists.
4. Review the same pull through both routes.

Pass criteria:
- canonical full-pull capture does not suppress legacy recap behavior;
- legacy recap remains bounded/death-focused;
- Analyzer workspace has the full-pull canonical data needed for broader analysis.

Evidence / notes: `PENDING`  
Blocker issue: `none`

## C. Shared navigation and review panels

### QA-08 — Timeline/findings/deaths/replay navigation is synchronized

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Load a pull with at least one structured finding and death/replay evidence.
2. Select a finding with actor/time evidence.
3. Move between Timeline, Findings/Overview, Deaths and Replay-related navigation.
4. Change the selected pull and confirm stale actor/time/result context does not bleed into the new pull.

Pass criteria:
- selecting a finding moves the shared actor/time context coherently;
- relevant panels reflect the shared selection rather than maintaining conflicting independent state;
- changing pulls clears invalid cross-pull selection context.

Evidence / notes: `PENDING`  
Blocker issue: `none`

## D. FFLogs import

### QA-09 — Public FFLogs report/fight import completes through the workspace

**Required:** Yes for release candidates advertising FFLogs import  
**Result:** `PENDING`

Prerequisite: valid FFLogs client credentials and an accessible representative report/fight.

Steps:
1. Open the Analyzer FFLogs import flow.
2. Enter valid client credentials.
3. Load report/fight metadata and select a fight.
4. Import the fight.
5. Confirm the imported pull is persisted and selected through the normal Analyzer pull path.

Pass criteria:
- network/auth work does not freeze the ImGui frame while loading;
- report/fight selection and import complete or produce a safe structured error;
- imported pull is a normal canonical pull in the workspace rather than a separate FFLogs-only analysis path.

Evidence / notes: `PENDING`  
Report/fight used: `PENDING`  
Blocker issue: `none`

### QA-10 — FFLogs credential UI and failure handling do not expose secrets

**Required:** Yes for release candidates advertising FFLogs import  
**Result:** `PENDING`

Steps:
1. Observe the client-secret input while entering credentials.
2. Create the FFLogs session/import controller through the normal UI flow.
3. Confirm the secret field is cleared as designed after session construction.
4. Exercise at least one safe failure path where practical (invalid credential/report, unavailable report, or network failure).

Pass criteria:
- secret input is visually masked;
- raw client secret/access token/Authorization header is not printed in visible error/status text or plugin logs inspected during the test;
- failure is actionable without exposing credential material.

Evidence / notes: `PENDING`  
Blocker issue: `none`

## E. Job and Encounter extension points

### QA-11 — Dancer Job analysis renders and drills into evidence

**Required:** Yes  
**Result:** `PENDING`

Prerequisite: representative Dancer pull/log containing at least one known M7 finding or partner/burst/core-execution observation.

Steps:
1. Load/import the representative Dancer pull.
2. Open the Jobs panel.
3. Inspect Dancer results.
4. Select at least one evidence-backed result and verify shared actor/time navigation.

Pass criteria:
- Dancer analysis appears through the generic Jobs panel;
- result meaning is plausible for the known fixture/log;
- evidence navigation identifies the expected actor/time region;
- no Dancer-specific rendering crash or source-specific behavior is observed.

Evidence / notes: `PENDING`  
Pull/log reference: `PENDING`  
Blocker issue: `none`

### QA-12 — Forsaken Encounter/Mechanics analysis renders and drills into evidence

**Required:** Yes  
**Result:** `PENDING`

Prerequisite: representative Dancing Mad Ultimate / Forsaken pull/log with sufficient opening evidence.

Steps:
1. Load/import the representative pull.
2. Open the Mechanics panel.
3. Inspect Forsaken opening assignment/result output.
4. Select a result and verify evidence navigation.

Pass criteria:
- Forsaken analysis appears through the generic Mechanics panel;
- insufficient/ambiguous evidence is not presented as a fabricated actionable failure;
- known incompatible exact evidence, if used, is presented as a structured non-blaming finding;
- actor/time navigation reaches the expected evidence region.

Evidence / notes: `PENDING`  
Pull/log reference: `PENDING`  
Blocker issue: `none`

## F. Session Intelligence

### QA-13 — Realistic multi-pull session loads and reports progression/recurrence

**Required:** Yes  
**Result:** `PENDING`

Prerequisite: a realistic raid-night set of canonical pulls for one territory/encounter; use enough pulls to make Session output meaningful.

Steps:
1. Open the Session panel.
2. Load/refresh a session for the representative pull set.
3. Inspect progression, recurrence/opportunity counts, unknown counts, wipe causes and trends where evidence exists.
4. Trigger a second refresh/filter change while observing responsiveness.

Pass criteria:
- loading is asynchronous enough that the UI remains usable;
- recurrence exposes findings/evaluable/unknown opportunity information rather than opaque counts;
- unknown evidence is visible and not silently counted as success;
- no full-session UI freeze or runaway memory symptom is observed during the tested data set.

Evidence / notes: `PENDING`  
Pull count: `PENDING`  
Blocker issue: `none`

### QA-14 — Session evidence drill-down opens the contributing pull/result

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. From a recurrence/wipe/trend row with evidence, choose a concrete evidence item.
2. Open/drill into it.
3. Confirm the correct contributing pull loads and the relevant analysis result/actor/time selection is restored.

Pass criteria:
- evidence uses the contributing pull rather than a same-number actor/result from another pull;
- shared selection points to the expected result/time;
- repeated drill-down across different pulls does not retain stale prior-pull state.

Evidence / notes: `PENDING`  
Blocker issue: `none`

## G. Export and privacy

### QA-15 — Canonical export writes a reloadable identity-bearing file

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Select a canonical pull.
2. Export using **Export canonical** to a known test directory.
3. Confirm the file is created and valid JSON.
4. Reload/deserialize it through the supported canonical path or a repository test/helper as appropriate.
5. Inspect representative identity-bearing fields.

Pass criteria:
- file is complete and reloadable;
- actor names/source references/absolute timestamps expected in canonical local interchange remain present;
- UI does not imply that canonical export is privacy-safe for public sharing.

Evidence / notes: `PENDING`  
Export filename: `PENDING`  
Blocker issue: `none`

### QA-16 — Anonymized export removes direct identity/source/time linkage

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Export the same pull using **Export anonymized**.
2. Open the exported JSON in a text editor.
3. Search for every known player name, pet name, FFLogs report code/source reference or local source reference, and the known absolute pull timestamp.
4. Inspect actor labels/relationships and representative event ordering.

Pass criteria:
- player names are replaced with deterministic `Player N` labels;
- pet names are replaced with deterministic `Pet N` labels;
- uncertain/Unknown actor names are not retained as identity text;
- original pull ID/source references are absent/replaced according to policy;
- pull/event absolute timestamps are absent;
- free-form world-marker labels are absent where present in source data;
- actor owner relationships, event ordering and pull-relative evidence remain usable.

Residual-risk note: passing this item does not claim formal anonymity. Distinctive retained combat/position sequences may still be correlatable by someone who already possesses the same combat data, as documented in `M10_EXPORT_PRIVACY.md`.

Evidence / notes: `PENDING`  
Export filename: `PENDING`  
Blocker issue: `none`

## H. Restart, persistence, and recovery sanity

### QA-17 — Plugin restart preserves canonical pull browsing and analysis

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. With several canonical pulls saved, close/reload the plugin or restart the game/Dalamud using the normal test procedure.
2. Reopen the Analyzer workspace.
3. Browse/query the saved pulls.
4. Load at least one previously captured local pull and one imported pull if available.

Pass criteria:
- canonical history remains available after restart;
- pulls load/analyze normally;
- no unexpected index rebuild/error appears in an ordinary clean restart;
- legacy Better Deaths saved data remains readable for the tester's existing characterized data where available.

Evidence / notes: `PENDING`  
Blocker issue: `none`

### QA-18 — Interrupted/stale export or canonical temp files do not surface as normal completed data

**Required:** Optional destructive/recovery smoke  
**Result:** `PENDING`

Only perform this with disposable test data/backups.

Steps:
1. Use a disposable configuration directory or backup the analyzer data first.
2. Introduce a stale `.tmp` file matching one of the documented persistence/export temp naming patterns without replacing a valid primary.
3. Restart/reload and inspect pull history/export target.

Pass criteria:
- stale temp data is not treated as the completed canonical primary;
- last-known-good primary remains usable;
- no silent incompatible-schema mutation occurs.

Evidence / notes: `PENDING`  
Blocker issue: `none`

## I. Live responsiveness / performance smoke

### QA-19 — Long Ultimate-style pull remains interactively reviewable

**Required:** Yes  
**Result:** `PENDING`

Prerequisite: a long representative pull, ideally 15–20 minutes or comparable high-event density.

Steps:
1. Capture or import the long pull.
2. Load it into the Analyzer workspace.
3. Move among Overview, Timeline, Jobs/Mechanics where applicable, Deaths/Replay and Export.
4. Scrub/change selection repeatedly.
5. Observe frame pacing and input responsiveness.

Pass criteria:
- no multi-second per-frame stall during ordinary navigation;
- no obvious runaway memory growth or repeated full-pull reprocessing symptom while idle on a panel;
- expensive load/analyze work occurs as a bounded operation rather than every Draw frame;
- any material hitch is recorded with reproduction steps and an issue rather than waived because hosted-CI timing passed.

Evidence / notes: `PENDING`  
Pull duration/event density: `PENDING`  
Blocker issue: `none`

### QA-20 — Realistic progression session remains interactively reviewable

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Load a realistic progression-night session, preferably tens to hundreds of pulls if available.
2. Refresh/filter the session.
3. Scroll recurrence/wipe/progression/trend output and drill into several evidence items.
4. Observe UI responsiveness and memory behavior.

Pass criteria:
- UI remains usable while async session work runs;
- result rendering remains bounded and responsive;
- evidence drill-down does not retain obviously growing full-pull history in memory;
- any material live-performance issue is recorded as a blocker/revisit trigger.

Evidence / notes: `PENDING`  
Pull count: `PENDING`  
Blocker issue: `none`

## J. Package/legal sanity

### QA-21 — Packaged third-party notice is present

**Required:** Yes  
**Result:** `PENDING`

Steps:
1. Inspect the exact release-candidate package contents.
2. Confirm `THIRD_PARTY_NOTICES.md` is present alongside the plugin files.
3. Confirm it includes the currently required BossMod, xivanalysis and WTFDiG attribution/provenance sections.

Pass criteria:
- notice file is packaged;
- required attribution is readable in the shipped package;
- package identity matches the exact tested candidate.

Evidence / notes: `PENDING`  
Blocker issue: `none`

# Release decision

## Required-item summary

| State | Count |
|---|---:|
| `PASS` | 0 |
| `FAIL` | 0 |
| `BLOCKED` | 0 |
| `PENDING` | 20 required items |

QA-18 is optional/destructive recovery smoke and is not included in the required-item count.

## Decision

**NOT YET RUNTIME-APPROVED.**

Architecture/automated v1 sign-off is complete, but this runtime checklist has not yet been executed against a recorded release-candidate package.

When testing completes, record one of:

- **APPROVED FOR RUNTIME RELEASE** — every required item is `PASS` and no unresolved release blocker remains.
- **CHANGES REQUESTED** — one or more `FAIL` results require code/config/documentation changes; link the blocker issues.
- **BLOCKED** — required runtime evidence cannot currently be obtained; record the external prerequisite and do not present the package as runtime-validated.

## Blocker log

| Issue | QA item(s) | Severity | Status | Notes |
|---|---|---|---|---|
| `none` | — | — | — | Add runtime blockers here. |
