# M0 Persistence Baseline

Status: characterization only
Date: 2026-08-18
Scope: current Better Deaths saved-pull behavior before the FFXIV Static Analyzer canonical persistence work.

## Current persisted root

The current saved detail object is `PullDeathSnapshot`. It is death-centric and contains `Deaths` plus replay/context collections such as positions, markers, mechanics, world markers, mitigation, and debuffs.

This is a baseline fact, not the future canonical model. M1/M2 must not silently reinterpret this format as `RecordedPull`.

## Current schema/version constants

Defined in `BetterDeaths/Plugin.cs`:

- `RecordedPullHistorySchemaVersion = 3`
- `RecordedPullIndexSchemaVersion = 7`

The plugin also records `CapturedPluginVersion` on pull details/summaries. These values are separate concerns: schema versions describe stored structure; plugin version records the producer version.

## Storage layout

Current storage is split into:

- legacy/history file: `recorded-pulls.json`
- index file: `recorded-pulls.index.json`
- per-pull detail directory: `recorded-pull-details`
- detail files may be JSON or compressed JSON (`.json.gz`)

The index contains compact pull metadata and a detail filename. Full `PullDeathSnapshot` detail can therefore be loaded separately from summaries.

## Load and recovery order

`Plugin.RecordedPulls.cs` currently attempts storage recovery in this order:

1. primary split index;
2. temporary split index;
3. backup split index;
4. rolling split-index backups;
5. primary legacy/history file;
6. temporary legacy/history file;
7. backup legacy/history file;
8. rolling legacy/history backups.

When legacy history is loaded, the plugin migrates it into split storage where possible.

## Legacy history shapes

`DeserializeRecordedPullHistory` currently accepts both:

1. a raw JSON array of `PullDeathSnapshot` records; and
2. an object containing a `Pulls` property through the versioned history envelope.

The M0 tests cover that the current public model can round-trip through both shapes. The private plugin deserializer remains the runtime authority until a later explicit extraction/refactor.

## Death-only persistence contract

The current persistence path intentionally excludes zero-death pulls in multiple places:

- legacy pull normalization/loading filters `Deaths.Count > 0`;
- normalized split states filter summaries with `DeathCount > 0`;
- split-index loading filters entries with `DeathCount > 0` and a non-empty detail filename.

Therefore M2 zero-death full-pull support requires an intentional persistence migration/change. Altering only pull finalization is insufficient.

## Pull-number normalization

Current legacy normalization preserves valid unique positive pull numbers. Invalid (`<= 0`) or duplicate numbers are reassigned to the next unused positive number while preserving the incoming pull order. Legacy loaded pulls are ordered by `CapturedAtUtc` before this normalization step.

The M0 contract test mirrors this algorithm so the intended baseline is explicit. Because the current implementation is private and coupled to the plugin persistence partial, the test is characterization of the contract rather than a direct call into the private runtime method.

## Save behavior

Current split persistence:

- snapshots the in-memory recorded-pull state before background save;
- tracks a storage revision and dirty flag;
- writes in the background;
- clears dirty state only when the saved revision still matches the live revision;
- schedules another save if the live state changed while a save was running;
- maintains temp/backup recovery paths;
- migrates eligible detail files to compressed storage.

M1/M2 should preserve these safety properties unless a replacement is explicitly designed and compatibility-tested.

## M1/M2 constraints derived from this baseline

- Do not delete legacy history loading when canonical persistence is introduced.
- Do not silently reuse schema version 3 or index version 7 for semantically different canonical data.
- Zero-death pulls need an explicit new storage/schema path or migration rule.
- Keep summary/index loading separate from heavy pull detail where practical.
- Preserve atomic/recoverable write behavior and background-save revision safety.
- Add compatibility fixtures before modifying existing persisted fields or filtering rules.
