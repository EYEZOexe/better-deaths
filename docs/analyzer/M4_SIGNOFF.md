# M4 New Workspace Shell — Integration Sign-off

Status date: 2026-08-18
Governing design: Technical Design v0.2
Parent issue: #41
Integration issue: #45

## Status

M4 is **READY FOR REVIEW** pending this combined-state sign-off branch CI.

M4 establishes the analyzer workspace shell and shared navigation state without pulling the broad M5 analysis suite, FFLogs, job analysis, or encounter/WTFDiG logic forward.

## M4-A — Shared workspace selection

Issue #42 / PR #46 — **APPROVED**.

Merged commit: `b6ab9d996ee0025477e2abf669ca90069b53009b`.

- one state object owns selected canonical pull, actor, time range, analysis result, and optional mechanic occurrence;
- selecting a structured `AnalysisResult` synchronizes result/actor/time in one state transition;
- changing pull clears stale cross-pull actor/result/time/mechanic context;
- panels react through one version/change notification rather than calling one another;
- state logic has no ImGui, Dalamud, persistence, or analyzer execution dependency.

## M4-B — Focused panel contracts and shell panels

Issue #43 / PR #48 — **APPROVED**.

Merged commit: `1264d90ccb79f3260c24b0def3df849150e53250`.
Final implementation CI: `32176263964` — restore, formatting, tests, and plugin/package build passed.

- added a focused panel context/interface and Overview/Timeline/Deaths/Replay shell composition;
- Overview selects structured analyzer findings through the shared selection state;
- Timeline writes canonical event time selection into the same shared time range;
- Deaths consumes canonical `DeathEvent` evidence only and intentionally does not implement M5 causal/blame analysis;
- Replay consumes the same actor/time selection and remains an incremental bridge to the existing detailed replay renderer;
- panels do not call one another, execute analyzers, access persistence, or grow the legacy `RecapWindow`.

### Lead-review performance fix

The first Deaths shell implementation rescanned the entire full-pull event stream every ImGui frame. This was rejected during review. The accepted implementation receives precomputed `DeathEvents` from the outer workspace context so large full-pull streams are not repeatedly scanned by that panel's render path.

## M4-C — AnalyzerWindow plugin integration

Issue #44 / PR #49 — **APPROVED**.

Merged commit: `72ec307d0189b0591a81b177450f30274285c0af`.
Final implementation CI: `32177896535` — restore, formatting, 272 tests, and plugin/package build passed.

- added `AnalyzerWorkspaceDataController` as the pure `IPullStore` + `AnalyzerEngine` orchestration boundary;
- canonical pull summaries/details are loaded asynchronously rather than blocking ImGui `Draw`;
- selected pulls are analyzed through the current analyzer registry and precompute Deaths-shell data once per pull;
- rapid pull changes cancel stale detail loads and generation checks reject stale async completions;
- `AnalyzerWindow` owns the shared selection and panel instances and passes one synchronized context to the active panel;
- the plugin registers the analyzer window through its existing `WindowSystem` in a dedicated partial integration file;
- the existing current-pull widget exposes an additive `Analyzer Workspace` launcher without changing existing `/bd` behavior or adding analyzer sections to `RecapWindow`;
- legacy Deaths/Replay navigation remains available through the existing pull-review UI rather than reaching into private death-centric replay internals.

### Lead-review compatibility fix

An early M4-C implementation attempted to call a direct legacy replay helper that is not part of `RecapWindow`'s public bridge surface. The accepted implementation opens the existing pull review instead and explicitly leaves direct replay targeting for a later incremental extraction.

## Combined M4 review

- [x] One shared state owns pull/actor/time/result/mechanic selection.
- [x] Result selection synchronizes actor/time for Timeline + Deaths/Replay consumers.
- [x] Panels communicate through shared state/context, never direct panel calls.
- [x] `AnalyzerWindow` is separate from the monolithic `RecapWindow`.
- [x] Existing death recap and replay implementations remain available and were not rewritten wholesale.
- [x] Canonical pull loading and analyzer execution are queued away from the ImGui render path.
- [x] Renderers do not depend directly on `FileCanonicalPullStore`; UI uses the `IPullStore` boundary through the controller/composition root.
- [x] M4 contains no broad M5 generic-analysis suite, FFLogs client/UI, job analyzer, or encounter/WTFDiG pack.
- [x] M4-A/B/C are merged on `main`.
- [ ] This M4-D combined-state branch must pass restore, format, tests, and plugin/package build before final approval.

## Runtime validation note

GitHub Actions cannot launch FFXIV/Dalamud. In-game confirmation that the launcher opens the workspace, canonical pulls populate, panel switching renders correctly, and legacy review navigation behaves as expected remains a manual UI smoke item. This does not replace the automated architecture/build gate and is not claimed as executed evidence.

## Exit gate

If this sign-off branch CI is green, M4 becomes **APPROVED**, issues #41/#45 may close, and M5 — Generic Hardcore Analysis — becomes authorized.
