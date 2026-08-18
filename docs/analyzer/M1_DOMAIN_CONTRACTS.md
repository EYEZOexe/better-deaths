# M1 Canonical Domain Contracts

Status: implementation baseline
Date: 2026-08-18
Governing design: FFXIV Static Analyzer Technical Design v0.2, Sections 4–5 and Appendix A.

## Design-mandated contracts

M1 introduces a source-agnostic `RecordedPull` root containing pull metadata/schema, actor records, a typed normalized event stream, spatial samples, and provenance. `NormalizedEvent` carries a stable event ID, explicit source ordering (`Sequence`), pull-relative `TimeSpan`, optional wall-clock metadata, optional source/target actor IDs, and event provenance.

Analyzer output is represented as structured `AnalysisResult` records with stable identity, severity/category, actors, optional time range, evidence, confidence, and metrics. UI rendering is not part of these contracts.

## Conservative engineering decisions where v0.2 is intentionally a sketch

The design explicitly says its suggested contracts are sketches rather than copy-paste mandates. M1 therefore keeps unspecified fields minimal:

- `PullMetadata` currently contains territory identity/name, duration, and optional start wall-clock time. Encounter-specific interpretation is not embedded here yet.
- `ActorRecord` contains pull-local identity, display name, broad actor kind, optional job identity, and optional owner actor identity for pets/companions. Source entity IDs are not part of the canonical contract.
- `AnalysisCategory` is a small source-agnostic taxonomy aligned with the planned analysis domains. It is not an analyzer registry.
- Event payloads contain only straightforward facts needed to establish typed shapes. More event-specific facts should be added only when a normalizer/analyzer has a concrete source-agnostic requirement.
- `System.Text.Json` polymorphism metadata on `NormalizedEvent` gives the M1 fixture a stable discriminator and allows the root aggregate to round-trip mixed event types. No pull-store/file layout is introduced by this decision.

## Boundary rules

`BetterDeaths.Domain` must not reference Dalamud services/types, ImGui/UI types, FFLogs client/DTO types, network clients, or persistence implementations. The lightweight `net10.0` unit-test project compiles all domain files directly, providing a dependency boundary independent of the Dalamud SDK.

M1 does not switch any existing Better Deaths capture or saved-history path. Existing `PullDeathSnapshot` behavior remains untouched until the later additive migration milestones.

## Deferred intentionally

- Full-pull live recording/finalization and zero-death persistence: M2.
- `IPullStore` implementation/migration: introduced with the persistence work required by M2; M1 defines only the root data contract.
- Analyzer module engine/indexes/dependency orchestration: M3.
- Workspace UI: M4.
- FFLogs client/normalizer: M6.
- WTFDiG encounter knowledge ports: M8.
