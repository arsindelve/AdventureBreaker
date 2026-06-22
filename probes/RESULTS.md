# Probe results matrix

Running record of what each probe found. Target = ZorkAI `origin/main` (deployed).

## Disambiguation / noun resolution (`disambiguation_planetfall.cs`)

| Area | Phrasing / config | Verb path | Result | Issue |
|---|---|---|---|---|
| bedistor, batteries, megafuses, cards (all phrasings), spools, fromitz, goo | precise, target-in-inv + collider-in-loc | take/drop/put (`GetItemInScope`) | **OK** (fixed) | #244 ✅ merged #245 |
| uniforms (lab/patrol) | precise | `GetItemInScope` | OK (never broke) | — |
| batteries / megafuses (pre-fix) | precise | `GetItemInScope` | was BUG | #244 (covered the family) |
| cards (kitchen vs shuttle) | bare "card" | examine/take | OK — "Do you mean…?" | — |
| cards | "kitchen card", `[shuttle,kitchen]` in inv | **multi-noun** (`put/slide X in Y`) | **BUG → SHUTTLE** | **#246 (open)** |

## Pronouns (`pronouns_zork.cs`)

| Case | Example | Result | Issue |
|---|---|---|---|
| "them" → collection | `take all; drop/put/take them` | **BUG** — "What item…?" | **#248-A (open)** |
| antecedent across move | `take lantern; east; drop it` | **BUG** — cleared, "What item…?" | **#248-B (open)** |
| single "it" | `take lantern; drop it` | OK | — |
| "them" for `IPluralNoun` | `take candles; drop them` | OK | — |
| caps + period | `drop IT.` | OK — Dropped | — |
| "it" after disambiguation | `examine card; →kitchen; take it` | OK — takes kitchen card | — |
| "it" no antecedent | `eat it` (fresh) | OK — "What item…?" | — |
| "put it in X" after "open X" | antecedent = most-recent ("case") | defensible, not filed | — |
| examine out-of-scope item | `eat lunch; examine lunch` | empty `[]` — **harness artifact, not a prod bug** | — |

## Still on the pronoun list (next)
- "it"/"them" to an item stolen by the thief / destroyed (prod, AI-narrated).
- AI pronoun resolver (`ResolvePronounsAsync`) binding "it" to a noun only in narrator flavor text (seen live once; needs a pin-down harness).
- "it" as `give it to <npc>`; pronoun inside multi-sentence after a disambiguation prompt.

## GET/POST envelope parity (`get_post_parity.py`)

| Field | POST | GET | Issue |
|---|---|---|---|
| inventory | held | (was empty) | #230 ✅ fixed |
| exits / actions in dark | hidden | leaked | #238 (open) |
| previousLocationName | "Behind House" | **null** | **#250 (open)** |
| lastMovementDirection / moves / score / locationName | — | match | OK |

## Data subsystem: diary / terminal / spool reader (`data_subsystem_planetfall.cs`)

| Item | Case | Result | Issue |
|---|---|---|---|
| computer terminal | `type N` while **off** | **BUG** — menu navigates, screen "dark" | **#252 (open)** |
| computer terminal | key>count / leaf / 0=up / multi-noun "type N on keyboard" | OK | — |
| diary | read / press-advance / press-before-read / rewind at end | OK | — |
| spool reader | insert / read green+red / "already a spool" / "doesn't fit" (non-spool) | OK | — |
