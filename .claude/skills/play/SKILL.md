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

## Prerequisites (check these first)

- **Working dir:** the AdventureBreaker repo (this skill's home). Run `git rev-parse`
  to confirm, and read `HANDOFF.md` at the repo root for cold-start context if you're
  new to the project.
- **Sibling ZorkAI checkout at `../ZorkAI`** — required for the white-box confirm step.
  It must contain the engine C# **and** the gitignored ZIL ground-truth sub-checkouts
  `zork1/` and `planetfall-source/` (the originals this engine recreates). The ZIL is
  the authority for "is this a real bug or faithful original behavior." **If `../ZorkAI`
  or its ZIL aren't present, you can still black-box play, but you cannot confirm
  behavioral bugs against ground truth — say so and lower your confidence rather than
  asserting "divergence."** Verify: `ls ../ZorkAI/planetfall-source/*.zil ../ZorkAI/zork1/*.zil`.
- **GitHub MCP scope:** both `arsindelve/AdventureBreaker` (ledger commits) and
  `arsindelve/zorkai` (issues + engine source). If zorkai isn't in scope you can't file
  the issue or read the source — tell the user.
- **Network:** outbound HTTPS to prod (the harness plays the live production backend).
- **Engine source for `file:line` citations and ZIL is read-only** — never copy/port ZIL
  into code; see ZorkAI's `CLAUDE.md`.

**Read first:** `.claude/skills/_reference.md` — the `quiet` "no effect" sentinel and
signal-reading, the bug-class taxonomy (where bugs hide), the severity rubric, ZIL
dispatch (faithful-vs-bug), game landmarks, and save/restore gotchas. It's the
how-to-read-and-judge companion to this procedure.

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
- **Find the spine step for your area.** The walkthrough lives at
  `adventurebreaker/spine/<game>.json` as an ordered list of steps, each
  `{"cmd": ..., "expect": [...]}`. **`spine-run --count N` runs N *additional* steps
  from wherever `spine_pos` currently is — it is NOT "replay to absolute position N."**
  (`spine_run`'s loop is `while n < args.count and run.spine_pos < len(steps)`, starting
  from the run's *existing* `spine_pos`.) The two coincide only when you call it once on
  a session still at `spine_pos == 0` — that's why `--count <index>` "just works" for a
  first call on a fresh run. **Never call `spine-run` a second time on the same run
  expecting the same count to land at the same target** — the second call adds `N` more
  steps on top of wherever the first one left off, which silently overshoots deep into
  the spine (a real overshoot this session ran ~80 steps past intent, straight through a
  long `wait` stretch, and looked exactly like a catastrophic session reset — same
  symptom as AB-047 — until re-derivation from a single clean call from `spine_pos == 0`
  proved it was pure step-count arithmetic, not an engine bug). If you need to resume
  from a non-zero position, first read the *actual* current `spine_pos` (`state`, or the
  run's `state.json`) and compute `count = target − current`. To reach a room/puzzle,
  grep the spine for a landmark command or an expected room name to get its index, then
  (on a **fresh** run) `spine-run --count <index>` (stop *just before* the step that
  performs the thing you want to probe, so you can probe it yourself). Example:
  ```bash
  python3 -c "
  import json; s=json.load(open('adventurebreaker/spine/planetfall.json'))
  steps=s['steps'] if isinstance(s,dict) else s
  for i,st in enumerate(steps):
      cmd=(st.get('cmd') or '').lower()
      if 'magnet' in cmd or 'rift' in cmd or 'ladder' in cmd: print(i, st.get('cmd'), st.get('expect'))
  "
  ```
  (Landmarks shift if the spine is re-extracted, so derive the index live — don't
  hard-code step numbers.)
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

**Worked example — do the reckless thing on purpose, ride it to its natural end, then
A/B-isolate the variable.** (AB-056, `zorkai#373` — fixed in release 2.0.0; this is kept
as a methodology example, not an open bug to re-verify.) The canonical Planetfall shuttle
walkthrough decelerates *immediately*: `push lever` then `pull lever` right back to
center. A real, careless player wouldn't — so instead of following that timing, the
probe deliberately did the wrong thing on purpose and then **kept going instead of
stopping at the first sign of trouble**: pushed the lever once and *never touched it
again*, sailing straight through the "Limit 45" sign and the "Begin Deceleration"
warning, letting speed climb to 120 (six times the death threshold) all the way to the
tunnel's end. That's the first move: **don't just poke a mechanism once and check the
immediate response — commit to the wrong path and let the state machine run to
completion**, because the interesting behavior is usually at the boundary/completion,
not mid-stream.

The arrival message ("approaching a brightly lit area...") looked like a safe landing —
until the *next* unrelated command (`score`) retroactively revealed a death message.
That's confusing on its own (is this flaky? a delayed effect? infra noise?) — the
temptation is to shrug and move on. Instead, the second move: **hold everything else
identical and vary only the one thing you're suspicious of.** Redo the *exact* same
reckless setup, but this time make the very next command `W` (leave) instead of `score`.
Result: clean escape, zero consequence. Same recklessness, same final speed, two
different outcomes — the only variable was which command came next. That A/B pair (not
a single observation) is what turned "huh, weird" into a deterministic, provable bug:
leaving the control cabin the instant the door opens deregisters the location as a
turn-based actor *before* the engine's already-scheduled (but not-yet-run) speed penalty
gets a chance to fire — a race between movement processing and actor processing,
confirmed by then reading `ShuttleControl.cs`'s `Act()`/`Move()`/`OnLeaveLocation()`.
Neither half of this would have surfaced from playing the walkthrough as written, or
from stopping at the first "that's odd." Generalizes to: **when a symptom looks
intermittent, don't write it off as noise — reproduce the identical setup twice and
change exactly one thing to find out whether "intermittent" is actually "deterministic
on a variable you haven't isolated yet."**

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
3. **Commit + land the ledger on `main` immediately** — ledger/finding progress
   (`coverage/findings.jsonl`, `journal.jsonl`, `FINDINGS.md`, `state.json`, `MAP.md`) is
   append-only telemetry, not reviewed code, so it should reach `main` right away rather
   than sit in a review-pending PR. **`main` has branch protection — a raw `git push`
   straight to it is rejected with HTTP 403** (confirmed; there is no way to bypass
   this), so the mechanism is: commit on a short-lived branch, then immediately create
   **and merge** the PR via the GitHub API in the same turn — don't wait for review or
   CI. The signing server often 503s — bypass it. `main` moves often (concurrent
   sessions), so fetch + rebase onto `origin/main` before branching off, to keep the
   diff minimal:
   ```bash
   git add -A
   git -c commit.gpgsign=false commit --no-gpg-sign -m "Find <AB-id>: <title> (#<issue>)"
   git fetch origin main && git rebase origin/main   # minimize the diff / conflict surface
   git checkout -b ledger-<ab-id>-<short-slug>
   git push -u origin ledger-<ab-id>-<short-slug>
   ```
   Then `mcp__github__create_pull_request` (base `main`) immediately followed by
   `mcp__github__merge_pull_request` on the same PR — no gap, no waiting for checks.
   If the rebase (or the PR merge) conflicts, it's almost always the append-only ledger
   files — resolve per `_reference.md` §7 (union the `*.jsonl` lines, regenerate
   `FINDINGS.md`/`state.json`/`MAP.md`, renumbering any colliding `AB-NNN` ids), then
   continue.
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
repo's `main` branch, landed immediately via a short-lived branch + self-merged PR
(`main` is branch-protected — no raw `git push` to it).

## Gotchas (learned the hard way)

- **Spine-desync trap:** running manual `play`/`quiet` commands *between* spine
  positions and *then* `spine-run` desyncs the walkthrough (it replays against a
  changed state → diverges, inventory/score reset). Always `spine-run` from a **fresh**
  session with **no** manual commands interleaved before it. To probe-then-continue,
  use `save`/`restore` instead of interleaving.
- **Survival clocks (Planetfall):** sleep/hunger will kill you mid-probe. For long deep
  positioning runs, use the controlled setup command `quiet "god mode no survival"`
  immediately after a fresh `new` to disable both clocks, then `spine-run`. Record the
  run as controlled setup, and do not use the toggle if the bug being judged involves
  survival-clock behavior. Re-enable with `quiet "god mode survival"`; fine-grained
  forms are `god mode no sleep`, `god mode sleep`, `god mode no hunger`, and
  `god mode hunger`. The toggle consumes one Planetfall turn. If you run it at Deck Nine
  before replaying the spine, set `runs/<run>/state.json` `spine_pos` to `1` and reduce
  the replay count by one; otherwise the opening explosion fires one command earlier
  than the spine expects and the route can desync before the escape pod.
- **Chronometer gate (Planetfall Alfie):** the walkthrough uses a test-only `ResetTime`
  setup before `slide shuttle access card through slot`. In prod, use
  `quiet "god mode reset time"` at that point. It should respond
  `God mode: chronometer reset to 2000.`; verify with `score` if needed, then run the
  exact shuttle-card command and advance `spine_pos` past that manual step before
  continuing. This is separate from `god mode no survival`.
- **NPCs wander (Floyd):** he periodically leaves and returns. For show/give/conversation
  tests, `wait` for "Floyd back!" or confirm he's present in `state` first.
- **Zork I combat gates (troll/thief/cyclops): use `god mode kill <creature>`, don't
  grind the RNG.** `quiet "god mode kill troll"` (also `thief`, `cyclops`) puts the
  creature straight into its dead/removed state — same end state a lucky real kill
  leaves it in (axe dropped, thief's stash placed at your location, cyclops gate opened)
  — without playing out combat. Use this immediately after any spine step that would
  normally require winning a fight (e.g. right after entering `The Troll Room`, before
  attempting the maze), rather than repeatedly `play "kill troll with sword"` and hoping.
  This exists *because* an earlier playtesting session found the troll fight genuinely
  brutal for automated positioning (several deaths in a row just trying to reach the
  Maze) — filed as a tooling request and shipped in release 2.0.0. If you still choose to
  fight it out (e.g. because the run is specifically about combat/death behavior), know
  that the creature registers as a turn-based actor **on room entry**, so it gets an
  unprovoked attack the same turn you walk in, before your first real action.
- **god mode is white-box** (a debug cheat: `god mode take <item>` / `go <place>` /
  `where <item>` / `kill <creature>` / `no survival`; see `_reference.md` §7 for the full
  verb syntax). Use it only for controlled setup, never as evidence for a player-visible
  bug. `take`/`go` rebuild the Repository **without `Init()`**, which **deactivates
  Floyd** and empties containers — so after any god-mode grab, re-check `state` and
  `activate floyd` before show/give/conversation tests.
- **If chasing a fix or tooling workaround balloons, stop.** Write the remaining detail
  into the GitHub issue and abandon the side-quest (delete any scratch artifact). The job
  is *find bugs, not fix them* — rabbit-holing into a fix violates it.
- **One issue per root cause.** When several player-visible symptoms trace to a single
  defect, file **one** issue covering all of them, not several (e.g. "ladder lost forever"
  and "re-placing a ladder duplicates it" were one bug — both from a handler matching on
  command text without checking ladder state). And don't run `finding` twice for the same
  bug — it harmlessly dups the ledger entry.
- **GitHub-MCP outage** surfaces as `Streamable HTTP error: ... reset reason: overflow`
  (the "503/overflow"). There's **no client-side workaround** — it recovers server-side.
  When `issue_write` fails this way, record the finding in the durable ledger with the
  **full drafted issue body** and file it once MCP recovers — don't block the run; keep
  doing MCP-free work (prod play, drafting, committing the ledger).
- **Don't over-claim:** if a test was confounded (desync, flake, missing item), say so
  and re-run clean rather than logging a dubious finding. Credible ledger > big ledger.
