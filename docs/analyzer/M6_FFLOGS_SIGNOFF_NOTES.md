# M6-F source verification notes

The actor-fidelity blocker was checked against official FFLogs documentation before implementation.

Verified FFLogs v2 schema facts used by this package:

- `Report.masterData(translate:)` returns report master data.
- `ReportMasterData.actors` contains report actors.
- `ReportActor` provides `id`, `name`, `type`, `subType`, and `petOwner`.
- FFLogs event/report APIs distinguish source and target actor instance identity in addition to report actor ID.

Implementation consequence: report/master actor and instance identifiers remain source-specific. They are consumed only inside `BetterDeaths/Sources/FFLogs` and converted into deterministic pull-local canonical `ActorId` values before Domain/Analysis use.

Official documentation reviewed 2026-08-19:

- FFLogs v2 `Report`
- FFLogs v2 `ReportMasterData`
- FFLogs v2 `ReportActor`
- FFLogs event/scripting documentation for source/target actor instance identity
