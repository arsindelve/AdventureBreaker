# Disambiguation / noun-resolution hunt — approach

Goal: systematically hammer the family of bugs around **two-or-more items sharing a base
noun and differing only by adjective** (cards, uniforms, goo, batteries, megafuses, spools,
boards, doors, buttons…). #244 (bedistor) is the first instance; this plan finds the rest and
their corners/edges.

## 1. The bug class — three resolvers that can disagree

| Path | Verbs | Resolver | Adjective-aware? |
|---|---|---|---|
| examine / read | examine, read, look at | `SimpleInteractionEngine.CheckDisambiguation` → `MatchNounAndAdjective` | **Yes** |
| take / drop / put-single | take, drop, get | `Repository.GetItemInScope` → `ItemBase.HasMatchingNoun` | **No** ← #244 lives here |
| multi-noun | put X in Y, give X to Y, slide X through Y | `MultiNounEngine` (`NounsForPreciseMatching`) | Partly |

`HasMatchingNoun` has a **word-boundary containment fallback**: `" good bedistor "`.Contains(`" bedistor "`)
→ the *fused* bedistor matches "good bedistor". And `GetItemInScope` searches the **location before
inventory**, first-match. So an adjective-qualified reference resolves to the wrong item.

## 2. Invariants the oracle checks (per family, per verb)

