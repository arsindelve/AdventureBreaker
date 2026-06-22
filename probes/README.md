# Probe library

Reusable, source-grounded probes used to hunt engine bugs in the ZorkAI repo.
These are **C# test files** (not part of the Python harness) — they're saved here so
they survive the ephemeral container and the next session can re-run them.

## How to use

1. Copy a probe `.cs` into the matching ZorkAI test project (noted in each file header):
   - `UnitTests/` for Zork-engine probes
   - `Planetfall.Tests/` for Planetfall probes
2. From the ZorkAI checkout (deployed code = `git checkout origin/main`):
   ```bash
   dotnet test <Project>/<Project>.csproj --filter "FullyQualifiedName~<ProbeClass>" \
     -l "console;verbosity=detailed" 2>&1 | grep -E "=>|->"
   ```
3. They print results via `TestContext.Out.WriteLine` and `Assert.Pass()` — they don't
   assert (they're exploratory). Promote a confirmed bug into a real `[Test]` with
   assertions in the appropriate suite when filing (red-before/green-after).

## Lessons baked in (so you don't re-learn them)

- **The deterministic test parser can't parse adjective single-noun phrasings**
  (`take the good bedistor` → empty). Test the resolver seam (`Repository.GetItemInScope`)
  directly, not `GetResponse`. (Bedistor #244.)
- **`GetResponse` returns empty when the response needs the AI generation layer**
  (suppressed in tests) — e.g. `examine <out-of-scope item>` → `[]`. That empty is a
  harness artifact, **not** a prod bug. Don't file it.
- **god mode (`Repository.GetItem`) is raw-matched** and can't set up two distinct
  same-noun items on prod — use the deterministic seam for collision families.
- Always set `AB_TARGET_SHA` and re-confirm prod findings against `origin/main` (prod lags).

See `RESULTS.md` for the running matrix of what each probe found.
