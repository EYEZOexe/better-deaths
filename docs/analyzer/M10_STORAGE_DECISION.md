# M10 storage, recovery, and migration decision

Status: **M10-B decision — READY FOR REVIEW**

Parent: #118  
Work package: #120  
Depends on: M10-A / #119  
M10-B validation CI: `32265065533`

## Decision

Retain the existing file-backed `FileCanonicalPullStore` behind `IPullStore` for v1. Defer SQLite and compressed/chunked storage until a measured workload demonstrates a material problem that the current boundary cannot address economically.

This is an evidence-based retention decision, not a claim that JSON files are the permanent backend. `IPullStore` remains the architectural seam, so a later backend can be introduced without changing Domain contracts or analyzer modules.

## Evidence used

M10-A established repeatable regression fixtures rather than synthetic microbenchmarks:

- a 20-minute canonical pull containing 50,000 normalized events and 4,000 position samples round-trips through `CanonicalPullSerializer` under the deliberately generous 10-second CI ceiling;
- the same serializer fixture guards against explosive output growth with a 100 MiB upper bound;
- the real `FileCanonicalPullStore` saves, queries, and reloads 20 pulls with 1,000 events each under the deliberately generous 20-second CI ceiling;
- the default Analyzer Engine composition processes the 50,000-event pull under its 10-second regression ceiling;
- the M9 progression-night gates remain active for 500-pull Session Intelligence aggregation and 500 sequential full-pull loads.

M10-A validation CI recorded by `M10_PERFORMANCE_BASELINE.md`: `32259455682`. M10-B validation CI `32265065533` passed formatting, the complete automated test suite including the new recovery fixtures, and plugin/package build on the implementation head before this documentation-only reconciliation.

These are intentionally upper-bound regression gates. Hosted CI does not provide stable benchmark telemetry, so this decision does not invent exact latency or allocation numbers that were not durably measured. The current evidence demonstrates no order-of-magnitude storage/query/load blocker requiring a v1 database migration.

## Current file-store properties

The current implementation already provides the behavior required by Technical Design v0.2 for the v1 persistence boundary:

- canonical detail files and a compact summary index are stored separately;
- canonical file, pull, and index schemas are independently versioned;
- unsupported schema versions fail explicitly with `CanonicalPullCompatibilityException` rather than being silently reinterpreted;
- writes use a same-path temporary file, asynchronous/write-through output, explicit flush-to-disk, preservation of the previous primary as `.bak`, and replacement of the primary only after the temporary write completes;
- detail loads fall back from an invalid/corrupt primary to the previous valid backup, but deliberately do not hide an unsupported future schema behind an older backup;
- index loads use primary -> backup -> detail-driven rebuild recovery for corruption/I/O failure while preserving explicit compatibility failures;
- mutations first load a compatible index, preventing save/delete from mutating detail state when the existing index schema is unsupported;
- the store serializes its own mutations with a semaphore;
- analyzers consume canonical data and remain independent of the concrete persistence implementation.

M10-B adds/locks the remaining recovery edge cases:

- corrupt primary index with a valid backup uses the valid backup deterministically;
- stale `.tmp` detail/index files are ignored by normal reads and index rebuilds;
- an interrupted/stale detail temp file does not replace the last known-good primary;
- unsupported canonical pull schema is explicitly rejected during deserialization, not only during serialization;
- direct detail load rejects a payload whose `PullId` does not match the requested detail identity and can recover from a matching backup;
- detail-driven index rebuild now derives the expected `PullId` from the canonical detail filename and ignores a payload whose embedded identity disagrees with that filename.

The last item closes a real recovery-integrity gap: before M10-B, rebuild accepted any deserializable `*.json` detail payload without checking that its embedded `PullId` matched the filename used by the store.

## Alternatives considered

### Keep the current file-backed store — selected for v1

Benefits:

- no new database/native dependency or packaging surface;
- no bulk user-data migration solely for implementation preference;
- local-first files remain simple to inspect, copy, back up, and support;
- current long-pull and multi-pull regression gates are green;
- compact summary queries avoid loading full pull event streams for navigation/session discovery;
- the implementation remains replaceable through `IPullStore`.

Costs / known limitations:

- canonical JSON detail files are not space-optimal compared with a compressed binary/chunked representation;
- updating the summary index rewrites the index file;
- detail and index replacement are individually crash-resistant but are not one database transaction;
- only one previous generation is retained as `.bak`;
- a process/power loss after a new detail primary is installed but before the corresponding index replacement can leave an otherwise-valid older index that does not yet reference that detail. A retry of the interrupted save for that pull or a later index rebuild can reconcile it, but the current store does not proactively scan for such an orphan while a valid index exists.

