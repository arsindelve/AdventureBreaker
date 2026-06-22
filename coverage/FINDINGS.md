# AdventureBreaker durable findings

_Generated 2026-06-22T00:07:59Z · 6 finding(s)_

## AB-002 [MEDIUM] Death scatters nothing: player keeps full (lit) inventory through resurrection  · _open_

- game `zork` · area `MECH:death-resurrection` · category `death` · target_sha `c31e9ec`
- command: `enter Troll Room and die`

Known engine TODO at DeathProcessor.cs:29-31. Not yet filed as issue.

## AB-003 [MEDIUM] GET / POST early-exit responses return empty inventory while items held  · _fixed#232_

- game `zork` · area `MECH:get-post-parity` · category `get-post-parity` · target_sha `c31e9ec`
- command: `GET state; pronoun 'What item are you referring to?'`

Filed zork#230; fixed by PR #232 (merged). Prod not yet redeployed.

## AB-001 [LOW] Narrator invents a paint-splattered broom not present in the room  · _fixed#234_

- game `zork` · area `Studio` · category `narrator-hallucination` · target_sha `c31e9ec`
- command: `examine the wizard standing in the corner`

Fixed in PR #234 (merged): deflection prompts now forbid inventing objects + lower temperature.

## AB-004 [LOW] Intermittent HTTP 500 (no body) on complex NL input — not reliably reproducible  · _open_

- game `zork` · area `MECH:get-post-parity` · category `get-post-parity` · target_sha `c31e9ec`
- command: `ask the dead spirits who the game designer is and what year it is`

3 transient 500s, not reproducible in 9 later trials. Recommend CloudWatch log review.

## AB-005 [LOW] Structured response leaks exits + action chips in the dark  · _filed#238_

- game `zork` · area `Cellar` · category `light-dark` · target_sha `c31e9ec`
- command: `turn off lantern; look`

Filed zork#238 (open). ItIsDarkHere gates prose but not GameResponse.Exits / GetAvailableActionsInLocation.

## AB-006 [LOW] Shuttle decel-to-stop prints contradictory 'stop' + 'continues to move (0)' and advances a tunnel position  · _open_

- game `planetfall` · area `MECH:shuttle-alfie-betty` · category `puzzle-step` · target_sha `c31e9ec`
- command: `pull lever (speed 5 -> 0 mid-tunnel)`

ShuttleControl.Act: 'if (Speed != 0 || SpeedChanged) Move()'. On the turn deceleration brings Speed to 0 (ChangeSpeed sets SpeedChanged=true then returns 'The shuttle car comes to a stop...'), Move() still runs (SpeedChanged true), prints 'The shuttle car continues to move. The display blinks, and now reads 0.' and does TunnelPosition++. Same turn shows both stop and continue-move, and advances one position while display reads 0. Confirmed in origin/main c31e9ec (lines 174/271-274/296). Pending user confirm on intended decel-to-stop behavior.
