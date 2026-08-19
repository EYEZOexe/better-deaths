# M6 FFLogs integration sign-off

Status: **PENDING CI / combined review**

Parent: #67  
Sign-off package: #73  
Actor-fidelity blocker: #79

## Reviewed boundary

M6 is reviewed as one end-to-end path:

`FFLogs OAuth/client -> report/fight metadata + paginated events -> FFLogs normalization -> canonical RecordedPull -> IPullStore -> Analyzer Engine -> Analyzer Workspace`

The review is intentionally stricter than the individual implementation PRs. A green component PR is not sufficient if information is lost between components.

## Official FFLogs schema evidence used by the review

Verified against the FFLogs v2 GraphQL schema before implementing the #79 fix:

- `Report.masterData(translate:)` exposes `ReportMasterData`.
- `ReportMasterData.actors` is the report actor directory.
- `ReportActor` exposes report `id`, `name`, `type`, `subType`, and `petOwner`.
- report event APIs expose source/target instance identity in addition to report actor identity.

These source-specific fields stay inside `BetterDeaths/Sources/FFLogs` and are projected into pull-local canonical `ActorId` values before analyzer exposure.

## #79 actor-fidelity fix

The combined review found that the first M6 implementation supported actor metadata in fixtures but did not carry report master actors through the production GraphQL client. It also keyed canonical actors only by report actor ID, which could merge multiple NPC/pet instances.

The sign-off fix therefore requires and now implements:

- report `masterData.actors` retrieval and parsing;
- master actor propagation into `FFLogsFightImportData`;
- deterministic source identity using report actor ID plus source/target instance evidence for non-player actors;
- player identity stability even when instance fields vary or are present inconsistently;
- pet-owner mapping through the report master actor directory;
- pull-local actor IDs with no FFLogs-specific identity fields added to Domain;
- selected-pull actor directories containing only referenced actors plus required owners, not every actor from the full report;
- explicit placeholder actors when master data is unavailable rather than invented names/jobs/ownership.

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
- [ ] CI restore, format, tests, and plugin/package build green on the final sign-off head.

## Exit gate

Do not close #67 or authorize M7 until the final sign-off PR is green and independently reviewed against this checklist. Once green, update `docs/analyzer/PROGRESS.md`, mark this document **APPROVED**, close #73/#79/#67 as appropriate, and start M7 from the resulting `main` commit.