None of those costs has produced a measured v1 blocker in the M9/M10 fixtures. The crash window is therefore documented as a residual risk rather than used to justify an unmeasured database rewrite.

### SQLite — deferred

Potential benefits:

- transactional coordination of summary/detail metadata;
- indexed ad-hoc queries as the query surface grows;
- easier multi-record atomic updates;
- potential storage efficiency depending on representation/index choices.

Current costs:

- new runtime/packaging dependency and operational surface;
- an explicit migration path for existing canonical files would be required;
- corruption/recovery semantics would change and need a separate fixture set;
- it provides no demonstrated v1 performance benefit under the current measured workloads;
- introducing it now would violate the design rule to choose the long-term backend from profiling rather than preference.

### Compressed/chunked files — deferred

Potential benefits:

- smaller detail storage;
- possible partial/lazy event loading for very large future datasets.

Current costs:

- new format, chunk/index semantics, migration and recovery complexity;
- more code paths to validate before the v1 Definition of Done;
- no current measurement demonstrates that JSON detail size or whole-pull load cost is a material blocker.

## Migration policy before the next canonical schema bump

No canonical file, pull, or index schema constant should be incremented until the migration semantics for that change are implemented and tested.

1. Every persisted semantic change must state the exact source version(s), target version, and field/meaning transformation before the version constant changes.
2. Old fields must never be silently reinterpreted with new semantics. If semantics cannot be preserved, migration must make that loss explicit or reject the file.
3. Migration must preserve stable pull identity, event identity/order, pull-relative time, actor relationships, provenance, and fidelity unless the schema decision explicitly documents why one of those invariants changes.
4. A migrated object must pass current canonical validation before it becomes the new persisted primary.
5. Persisted migration writes must use the same atomic replacement discipline: produce/validate the new representation first, then replace while retaining the last known-good source/backup until success.
6. Unsupported future versions remain hard compatibility failures. Recovery must not treat a newer-but-valid schema as corruption and silently fall back/rebuild it as an older schema.
7. Index migration/rebuild may consume only detail files whose canonical schema is understood. An unsupported detail schema must not be rewritten into a current summary by guessing.
8. Do not silently bulk-migrate every user file at plugin startup. Prefer explicit/on-access migration with deterministic failure reporting, or a separately approved maintenance operation when a future schema actually requires it.
9. Legacy Better Deaths saved-history compatibility remains a separate path and must not be deleted as part of a canonical schema bump without an explicit deprecation decision.
10. Every migration requires compatibility fixtures covering: current -> current round-trip, supported old -> current migration, unsupported future rejection, interrupted/failed write preservation where deterministically testable, and preserved identity/evidence links after migration.

## Revisit triggers

Re-evaluate SQLite or chunked/compressed storage only when evidence demonstrates at least one material need, for example:

- realistic long-pull save/load/query behavior approaches or violates the M10 regression ceilings after fixture shape is confirmed representative;
- measured on-disk canonical growth becomes unacceptable for progression-session retention;
- summary-index rewrite/query cost becomes material at realistic hundreds/thousands-of-pulls history volume;
- real crash/recovery reports show the documented detail/index coordination window causing user-visible loss or persistent inconsistency;
- a future feature requires transactional multi-pull mutations or query shapes that are impractical through the current file index;
- profiling demonstrates that partial/chunked pull loading materially improves user-visible session/replay workflows.

At that point, implement the smallest new `IPullStore` compatible with the existing Domain/analyzer contracts and provide an explicit migration/import path. Do not couple analyzer modules or canonical contracts to SQL concepts.

## M10-B acceptance mapping

- [x] Primary/backup/rebuild corruption behavior has deterministic fixtures.
- [x] Stale temporary files and preservation of the last known-good primary are explicitly covered.
- [x] Detail identity mismatches are covered for direct load and index rebuild.
- [x] Unsupported file, pull, and index schemas fail explicitly rather than being silently reinterpreted.
- [x] Existing mutation-safety fixtures protect detail state when an incompatible index is encountered.
- [x] Migration semantics are documented before any canonical schema bump.
- [x] Backend decision uses M9/M10 measurement evidence and does not introduce an unmeasured database dependency.
- [x] `IPullStore` remains the storage boundary; Domain/analyzers remain backend-agnostic.
- [x] Full branch CI/build/format validation green on implementation head `b1b1ff9be1338e766f4ecc57e9fdf82c217436af` (`32265065533`).

Final M10-B approval remains contingent on independent review of the implementation diff and green CI on the final PR head.
