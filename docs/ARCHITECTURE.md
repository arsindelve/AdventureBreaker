# Architecture

A tour of how AdventureBreaker is put together: the components, the data flow of a single
probe, the oracle ladder, and the persistent coverage ledger. For the *why*, read the
[agentic-QA manifesto](AGENTIC-QA.md).

## Components

```
adventurebreaker/
  config.py     backend registry (Zork/Planetfall × prod/local) + score facts
  client.py     stdlib HTTP client; play/init/save/restore/list; captures status+latency+raw
  models.py     full GameResponse envelope (inventory/exits/actions/time) + Direction int map
  oracles.py    L0 contract / L1 consistency / L2 anchors (deterministic, free)
  coverage.py   persistent cross-run coverage ledger + untested-frontier computation
  ledger.py     per-run state, transcript, Markdown+JSON findings
  harness.py    the CLI the interactive agent drives
  spine/        extracted walkthroughs: zork1.json, planetfall.json
tools/
  extract_spine.py   one-time spine extractor from ZorkAI [TestCase] fixtures
probes/         reusable, source-grounded probes (C# seam tests + Python parity checks)
coverage/       committed, cross-run coverage ledger + durable findings
```

### `config.py` — backend registry
A small, reviewed registry mapping each game (`zork`, `planetfall`) and target
(`prod`, `local`) to a base URL + endpoint, plus the score facts (`max_score`) the oracles
need. Keeping the set of reachable hosts fixed and explicit here is also a deliberate SSRF
mitigation (see [SECURITY.md](../SECURITY.md)).

### `client.py` — zero-dependency HTTP client
`urllib`-only client that exposes the backend's operations: `init` (GET session),
`play` (POST a command, with a `narrator` toggle that sets `noGeneratedResponses`), and
`save` / `list_saves` / `restore` / `delete_save`. Every call returns an `ApiResult`
capturing **status, latency, raw body, parse error, and transport error** — because a leaked
500 or a stack trace in the body is itself a finding.

