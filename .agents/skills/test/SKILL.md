---
name: test
description: >-
  Verify a shipped ZorkAI release in production. Given a release number (a tag like
  1.6.5), look up its GitHub release notes (the merged PRs / fixes), confirm the
  release actually deployed to prod, then smoke-test each shipped fix and feature LIVE
  through the AdventureBreaker harness — driving the walkthrough spine to each affected
  area and asserting the fixed behavior a real player would see. Reports a pass/fail
  matrix; if a shipped fix regressed (doesn't work in prod), surfaces it and, on user
  confirm, files a GitHub issue and records it. Invoke as `/test 1.6.5` (or `/test` for
  the latest release).
---

# test {release} — verify a shipped release in production

You verify that a ZorkAI **release** is actually live and that the fixes/features it
shipped work in **production**. Same harness, navigation, and discipline as `/play`,
but the goal is **confirmation**, not discovery: walk the release's changelog and prove
each item in prod. The valuable output is a clean pass/fail matrix — and a **loud flag**
if any shipped fix did not actually take in prod (a regression that escaped).

`$ARGUMENTS` = the release number / tag, e.g. `1.6.5`. If omitted, use the **latest**
release.

## Prerequisites (check these first)

- **Working dir:** the AdventureBreaker repo; read `HANDOFF.md` at the repo root for
  cold-start context if you're new to the project.
- **Sibling ZorkAI checkout at `../ZorkAI`** (engine C# + the gitignored ZIL
  sub-checkouts `zork1/`/`planetfall-source/`) — needed to learn each fix's exact
  expected behavior and `file:line`. If absent, you can still verify against the PR/issue
  text, but note you couldn't cross-check the code.
- **GitHub MCP scope:** `arsindelve/zorkai` (release notes, PRs, issues, Actions/deploy,
  engine source) **and** `arsindelve/AdventureBreaker` (ledger commits). If zorkai isn't
  in scope you can't read the release or file a regression — tell the user.
- **Network:** outbound HTTPS to prod (the harness plays the live production backend).

**Read first:** `.Codex/skills/_reference.md` — the `quiet` "no effect" sentinel and
signal-reading, the severity rubric (for grading any regression), ZIL dispatch, game
landmarks, and save/restore gotchas.

## White-box vs black-box (different from `/play`)

Here white-box is allowed *up front* — you must read the release's PRs, linked issues,
engine C# (`../ZorkAI`), and ZIL to learn the **expected** behavior (the exact strings,
states, and repros). But the actual prod **verification is black-box**: drive the game
through the harness like a player and observe what a player would see. So:
**white-box to learn what *should* happen; black-box to confirm it *does*.**

## Golden rules

1. **Confirm it's deployed before testing.** The harness hits live prod; if the release
   didn't deploy, you'd be testing old code. Verify the deploy succeeded first.
2. **Test the player-visible behavior, not the diff.** Reproduce each fix's symptom in
   prod and assert the corrected behavior (prefer a clean before→after: the PR/issue
   states the old broken behavior and the new expected one).
3. **Don't fake setup.** If an item needs a deep or special state you can't cleanly
   reach, say so and mark it **not smoke-tested** rather than asserting a pass. Credible
   matrix > green-washing.
4. **A regression is a real bug.** If a shipped fix doesn't work in prod, treat it like a
   `/play` find: confirm with the user, file a GH issue against `arsindelve/zorkai`,
   record it.
5. **Report honestly** — pass / fail / not-testable, with the actual prod output as
   evidence.

## Procedure

### 1. Identify the release and its contents
- Fetch the release notes:
  - specific: `mcp__github__get_release_by_tag` owner `arsindelve` repo `zorkai` tag `<num>`
  - latest: `mcp__github__get_latest_release` owner `arsindelve` repo `zorkai`
- Parse the **"What's Changed"** body into its PR list (numbers + titles).
- For each PR, learn the **expected behavior**: read the PR body (it usually has root
  cause + repro + expected output) via `mcp__github__pull_request_read`, the linked
  issue, and — for exact strings/states — the engine code/ZIL in `../ZorkAI`.