- **A. Precise reference resolves correctly.** "kitchen card" / "old battery" → the right item, for *every* verb.
- **B. Bare ambiguous reference disambiguates.** "card" / "battery" → "Which … do you mean, …?" — never a silent pick.
- **C. Cross-verb consistency.** examine X and take/put X resolve to the **same** item. (#244 = they disagree.)
- **D. Wrong/absent adjective doesn't silently act.** "good battery" with only the old present → no-match / disambiguation, not the old one.
- **E. Disambiguation follow-up.** "the kitchen one" resolves and doesn't loop (the longest-noun remap guard).
- **F. Pronoun carry-over.** After "examine the kitchen card", "take it" → the kitchen card.

## 3. Target families (from source enumeration `NounsForMatching`)

Priority by puzzle-criticality and adjective-collision risk:

1. **Access cards (7)** — `IdCard, KitchenAccessCard, LowerElevatorAccessCard, MiniaturizationAccessCard,
   ShuttleAccessCard, TeleportationAccessCard, UpperElevatorAccessCard`; share "card"/"access card";
   Lower/Upper also share "elevator access card". Puzzle-critical (slide through slot / readers). **Highest.**
2. **Batteries** (`FreshBattery`/`OldBattery`, share "battery") — clean good/old pair. **Confirmed BUG.**
3. **Megafuses** (`BSeriesMegafuse`/`KSeriesMegafuse`, share "megafuse"/"fuse"). **Confirmed BUG.**
4. **Spools** (`Brown/Green/RedSpool`, share "spool") + the **"red"** cross-collision (`RedSpool`/`RedButton`/`GooBase`).
5. **Uniforms** (`PatrolUniform`/`LabUniform`, share "uniform") + worn-vs-carried + pockets. **Pre-map: OK — understand why.**
6. **Goo / Slime** (`GooBase`/`Slime`, share "goo"; `GooBase` also shares "red").
7. **Buttons** (`Black/Red/White/CryoElevator`, share "button") — compare to Zork `SoManyButtons` baseline.
8. **Fromitz boards** (`First/Third/Fourth`, share "fromitz board"; plus `Shiny`/`Cracked`).
9. **Slots** (`KitchenSlot`/`Lower`/`UpperElevatorAccessSlot`, share "slot") — the Y in "put card in slot".
10. **Doors (13)** incl. radiation-lock inner/outer, elevator doors — "open the door".
11. Tail: **cans** (`OilCan`/`TinCan`), **desks** (`Lab/Large/Small`), **paper** (`Memo`/`PieceOfPaper`),
    **robots** (`Floyd`/`BrokenRobot`), **screen** (`ComputerTerminal`/`SpoolReader`), **bed** (`Bed`/`Infirmary`).

## 4. Test matrix (per family)

- **Verbs:** examine, read, take, drop, put X in Y, give X to Y, slide/insert (cards→slots),
  push (buttons), open/close (doors), wear/remove (uniforms), turn on/off, show, throw.
- **Phrasings:** bare noun ("card"); adjective+noun ("kitchen card", "kitchen access card");
  full precise name; synonym ("new battery"=fresh); wrong adjective ("blue card" — nonexistent);
  "the X one" follow-up.
- **Scope configs (set up via god mode — now reliable post-#241):** both in inventory;
  one inventory + one location (← triggers the location-first bug); both in location;
  one in an open container; one in a closed/opaque container (should be out of scope);
  one **worn** + one carried (uniforms); in the dark.
- **Cross-verb diff:** compare examine-resolution vs take/put-resolution for the *same* phrase.

## 5. Corners & edges (explicit checklist)

- Containment false-positive (base noun inside the longer phrase: "card"⊂"kitchen card", "red"→3 items).
- Location-first ordering bias (room item beats the one in hand).
- **Worn vs carried** (uniform): is a worn uniform in scope for take/examine? does patrol-vs-lab disambiguate?
- Nested / transparent containers (card in a pocket in a worn uniform; bedistor in a box).
- Multi-noun where **both** nouns are ambiguous: "put card in slot" with 2+ cards **and** 3 slots.
- Disambiguation **follow-up** ("the kitchen one") + the infinite-disambiguation-loop guard.
- Pronoun "it" after referencing one of a pair.
- "take all cards" / scope-all over a family.
- Adjective that is itself another item's noun ("red" = `GooBase` noun **and** `RedSpool`/`RedButton` adjective).
- Zork baselines (`SoManyKnives`, `SoManyButtons`) as known-good regression anchors.

## 6. Method (per family) — respecting prod-lag + the #244 TDD lesson

1. Pull `NounsForMatching` + `NounsForPreciseMatching` from source for the family.
2. **Pre-map at the resolver seam** (deterministic unit test, no deploy): `Repository.GetItemInScope(phrase, ctx)`
   with target-in-inventory + collider-in-location → does it return the target? (This caught batteries+megafuses
   offline, before any deploy.)
3. Confirm player-reachability on prod via god-mode scope setup + the verb matrix.
4. **Verify every prod anomaly against `origin/main` source** before filing (prod lags main).
5. **TDD at the right seam** — ⚠️ `GetResponse("take the good X")` does NOT reproduce (the deterministic
   parser can't parse it; needs the AI parser). Test **`GetItemInScope` directly** (take/drop/put), or
   `MultiNounEngine`/`GetResponse` for parser-handled multi-noun phrasings. (Lesson from #244.)
6. Log to coverage (`cover --category parser-pronoun`), per-family frontier; dedup vs known issues.

## 7. Consolidation insight (avoid issue spam)

Batteries, megafuses, and bedistor are the **same** root cause (`GetItemInScope` adjective-blindness).
The #244 fix (make `GetItemInScope` prefer precise/adjective matches + disambiguate) should clear **all**
of them. So:
- File **one** umbrella fix (#244) + a **parametrized regression test** covering every affected family.
- After it deploys, **re-run the whole matrix** — most families auto-clear. File **new** issues only for
  cases the fix doesn't cover: multi-noun resolver, worn-uniform scope, disambiguation-loop, family-specific quirks
  (e.g. why uniforms already pass, whether cards behave like batteries).

## 8. Pre-deploy map (run at the `GetItemInScope` seam, pre-#244-fix)

| Family | Phrasing | Result |
|---|---|---|
| bedistor (good/fused) | "good bedistor" | **BUG** (→ #244) |
| battery (fresh/old) | "fresh battery", "new battery" | **BUG** |
| megafuse (b/k-series) | "b-series megafuse", "k-series megafuse" | **BUG** |
| uniform (patrol/lab) | "patrol uniform", "lab uniform" | **OK** (exception — investigate) |
| cards, spools, goo, buttons, fromitz, doors, … | — | **TODO** (next: run the matrix) |

## 9. Execution order once #244 ships to prod

1. Re-run §8 map → confirm batteries/megafuses/bedistor cleared by the fix.
2. Extend the pre-map to **cards** (all 7), **spools**, **goo/slime**, **fromitz**, **buttons**, **cans**, **doors**.
3. For each still-broken or fix-missed case: god-mode prod repro → source-confirm → deterministic seam test → file with TDD.
4. Then the **corners/edges** (§5): worn uniforms, multi-noun double-ambiguity (card+slot), pronoun carry-over, "take all".
5. Roll up coverage; keep the frontier honest with `AB_TARGET_SHA`.
