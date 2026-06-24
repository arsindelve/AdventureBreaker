# Shared reference for `/play` and `/test`

Hard-won, reusable knowledge both skills depend on but that you can't reconstruct from
the harness help text. Read this once before a run. (Companion to each skill's own
Prerequisites / loop / gotchas — this is the "how to read what you see and judge it.")

## 1. Reading harness signals (`play` vs `quiet`)

- `play <cmd>` = narrator **ON** (what web players see). `quiet <cmd>` = narrator **OFF**
  (`noGeneratedResponses`), engine-only.
- **The "no effect" sentinel — the most important interpretive key.** In `quiet` mode,
  a command with **no engine handler** (one that would otherwise fall through to the AI
  narrator) returns the literal string:
  > `This action or command has no effect on the game.`
  That sentinel means **the engine does not recognize/handle this command** — NOT that
  the game considered it and refused. A *genuine* engine response (a static string, or a
  state change like `Taken` / score / inventory delta) shows through `quiet` unchanged.
  - Use it to separate **"parser/engine doesn't recognize it"** ("no effect") from
    **"engine handled it"** (real text). This is how you prove parser-gap bugs:
    `quiet "get key with magnet"` → "no effect" = canonical phrasing unrecognized;
    `quiet "put magnet on crevice"` → "...a steel key!" = handled.
- **Static engine strings show through BOTH modes**; only AI/narrator-generated text is
  replaced by the sentinel in `quiet`. So:
  - **"Is this engine-recognized?"** → use `quiet`.
  - **Player-facing narrator behavior** (hallucination, contradiction, tone, character
    break) → use `play`.
- A/B diagnosis: `quiet` wrong ⇒ **engine/parser** bug; `quiet` right but `play` wrong ⇒
  **narrator** bug.
