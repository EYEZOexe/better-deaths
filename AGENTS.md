# FFXIV Static Analyzer — Agent Execution Contract

This repository is being evolved from Better Deaths into the FFXIV Static Analyzer described by the governing technical design.

## Source of truth

Implementation work must follow, in order:

1. `docs/analyzer/TECHNICAL_DESIGN_v0.2.md` once added to the repository, or the supplied Technical Design v0.2 document until then.
2. `docs/analyzer/PROGRESS.md` for the active milestone, ownership, review state, and next approved work.
3. Existing code and tests. Never infer behavior only from file names.

If requested work conflicts with an architectural invariant, stop and report the conflict instead of bypassing it.

## Mandatory architecture rules

- Work milestone-by-milestone. Do not opportunistically implement later milestones.
- Keep every meaningful batch buildable and testable.
- Add characterization coverage before refactoring behavior that already works.
- Prefer additive seams first; migrate consumers only after the seam is proven.
- Do not rewrite raw capture/hook logic for style alone.
- Preserve Better Deaths' short lead-up/death-recap buffers. Full-pull capture must be a separate append-only path.
- Domain/analyzer contracts must not depend on Dalamud services, FFLogs DTOs, packet structs, ImGui, network clients, or persistence implementations.
- Analyzer modules must not mutate canonical pull data, perform network I/O, or render UI.
- Findings must be structured and evidence-backed; formatted prose is presentation, not the analyzer contract.
- Use deterministic event ordering and pull-relative time for analysis.
- Persisted canonical data must be schema-versioned and migration-tested.
- Do not put FFLogs secrets/tokens into logs, pull files, exports, fixtures, or source control.
- New analyzer UI belongs in focused panels/components, not as more logic inside the existing RecapWindow monolith.
- Do not split into multiple assemblies prematurely; enforce boundaries with namespaces/folders/interfaces first.
- Heuristic conclusions must expose confidence and evidence. Prefer Unknown/InsufficientEvidence to invented certainty.
- Preserve legacy saved-data support until an explicit migration/deprecation decision is approved.
- Preserve existing user-visible Better Deaths behavior unless the active milestone intentionally changes it.
- Direct WTFDiG reuse must be verified against actual source, preserve MIT attribution, and record upstream file + commit provenance.

## Required agent workflow

For each assigned task:

1. Read the active milestone in `docs/analyzer/PROGRESS.md`.
2. Inspect the exact existing files and tests involved.
3. State the smallest intended code surface before editing.
4. Add/adjust tests first where existing behavior is being moved or characterized.
5. Implement one reviewable architectural concept.
6. Run the relevant test suite and build/format checks.
7. Review the diff for forbidden coupling and unrelated churn.
8. Update `docs/analyzer/PROGRESS.md` with evidence, test/build results, changed files, and remaining work.
9. Open a PR. Do not self-merge.

## Review / sign-off model

Every implementation PR must be reviewed independently before merge.

Approval requires all applicable gates:

- acceptance criteria for the assigned slice are met;
- tests and build pass;
- no forbidden dependency direction was introduced;
- no unrelated cleanup/churn is mixed in;
- persistence changes have compatibility coverage;
- performance-sensitive capture changes include measurement evidence;
- copied/ported third-party code/data has attribution and provenance.

A reviewer may return one of:

- **APPROVE** — technically acceptable and milestone-safe.
- **CHANGES REQUESTED** — concrete defects or architecture violations must be fixed.
- **BLOCKED** — missing information, source fidelity, license verification, or prerequisite prevents safe implementation.

## Current implementation boundary

The active work begins with **M0 — Baseline and characterization** only. M1+ architecture must not be introduced until M0 is reviewed and signed off.
