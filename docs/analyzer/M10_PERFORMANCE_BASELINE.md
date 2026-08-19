# M10 performance baseline

Status: **M10-A baseline fixture — awaiting CI measurements**

Parent: #118  
Work package: #119

## Purpose

Technical Design v0.2 treats performance as a feature and requires M10 hardening decisions to be based on realistic measurements rather than speculative storage/capture rewrites. This package establishes repeatable regression fixtures before any optimization or persistence-backend decision.

The automated gates intentionally use generous wall-clock ceilings. Hosted CI timing is noisy, so the pass/fail thresholds are regression tripwires rather than product latency targets. A passing run establishes an upper bound; exact local/profiler values should be recorded separately when a measured optimization is proposed.

## Baseline fixture sizes

### Long canonical pull

- duration: 20 minutes;
- canonical normalized events: 50,000;
- representative position samples for serialization: 4,000;
- one player actor;
- exact local provenance;
- typed `ActionUseEvent` stream with strict deterministic sequence/order.

This is intentionally a high-volume structural fixture, not a claim that every real Ultimate pull contains exactly this event distribution.

### Multi-pull file store

- 20 canonical pulls;
- 1,000 events per pull;
- save all pulls through the real `FileCanonicalPullStore`;
- query compact summaries;
- reload every returned detail pull.

The M9 combined suite separately retains the progression-night session gate of 500 pulls and 500 sequential full-pull loads.

## Automated boundaries

| Boundary | Fixture | CI regression ceiling | What the gate proves |
|---|---:|---:|---|
| `FullPullRecorder` append + finalize | 50k events / 20 min | 5 s | append/finalize remains practical at Ultimate-scale volume and produces the expected canonical event count |
| `CanonicalPullSerializer` round-trip | 50k events + 4k positions | 10 s | versioned JSON serialization/deserialization remains usable and round-trips the long-pull structure |
| Default Analyzer Engine composition | 50k-event pull | 10 s | current generic + job + encounter registry composition can process a long canonical stream without analyzer failure |
| `FileCanonicalPullStore` save/query/reload | 20 × 1k-event pulls | 20 s | current split index/detail file backend remains usable at a multi-pull volume and exercises real atomic/recovery-aware store code |
| Session Intelligence | 500 pulls | existing M9 5 s pure / 10 s orchestration gates | progression-night cross-pull aggregation/loading remains guarded after M10 changes |

## Capture/finalization interpretation

The recorder fixture measures the pure append-only recorder plus canonical model finalization, not plugin/Dalamud hook cost and not disk serialization. This separation is deliberate: the design requires expensive serialization/analysis to stay off the render/game thread, and combining all work into one timing number would hide which boundary regressed.

Live hook overhead still requires manual in-game/frame-time validation because a hosted unit-test runner cannot reproduce FFXIV/Dalamud frame scheduling. M10-E should retain that as a manual release check rather than inventing a false CI measurement.

## Storage decision input

M10-A does **not** introduce SQLite, compressed chunking or a new `IPullStore`. M10-B must use these fixtures plus any additional measured file-size/query/recovery evidence to decide whether the current file-backed implementation presents a material v1 problem.

Default decision in the absence of measured failure: keep the existing replaceable file-backed `IPullStore` and defer a backend migration.

## Measurement policy

- Wall-clock assertions are deliberately generous and should only catch order-of-magnitude regressions.
- Do not tighten ceilings from one hosted CI run.
- Before a performance optimization, record before/after fixture shape and measurements.
- Allocation profiling is not asserted in this first package because stable cross-run allocation numbers have not yet been established for the complete linked-source test environment.
- File-size is bounded in the serializer fixture to catch accidental explosive output; M10-B may add explicit comparative size evidence if backend selection becomes a real question.

## Acceptance mapping

- [x] Realistic 15–20 minute canonical pull-volume fixture added.
- [x] Recorder append/finalize boundary measured separately from serialization/store work.
- [x] Canonical serializer long-pull round-trip measured.
- [x] Current file store save/query/load path measured.
- [x] Current default Analyzer Engine composition measured on a long stream.
- [x] Existing M9 500-pull Session gates remain the cross-pull performance baseline.
- [ ] Full CI green on the M10-A branch.
- [ ] Exact CI run recorded after validation.

No production behavior or persistence architecture changes are included in M10-A.
