<!--
  Thanks for contributing to AdventureBreaker.
  Every PR is gated by an agentic security review — see the Security review section below
  and SECURITY.md. Fill out each section; delete the comments as you go.
-->

## What & why

<!-- What does this change do, and why? Link any related finding (AB-NNN) or upstream issue. -->

-

## Type of change

- [ ] Harness / oracle change
- [ ] New or updated probe (`probes/`)
- [ ] Coverage / findings ledger update (`coverage/`)
- [ ] Docs
- [ ] CI / tooling

## How it was tested

<!-- Commands you ran. At minimum the smoke suite. -->

- [ ] `python3 -m unittest discover -s tests` passes
- [ ] Ran against a backend (note game/target) and pasted relevant output, if applicable
- [ ] `roll-up` run and `coverage/` regenerated, if findings changed

## Findings / ledger impact

<!-- If this PR adds or changes findings, note the AB-NNN id(s) and status. Otherwise "none". -->

-

## 🔒 Security review (required)

> Every PR must clear an agentic security review before merge. The
> [`security-review`](.github/workflows/security-review.yml) workflow comments on the diff
> automatically; confirm the checklist below and resolve or justify anything it flags.
> See [SECURITY.md](SECURITY.md).

- [ ] I reviewed the [agentic security review](.github/workflows/security-review.yml) output and **no unresolved high/critical findings remain** (each is fixed or justified in a comment).
- [ ] **No secrets, API keys, tokens, or credentials** are added to code, tests, logs, or committed ledgers.
- [ ] **No new outbound hosts** were introduced outside the reviewed `config.py` backend registry (SSRF surface).
- [ ] **Untrusted output** (LLM/engine responses) is treated as data — never executed, evaluated, or used to build shell/SQL/paths.
- [ ] Committed `coverage/` artifacts contain **no PII or session secrets** carried over from transcripts.
- [ ] CI workflow permissions remain **least-privilege**; no new secret is exposed to untrusted (forked) PR runs.

## Checklist

- [ ] Core stays **dependency-free** (any new dep is an optional extra with justification)
- [ ] Style matches the surrounding code
- [ ] Docs updated if behavior or the CLI changed
