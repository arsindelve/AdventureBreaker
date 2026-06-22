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

## Conference-room dial/door (`conference_dial_planetfall.cs`)

Combination lock, random `UnlockCode = RollDice(999)` (discoverable via PieceOfPaper),
set with "set/turn dial to N" while in RecArea. **CLEAN — robust, no crashes.**

| Case | Result | Issue |
|---|---|---|
| `set dial to 1000` | "does not go that high" | OK |
| `set dial to 999` / `0` | set | OK |
| `set dial to -5` | "do not go below zero" | OK |
| `set dial to abc` / `1,000` | "can only be set to numbers" | OK |
| `set dial to 2147483648` / `99999999999999999999` (overflow) | "can only be set to numbers" (TryParse → null, no crash) | OK |
| `set dial to` (no number) | "You must specify a number" | OK |
| `set dial to twelve` / `five hundred` / `462.5` | 12 / 500 / 462 (word + truncate) | OK |
| correct code | "door swings open, dial resets to 0", IsOpen=true, Code="0" | OK |
| `examine dial` | "The dial is set to {Code}." (only in RecArea) | OK |
| `open door` (RecArea) | proper locked message | OK |
| dial commands from ConferenceRoom side | fall through (not RecArea) | OK (correct) |
| `set door to N` / `set lock to N` | empty → narrator | soft-spot (not filed) |
| `HasEverBeenOpened` after dial-open | stays false (dial bypasses open verb) | harmless, unread |

Soft-spots not filed: number-as-nounTwo never forms a MultiNounIntent so the door/lock
synonyms in the multi-noun handler are unreachable numerically (general parser limitation,
canonical `set dial to N` works); `HasEverBeenOpened` dead for this door.

## Floyd companion — power/scoring/take/give/search (`floyd_planetfall.cs`)

Deterministic seams only (AI chatter + random card-offer out of scope).

| Case | Result | Issue |
|---|---|---|
| `activate floyd` ×3 during 3-turn countdown | **score 2 → 4 → 6** (re-awards +2 each turn) | **#254 ✅ fixed** |
| normal single activation | +2 once | OK |
| deactivate → reactivate after waking | +0 (no re-award) | OK |
| `take floyd` (ON) | "too heavy … drops him", not pocketed | OK |
| `take floyd` (OFF) | narrator, not pocketed | OK (null CannotBeTaken, base never takes) |
| `give the diary/key to floyd` | held; 2nd item → "shrugs, drops it" | OK (existing tests) |
| `search floyd` OFF / ON | finds card / "giggles" | OK |
| `give card to floyd` | no-op (bare "card" unresolved) | artifact (#246 family, not filed) |

#254 root cause: activation +2 guarded on `HasEverBeenOn`, which only flips when the wake-up
countdown completes — so repeats during the countdown re-award. Fixed with a dedicated one-shot
`HasAwardedActivationPoints` flag (leaves `HasEverBeenOn` wake-timing/flavor untouched).
