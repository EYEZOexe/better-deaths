# M10 canonical export and anonymization policy

Status: **M10-C implementation — READY FOR VALIDATION**

Parent: #118  
Work package: #121

## Purpose

Technical Design v0.2 distinguishes ordinary local saved/exported data from a **true anonymized export**. Ordinary canonical data may contain player names and source references; a shareable anonymized export must redact direct player identity, and FFLogs credentials/tokens/authorization material must never enter pull files or debug/export payloads.

M10-C therefore adds one explicit export boundary rather than treating UI name redaction as privacy. The transformation is source-agnostic and consumes only a canonical `RecordedPull`; Analyzer workspace rendering and file writing remain outside the transformation.

## Export contract

`BetterDeaths.Exports.CanonicalPullExporter` exposes:

- `CanonicalPullExportMode.Canonical`;
- `CanonicalPullExportMode.Anonymized`;
- `CanonicalPullExportRequest` containing only the canonical `RecordedPull` plus export options;
- `CanonicalPullExportResult` containing the export-policy version, mode, exported pull ID and canonical JSON payload;
- `CurrentExportPolicyVersion = 1` so future privacy-policy changes are explicit rather than silently changing what "anonymized" means.

Both modes serialize through the existing versioned `CanonicalPullSerializer`. No FFLogs client, token, configuration object, Dalamud service or ImGui type is accepted by the export core.

## Canonical export semantics

Canonical mode is lossless local data interchange, **not a privacy-safe sharing mode**.

It preserves the existing versioned `RecordedPull`, including:

- original `PullId`;
- actor display names and owner relationships;
- pull/event provenance and `SourceReference`;
- `StartedAt` / event `ObservedAt` wall-clock timestamps;
- event IDs, deterministic sequence/order and pull-relative time;
- actor/job/action/status/mechanic evidence;
- positions and world-marker data.

The resulting payload round-trips through `CanonicalPullSerializer` and is deterministic for the same in-memory pull.

## Anonymized export policy v1

Anonymized mode creates a new canonical `RecordedPull` without mutating the source pull.

### Direct identity removed

Actor display names are replaced deterministically in canonical ActorId order where the actor can carry player identity:

- `Player` -> `Player 1`, `Player 2`, ...;
- `Pet` -> `Pet 1`, `Pet 2`, ...;
- `Unknown` -> `Unknown Actor 1`, ... because an unknown classification is insufficient evidence that a display name is non-player.

Enemy/NPC/Object display names remain because they are game/mechanic identity rather than player identity and are useful when inspecting encounter evidence.

Pet `OwnerActorId` is preserved, so owner/pet relationships remain structurally useful after the names are replaced.

### Source linkage removed

Anonymized mode removes every canonical source reference currently present in the model:

- `RecordedPull.Provenance.SourceReference` -> `null`;
- every event `EventProvenance.SourceReference` -> `null`;
- every position/marker provenance `SourceReference` -> `null`.

This removes FFLogs report/fight references such as `fflogs:report:...:fight:...` and local `local:<pull-id>` references from shareable bytes. `PullDataSourceKind`, fidelity and confidence remain because they describe evidence quality/source class without carrying the external source identifier.

### Absolute-time linkage removed

Anonymized mode removes wall-clock timing while retaining analysis timing:

- `PullMetadata.StartedAt` -> `null`;
- every event `ObservedAt` -> `null`;
- event/position/marker `PullTime` remains unchanged.

This preserves pull-relative analysis and evidence navigation without exposing the exact raid timestamp.

### Free-form marker text removed

`WorldMarkerSample.Label` is set to `null` in anonymized output. Marker index, active state, pull-relative time and coordinates remain, so encounter structure is preserved without carrying arbitrary/custom label text.

### Pull identity replaced without source-derived material

The original `PullId` is not exported in anonymized mode. Export policy v1 first builds the fully sanitized canonical pull with an empty placeholder ID, serializes that sanitized representation, and derives a deterministic replacement ID from that sanitized content plus the export-policy version.

