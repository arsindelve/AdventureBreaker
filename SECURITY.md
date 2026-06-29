# Security Policy

AdventureBreaker is a tool for **agentic QA** — it autonomously attacks an AI-narrated game
engine to find bugs. Because it talks to a live production backend, drives an LLM narrator,
and processes untrusted model output, security is treated as a first-class concern: the
project that does adversarial QA holds its own code to the same bar.

## PR requirement: every change is security-reviewed

**All pull requests must pass an agentic security review before merge.** This is enforced
two ways:

1. **Automated agentic review (CI).** The
   [`.github/workflows/security-review.yml`](.github/workflows/security-review.yml) workflow
   runs Anthropic's
   [`claude-code-security-review`](https://github.com/anthropics/claude-code-security-review)
   action on every pull request. It reads the diff and comments inline on real
   vulnerabilities (injection, SSRF, secret leakage, unsafe deserialization, path traversal,
   etc.). PRs should not be merged while the security review reports an unresolved
   high/critical finding.
2. **Reviewer checklist.** The
   [pull request template](.github/PULL_REQUEST_TEMPLATE.md) includes a mandatory **Security
   review** section. The author confirms each item and the human reviewer signs off.

> **Maintainers:** the automated review requires a repository secret named `CLAUDE_API_KEY`
> (Settings → Secrets and variables → Actions). Forked PRs without secret access are skipped,
> but same-repo PRs fail fast when the secret is missing. To make the review a hard gate,
> mark the **Security Review** check as required under branch protection for `main`.

## Threat model (what we actually worry about)

AdventureBreaker is a client, not a server, but it has a real attack surface:

| Surface | Risk | Mitigation in this repo |
|---|---|---|
| **Outbound HTTP to game backends** | SSRF / talking to an unintended host | Endpoints are a fixed, reviewed registry in `config.py`; only `prod`/`local` targets. |
| **Untrusted LLM / engine output** | injection, secret/PII leakage, prompt-leak echoed into logs & committed ledgers | Output is treated as data, never executed; oracles flag leaked internals (`INTERNALS_MARKERS`) and narrator prompt-leaks (`NARRATOR_LEAK_MARKERS`). |
| **Committed coverage/finding ledgers** | accidentally committing a session secret or PII from a transcript | `runs/` (raw transcripts) is git-ignored; only curated `coverage/` artifacts are committed — review them before committing. |
| **Probe files (`probes/*.cs`, `*.py`)** | a probe that does something destructive when copied into the engine repo | Probes are exploratory and read-mostly by convention; review any probe before running it against a real backend. |
| **CI secrets** | `CLAUDE_API_KEY` exposure to untrusted PRs | The workflow uses least-privilege permissions (`contents: read`, `pull-requests: write`) and the secret is only available to trusted runs. |

## Reporting a vulnerability

If you discover a security issue in AdventureBreaker itself (not a *finding about the game
engine* — those go in the coverage ledger and upstream issues):

- **Preferred:** open a [GitHub Security Advisory](https://github.com/arsindelve/AdventureBreaker/security/advisories/new)
  (private disclosure).
- **Alternative:** email the maintainer at the address on the repository owner's profile.

Please include a description, affected files/versions, and a minimal reproduction. We aim to
acknowledge within a few days. Do not open a public issue for an undisclosed vulnerability.

## Scope

In scope: the AdventureBreaker harness, tools, CI workflows, and committed artifacts in this
repository. Out of scope: bugs in the ZorkAI engine or its production deployment — those are
the *output* of this tool and are tracked as findings and filed upstream in
[`arsindelve/zorkai`](https://github.com/arsindelve/zorkai).
