# M11 FFLogs Golden Pull Truth Manifest

Status: characterization baseline for issue #146

Design basis: Technical Design v0.3 M11-A

Fixture: `tests/BetterDeaths.Tests/Fixtures/M11/canonical-pull-fight3.anonymized.json`
Export policy: canonical anonymized export policy v1

## Purpose and privacy boundary

This fixture freezes the observed fight-3 canonical behavior before M12 semantic normalization. It is not a corrected pull and must not be used to imply that current FFLogs job, status, resource, or capability semantics are complete.

The sensitive source artifact was read outside the repository, deserialized through `CanonicalPullSerializer`, and passed to `CanonicalPullExporter` with `CanonicalPullExportMode.Anonymized`. The exporter was run twice and produced byte-identical output. No raw source bytes, source path, source hash, player name, report code/reference, wall-clock timestamp, or credential material is recorded here.

The committed fixture has SHA-256 `D370AE57ECA46CFE97863D7F1E44D6E254A349DA74F228AC6838C7D7B80BB5FA`. This hash identifies only the approved anonymized fixture.

## Stable pull truths

| Truth | Observed value |
|---|---:|
| Territory | `1363` |
| Encounter name | `Sigmascape V4.0` |
| Pull duration | `00:07:40` |
| Actors | `48` |
| Canonical events | `14,912` |
| Positions | `0` |
| World markers | `0` |

The fixture contains exactly eight primary combat-job players with source job identities: Dancer, Gunbreaker, Monk, Paladin, Pictomancer, Sage, Viper, and WhiteMage. It also contains one FFLogs LimitBreak pseudo-actor currently classified as `ActorKind.Player`; that actor is not counted as a primary party member. Two pet actors retain valid owner relationships after anonymization.

All sequences and EventIds are unique and contiguous from `1` through `14,912`. Pull-relative time is nondecreasing. Canonical serialize/deserialize preserves event type, EventId, sequence, and pull-relative time.

## Event inventory

| Canonical event type | Count |
|---|---:|
| Damage | `5,177` |
| Heal | `2,107` |
| Action use | `3,148` |
| Cast start | `351` |
| Status apply | `1,914` |
| Status remove | `2,200` |
| Death | `11` |
| Targetability | `4` |

Eight of the eleven deaths have at least one target-matching DamageEvent in the preceding ten-second canonical window. EventId `14,571` is a retained death example with nearby damage evidence including EventId `14,520`. The remaining deaths are not assigned a cause from chronology alone.

## Current semantic mismatches retained intentionally

- The primary Dancer actor retains source identity `Dancer`, not canonical `DNC`.
- Representative canonical Dancer action identities are present, including `15997`, `15998`, `16011`, and `16013`.
- Status identity `1001825` occurs 16 times across apply/remove events; canonical Devilment status `1825` occurs zero times.
- Source-encoded Forsaken status families `1005084`, `1005085`, and `1005086` are present; canonical Forsaken IDs `5084`, `5085`, and `5086` are absent.

These are M11 characterization facts. This fixture does not implement job mapping, source-ID classification, or a numeric transform.

## Current default-engine outcome

The current default Analyzer Engine produces:

- 22 results: 11 `generic.death-raise-context` and 11 `generic.healing-activity`;
- zero analyzer failures;
- two `Unsupported` skips: `job.dnc.core` and `job.dnc.burst-uptime`;
- no Forsaken result. The Forsaken module supports territory 1363 but its exact canonical status-ID filter does not match the retained source-encoded status families.

## Capability and diagnostic observations

- Spatial replay evidence is unavailable: the supplied canonical artifact has no positions or world markers. No samples were fabricated.
- Canonical v1 HealEvent retains raw amount/action identity only. Effective healing, overheal, shield contribution, and resource-state capability cannot be established from this artifact and remain unavailable/unknown.
- The canonical export contains normalized events, not the FFLogs normalizer's pre-canonical skipped-event diagnostics. Skipped/unsupported source event categories and their counts are therefore **unavailable from the supplied artifact**, not zero.
- The fixture proves current canonical event inventory and engine outcomes only. It does not decide the M12 resource-enrichment profile or M13 capability/schema contracts.

## Privacy audit

The fixture carries `ExportMode = anonymized` and `ExportPolicyVersion = 1`. Player and pet display names use deterministic `Player N` / `Pet N` labels. Pull/event source references and wall-clock timestamps are null. The fixture contains no FFLogs report URL/reference, Authorization header, or Bearer token marker. Enemy/NPC names remain under the approved anonymized export policy because they are game/source semantics rather than player identity.

As documented in `M10_EXPORT_PRIVACY.md`, anonymized combat data can retain correlation risk through distinctive event sequences. This fixture is approved for deterministic repository testing, not a claim of formal anonymity.
