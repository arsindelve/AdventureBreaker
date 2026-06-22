<div align="center">

# 🗡️ AdventureBreaker

### Agentic QA for an AI-narrated game engine

**An autonomous, adversarial playtester that drives the [ZorkAI](https://github.com/arsindelve/zorkai) engine through its real production backend, walks it into deep game states, and attacks each one to break the engine, the parser, and — above all — the AI narrator.**

[![Agentic QA](https://img.shields.io/badge/agentic-QA-7b2ff7)](docs/AGENTIC-QA.md)
[![CI](https://github.com/arsindelve/AdventureBreaker/actions/workflows/ci.yml/badge.svg)](https://github.com/arsindelve/AdventureBreaker/actions/workflows/ci.yml)
[![Security Review](https://github.com/arsindelve/AdventureBreaker/actions/workflows/security-review.yml/badge.svg)](https://github.com/arsindelve/AdventureBreaker/actions/workflows/security-review.yml)
[![Python 3.11+](https://img.shields.io/badge/python-3.11%2B-3776ab)](https://www.python.org/)
[![Dependencies: none](https://img.shields.io/badge/dependencies-stdlib%20only-2ea44f)](pyproject.toml)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)

</div>

---

## What this is

Traditional QA asserts on fixed outputs. That model **breaks** the moment a large
language model enters the loop: an AI narrator's prose is non-deterministic, open-ended,
and unbounded — there is no golden string to `assert ==` against. You cannot unit-test a
hallucination.

**AdventureBreaker is the answer: agentic QA.** It pairs a fleet of free, deterministic
*oracles* with an LLM *critic* (Claude) acting as the test agent. The agent decides what
to attack, the oracles catch the mechanical failures for free, and the agent adjudicates
the judgement calls that only a reasoning model can make — hallucinations, character
breaks, prompt-injection compliance, unprompted spoilers, lore contradictions, "that's
just not right."

> Its goal is **not** to win. The walkthrough is a GPS to reach interesting states; the
> point is to **break things** along the way — and to keep a credible, durable, deduped
> record of every break so the engine team can fix it.

📖 **[Read the agentic-QA manifesto →](docs/AGENTIC-QA.md)**  ·  🏗️ **[Architecture deep-dive →](docs/ARCHITECTURE.md)**

> **Sibling project:** [PlayZork](https://github.com/arsindelve/PlayZork) tries to *beat*
> Zork with an LLM. AdventureBreaker reuses PlayZork's plumbing (HTTP client, backend
> registry, reporting) but replaces the "how to win" brain with a walkthrough spine + an
> adversarial, judging driver.

---

## The mechanic: Spine + Ribs

```
              ┌──────────────────── one game session ────────────────────┐

   spine ──►  ●━━━━━━━━━━━━━━━●━━━━━━━━━━━━━━━●━━━━━━━━━━━━━━━●━━━━━━►  (walk to deep states)
   (GPS)      │               │               │               │
              │   save ↓      │   save ↓      │   save ↓      │   save ↓
   ribs ──►   ╲ probe probe   ╲ probe probe   ╲ probe probe   ╲ probe probe   (attack each state)
   (attack)    ╲ restore ↑     ╲ restore ↑     ╲ restore ↑     ╲ restore ↑    (fork — never derail)
```

- **Spine (progress).** Replay the known-good walkthrough — extracted from the ZorkAI NUnit
  `[TestCase]` fixtures — to drive the game into deep, varied states. Each step also carries
  the test's expected output, so the spine doubles as a **golden transcript** (a free
  regression check).
- **Ribs (attack).** At each state, before advancing, fire adversarial inputs to break the
  engine, parser, and narrator.
- **Fork via save/restore.** The backend supports save/restore, so the harness checkpoints,
  probes *destructively*, then restores — probing never derails progress.
- **Narrator A/B.** Every command can run with the narrator on or off (`NoGeneratedResponses`).
  Off **+** wrong ⇒ engine bug; on **+** wrong but off **+** right ⇒ narrator bug.

---

## The oracle ladder (cheap → expensive)

| Layer | Cost | What it catches |
|---|---|---|
| **L0 contract** | free | non-2xx/5xx, malformed JSON, leaked stack traces, score out of `[0, max]`, empty/silent narrator |
| **L1 consistency** | free | prose vs. the structured state envelope — `Taken` but inventory didn't grow, moved but location didn't change, score/moves regressions |
| **L2 anchors** | free | known static strings; narrator-leak / character-break heuristics; spine golden-transcript divergence |
| **L3 critic** | the agent (Claude) | hallucinated objects/lore, state contradictions, character breaks, injection compliance, spoilers, "that's not right" |

The full envelope (`inventory`, `exits`, `time`, `actionsAvailableFrom*`) is the free ground
truth the L0/L1 oracles check the prose against. The engine source (and the original ZIL,
read-only) is the authority for factual disputes — **verify before logging**, to keep the
ledger credible.

---

## Why it's interesting

- **Zero dependencies.** Pure Python 3.11+ stdlib. It runs against production with no install
  step — `git clone` and go.
- **It hunts AI-specific bugs.** Hallucinations, prompt-injection, character breaks, and
  spoilers are *not* catchable by ordinary assertions. AdventureBreaker is built around them.
- **It remembers across runs.** The container is ephemeral, but a committed, append-only
  **coverage ledger** tracks what's been tested, computes the untested **frontier**, and
  re-opens cells when the engine code advances (so a stale "clean" is never banked forever).
- **It produces filed, deduped, durable findings** with stable `AB-NNN` ids and issue status
  (`open` / `filed#NNN` / `fixed#NNN`) — see [`coverage/FINDINGS.md`](coverage/FINDINGS.md).
- **It has receipts.** **13 durable findings** to date across Zork I and Planetfall — several
  already filed and fixed upstream (e.g. a narrator hallucination, an adjective-blind noun
  resolver, GET/POST state-parity drift, a points-farming exploit). See the
  [results matrix](probes/RESULTS.md).

---

## Quickstart

Dependency-free (Python 3.11+, stdlib only):

```bash
# one-time: extract the walkthrough spines from a local ZorkAI checkout
python3 tools/extract_spine.py --game zork \
  --src /path/to/ZorkAI/ZorkOne.Tests/Walkthrough/WalkthroughTestOne.cs \
  --out adventurebreaker/spine/zork1.json

# play & probe (prod by default; --target local for a local backend)
python3 -m adventurebreaker.harness new --game zork
python3 -m adventurebreaker.harness spine-run --count 12        # advance via walkthrough
python3 -m adventurebreaker.harness save checkpoint
python3 -m adventurebreaker.harness play examine the troll      # probe (narrator on)
python3 -m adventurebreaker.harness quiet open mailbox          # engine only (no narrator)
python3 -m adventurebreaker.harness restore <save-id>           # fork back
python3 -m adventurebreaker.harness finding --severity high --category narrator \
    --title "..." --command "..." --detail "..." --evidence "..."
python3 -m adventurebreaker.harness report

# ask the persistent ledger what to attack next
python3 -m adventurebreaker.harness frontier --game planetfall --top 25
python3 -m adventurebreaker.harness roll-up
```

See **[CONTRIBUTING.md](CONTRIBUTING.md)** for the full CLI reference and the day-to-day
workflow.

---

## Layout

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
coverage/       committed, cross-run coverage ledger + durable findings (survives the container)
docs/           architecture & the agentic-QA manifesto
```

---

## Security

Every pull request is gated by an **agentic security review** — Anthropic's
[`claude-code-security-review`](https://github.com/anthropics/claude-code-security-review)
action reads the diff and comments on real vulnerabilities before merge. The project that
*does* agentic QA holds its own changes to the same bar.

See **[SECURITY.md](SECURITY.md)** for the policy, the PR requirement, and how to report a
vulnerability.

---

## License

[MIT](LICENSE) © Michael Lane.
