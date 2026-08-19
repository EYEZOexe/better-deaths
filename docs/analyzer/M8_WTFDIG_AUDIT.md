# M8 WTFDiG audit — Dancing Mad Ultimate / Forsaken

Status: **M8-A implementation audit**

Parent milestone: #94  
M8-A: #95

## Pinned upstream

Repository: `EYEZOexe/wtfdig`  
Commit: `73a2ffa959b8f57bfbe7a1a75d5e43383ae2ea81`  
License: MIT, copyright 2024 Matthew Czubakowski

The audit pins an exact commit rather than treating the current website as an implicit mutable rules source.

## Files inspected

### `src/routes/ultimates/umad/data.ts`

Useful M8 information:

- encounter phase hierarchy contains `P2 - Forsaken` / `Forsaken`;
- the Kroxy-Rinon overview states that tanks/melees are the flexing roles for the strategy;
- the opening `Start` instruction says to find the role/group partner;
- its opening group rule is: **Group A if the pair has different debuffs and one has Stack; Group B if both debuffs are the same**;
- later sections describe Odd Towers, Even Towers, Future/Past baiting, and eight tower-resolution steps;
- South Adjust is a separate strategy variant.

Only the opening partner/group rule is brought into the first analyzer package. Later tower instructions are deliberately not converted into analyzer facts until their spatial/resolution evidence can be represented precisely.

### `src/lib/arena.ts`

Useful reusable concepts:

- explicit Tank, Healer, Melee, and Ranged role categories;
- support/DPS and G1/G2 grouping concepts;
- arena/waymark/player/AoE/geometry structures.

M8 does **not** copy the TypeScript/Svelte arena architecture. The C# analyzer defines small source-agnostic `EncounterDefinition`, `EncounterPhaseDefinition`, `AssignmentRule`, and `ArenaGeometry` contracts appropriate to the existing Analyzer Engine.

## Existing Better Deaths evidence inspected

`BetterDeaths/ReplayEncounterModules.cs` already contains a legacy Dancing Mad Ultimate replay module. It establishes current plugin facts used by the new definition without importing legacy replay types into Analysis:

- territory ID `1363`;
- circle arena centered at `(100,100)` with radius `20`;
- Forsaken overhead status IDs `5084` Stack, `5085` Spread, `5086` Cone;
- older replay-only Forsaken pairing/group visualization logic.

The new encounter layer does not reference `ReplayEncounterModules`, `ReplayMarkerSnapshot`, or any ImGui/replay renderer type. Status evidence is consumed through canonical `StatusApplyEvent`/indexes in the analyzer package.

## Canonical evidence available now

The canonical model currently provides enough information for the opening assignment slice:

- player actors and job abbreviations;
- typed status application/removal events with source fidelity/confidence;
- deterministic pull-relative timestamps/event IDs;
- positions/world markers when captured;
- source provenance that distinguishes exact vs sampled evidence.

A critical limitation is that `ActorRecord` does **not** contain static-specific slot labels such as MT/OT/H1/H2/M1/M2/R1/R2. M8 therefore must not sort ActorIds and silently call those slots. The first analyzer evaluates role-compatible Tank↔Healer and Melee↔Ranged pairing layouts and reports ambiguity when more than one layout remains compatible with the observed evidence.

## First mechanic selected

**P2 Forsaken — opening partner/group assignment, Kroxy-Rinon strategy.**

Why this is evidence-safe:

1. the expected group rule is explicit in the pinned WTFDiG data;
2. relevant Stack/Spread/Cone statuses already exist as canonical status evidence;
3. player role can be derived deterministically from canonical job abbreviation without source DTOs;
4. a complete exact debuff set can determine whether at least one strategy-compatible role-pair layout exists;
5. ambiguous/missing/sampled evidence can remain unknown instead of being guessed.

The first pass/fail judgment is **strategy compatibility**, not player blame. A failure means the observed exact opening role/debuff set cannot be arranged into a valid Kroxy-Rinon partner layout under the encoded rule. It does not assert who caused the server-assigned debuffs or whether a later tower was resolved incorrectly.

## Deferred from the first slice

The following WTFDiG material is intentionally deferred:

- exact Odd/Even tower positions;
- South Adjust movement/positions;
- the eight-tower movement sequence;
- Future/Past bait geometry;
- cone/stack bait resolution outcomes;
- waymark-relative movement and consequence chains;
- direct use of WTFDiG diagram coordinates/images.

Those require either precise arena geometry derived from a verified source or a stronger canonical resolution/position contract. Encoding approximate coordinates merely to claim a mechanic analyzer would violate the project's evidence-first rule.

## Provenance rule for later M8 work

Any later WTFDiG code/data copied or derived must name the exact upstream path and this pinned commit (or deliberately update the pin after a new audit) and preserve the MIT attribution in `THIRD_PARTY_NOTICES.md`.