- **Classify** each item:
  - **Gameplay-testable** (a bug fix or feature with a reproducible in-game effect) →
    goes in the test plan.
  - **Not smoke-testable via play** (CI/infra bumps, model swaps, refactors, "never
    return 500" safety nets, save-state internals) → list as **➖ not gameplay-testable**
    with a one-line reason. Don't fake these.

### 2. Confirm the release deployed to prod
- The tag is the deployed version (`deploy.yml` runs `on: release: published`).
- List deploy runs and find this release's:
  `mcp__github__actions_list` method `list_workflow_runs`, owner `arsindelve`, repo
  `zorkai`, resource_id `deploy.yml`, `workflow_runs_filter: { event: release }`.
  - Match the run whose `display_title` == `<tag>`, requiring `status: completed`,
    `conclusion: success`, `event: release`.
  - The `actions_list` payload routinely exceeds the tool's token limit (~350K chars on a
    single line) and spills to a `tool-results/*.txt`. Parse it with python:
    ```bash
    python3 -c "import json,sys; d=json.load(open(sys.argv[1])); \
      [print(r['id'],r['event'],r['status'],r['conclusion'],r['display_title'],r['created_at']) \
       for r in d['workflow_runs']]" <file>
    ```
    Worked example (1.6.5): `28074443243 | release | completed | success | 1.6.5` → prod
    serving 1.6.5.
- If the deploy is still running, **wait** and re-check (don't poll with `sleep`; it
  finishes in a few minutes). If it failed, **stop** and report — prod is not serving
  this release.
- **Versioning + publishing facts:** releases follow a sequential **patch cadence**
  (1.6.1 → 1.6.2 → … ); the next tag is latest-patch + 1, target `main` (find latest via
  `mcp__github__get_latest_release` owner `arsindelve` repo `zorkai`). Release notes are
  the GitHub auto-generated "## What's Changed" PR list. There is **no
  create-release/create-tag/publish MCP tool** (only read: `list_releases`,
  `get_release_by_tag`, `get_tag`) — publishing a release tag on `main` triggers the prod
  deploy, so it's deliberately a **human action**. An agent only drafts a paste-ready body.

### 3. Build the test plan
For each gameplay-testable item, write down: **game** (zork/planetfall), **where**
(room/puzzle → spine target), **repro command(s)**, **expected** result (exact string or
state), and **narrator on/off** (static engine strings → either; narrator behavior →
`play` on; engine-truth → `quiet`).

### 4. Smoke-test each item in prod (black-box)
Use the same harness flow and discipline as `/play`:
```bash
cd /home/user/AdventureBreaker
export AB_RUNS_DIR=runs
export AB_TARGET_SHA=$(cd ../ZorkAI && git rev-parse --short origin/main 2>/dev/null || echo unknown)
python3 -m adventurebreaker.harness new --game <game> --target prod
python3 -m adventurebreaker.harness spine-run --count <N>     # drive to the area
python3 -m adventurebreaker.harness state                     # confirm position/inventory
python3 -m adventurebreaker.harness play  "<repro>"           # narrator on  (player view)
python3 -m adventurebreaker.harness quiet "<repro>"           # narrator off (engine truth)
```
- **Planetfall long-run setup:** after a fresh `new` and before a deep `spine-run`, you
  may disable the sleep+hunger clocks with
  `quiet "god mode no survival"` for deterministic positioning. This is a white-box
  setup affordance from ZorkAI Issue #277, not player-visible behavior. Record that the
  run used controlled setup, then verify the actual release behavior with ordinary
  `play`/`quiet` commands. Re-enable with `quiet "god mode survival"` when testing
  survival-clock behavior itself. Fine-grained toggles also exist:
  `god mode no sleep`, `god mode sleep`, `god mode no hunger`, `god mode hunger`.
  **Important:** the god-mode toggle is still a game command and consumes one
  Planetfall turn. If you run it at Deck Nine before replaying the spine, compensate by
  treating it as the first padding `wait`: set the run's `spine_pos` to `1` and run
  `spine-run --count (N-1)`. Otherwise the opening explosion timing shifts by one turn
  and the route can desync before the escape pod. Example:
  ```bash
  python3 -c "import json,pathlib; p=pathlib.Path('runs/<run>/state.json'); d=json.loads(p.read_text()); d['spine_pos']=1; p.write_text(json.dumps(d, indent=2))"
  python3 -m adventurebreaker.harness --run <run> spine-run --count $((N-1))
  ```
- **Planetfall `ResetTime` setup:** the walkthrough spine includes a test-only
  `ResetTime` setup before `slide shuttle access card through slot` in Alfie Control
  East. In prod, use the deployed equivalent:
  `quiet "god mode reset time"`. It should respond
  `God mode: chronometer reset to 2000.` and `score` should show Current Galactic
  Standard Time `2000` before you swipe the shuttle card. Then run the exact spine
  command `quiet "slide shuttle access card through slot"` and set `spine_pos` to the
  next step before resuming. This is controlled setup for the chronometer gate, separate
  from `god mode no survival`.