The replacement ID therefore does **not** hash the original local/FFLogs pull ID, report code, absolute timestamps, original player names or source references. Pulls that differ only in those stripped identity fields produce the same anonymized identity/payload.

This keeps fixtures deterministic without retaining a trivial reversible/linkable identifier to the original source.

## Evidence intentionally preserved

Anonymization does not destroy the canonical facts required by analyzers. It preserves:

- canonical actor IDs and all source/target actor references;
- pet-owner links;
- actor kind, job/class metadata;
- event runtime type, event ID, sequence and pull-relative time;
- action/status/tether/marker/mechanic IDs and structured values;
- damage/healing amounts and relevant event flags;
- targetability/gauge/mechanic semantics;
- position samples and marker geometry;
- territory identity/name and pull duration;
- evidence fidelity/confidence and source *kind*.

The exported pull can therefore still be deserialized and analyzed using the same canonical contracts.

## Credential and secret boundary

The export core has no parameter for FFLogs client ID, client secret, access token, authorization header, plugin configuration or transient integration clients. It accepts only a `RecordedPull` and export options.

The existing FFLogs normalizer stores the sanitized report/fight reference in canonical provenance; it does not place client credentials or access tokens in `RecordedPull`. Existing `FFLogsAccessToken` also keeps its value private and redacts `ToString()`/JSON exposure. Anonymized export removes the report/fight source reference as an additional sharing boundary.

M10-C privacy fixtures additionally inject representative credential/header strings into the canonical fields that anonymized export treats as sensitive and assert that none survive in exported bytes. This is a defense-in-depth fixture; it does not redefine arbitrary canonical strings as a general secret vault.

Ordinary canonical export intentionally retains canonical source/name/time fields and must not be presented as safe for public sharing.

## Analyzer workspace integration

A focused `AnalyzerExportPanel` is registered in the Analyzer workspace after the pure export core and privacy fixtures exist.

The panel:

- clearly distinguishes canonical vs anonymized behavior;
- invokes the pure exporter outside ImGui draw work;
- writes to a user-selectable directory outside the export core;
- names anonymized files with the anonymized pull ID rather than the original pull ID;
- uses a temporary file followed by replacement so an interrupted write does not intentionally publish partial JSON;
- does not receive or forward the Analyzer window's FFLogs client credential fields.

No export logic is added to `RecapWindow` or analyzer modules.

## Privacy scope and residual inference

Policy v1 removes direct player names, pet names, uncertain actor names, original pull identity, external source references, absolute timestamps and free-form marker labels while preserving the combat facts necessary for analysis.

It is not a formal k-anonymity/differential-privacy system. A sufficiently distinctive event/position sequence could be correlated by someone who already possesses the same combat data. Removing those facts would defeat the stated purpose of sharing analyzable canonical fixtures. If real use demonstrates that this residual correlation risk is unacceptable, that requires a new reviewed export-policy version rather than silently weakening v1 semantics.

## Validation / acceptance mapping

- [x] Export transformation is a focused source/UI-agnostic boundary.
- [x] Canonical export round-trips the versioned `RecordedPull` without semantic loss.
- [x] Anonymized player/pet/unknown names are deterministic replacements.
- [x] Pet-owner and event actor relationships remain intact.
- [x] Original pull ID, external source references and absolute timestamps are removed/replaced.
- [x] Free-form world-marker labels are removed from anonymized payloads.
- [x] Stable event IDs/order/pull-relative evidence remain unchanged.
- [x] Representative credential/token/authorization material cannot survive sensitive anonymized fields.
- [x] Export does not mutate the source `RecordedPull`.
- [x] Export contracts contain no FFLogs/Dalamud/ImGui types.
- [x] Analyzer workspace exposes explicit canonical/anonymized actions at the application boundary.
- [ ] Full branch CI/build/format validation green.

M10-C remains **READY FOR VALIDATION** until the branch CI passes and the final diff receives independent review.