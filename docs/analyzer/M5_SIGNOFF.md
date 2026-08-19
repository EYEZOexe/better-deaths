# M5 — Generic Hardcore Analysis Sign-off

Status: **READY FOR REVIEW**
Governing design: Technical Design v0.2
Parent issue: #51
Integration issue: #58

## Scope reviewed

M5 establishes the first source-agnostic generic raid-analysis layer over canonical `RecordedPull` data. It intentionally stops before FFLogs source integration, job-specific rotation rules, and encounter/WTFDiG strategy packs.

Implemented work packages:

- **M5-A / #52 / PR #59** — shared targetability/status interval indexes; merge `6cb116415aaeb9cc7ff5b2eb2a1cd9f4df2e297f`.
- **M5-B / #53 / PR #60** — live sampled status/targetability enrichment at the source boundary; merge `0ea7b9a25a1a714328a8b1068783db2fea3ccae7`.
- **M5-C / #54 / PR #61** — targetability-aware generic uptime/activity; merge `f1d943da2b5f5888b763708b22d7d52457066867`.
- **M5-D / #55 / PR #62** — evidence-first death/raise context; merge `c02193cbd569c5bf26f1c40840c3b4abb14b2278`.
- **M5-F healing slice / PR #63** — neutral raw-healing context; merge `10677289e609d640285d31beb0ad43f06e8fbfc7`.
- **M5-E / #56 / PR #64** — configured mitigation coverage/what-if; corrected and merged `1145ad3ee5678499d32b77d7f25223634f62df0c` after CI `32215987673`.
- **M5-F timeline completion / #57 / PR #65** — explicit buff/cooldown timelines; corrected and merged `2e3c65fe298d2dd4d13ba481d79a58dfcffd30cb` after CI `32216083596`.

## Lead-review corrections

Two issues were caught before M5 sign-off:

1. **Mitigation semantic scope was too compressed.** `TargetStatus` correctly described where evidence was observed but collapsed personal, targeted, and party-wide mitigation into one semantic bucket. The corrected contract keeps evidence application (`TargetStatus` / `DamageSourceStatus`) separate from explicit scope (`Personal`, `Targeted`, `PartyWide`, `DamageSourceDebuff`, `Other`), validates incompatible combinations, and carries scope into structured metrics and summaries.
2. **Cooldown CastStart fallback was global instead of per actor.** One actor's `ActionUseEvent` could suppress another actor's valid `CastStartEvent` fallback for the same configured action ID. The corrected implementation chooses ActionUse-vs-CastStart evidence independently per source actor. The failing fixtures were also corrected to use the canonical `CastStartEvent.CastDuration` field.

Both corrected heads passed full repository CI before merge.

## Combined behavioral review

- [x] Targetability intervals preserve unknown pre-first-observation coverage and forced untargetable windows.
- [x] Status intervals preserve source distinction and explicit known-vs-uncertain ending coverage.
- [x] Live status/targetability enrichment remains in the capture/normalization boundary; no Dalamud type enters analyzer contracts.
- [x] Generic uptime findings are restricted to evidence-supported targetable windows and do not turn forced downtime into inactivity.
- [x] Players with canonical deaths are excluded from normal execution-gap findings so death/raise downtime is handled separately.
- [x] Death analysis uses recent damage/status evidence as context and explicitly refuses last-hit-equals-blame/lethal attribution without HP/shield evidence.
- [x] Raise observations are downstream evidence only and do not claim resurrection completion or recovery-end time.
- [x] Healing results remain neutral raw-healing summaries because current canonical data does not encode effective healing, overheal, HP deficit, MP/resource cost, or opportunity cost.
- [x] Mitigation reports positive observed coverage only; absence is not called a missed cooldown without availability evidence.
- [x] Personal, targeted, party-wide, and damage-source-debuff mitigation semantics remain distinguishable from effect kind (damage reduction/shield/invulnerability/other).
- [x] Mitigation overlap is explicitly coverage evidence, not automatically waste.
- [x] What-if mitigation estimates are emitted only from explicit configured reduction assumptions and are labeled counterfactual rather than reconstructed server damage/survival proof.
- [x] Generic buff/cooldown timelines require explicit definitions and never invent FFXIV action/status semantics.
- [x] Cooldown timelines prefer explicit ActionUse evidence per actor, use CastStart only as that actor's fallback, and never reinterpret damage/heal packet fanout as extra uses.
- [x] Unknown status endings stay uncertain and are not converted into full buff uptime or missed-refresh verdicts.
- [x] Structured findings retain stable result/event/actor/time evidence and confidence.
- [x] No FFLogs client/DTO, job-specific expectations, encounter pack, or WTFDiG data was pulled forward.

## Workspace integration

The default Analyzer Workspace composition now replaces M3's deliberately minimal `DeathEventAnalyzer` vertical slice with the M5 `DeathRaiseContextAnalyzer` and also registers the generic analyzers that require no external semantic catalog:

- `DeathRaiseContextAnalyzer`
- `HealingActivityAnalyzer`
- `TargetabilityAwareUptimeAnalyzer`

Mitigation and buff/cooldown analyzers intentionally remain definition-driven. The repository does not silently invent a global FFXIV mitigation/cooldown catalog in M5; those semantics can be supplied by later job/encounter/source configuration layers while the generic analyzers remain source-agnostic.

## M5-G fixture pack

`M5GenericAnalysisIntegrationTests` runs one representative canonical pull through death/raise, healing, uptime, mitigation, and explicit timeline analyzers together and verifies:

- deterministic result identities across repeated runs;
- death context without blame-by-last-hit;
- death -> raise evidence;
- very high raw healing remains neutral rather than an overheal/waste warning;
- mitigation overlap remains neutral and scope-distinct;
- an open-ended/uncertain mitigation status is not assumed active;
- uptime findings never cross the evidence-supported forced untargetable window;
- cooldown/buff timelines remain explicit and do not claim missed uses/refreshes.

A clean no-evidence pull is also required to produce no invented findings.

## Remaining gate

The combined M5-G branch must pass repository CI: restore, formatting verification, all automated tests, and plugin/package build. Only after that succeeds may this document be changed to **APPROVED**, #51/#58 be closed, and M6 — FFLogs Integration — be authorized.

## Manual runtime note

CI cannot launch FFXIV/Dalamud. In-game validation of enriched live status/targetability capture and presentation of M5 results in the Analyzer Workspace remains a manual runtime smoke item and is not represented as automated evidence.
