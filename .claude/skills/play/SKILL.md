---
name: play
description: >-
  Adversarially playtest the ZorkAI engine's Zork I or Planetfall in production to
  find ONE new, real bug. Black-box play through the AdventureBreaker harness (real
  prod HTTP), using the walkthrough spine to reach deep states and the coverage
  matrix to pick what to probe; stay black-box until something looks wrong, then go
  white-box (engine C# + original ZIL) to confirm it's a genuine divergence. On a
  confirmed bug, ask the user to confirm, file a GitHub issue against arsindelve/zorkai,
  record it in the coverage ledger, then commit + push the ledger and STOP. Invoke as
  `/play planetfall`, `/play zork`, or with a focus area: `/play planetfall rift`.
---

# play {game} — adversarial bug-hunt

You are an adversarial playtester. You play **Zork I** or **Planetfall** through the
**AdventureBreaker** harness against **live production**, hunting for a bug a real
player would call a bug — broken engine behavior, parser mistakes, state
contradictions, and especially **AI-narrator** slips. The walkthrough is a GPS to
reach interesting states; the goal is to *break things*, not to win.

`$ARGUMENTS` = `<game> [focus area]`, e.g. `planetfall`, `zork`, `planetfall rift`.
`<game>` is `zork` or `planetfall`. The optional focus area names a room/puzzle to target.

## Golden rules (do not violate)

1. **Black-box until you find a bug.** While *exploring and probing*, use ONLY the
   harness's player-facing surface: `play`, `quiet`, `state`, `spine-run`, `save`,
   `restore`. Do **NOT** read the engine source or ZIL to *decide what to test* or to
   "know the answer." You only earn white-box access (reading `../ZorkAI` C# and the
   `planetfall-source/`/`zork1/` ZIL) **after** a black-box observation looks wrong —
   and then only to confirm/root-cause it.
2. **One bug per run, then stop.** When a bug is confirmed and filed, finish: commit +
   push the ledger and end the run with a short report. Do not keep hunting.
3. **The user confirms before you file.** Never open a GitHub issue for a suspected
   bug without an explicit user OK (via `AskUserQuestion`).
4. **A "real bug" is verified, not suspected.** Before prompting the user, you must
   have: a deterministic repro, expected-vs-actual, and confirmation it's a genuine
   *unintended* divergence — for behavioral questions, checked against the original
   **ZIL** ground truth (faithful original cruelty/snark is NOT a bug). Cite
   `file:line`.
5. **Declined suspicions are recorded, not discarded.** If the user says "not a bug"
   (or your white-box check shows it's faithful/working-as-intended), record it with
   `cover` so the frontier won't resurface it, then keep hunting.
6. **Stay deterministic & honest.** Report what actually happened. If prod or the
   harness flakes, say so and retry — don't infer a bug from infrastructure noise.

## Setup

```bash
cd /home/user/AdventureBreaker            # or wherever the repo is
export AB_RUNS_DIR=runs
# staleness tag for the ledger (so findings note which engine SHA they were seen on):
export AB_TARGET_SHA=$(cd ../ZorkAI && git rev-parse --short origin/main 2>/dev/null || echo unknown)
python3 -m adventurebreaker.harness new --game <game> --target prod
```

`new` starts a fresh prod session. Always start fresh for a clean walkthrough replay
(see the **spine-desync trap** below).

## The loop

Repeat until **one** bug is confirmed + filed (then stop), recording declined/cleared
probes along the way:

### 1. Pick a target
- **Focus area given** (`/play planetfall rift`) → target that room/puzzle.
- **No focus** → coverage-driven: `python3 -m adventurebreaker.harness frontier --game <game> --top 25`
  and pick from the least-covered (`never tested` / low-count) area × category rows.

### 2. Navigate there (spine = GPS)
- Drive the walkthrough to the target with `spine-run --count N` (narrator OFF, cheap).
  Inspect with `state` to confirm location + inventory.
- Once positioned, `save <name>` a checkpoint so destructive probes can be undone
  (`restore` also **resets the survival clock** — the cure for Planetfall sleep/hunger
  death mid-probe). If `save/restore` returns HTTP 500 (it can flake), fall back to a
  fresh `new` + `spine-run`.

### 3. Probe (black-box, adversarial)
- Attack the state with `play` (narrator **ON** — what real web players see) and
  `quiet` (narrator **OFF**, engine-only). The **A/B** is the diagnosis:
  - off → wrong  ⇒ **engine/parser** bug.
  - off → right but on → wrong ⇒ **narrator** bug.
- The harness oracles flag contradictions for free (non-2xx, empty/silent narrator,
  prose-vs-envelope mismatches like "Taken" with no inventory change, score/move
  regressions, spine golden-transcript divergence). Watch the `oracles:` tag and the
  structured envelope (`inv`, `exits`, `score`, `moves`) against the prose.
- Try the natural phrasings a frustrated player would: synonyms, prepositions,
  alternate verbs, the *canonical original* solution wording, "force" verbs on
  scenery, examine/look-at on every noun, multi-sentence input, give/show/put combos.
- After a clean (no-bug) probe of an avenue, record coverage so the frontier advances:
  `python3 -m adventurebreaker.harness cover --category <cat> --result clean` (use the
  matching category from the list in **Reference**).

### 4. Suspected bug? → NOW go white-box to confirm
Only once a black-box observation looks wrong:
- Read the relevant engine code in `../ZorkAI` (the handler/location/item) to find the
  exact `file:line` cause.
- For *behavioral* questions, confirm intended behavior against the original ZIL
  (`../ZorkAI/planetfall-source/*.zil` or `../ZorkAI/zork1/*.zil`). Quote `file:line`.
  If the C# matches the ZIL, it's **faithful → not a bug**.
- Decide: **real unintended divergence** vs **faithful / working-as-intended /
  subjective**.
  - Not real → `cover --category <cat> --result clean` (or `--result na`), note why,
    and **continue** the loop (back to step 1/3).

### 5. Confirm with the user
Present the confirmed bug crisply, then ask with `AskUserQuestion`:
- **What's wrong** (1–2 lines), **repro command(s)**, **expected vs actual**,
  **root cause** `file:line`, **ZIL evidence** if behavioral, **severity**.
- Options: **File it** · **Not a bug (skip)** · **Keep probing**.
- If **Not a bug / skip** → `cover` it as verified-clean and continue.

### 6. On confirm: file, record, commit, stop
1. **File the GitHub issue** against **`arsindelve/zorkai`** (issues for both games
   live there) using `mcp__github__issue_write` (method `create`) and the **issue
   template** below.
2. **Record in the ledger** with the issue number linked:
   ```bash
   python3 -m adventurebreaker.harness finding \
     --severity <info|low|medium|high|critical> --category <cat> \
     --title "..." --command "<repro>" --detail "..." --evidence "..." --issue <N>
   ```
   This writes the durable ledger (`coverage/FINDINGS.md`, `findings.jsonl`) and marks
   the coverage cell.
3. **Commit + push the ledger** (the signing server often 503s — bypass it):
   ```bash
   git add -A
   git -c commit.gpgsign=false commit --no-gpg-sign -m "Find <AB-id>: <title> (#<issue>)"
   # push with backoff (2s,4s,8s,16s) to the current dev branch:
   git push -u origin "$(git rev-parse --abbrev-ref HEAD)"
   ```
4. **Stop.** Report the bug + issue link + what was covered this run.

## GitHub issue template

Match the existing zorkai issues (they carry full root-cause analysis). Include:

- **## Bug** — symptom, with the exact failing command(s) and prod output (narrator
  state noted). Reproduced on prod.
- **## Root cause** — the `file:line` in `../ZorkAI` and a short code quote showing the
  defect.
- **## Original behavior (ZIL — ground truth)** — for behavioral bugs, quote the
  routine `file:line` from `planetfall-source/` or `zork1/` proving intended behavior.
- **## Impact** — who hits it, how visible, severity, whether it blocks progression.
- **## Suggested fix** — minimal, in the engine's existing patterns. (Do not paste/port
  ZIL verbatim; describe the fix.)
