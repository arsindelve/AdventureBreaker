<div align="center">

# AdventureBreaker

### Agentic QA for an AI-narrated game engine

**An autonomous, adversarial playtester that drives the [ZorkAI](https://github.com/arsindelve/ZorkAI) engine through its real production backend, walks it into deep game states, and attacks each one to break the engine, the parser, and above all, the AI narrator.**

[![Agentic QA](https://img.shields.io/badge/agentic-QA-7b2ff7)](docs/AGENTIC-QA.md)
[![CI](https://github.com/arsindelve/AdventureBreaker/actions/workflows/ci.yml/badge.svg)](https://github.com/arsindelve/AdventureBreaker/actions/workflows/ci.yml)
[![Security Review](https://github.com/arsindelve/AdventureBreaker/actions/workflows/security-review.yml/badge.svg)](https://github.com/arsindelve/AdventureBreaker/actions/workflows/security-review.yml)
[![Python 3.11+](https://img.shields.io/badge/python-3.11%2B-3776ab)](https://www.python.org/)
[![Dependencies: none](https://img.shields.io/badge/dependencies-stdlib%20only-2ea44f)](pyproject.toml)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)

</div>

---

## What this is

> **Agentic QA** is an AI agent that autonomously explores your application, attacks it adversarially, and files structured bug reports. No test cases written by hand.

Traditional QA asserts on fixed outputs. That model **breaks** the moment a large language model enters the loop: an AI narrator's prose is non-deterministic, open-ended, and unbounded. You cannot unit-test a hallucination.

**AdventureBreaker is the answer: agentic QA.** It pairs a fleet of free, deterministic *oracles* with an LLM *critic* acting as the test agent. The agent decides what to attack, the oracles catch the mechanical failures for free, and the critic adjudicates the judgement calls that only a reasoning model can make: hallucinations, character breaks, prompt-injection compliance, unprompted spoilers, lore contradictions, and "that's just not right."

The goal is **not** to win. The walkthrough is a GPS to reach interesting states; the point is to **break things** along the way and to keep a credible, durable, deduped record of every break so the engine team can fix it.

[Read the agentic-QA manifesto](docs/AGENTIC-QA.md) | [Architecture deep-dive](docs/ARCHITECTURE.md)

> **Sibling project:** [PlayZork](https://github.com/arsindelve/PlayZork) tries to *beat* Zork with an LLM. AdventureBreaker reuses PlayZork's plumbing and replaces the "how to win" brain with a walkthrough spine plus an adversarial, judging driver.

---

## How it works

```
              ┌──────────────────── one game session ────────────────────┐

   spine ──►  ●━━━━━━━━━━━━━━━●━━━━━━━━━━━━━━━●━━━━━━━━━━━━━━━●━━━━━━►  (walk to deep states)
   (GPS)      │               │               │               │
              │   save ↓      │   save ↓      │   save ↓      │   save ↓
   ribs ──►   ╲ probe probe   ╲ probe probe   ╲ probe probe   ╲ probe probe   (attack each state)
   (attack)    ╲ restore ↑     ╲ restore ↑     ╲ restore ↑     ╲ restore ↑    (fork, never derail)
```

- **Spine (progress).** Replay the known-good walkthrough, extracted from the ZorkAI NUnit `[TestCase]` fixtures, to drive the game into deep, varied states. Each step also carries the test's expected output, so the spine doubles as a **golden transcript**.
- **Ribs (attack).** At each state, before advancing, fire adversarial inputs to break the engine, parser, and narrator.
- **Fork via save/restore.** The backend supports save/restore, so the harness checkpoints, probes *destructively*, then restores. Probing never derails progress.
- **Narrator A/B.** Every command can run with the narrator on or off (`NoGeneratedResponses`). Off **+** wrong means engine bug; on **+** wrong but off **+** right means narrator bug.

---

## The oracle stack

Four layers of verification, cheapest first:

| Layer | Cost | What it catches |
|---|---|---|
| **L0 Contract** | free | HTTP errors, malformed JSON, leaked stack traces, score out of bounds, empty narrator |
| **L1 Consistency** | free | Prose vs. structured envelope: item "taken" but inventory did not grow, moved but location did not change, score/move regressions |
| **L2 Anchors** | free | Known static strings, narrator-leak heuristics, golden transcript divergence |
| **L3 Critic** | AI judge | Hallucinated objects/lore, state contradictions, character breaks, injection compliance, spoilers |

L0-L2 are free and fast. L3 is the reasoning judge, reached only when the cheap layers pass. The full response envelope (`inventory`, `exits`, `time`, `actionsAvailableFrom*`) gives L0/L1 precise ground truth to check prose against without burning tokens.

The engine source and the original ZIL are the authority for factual disputes. Verify before logging, so the ledger stays credible.

---

## Why it is interesting

- **Zero dependencies.** Pure Python 3.11+ stdlib. It runs against production with no install step: `git clone` and go.
- **It hunts AI-specific bugs.** Hallucinations, prompt-injection, character breaks, and spoilers are not catchable by ordinary assertions. AdventureBreaker is built around them.
- **It remembers across runs.** A committed, append-only **coverage ledger** tracks what has been tested, computes the untested **frontier**, and reopens cells when the engine code advances.
- **It produces filed, deduped, durable findings** with stable `AB-NNN` ids and issue status (`open` / `filed#NNN` / `fixed#NNN`). See [`coverage/FINDINGS.md`](coverage/FINDINGS.md).
- **It has receipts.** Durable findings already cover Zork I and Planetfall, including narrator hallucinations, parser defects, state-parity drift, and scoring exploits. See the [results matrix](probes/RESULTS.md).

---

## Quickstart

Dependency-free: Python 3.11+, stdlib only.

```bash
# one-time: extract walkthrough spines from a local ZorkAI checkout
python3 tools/extract_spine.py --game zork \
  --src /path/to/ZorkAI/ZorkOne.Tests/Walkthrough/WalkthroughTestOne.cs \
  --out adventurebreaker/spine/zork1.json

python3 tools/extract_spine.py --game planetfall \
  --src /path/to/ZorkAI/Planetfall.Tests/Walkthrough/WalkthroughTestOne.cs \
  --out adventurebreaker/spine/planetfall.json

# start a session and drive it
python3 -m adventurebreaker.harness new --game zork
python3 -m adventurebreaker.harness spine-run --count 12   # advance via walkthrough
python3 -m adventurebreaker.harness save checkpoint
python3 -m adventurebreaker.harness play examine the troll # probe with narrator
python3 -m adventurebreaker.harness quiet open mailbox     # probe engine only
python3 -m adventurebreaker.harness restore <save-id>      # fork back to checkpoint

# log a confirmed finding
python3 -m adventurebreaker.harness finding \
  --severity high --category narrator \
  --title "Narrator hallucinated non-existent sword" \
  --command "examine sword" \
  --detail "Narrator described a sword; inventory envelope shows none" \
  --evidence "<raw response>"

python3 -m adventurebreaker.harness report

# ask the persistent ledger what to attack next
python3 -m adventurebreaker.harness frontier --game planetfall --top 25
python3 -m adventurebreaker.harness roll-up
```

Target a local backend during development with `--target local`.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full CLI reference and the day-to-day workflow.

---

## Project layout

```
adventurebreaker/
  config.py     backend registry (Zork/Planetfall x prod/local) + score facts
  client.py     stdlib HTTP client; play/init/save/restore/list; captures status+latency+raw
  models.py     full GameResponse envelope (inventory/exits/actions/time) + Direction int map
  oracles.py    L0 contract / L1 consistency / L2 anchors
  coverage.py   persistent cross-run coverage ledger + untested-frontier computation
  ledger.py     per-run state, transcript, Markdown+JSONL findings
  harness.py    CLI the interactive agent drives
  spine/        extracted walkthroughs: zork1.json, planetfall.json
tools/
  extract_spine.py   one-time spine extractor from ZorkAI [TestCase] fixtures
probes/              reusable, source-grounded probes
coverage/            committed, cross-run coverage ledger + durable findings
docs/                architecture, contracts, spine format, and the agentic-QA manifesto
```

---

## Porting to a new game

Two documents cover everything needed to target a different backend:

- [docs/api-contract.md](docs/api-contract.md) describes the HTTP contract, the response envelope, and how to register a new game in `config.py`.
- [docs/spine-format.md](docs/spine-format.md) describes the spine JSON schema, a worked example, and how to write one by hand or extract it from existing tests.

No source code changes are required: add a config entry, drop in a spine file, and run.

---

## Security

Every pull request is gated by an **agentic security review** using Anthropic's [`claude-code-security-review`](https://github.com/anthropics/claude-code-security-review) action. The project that *does* agentic QA holds its own changes to the same bar.

See [SECURITY.md](SECURITY.md) for the policy, the PR requirement, and how to report a vulnerability.

---

## Related

- [ZorkAI](https://github.com/arsindelve/ZorkAI) is the engine under test.
- [PlayZork](https://github.com/arsindelve/PlayZork) is the sibling project that tries to *beat* Zork with an LLM.

---

## License

[MIT](LICENSE) © Michael Lane.
