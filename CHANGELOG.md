# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Project documentation: README rewrite framing the tool as **agentic QA**, plus
  `docs/AGENTIC-QA.md` (manifesto) and `docs/ARCHITECTURE.md` (deep-dive).
- `CONTRIBUTING.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, and an MIT `LICENSE`.
- **Security PR requirement:** an agentic security-review GitHub Action
  (`anthropics/claude-code-security-review`) runs on every pull request, plus a PR template
  with a mandatory security checklist.
- CI workflow (`.github/workflows/ci.yml`): stdlib smoke suite on Python 3.11–3.13, CLI
  wiring check, and committed-JSON validation.
- `tests/` smoke suite (stdlib `unittest`) covering config, model parsing, the deterministic
  oracles, coverage categorisation, the CLI parser, and the spine JSON.
- Issue templates for findings and harness bugs.

## [0.1.0]

### Added
- AdventureBreaker harness: spine/ribs adversarial playtester for the ZorkAI engine
  (Zork I & Planetfall) over the production HTTP backend.
- Deterministic oracle ladder (L0 contract / L1 consistency / L2 anchors).
- Persistent cross-run coverage ledger with untested-frontier computation and staleness
  tracking, plus durable findings.
- Reusable probe library (C# resolver-seam probes + Python parity checks).