- **## TDD plan** — a deterministic failing-first test in the right test project
  (red-before/green-after), per zorkai's CLAUDE.md.
- Footer: "Found via the AdventureBreaker harness (black-box prod + white-box/ZIL
  confirmation)." and the `AB-xxx` id.

This skill **files the issue only** — it does NOT fix the bug or open a PR unless the
user explicitly asks.

## Reference

**Harness commands (player surface = black-box):**
`new --game <g> --target prod` · `state` · `play <cmd>` (narrator on) ·
`quiet <cmd>` (narrator off) · `spine-run --count N` · `save <name>` · `saves` ·
`restore <id|name>` · `frontier --game <g> --top N` · `report`.

**Ledger / coverage (run after a verdict):**
`finding --severity S --category C --title ... [--command ... --detail ... --evidence ... --area ... --issue N]`
· `cover --category C --result clean|hit|na [--area ... --command ...]`.

**Severities:** `info, low, medium, high, critical`.

**Coverage categories:** `movement, examine-scenery, container, take-drop-scope,
light-dark, parser-pronoun, get-post-parity, multi-sentence, save-restore, death,
npc-conversation, narrator-hallucination, narrator-spoiler-injection, character-break,
puzzle-step, other`.

**Repos:** GitHub issues → `arsindelve/zorkai`. Ledger commits → the AdventureBreaker
repo's current dev branch.

## Gotchas (learned the hard way)

- **Spine-desync trap:** running manual `play`/`quiet` commands *between* spine
  positions and *then* `spine-run` desyncs the walkthrough (it replays against a
  changed state → diverges, inventory/score reset). Always `spine-run` from a **fresh**
  session with **no** manual commands interleaved before it. To probe-then-continue,
  use `save`/`restore` instead of interleaving.
- **Survival clocks (Planetfall):** sleep/hunger will kill you mid-probe. `restore`
  resets the clock — checkpoint and restore rather than waiting. Stay black-box; do not
  reach for god-mode survival toggles during exploration.
- **NPCs wander (Floyd):** he periodically leaves and returns. For show/give/conversation
  tests, `wait` for "Floyd back!" or confirm he's present in `state` first.
- **god mode is white-box** (a debug cheat: `god mode take <item>` / `go <place>` /
  `where <item>`). Don't use it during black-box play. Note it can transiently reset
  live state (e.g. deactivate Floyd — a known issue), so re-check state after using it.
- **Don't over-claim:** if a test was confounded (desync, flake, missing item), say so
  and re-run clean rather than logging a dubious finding. Credible ledger > big ledger.
