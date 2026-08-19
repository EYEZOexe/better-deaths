# FFXIV Static Analyzer — Agent Execution Contract

This repository is being evolved from Better Deaths into the FFXIV Static Analyzer described by the governing technical design.

## Source of truth

Implementation work must follow, in order:

1. `docs/analyzer/TECHNICAL_DESIGN_v0.2.md` once added to the repository, or the supplied Technical Design v0.2 document until then.
2. `docs/analyzer/PROGRESS.md` for the completed architecture, preserved contracts, current approved work, review state, and exact milestone evidence.
3. The approved GitHub issue/work package for the task being performed.
4. Existing code and tests. Never infer behavior only from file names.

If requested work conflicts with an architectural invariant, stop and report the conflict instead of bypassing it. If the in-repository technical-design Markdown is still absent, do not invent or silently rewrite the governing design; use the supplied Technical Design v0.2 source.

## Mandatory architecture rules

- Work from explicitly approved issues/work packages. Do not invent a new milestone or opportunistically implement unrelated future scope.
- Keep every meaningful batch buildable and testable.
- Add characterization coverage before refactoring behavior that already works.
- Prefer additive seams first; migrate consumers only after the seam is proven.
- Do not rewrite raw capture/hook logic for style alone.
- Preserve Better Deaths' short lead-up/death-recap buffers. Full-pull capture must remain a separate append-only path.
- Domain/analyzer contracts must not depend on Dalamud services, FFLogs DTOs, packet structs, ImGui, network clients, or persistence implementations.
- Analyzer modules must not mutate canonical pull data, perform network I/O, or render UI.
- Findings must be structured and evidence-backed; formatted prose is presentation, not the analyzer contract.
- Use deterministic event ordering and pull-relative time for analysis.
- Persisted canonical data must be schema-versioned and migration-tested.
- Do not put FFLogs secrets/tokens into logs, pull files, exports, fixtures, or source control.
- New analyzer UI belongs in focused panels/components, not as more logic inside the existing RecapWindow monolith.
- Do not split into multiple assemblies without the evidence/revisit conditions recorded in `docs/analyzer/M10_EXTRACTION_DECISION.md`.
- Heuristic conclusions must expose confidence and evidence. Prefer Unknown/InsufficientEvidence to invented certainty.
- Preserve legacy saved-data support until an explicit migration/deprecation decision is approved.
- Preserve existing user-visible Better Deaths behavior unless an approved task intentionally changes it.
- Direct WTFDiG reuse must be verified against actual source, preserve MIT attribution, and record upstream file + commit provenance.
- Canonical local export and true anonymized sharing export are distinct privacy modes; do not weaken the policy recorded in `docs/analyzer/M10_EXPORT_PRIVACY.md` without an explicit reviewed policy version change.
- Retain the file-backed `IPullStore` decision for v1 unless measured evidence satisfies a storage-backend revisit trigger in `docs/analyzer/M10_STORAGE_DECISION.md`.

## Required agent workflow

For each assigned task:

1. Read the active/current state and preserved contracts in `docs/analyzer/PROGRESS.md`.
2. Read the exact approved issue/work package and its acceptance criteria.
3. Inspect the exact existing files and tests involved.
4. State the smallest intended code surface before editing.
5. Add/adjust tests first where existing behavior is being moved, hardened, or characterized.
6. Implement one reviewable architectural concept.
7. Run the relevant test suite and build/format checks.
8. Review the diff for forbidden coupling and unrelated churn.
9. Update `docs/analyzer/PROGRESS.md` when the task changes milestone/release state or preserved architectural evidence. Do not rewrite historical evidence for a bounded bug fix that does not change those records.
10. Open a PR with exact validation evidence.
11. Do not silently self-merge.

## Review / sign-off model

Independent implementer/reviewer separation remains the default for implementation PRs.

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

### Repository-owner-directed lead-integrator exception

The repository owner may explicitly designate a lead-integrator for a specific task or work sequence and authorize that lead to perform the requirement review and merge after reviewing the exact final diff, acceptance criteria, and final-head CI evidence.

When this exception is used:

- the authorization must be explicit for the current task/work sequence; do not infer it from ordinary write access;
- the lead must record a review/sign-off on the PR with the requirements checked and any limitations called out;
- the exact PR head must have the required validation before merge;
- the lead must not bypass a known blocker, failing test, unresolved compatibility/privacy issue, or third-party attribution requirement;
- the exception does not weaken the default independent-review model for other agents/tasks;
- an agent without explicit owner authorization must not self-review and merge its own implementation silently.

## Current implementation boundary

**Technical Design v0.2 M0–M10 are complete. The v1 architecture is approved.**

Final architectural sign-off landed through M10-E / PR #129, with the detailed Definition-of-Done review in `docs/analyzer/M10_V1_SIGNOFF.md` and the authoritative milestone/contract ledger in `docs/analyzer/PROGRESS.md`. Post-v1 export-panel null-selection hardening landed in PR #131 without changing the v1 architecture.

There is no implicitly authorized M11. New feature work, product/rebrand changes, storage/backend changes, new Job/Encounter packs, release automation, or other post-v1 scope must start from an explicitly approved issue/work package and preserve the v1 contracts unless that task intentionally and explicitly replaces one.

The remaining non-architectural release gate is in-game FFXIV/Dalamud runtime QA. GitHub CI validates restore, formatting, automated tests/performance fixtures, plugin build, and package invariants, but it cannot prove live UI/capture/frame-time behavior inside the game. Do not represent automated architecture sign-off as a substitute for runtime release smoke testing.
