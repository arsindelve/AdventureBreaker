# Contributing to AdventureBreaker

Thanks for helping break things responsibly. This guide covers the dev setup, the day-to-day
workflow, how to add new probes/oracles, and the rules every pull request must meet.

## TL;DR

- The harness is **pure Python 3.11+ stdlib** — no install required.
- Run the smoke tests with `python3 -m unittest discover -s tests`.
- Every probe accrues **coverage** automatically; commit `coverage/` so it persists.
- **Verify findings against engine source before logging** — keep the ledger credible.
- Every PR must pass the **agentic security review** (see [SECURITY.md](SECURITY.md)).

## Development setup

```bash
git clone https://github.com/arsindelve/AdventureBreaker.git
cd AdventureBreaker
python3 --version            # need 3.11+
python3 -m unittest discover -s tests -v
```

That's it — there is nothing to `pip install` for the core harness. Optional extras
(`rich` for a TUI, `anthropic` for an embedded critic) are declared in `pyproject.toml`
and are not required for normal use.

## How the harness works (the mechanic)

AdventureBreaker drives the game with a **spine** (the walkthrough, for progress) and
**ribs** (adversarial probes at each state, for attack), forking with save/restore so probes
never derail progress. Read the [architecture deep-dive](docs/ARCHITECTURE.md) and the
[agentic-QA manifesto](docs/AGENTIC-QA.md) before adding to the core.

## CLI reference

```
new --game {zork|planetfall} [--target prod|local] [--name NAME]   # start a run
use NAME                       # switch the current run
state                          # GET session state (structured)
play <cmd...>                  # probe, narrator ON, auto-oracles
quiet <cmd...>                 # engine only, narrator OFF (cheap; advances state)
spine [--from N] [--count M]   # show upcoming spine steps
spine-run [--count N] [--narrator] [--verbose]   # auto-advance via walkthrough
save NAME / saves / restore ID # checkpoint / list / fork back
finding --severity {info,low,medium,high,critical} --category C --title T \
        [--detail D] [--evidence E] [--command CMD] [--repro R] [--area A] [--issue N]
report                         # per-run finding summary

# persistent, cross-run coverage ledger
frontier [--game G] [--top N]  # the untested avenues to attack next
cover --category C [--area A] [--result clean|hit|na] [--severity S]
roll-up                        # fold the journal into MAP.md + state.json
import-issues [--file F]       # snapshot open upstream issues so the frontier de-prioritizes them
```

## A typical session

```bash
export AB_RUNS_DIR=runs
export AB_TARGET_SHA=$(cd ../ZorkAI && git rev-parse --short origin/main)   # for staleness tracking

python3 -m adventurebreaker.harness new --game zork --target prod --name hunt
python3 -m adventurebreaker.harness frontier --game zork --top 25     # what's untested?
python3 -m adventurebreaker.harness spine-run --count 30              # walk to a deep state (narrator OFF)
python3 -m adventurebreaker.harness save deep
python3 -m adventurebreaker.harness play <adversarial command>        # attack (narrator ON)
python3 -m adventurebreaker.harness restore <save-id>                 # fork back, attack again
python3 -m adventurebreaker.harness roll-up                           # regenerate coverage/MAP.md
git add coverage/ && git commit -m "coverage: hunt session"           # persist the ledger
```

## Logging a finding (do this carefully)

A credible ledger is the whole point. Before you record a finding:

1. **Reproduce it** deterministically where possible (`quiet` removes narrator randomness).
2. **Verify against engine source.** The [ZorkAI](https://github.com/arsindelve/zorkai)
   source — and the original ZIL when available — is the authority. A narrator describing a
   "gothic door to the west" looked wrong but was *correct* per the engine (nailed shut, so
   absent from the live `exits` list). Don't cry wolf.
3. **Check prod-lag.** Production can be many PRs behind `main`; confirm a prod-observed bug
   against current `origin/main` before filing — it may already be fixed-but-undeployed.
4. Record it: `finding --severity ... --category ... --title ... --detail ...`. This writes
   the per-run ledger **and** the durable `coverage/FINDINGS.md` (stable `AB-NNN` id).

## Adding a probe

Probes live in [`probes/`](probes/) so they survive the ephemeral container.

- **C# seam probes** (`probes/*.cs`) are exploratory NUnit tests copied into the ZorkAI test
  projects. They print via `TestContext.Out.WriteLine` rather than asserting. Promote a
  confirmed bug into a real `[Test]` with assertions when filing (red-before / green-after).
- **Python probes** (`probes/*.py`) run against the live backend using the harness client.

Each probe file starts with a header comment: where to copy it, how to run it, and the
result against a known target SHA. Update [`probes/RESULTS.md`](probes/RESULTS.md) with what
it found.

## Adding an oracle

Deterministic oracles live in [`adventurebreaker/oracles.py`](adventurebreaker/oracles.py),
split by layer (L0 contract / L1 consistency / L2 anchors). Guidelines:

- **Never raise into the caller's hot path.** An oracle returns `OracleHit` objects or `[]`.
- **Tune noisy oracles down, don't delete them.** Drop the severity to `low`/`info` and let
  the agent (L3) make the final call.
- Add a smoke assertion in `tests/` for any new oracle.

## Coding conventions

- Match the surrounding style: module docstrings, `from __future__ import annotations`,
  dataclasses, type hints, small focused functions.
- Keep the core **dependency-free**. New third-party deps must be optional extras with a
  clear justification.
- Comment density should match neighbouring code — explain *why*, not *what*.

## Pull request requirements

1. **Tests pass:** `python3 -m unittest discover -s tests`.
2. **Coverage artifacts** are regenerated (`roll-up`) and committed if you changed findings.
3. **The PR template is filled out**, including the mandatory **Security review** section.
4. **The agentic security review is green** (or every flagged issue is resolved/justified) —
   see [SECURITY.md](SECURITY.md).

By contributing you agree your contributions are licensed under the [MIT License](LICENSE).