- Read the structured envelope after each command / in `state`:
  `[Location] score= moves= time= exits=[...] inv=[...]` and `actions@loc: [...]`. The
  envelope is free ground truth — check the prose against it (e.g. "Taken" with no `inv`
  change is a bug). NPCs/Floyd often appear only in the **prose** ("There is a multiple
  purpose robot here"), not in `actions@loc`.

## 2. Where bugs hide — the edge-class taxonomy

Phrasing variations (synonyms / prepositions / alternate verbs) find parser gaps, but the
meatiest bugs are **state-machine edges**. For any puzzle or action, deliberately try:

- **Happy-path first.** Confirm the intended solve actually works, so you know the
  baseline and don't misread a setup failure as a bug.
- **Empty-resource / stale-state** — do the action when the required item is gone/absent.
  (→ AB-037: "place ladder across rift" narrated losing a ladder the player didn't have.)
- **Double-application / idempotency** — do an already-completed action again.
  (→ AB-038: re-placing the ladder when it already spanned → duplicate in the room's
  item list + wrong success text.)
- **Post-consume / post-change** — examine/use/read a thing after it's been
  retrieved/consumed/toggled. (→ #291: the crevice kept reporting the key after it was
  taken.)
- **Wrong-tool / wrong-target** — right verb, wrong object or indirect object
  (`unlock door with key` vs the padlock; `unlock padlock with brush`).
- **Canonical-original phrasing** — the *original game's* solution wording, which a
  faithful port should accept. (→ #298: `get key with magnet` was unrecognized.)

## 3. Severity rubric (grade consistently)

- **critical** — crash / data loss / save corruption / hard progression block, no
  workaround.
- **high** — progression blocker with an awkward workaround, or a wrong outcome that
  costs the player items/score.
- **medium** — user-visible wrong output on a path a normal player hits (wrong
  description, false narrator claim, stale-state message, a natural command silently
  failing).
- **low** — edge phrasing, cosmetic, or an unusual-input quirk a typical player rarely
  hits.
- **info** — verification of a fix, a confirmed non-bug, or a neutral observation.

Heuristic: "a player would notice this on a normal playthrough" → **medium** or higher;
"only weird input triggers it" → **low**.

## 4. ZIL dispatch — turning "looks weird" into "provably a divergence"

The original ZIL is ground truth (read-only; **never copy/port** it — see ZorkAI's
`CLAUDE.md`). To use it for "faithful vs bug" judgments you need how dispatch works:

- `PRSO` = direct object, `PRSI` = indirect object; `VERB?` tests the verb.
- **An object's action routine (`KEY-F`, `LADDER-FCN`, `FLOYD-F`, …) only runs when the
  parser resolves that object *in scope*** (held or present). Once an object is
  `<REMOVE>`d from the world, the parser can't resolve it, so its routine never fires —
  the player gets "you can't see any X here," NOT the routine's text.
  - This is the lens that proved AB-037: the C# re-ran the ladder's "lost forever" branch
    by matching on *command text*, but the original could never reach that branch with no
    ladder in scope ⇒ unintended divergence.
- Room/location routines fire on entry / per turn and can gate on flags (e.g. the
  Computer Room sets the Floyd-concern flag only when Floyd is present).
- Find routines by grepping `../ZorkAI/planetfall-source/*.zil` or
  `../ZorkAI/zork1/*.zil` for the object/verb; cite `file:line` as issue evidence.

## 5. Game landmarks & item initial states (VERIFY LIVE — may drift)

Derive spine indices live (see each skill's navigation section); these are time-savers,
not gospel — the spine can be re-extracted.

**Planetfall** (`adventurebreaker/spine/planetfall.json`):
- Magnet: take ≈42; `put magnet on crevice` (the solve) ≈54. Padlock/key: `unlock padlock
  with key` ≈58. Ladder: take ≈64 / drop ≈70 / extend ≈71 / `place ladder across rift`
  ≈72.
- Floyd: `activate floyd` ≈45 (then he follows and wanders). Cards: upper ≈77, kitchen
  ≈79, shuttle ≈83, lower ≈97. Computer Room entry ≈294 — **this sets the Floyd-concern
  flag, so test the show-printout branch BEFORE it**; `read output` ≈295; bio-lab ≈298+;
  Floyd dies in the bio-lab sacrifice ≈308–320.
- **Item initial states:** the **survival kit is NOT a starting item** (acquired mid-game
  ≈25–48); it **starts closed, holding all three goos** (red/brown/green). The **steel
  key starts at the bottom of the crevice** (retrieved with the magnet).
- **Floyd presence cues:** prose "There is a multiple purpose robot here" / "Floyd back!"
  = present; narrator calling him an "imaginary friend" / "deactivated robot" = he's
  wandered off (or got reset) — `wait` for his return or confirm via `state` before
  show/give/conversation tests.

**Zork I** (`adventurebreaker/spine/zork1.json`): the opening spine reaches the Kitchen
(score 10) by ≈step 6; derive deeper landmarks live.

## 6. `save` / `restore` gotchas

- `restore` **resets the survival clock** — the cure for Planetfall sleep/hunger death
  mid-probe.
- The endpoint flakes (intermittent HTTP 500). Before giving up: **`restore <name>`
  sometimes fails where `restore <id>` works** — get the id from `saves` and restore by
  id. If both fail, fall back to a fresh `new` + `spine-run`.
- Forking model: `save` a checkpoint → probe destructively → `restore` to continue. Never
  interleave manual commands before a `spine-run` (the **spine-desync trap**: the
  walkthrough replays against changed state and diverges — inventory/score reset).

## 7. Operational odds & ends

- Harness entrypoint is `python3 -m adventurebreaker.harness` (not `.cli`). List saves
  with `saves` (not `list`).
- Long `spine-run`s (≳50 steps) may auto-background; poll the run's output file for
  `spine_pos now N`, then `state`.
- To extract just the narrator text from harness output, filter the envelope/markers,
  e.g. `grep -vE '^$|oracles@|actions@loc|^> .*HTTP|^---'`.
- Commit through the signing-server 503 with
  `git -c commit.gpgsign=false commit --no-gpg-sign`; push with `-u` and retry/backoff.