- **Finding `<N>` (the step count for an area):** the walkthrough is
  `adventurebreaker/spine/<game>.json` — an ordered list of `{"cmd", "expect"}` steps;
  `spine-run --count N` replays steps `0..N-1`. Grep the spine for a landmark command or
  expected room name to get its index, then stop just before the step you want to probe.
  Quick lister:
  ```bash
  python3 -c "
  import json; s=json.load(open('adventurebreaker/spine/<game>.json'))
  steps=s['steps'] if isinstance(s,dict) else s
  for i,st in enumerate(steps):
      if '<keyword>' in (st.get('cmd') or '').lower(): print(i, st.get('cmd'), st.get('expect'))
  "
  ```
  Derive the index live (don't hard-code — it shifts if the spine is re-extracted).
- Assert the fixed behavior vs the PR's expected output. Where a clean before/after is
  known, show both sides in the report.
- Use `save`/`restore` to fork (restore also resets Planetfall's survival clock).
- **Respect the spine-desync trap:** no manual commands before a `spine-run`; start
  fresh and re-drive for each item that needs a different position (see Gotchas).

### 5. Report the matrix
Produce a table: each release item → ✅ verified / ❌ regressed / ➖ not gameplay-testable,
with the prod evidence (the actual response) for the ✅/❌ rows. State plainly which
items were and weren't exercised. Lead with any ❌.

### 6. If a fix regressed → confirm, file, record
1. Root-cause it white-box (`file:line` + ZIL if behavioral).
2. `AskUserQuestion` to confirm filing (options: **File it** · **Not a regression** ·
   **Keep checking**).
3. On confirm: file the GH issue against `arsindelve/zorkai` (use the issue template in
   the `/play` skill — root cause, ZIL evidence, impact, suggested fix, TDD plan; note
   it **regressed in `<release>`** and reference the PR that was supposed to fix it),
   then record:
   ```bash
   python3 -m adventurebreaker.harness finding --severity <S> --category <C> \
     --title "Regression in <release>: ..." --command "<repro>" --detail "..." \
     --evidence "..." --issue <N>
   ```

### 7. Commit + push the ledger / journal
The harness writes journal/ledger entries during the run; commit and push them so the
verification is recorded (signing server often 503s — bypass it):
```bash
git add -A
git -c commit.gpgsign=false commit --no-gpg-sign -m "Smoke-test <release> in prod: <summary>"
git push -u origin "$(git rev-parse --abbrev-ref HEAD)"     # retry w/ backoff 2,4,8,16s
```

## Reference (shared with `/play`)

**Harness (player surface):** `new --game <g> --target prod` · `state` · `play <cmd>` ·
`quiet <cmd>` · `spine-run --count N` · `save <name>` · `saves` · `restore <id|name>` ·
`finding ...` · `cover ...` · `report`.

**GitHub MCP:** `get_release_by_tag` / `get_latest_release`, `pull_request_read`,
`issue_read`, `actions_list` (deploy verification), `issue_write` (regression filing).

**Repos:** issues → `arsindelve/zorkai`; ledger commits → AdventureBreaker current branch.

## Gotchas (same hazards as `/play`)

- **Spine-desync trap:** manual commands before `spine-run` desync the walkthrough
  (state diverges, inventory/score reset). Start **fresh** per position; use
  `save`/`restore` to fork instead of interleaving.
- **Survival clocks (Planetfall):** sleep/hunger can kill you mid-test. For long deep
  positioning runs, use the controlled setup command `quiet "god mode no survival"`
  immediately after a fresh `new` to disable both clocks, then `spine-run`; document this
  in the report. `quiet "god mode survival"` re-enables them. Do not use the toggle when
  the item under test is survival-clock behavior. The toggle consumes a turn, so for the
  opening Deck Nine spine set `spine_pos=1` before replaying; otherwise timed events are
  one turn ahead of the spine expectations.
- **Chronometer gate (Planetfall Alfie):** sleep/hunger toggles do not affect
  `CurrentTime`. If the spine reaches Alfie after `CurrentTime > 6000`, the shuttle card
  is rejected with the evening-hours authorization message. Use
  `quiet "god mode reset time"` at the walkthrough's `ResetTime` point; prod should set
  the chronometer to `2000`, after which the shuttle card activates Alfie.
- **NPCs wander (Floyd):** `wait` for "Floyd back!" / confirm presence in `state` before
  show/give/conversation checks.
- **god mode is white-box** and can transiently reset live state (e.g. deactivate Floyd)
  — avoid during black-box verification; re-check `state` if used for setup.
- **Big Actions output:** `actions_list` can blow the token limit and spill to a file —
  parse with `python3`/`jq`, slicing by char range.
- **Don't over-claim:** confounded runs (desync, flake, missing item) → re-run clean or
  mark not-tested; never log a pass/fail you didn't actually observe.
