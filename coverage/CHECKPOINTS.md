# Late-game Planetfall checkpoints (server-side saves)

Server-side save checkpoints on the **prod** Planetfall backend, so future sessions can
reach deep/late-game state **without** re-driving the fragile ~300-step walkthrough spine
(the elevator/shuttle/miniaturization sequences desync on live prod and take many
minutes + careful manual correction to get through).

## How the saves are keyed

Saves live on the prod backend (DynamoDB), keyed by **`client_id`** + a **save GUID**.
`restore` needs the run's `session_id` + `client_id` + the save GUID. Cross-session
restore is **validated working**: a fresh run that adopts the recorded `session_id` +
`client_id` can restore these saves by GUID.

## Restore procedure (from a new session)

```bash
cd /home/user/AdventureBreaker && export AB_RUNS_DIR=runs
python3 -m adventurebreaker.harness new --game planetfall --target prod --name cp
# adopt the recorded identity so the save is addressable:
python3 -c "import json,pathlib; p=pathlib.Path('runs/cp/state.json'); d=json.loads(p.read_text()); d['session_id']='riiyUIIIpHH5MLw'; d['client_id']='n8ufR0JyJGMSEXi'; p.write_text(json.dumps(d,indent=2))"
python3 -m adventurebreaker.harness --run cp restore <SAVE_GUID>
python3 -m adventurebreaker.harness --run cp state
```

**Trap (learned the hard way):** `restore` overwrites the *server-side* state for that
`session_id`. Do **not** restore into a session another live run is mid-drive on — it
reverts that run. For destructive edge-case forking, restore only that run's *own* saves
on *its own* session (the intended fork mechanism), or spin up an isolated run on a fresh
session that merely *adopts* the identity to read a checkpoint.

## Identity

- **game:** planetfall · **target:** prod
- **session_id:** `riiyUIIIpHH5MLw`
- **client_id:** `n8ufR0JyJGMSEXi`
- Floyd wandering disabled (`god mode no wander`) and chronometer reset in these saves, so
  Floyd deterministically follows (mirrors the walkthrough's mocked RNG).

## Checkpoints

| Save GUID | Name | Location | Notes |
|-----------|------|----------|-------|
| `ed97022e-f237-4c0a-8b92-a4738cbb3348` | `kalamontee-preshuttle` | Kalamontee Platform | Pre-shuttle, all elevators done, Floyd present, wander disabled. Board Alfie (S), `god mode reset time`, slide shuttle card, drive to Lawanda. |
| `8a671f3b-9b4d-49ae-b11b-61c02c2611c6` | `lawanda-arrival` | Lawanda Platform | Post-shuttle (coolant/Tower detour skipped — safe, nothing later needs it), Floyd present, score 32. Spine-resume point = step 204. |
| `72349f45-b175-4f0d-a620-5ad5ea1e5bff` | `biolock-east-arrived` | Bio Lock East | First arrival at the sacrifice room, Floyd present+on, mini card visible through the window (`examine window`). |
| `3fa32db0-9310-4beb-a634-cb80a977f837` | `biolock-presacrifice` | Bio Lock East | **Reproduces AB-086 / zorkai#493.** Floyd present+on, Computer-Room concern set, mini card in the lab. `wait` never produces `NeedToGetCard`; `open door` → instant death. Restore here to verify the sacrifice/StateMachine-serialization fix. |

_Note: `lawanda-arrival` skips the Tower coolant puzzle, so its score (32) runs ~10 below
the golden walkthrough — cosmetic; no later step depends on the coolant/distress state.
The two `biolock-*` saves sit at the deep end-game and are the fast path for re-testing
the Floyd sacrifice once zorkai#493 is fixed._
