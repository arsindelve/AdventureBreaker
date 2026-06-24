# AdventureBreaker

> **Agentic QA** — an AI agent that autonomously explores your application, attacks it adversarially, and files structured bug reports. No test cases written by hand.

Traditional QA scales linearly with human effort. AdventureBreaker doesn't. It deploys a Claude-powered agent against the [ZorkAI](https://github.com/arsindelve/ZorkAI) engine — an AI-narrated interactive fiction backend — and lets it loose: drive deep into game state, fire adversarial inputs, judge the results, and log confirmed findings. The agent runs until it runs out of things to break.

This is what QA looks like when the tester is also an AI.

---

## Why Agentic QA?

AI-powered applications introduce a class of bugs that traditional testing can't catch:

- **Hallucinations** — the narrator invents objects, exits, or lore that don't exist
- **State contradictions** — the prose says one thing, the structured envelope says another
- **Character breaks** — the narrator steps outside its role
- **Prompt injection** — player input manipulates the narrator in unintended ways
- **Spoilers** — the narrator reveals information the player shouldn't have yet

You can't write assertions for these. You need a judge that understands language and context. That judge is Claude.

AdventureBreaker pairs a deterministic walkthrough spine (to navigate deep into the application) with a Claude-powered adversarial driver (to attack it). The agent isn't trying to win the game. The walkthrough is a GPS to reach interesting states — the goal is to break things along the way.

---

## How It Works

### Spine + Ribs

```
Spine ──── walkthrough steps ──→ drives the game into deep, varied states
            │                       each step has an expected output
            │                       (doubles as a golden transcript)
            ▼
Ribs  ──── adversarial probes ──→ attack each state before advancing
            │                       fork via save/restore (probing never
            │                       derails progress)
            ▼
Oracle ─── L0 → L1 → L2 → L3 ──→ judge the response at every layer
```

**Fork via save/restore:** the backend supports save/restore checkpoints. The harness saves state, fires destructive probes, then restores — so adversarial testing never derails the walkthrough's progress through the game.

**Narrator A/B:** every command runs with the narrator on *and* off. Off + wrong → engine bug. On + wrong, off + right → narrator bug. The structured response envelope is ground truth; the prose has to match it.

---

## The Oracle Stack

Four layers of verification, cheapest first:

| Layer | Cost | What it catches |
|---|---|---|
| **L0 Contract** | free | HTTP errors, malformed JSON, leaked stack traces, score out of bounds, empty narrator |
| **L1 Consistency** | free | Prose vs. structured envelope — item "taken" but inventory didn't grow; moved but location didn't change; score/move regressions |
| **L2 Anchors** | free | Known static strings; narrator-leak heuristics; golden transcript divergence |
| **L3 Critic** | Claude | Hallucinated objects/lore, state contradictions, character breaks, injection compliance, spoilers |

L0–L2 are free and fast. L3 is the AI judge — only reached when the cheap layers pass. The full structured envelope (`inventory`, `exits`, `time`, `actionsAvailableFrom*`) gives L0/L1 precise ground truth to check prose against without burning tokens.

---

## Persistent Coverage Ledger

Every run appends to a shared ledger. The agent tracks which game states it has visited and which probes it has fired, so each run goes somewhere new rather than replaying the same ground. Coverage compounds across sessions.

```
runs/<session>/
  findings.jsonl     confirmed bugs — deduped, severity-sorted, with repro steps
  findings.md        same, human-readable
  transcript.jsonl   every turn: command, full envelope, oracle hits
  state.json         session id, turn, spine position (enables resume)
```

---

## Findings

AdventureBreaker has already filed confirmed bugs against ZorkAI:

- Narrator hallucinating inventory items not present in the structured state
- Score regression on specific command sequences
- Parser silent-failure on malformed input (no error, no state change)
- Narrator breaking character under prompt-injection attempts
- Golden transcript divergence after engine refactors

Each finding includes the exact command sequence to reproduce it, the full response envelope at the time of failure, and the oracle layer that caught it.

---

## Usage

Zero dependencies — Python 3.11+ stdlib only.

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
```

Target a local backend during development with `--target local`.

---

## Project Layout

```
adventurebreaker/
  config.py          backend registry (Zork/Planetfall × prod/local) + score facts
  client.py          stdlib HTTP client — play/init/save/restore/list
  models.py          full GameResponse envelope (inventory/exits/actions/time)
  oracles.py         L0 contract / L1 consistency / L2 anchors
  ledger.py          run state, transcript, Markdown+JSONL findings
  harness.py         CLI the agentic driver operates
  spine/             extracted walkthroughs (zork1.json, planetfall.json)
tools/
  extract_spine.py   one-time spine extractor from ZorkAI [TestCase] fixtures
```

---

## The Bigger Picture

AdventureBreaker is a proof of concept for a new class of testing that doesn't exist in traditional CI pipelines: **an agent that autonomously discovers bugs in AI-powered applications by reasoning about correctness, not just asserting against expected values.**

The pattern generalizes:

- Any application with a **structured response envelope** alongside **generated prose** can be tested this way — the envelope is free ground truth, the prose is what gets judged
- Any domain with a **known walkthrough** — an ordered set of inputs that drives the system into interesting states — can use the spine + ribs mechanic
- Any AI application where correctness requires **language understanding** needs an AI oracle at L3

The game is the test harness. The bugs are real.

---

## Porting to a New Game

Two documents cover everything needed to target a different backend:

- **[docs/api-contract.md](docs/api-contract.md)** — the HTTP contract your backend must implement, field-by-field response envelope reference, and how to register a new game in `config.py`
- **[docs/spine-format.md](docs/spine-format.md)** — the spine JSON schema, a worked example, and how to write one by hand or extract it from existing tests

No source code changes required — add a config entry, drop in a spine file, and run.

---

## Related

- [ZorkAI](https://github.com/arsindelve/ZorkAI) — the engine under test
- [PlayZork](https://github.com/arsindelve/PlayZork) — sibling project that tries to *beat* Zork with an LLM; AdventureBreaker reuses its HTTP client and config but replaces the "win" brain with an adversarial one

---

## License

MIT — see [LICENSE](LICENSE).
