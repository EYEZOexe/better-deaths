# M6 FFLogs integration sign-off

Status: **APPROVED**

Parent: #67 — completed  
Sign-off package: #73 — completed  
Actor-fidelity blocker: #79 — completed  
Sign-off PR: #80 — merged as `1f600fc6f91800f2b64ce9e035d05014d4c0cd2b`  
Implementation/sign-off CI: `32225159132`  
Final PR-head CI: `32225505916`

## Reviewed boundary

M6 was reviewed as one end-to-end path:

`FFLogs OAuth/client -> report/fight metadata + paginated events -> FFLogs normalization -> canonical RecordedPull -> IPullStore -> Analyzer Engine -> Analyzer Workspace`

The review was intentionally stricter than the individual implementation PRs. A green component PR was not sufficient where information could be lost between components.

## Official FFLogs schema evidence used by the review

Verified against the FFLogs v2 GraphQL schema before implementing the #79 fix:

- `Report.masterData(translate:)` exposes `ReportMasterData`.
- `ReportMasterData.actors` is the report actor directory.
- `ReportActor` exposes report `id`, `name`, `type`, `subType`, and `petOwner`.
- report event APIs expose source/target instance identity in addition to report actor identity.

These source-specific fields stay inside `BetterDeaths/Sources/FFLogs` and are projected into pull-local canonical `ActorId` values before analyzer exposure.

## #79 actor-fidelity correction

The combined review rejected the first M6 state because actor metadata was supported by fixtures but not propagated by the production GraphQL client, and because actor identity used report actor ID alone. That could collapse distinct NPC/pet instances into one canonical actor.

PR #80 corrected that by:

- retrieving and parsing report `masterData.actors`;
- propagating report actors into `FFLogsFightImportData`;
- using report actor ID plus source/target instance evidence for non-player source identity;
- deliberately keeping player identity stable when instance fields vary;
- resolving pet-owner relationships through report master data;
- allocating deterministic pull-local canonical actor IDs without adding FFLogs identity fields to Domain;
- emitting only selected-fight referenced actors plus required owners rather than copying the full report actor directory;
- retaining explicit unknown placeholders when source master data is missing instead of inventing names/jobs/ownership.

## Combined M6 acceptance checklist

- [x] OAuth/client credentials and GraphQL details remain inside the FFLogs source boundary.
- [x] Access tokens/client secrets are redacted and are not persisted into canonical pulls, caches, fixtures, exports, or analyzer results.
- [x] Public client-credentials flow and private-report authorization failures are represented as integration errors.
- [x] Report/fight metadata and event pages are loaded asynchronously.
- [x] Pagination requires a strictly advancing cursor and is fixture-tested.
- [x] Report master actor metadata is loaded and propagated to normalization.
- [x] Non-player source instances remain distinct when FFLogs supplies instance evidence.
- [x] Player identity remains stable across source-instance noise.
- [x] Actor IDs, event ordering, sequence, pull-relative time, provenance and fidelity are deterministic.
- [x] Imported report code/fight ID remain in sanitized `PullProvenance.SourceReference` rather than FFLogs-specific Domain fields.
- [x] Imported pulls persist through `IPullStore`.
- [x] Local and FFLogs pulls execute through the same Analyzer Engine without source-specific analyzer branches.
- [x] Unsupported/missing FFLogs facts remain skipped/unknown rather than being guessed.
- [x] Workspace import stays outside synchronous ImGui rendering work and outside `RecapWindow`.
- [x] Integration errors stay separate from analyzer-module failures.
- [x] Domain and Analysis contain no FFLogs DTO/client/auth dependencies.
- [x] No M7 job-specific or M8 encounter/WTFDiG implementation was pulled into M6.
- [x] CI `32225159132` passed restore, formatting, all tests, and plugin/package build on the reviewed implementation/sign-off state.
- [x] Final PR-head CI `32225505916` passed restore, formatting, all tests, and plugin/package build before merge.

## Lead-integrator decision

**APPROVED.**

The changed-file review for PR #80 was limited to the FFLogs source/client boundary, FFLogs fixtures, and M6 documentation. Domain, Analysis, and `RecapWindow` were unchanged. The #79 fidelity defect is covered by deterministic same-report-ID/different-instance fixtures and production master-actor propagation.

PR #80 merged to `main`; #79, #73 and #67 are complete. M7 — First Job Analyzer — is authorized from merge commit `1f600fc6f91800f2b64ce9e035d05014d4c0cd2b`.
