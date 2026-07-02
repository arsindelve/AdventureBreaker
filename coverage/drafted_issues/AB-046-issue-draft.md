TITLE: Planetfall prod: session state fully resets after ~14 consecutive wait/idle commands, silently discarding all progress

## Bug

Black-box against Planetfall prod (`https://6kvs9n5pj4.execute-api.us-east-1.amazonaws.com/Prod/Planetfall`): sending consecutive `wait`-type (idle, non-state-changing) commands to a session causes the server to silently and completely reset that session's authoritative state — `moves` reverts to 0, `time=` drops back close to its session-start value, and any acquired ambient item (the ship's `brochure`, normally auto-acquired a few turns in) disappears from `inv=`. The HTTP response is still 200 with plausible flavor text ("Time passes..."), so nothing in the response signals to a client that a reset occurred.

Reproduced deterministically 3 times, each on an independent fresh session (`new --game planetfall --target prod`):

1. **300× identical `wait`** (quiet / `noGeneratedResponses:true`) → resets at request #14, 28, 42, 56 ... 294 — every single multiple of 14, zero exceptions across 21 cycles.
2. **60× alternating `wait`/`z`** (different literal input text, same in-fiction effect) → resets again at exactly 14, 28, 42, 56. Rules out a naive identical-text dedup/idempotency-key explanation.
3. **24× `wait` with a forced 4-second gap** between every call → reset still lands at exactly request #14 (72s elapsed, vs ~15-20s elapsed to reach #14 in the unpaced tests). Rules out a simple wall-clock TTL/cache-expiry explanation — the trigger is the **count of consecutive idle commands**, not elapsed time.

**Contrast case:** a session replaying the real walkthrough (movement, taking items, puzzle actions, with only short wait streaks ≤10 interspersed with substantive actions) reached move ~150+ cleanly with no reset anywhere near move 14. So this is not "every Nth HTTP request to any session" — it specifically implicates **long streaks of consecutive wait/no-op commands**.

## Why this blocks real progression

Planetfall's own walkthrough requires riding the Alfie/Betty shuttle to reach the Lawanda Complex — a scripted sequence of **~20 consecutive `wait` commands** (`adventurebreaker/spine/planetfall.json` steps ~179–200) with no opportunity to interleave other actions without breaking the shuttle sequence. Every attempt this session to drive `spine-run` through that exact stretch reset back to Deck Nine (score 0, starting inventory) partway through the wait run — consistent with this bug being the root cause. A real player following the intended solution to reach a major game area would have their entire session silently wiped mid-ride, with no error or warning, and would land back at the very beginning having no idea why.

## Expected vs actual

- **Expected:** session state persists durably for the life of a session; `moves` increases monotonically; inventory and elapsed `time` never regress absent an explicit restore.
- **Actual:** after ~14 consecutive idle/wait commands, `moves`/`time`/`inv` silently revert to near-initial values, repeating every 14 such commands indefinitely.

## Root cause

Not confirmed against engine source this run — `arsindelve/zorkai` was out of scope for this session's GitHub access, and no local `../ZorkAI` checkout was available (a direct `git clone` to github.com was also blocked by the sandbox's org egress policy). Recommend engine-side investigation of session-state persistence around consecutive no-op/`wait` handling — candidate hypothesis: a fixed-size in-memory cache/transcript buffer with ~14-entry capacity that silently evicts/reinitializes on overflow rather than reading through to durable storage (session state is DynamoDB-backed per `AdventureBreaker/adventurebreaker/ledger.py` comments).

## Impact

- Severity: **critical** — silent, total progress loss with no player-facing error, directly blocking a mandatory puzzle step (the shuttle ride) on the path to a major content area (Lawanda Complex). No practical player-facing workaround exists for that specific scripted wait sequence.
- Every player attempting the canonical shuttle-ride solution is at risk of hitting this.

## Suggested fix

Investigate the session-state write/read path for commands that don't otherwise mutate game state (e.g. `wait`/`z`). Check for any fixed-capacity in-memory structure (cache, ring buffer, per-container session cache) keyed near 14 entries that could silently reinitialize a session rather than reading through to the DynamoDB-backed source of truth. Ensure every turn — regardless of command content — durably persists moves/inventory/time before returning.

## TDD plan

Add a deterministic integration test that sends 20+ consecutive `wait` commands to a single session (mirroring the existing shuttle-ride walkthrough fixture) and asserts `moves` is strictly increasing and inventory/`time` never regress across the whole sequence. Red before fix (should fail around the 14th `wait`), green after.

## Reproduction

```
python3 -m adventurebreaker.harness new --game planetfall --target prod --name repro
for i in $(seq 1 20); do
  python3 -m adventurebreaker.harness quiet wait
done
# observe: moves resets to 0, inventory reverts, time= drops, around the 14th call
```

---
Found via the AdventureBreaker harness (black-box prod; white-box/ZIL confirmation was unavailable this session — arsindelve/zorkai was out of scope). AB-046.