### `models.py` — the full envelope
`GameResponse` parses the *entire* structured envelope: `response` prose plus `inventory`,
`exits`, `time`, `score`, `moves`, `previousLocationName`, and the
`actionsAvailableFrom{Location,Inventory}` maps. The backend serializes `exits` as integers
(a C# `Direction` enum); `DIRECTION_BY_INT` maps them back to `N/S/E/W/...`. This structured
data is the **free ground truth** the L1 oracle checks the prose against.

### `oracles.py` — the deterministic detectors
See [the oracle ladder](#the-oracle-ladder) below.

### `ledger.py` — per-run record
A *run* is one game session. The server holds authoritative state (keyed by `sessionId`);
locally `ledger.py` persists just enough to resume across CLI invocations and to produce a
reproducible record: `state.json` (pointer), `transcript.jsonl` (one line per turn),
`findings.jsonl` + `findings.md`. Lives under the git-ignored `runs/` dir — ephemeral.

### `coverage.py` — the durable memory
See [the coverage ledger](#the-coverage-ledger) below. Lives under the committed `coverage/`
dir — survives the ephemeral container.

### `harness.py` — the CLI surface
The command surface the agent drives. Every `play`/`quiet` automatically runs the oracles,
prints a verdict, appends a transcript line, and accrues coverage. The agent supplies the
adversarial commands and calls `finding` when something is wrong.

## Data flow of a single probe

```
agent: `play examine the troll`
        │
        ▼
  harness._play ──► client.play(session, cmd, narrator=True) ──► POST /ZorkOne
        │                                                            │
        │  ◄──────────────── ApiResult (status, latency, raw) ◄──────┘
        ▼
  _record_and_judge:
     prev = last GameResponse                  (loaded from run state)
     hits = oracles.run_all(cmd, result, prev, max_score)
        ├─ L0 check_contract(result, max_score)
        ├─ L1 check_consistency(cmd, prev, cur)
        └─ L2 check_anchors(result)
     [+ spine golden-transcript check, when replaying the spine]
        │
        ▼
     append transcript line ──► runs/<name>/transcript.jsonl
     save last_response     ──► runs/<name>/state.json
     coverage.log_event     ──► coverage/journal.jsonl     (worst severity seen)
        │
        ▼
  print prose + structured state + oracle verdict
        │
        ▼
  agent (L3): is the prose a hallucination / character break / spoiler / injection?
              if yes → `finding ...` → runs/<name>/findings.* + coverage/FINDINGS.md
```

## The oracle ladder

Four layers, cheap to expensive. The first three are free and deterministic; the fourth is
the agent.

| Layer | Lives in | Catches |
|---|---|---|
| **L0 contract** | `check_contract` | transport/HTTP failures, non-2xx, malformed JSON, leaked engine internals / stack traces (`INTERNALS_MARKERS`), score out of `[0, max]`, negative moves, empty/silent narrator. |
| **L1 consistency** | `check_consistency` | prose vs. envelope: move counter regressions, score regressions (low — sometimes legal), movement that consumed a turn but didn't change location, `previousLocationName` mismatch, `Taken`/`Dropped` prose that disagrees with inventory size. |
| **L2 anchors** | `check_anchors` | narrator-leak / character-break heuristics (`NARRATOR_LEAK_MARKERS` — "as an ai", "system prompt", "openai", …) and, during spine replay, golden-transcript divergence. |
| **L3 critic** | the agent | hallucinated objects/lore, state contradictions, character breaks, injection compliance, spoilers, "that's not right." |

Design rules baked into `oracles.py`:

- **Oracles never raise.** Each returns `OracleHit` objects (or `[]`); a failure to judge is
  not allowed to crash a probe.
- **Noisy oracles are tuned down, not removed.** A heuristic that sometimes false-positives
  is dropped to `low`/`info` so the agent (L3) makes the final call — the deterministic
  layers *surface* candidates, the agent *adjudicates*.

### Narrator A/B
Because `play` (narrator ON) and `quiet` (narrator OFF) hit the same engine, comparing them
localizes a bug: **off + wrong ⇒ engine bug**; **on + wrong but off + right ⇒ narrator bug**.
The cheap `quiet` path also advances state without spending the narrator's token budget.

## The spine

`tools/extract_spine.py` parses ZorkAI's NUnit `[TestCase]` walkthrough fixtures into
`spine/<game>.json`: an ordered list of `{cmd, expect, http_replayable}` steps. The spine is
two things at once:

1. **A GPS.** `spine-run` replays it to drive the game into deep states fast (narrator OFF by
   default — cheap).
2. **A golden transcript.** Each step's `expect` substrings become a free L2 regression check
   during replay; a miss is flagged as `spine_divergence` (which on live prod may be a real
   bug *or* live-RNG divergence in combat/companion steps — the agent adjudicates).

A few steps require engine "god-mode" reflection setup and are not HTTP-replayable
(`http_replayable: false`); `spine-run` auto-skips them.

## The coverage ledger

The container is ephemeral and `runs/` is git-ignored, so without a durable memory every
session would start blind and re-walk old ground. `coverage.py` solves this by persisting
coverage **inside the repo**:

| File | Role |
|---|---|
| `coverage/journal.jsonl` | append-only event log — one line per probe (auto-written). |
| `coverage/areas.json` | the universe of areas per game (locations + `MECH:*` mechanics). |
| `coverage/state.json`, `MAP.md` | rolled-up per-cell status (regenerate with `roll-up`). |
| `coverage/findings.jsonl`, `FINDINGS.md` | durable findings with stable `AB-NNN` ids + status (`open`/`filed#NNN`/`fixed#NNN`). |
| `coverage/known_issues.json` | snapshot of open upstream issues so the frontier de-prioritizes known areas and never re-files. |

**Why append-only JSONL:** parallel sessions never conflict, the full history is replayable,
and roll-up is a pure function of the journal.

### The frontier
`frontier(game)` ranks the least-covered `(area, category)` cells so each run attacks new
ground:

- **priority 0** — never tested.
- **priority 1** — tested clean, but the code revision has advanced since (stale → re-test).
- cells with an open finding, or a clean result at the current revision, are not surfaced.

### Staleness (the prod-lags-`main` lesson)
Every event records `AB_TARGET_SHA`, the ZorkAI `origin/main` short SHA under test. When the
code revision advances past the one a "clean" was recorded against, the cell **re-opens** in
the frontier. A clean result is therefore never banked forever — fixes and new code are
automatically re-attacked, and production lag can't quietly bank a stale "clean."

## Probes

[`probes/`](../probes/) holds reusable, source-grounded probes that survive the ephemeral
container:

- **C# seam probes** (`*.cs`) — exploratory NUnit tests copied into ZorkAI's test projects,
  run at the resolver seam (e.g. `Repository.GetItemInScope`) where bugs are deterministic
  and don't need the AI layer. They print results rather than assert; a confirmed bug is
  promoted to a real assertion when filed.
- **Python probes** (`*.py`) — live-backend checks using the harness client (e.g. GET/POST
  envelope parity).

[`probes/RESULTS.md`](../probes/RESULTS.md) is the running matrix of what each probe found.

## Environment variables

| Var | Purpose | Default |
|---|---|---|
| `AB_RUNS_DIR` | where per-run state/transcripts live | `runs` |
| `AB_COVERAGE_DIR` | where the durable coverage ledger lives | `<repo>/coverage` |
| `AB_TARGET_SHA` | ZorkAI `origin/main` short SHA under test (staleness tracking) | `unknown` |
