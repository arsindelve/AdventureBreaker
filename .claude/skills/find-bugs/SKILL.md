---
name: find-bugs
description: >-
  Bulk, looping bug-hunt for the ZorkAI engine: given a COUNT and a GAME (zork or
  planetfall), adversarially playtest in production and keep going until COUNT distinct,
  real bugs are found, confirmed, filed as GitHub issues against arsindelve/zorkai, and
  recorded in the coverage ledger — one bug per area, moving to a new part of the game after
  each. This is the `/play` skill run in a loop with the per-bug user-confirmation waived,
  so use it whenever the user gives a NUMBER of bugs to hunt: "find ten bugs in planetfall",
  "find and log 5 bugs", "loop zork until you find 8 bugs", "do a bulk bug hunt", or
  `/find-bugs 10 planetfall`. For a single bug WITH per-bug confirmation, use `/play` instead.
---

# find-bugs `<count>` `<game>` — looping adversarial bug-hunt

Find, confirm, file, and record **`<count>` distinct real bugs**, each in a **different part
of the game**, then land the ledger and report a matrix. This is the **`/play` skill run in
a loop** — don't reinvent the hunt. `/play` owns *how to find and confirm one bug*; this
skill owns *the loop, the bulk overrides, and the final report*.

`$ARGUMENTS` = `<count> <game> [focus areas]`
- `<count>` — how many bugs to find (a positive integer).
- `<game>` — `zork` or `planetfall`.
- `[focus areas]` — optional hint list of rooms/puzzles to prioritize. If omitted, choose
  areas coverage-first from `frontier`.

## First: read the /play skill — it is the per-bug engine

Read `.claude/skills/play/SKILL.md` and `.claude/skills/_reference.md` in full before
starting. Everything about finding *one* bug lives there and is not repeated here:
black-box-first probing (`play` narrator-on / `quiet` narrator-off) and the A/B
narrator-isolation; going white-box to `../ZorkAI` **only after** something looks wrong;
ZIL ground-truth checks (faithful original cruelty is not a bug); the harness commands; the
GitHub issue template; the ledger `finding`/`cover` commands and how `main` is landed; and —
highest-value for a bulk run — `/play`'s **engine anti-pattern probe list** and its
verification **Gotchas** (god-mode needs a *unique* item token; what god-mode keeps vs
destroys; verify parser/label bugs on **prod** because the AI parser both hides and invents
gaps; assert timed objects inside their window; fragility triage). Assume all of that.

## What a bulk run changes (overrides to /play)

1. **No per-bug user confirmation.** `/play`'s rule "the user confirms before you file" is
   **waived** — invoking `find-bugs` with a number *is* the batch authorization, so stopping
   to ask before each file would only be noise. The safeguard doesn't disappear, it
   **shifts**: with no human gating each one, your bar for "real" has to be *higher*, not
   lower (see **Honesty**). File as you go.
2. **Loop instead of stop.** `/play` finds one bug and stops. Here you repeat until
   `<count>` are filed, moving to a **new area each time** — "each time you find and log a
   bug, move to another part of the game." Breadth is the point.
3. **Batch the ledger.** One GitHub issue per bug (as you go), but **don't open a PR per
   bug** — record each finding immediately (`finding … --issue N`), commit the ledger every
   few bugs, and land it in **one** PR at the end. Ledger telemetry is append-only, so
   batching it keeps the noise down without losing anything.

## The loop

Do `/play`'s Setup once, then repeat until `<count>` bugs are filed:

1. **Pick a NEW area.** Never probe an area you've already filed a bug in this run. Use the
   focus list if given; otherwise `frontier --game <game> --top 25` and take a least-covered
   row. Keep a running list of areas used so you don't double-dip.
2. **Find one bug there — the `/play` way.** Black-box probe; when something looks wrong, go
   white-box to confirm a genuine *unintended* divergence with a `file:line` root cause,
   reproduced on **prod**. *(Optional accelerator when subagents are available: fan out
   white-box readers over the area's handler files to surface candidates against the
   anti-pattern list. But a candidate is a **hypothesis**, not a finding, until you
   reproduce it live with `quiet`/`play` — static analysis over-fires in both directions, so
   prod-verify every one.)*
3. **Dedup before filing.** Search `arsindelve/zorkai` issues — **including closed ones** —
   for the symptom and the root cause. If a sibling exists (e.g. a prior fix that missed
   this handler), cite it rather than duplicating; if it's the same bug, skip it and pick a
   different area.
4. **File + record, then move on.** File the issue against `arsindelve/zorkai` using
   `/play`'s template; `finding … --area "<area>" --issue <N>` to record the `AB-NNN`. No
   confirmation, no PR yet. Return to step 1 in a **different** area.

## When `<count>` are filed: land + report

1. **Land the ledger** on AdventureBreaker's `main` using `/play`'s "commit + land the
   ledger" mechanics (short-lived branch → PR → self-merge, since `main` is branch-protected;
   union `*.jsonl` lines on conflict). After it merges, `git reset --hard origin/main` so the
   next batch starts clean.
2. **Report a matrix** — one row per bug: `# | area | bug (one line) | severity | issue |
   AB-id`. Then a short line on **false positives rejected** (candidates that looked like
   bugs but were faithful-to-ZIL, parser-compensated, or unreachable). Showing the rejects
   is what makes the matrix credible.

## Honesty (the whole point)

Filing without a human gating each one means credibility rests entirely on the bar you hold
yourself to:

- **Only real, prod-confirmed, root-caused bugs.** Reproduced *live* (not "the code looks
  wrong"), with a `file:line` cause, and — for behavioral questions — checked against ZIL
  ground truth. A confounded probe (god-mode contamination, spine desync, flake, wrong item
  grabbed) is **re-run clean or dropped**, never filed on suspicion.
- **A shortfall is a valid, honest result.** If `<count>` genuine bugs aren't reachable (the
  engine is well-maintained, or the reachable surface is exhausted), **stop and report what
  you found** — "8 of 10, and here's why the well ran dry" — rather than padding with
  non-bugs, duplicates, or trivia. A credible smaller matrix beats a padded one.
- **Don't split one bug into two to hit the number.** Two real bugs sharing an item is fine
  to note, but reframing one defect as several to reach `<count>` is padding.

## Stop conditions

Stop, then land + report, when **any** of these is true:
- `<count>` distinct real bugs are filed; or
- the reachable, un-probed surface is exhausted (report the shortfall and why); or
- the user interrupts.
