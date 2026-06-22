# AdventureBreaker durable findings

_Generated 2026-06-22T17:06:04Z · 11 finding(s)_

## AB-007 [HIGH] god mode (LoadAllItems/LoadAllLocations) rebuilds the repository without Init(), returning empty containers and discarding live state  · _open_

- game `planetfall` · area `MECH:god-mode` · category `puzzle-step` · target_sha `c31e9ec`
- command: `god mode take cardboard box -> empty box`

Repository.LoadAllItems and LoadAllLocations do '_allItems = new Dictionary<...>()' then create each instance via Activator.CreateInstance WITHOUT calling Init(). god mode Take/Go call these, so: (1) items come back missing their Init() state - 'god mode take cardboard box' yields an EMPTY box on a fresh prod run, though CardboardBox.Init StartWithItemInside GoodBedistor + K/BSeriesMegafuse + CrackedFromitzBoard; (2) replacing the dict discards the restored state of all other items/locations, scrambling an in-progress session. This also caused the false-alarm 'bedistor noun collision' (god mode's Repository.GetItem lacks the precise-matching/disambiguation the real SimpleInteractionEngine parser uses, so it grabbed the fused bedistor). Blocks god-mode-based testing of any Init-dependent item/container. Fix: call Init() on created instances and/or only add missing types instead of replacing the dict (mirror GetLocation<T> which Init()s on creation). Verified in origin/main c31e9ec.

## AB-002 [MEDIUM] Death scatters nothing: player keeps full (lit) inventory through resurrection  · _open_

- game `zork` · area `MECH:death-resurrection` · category `death` · target_sha `c31e9ec`
- command: `enter Troll Room and die`

Known engine TODO at DeathProcessor.cs:29-31. Not yet filed as issue.

## AB-003 [MEDIUM] GET / POST early-exit responses return empty inventory while items held  · _fixed#232_

- game `zork` · area `MECH:get-post-parity` · category `get-post-parity` · target_sha `c31e9ec`
- command: `GET state; pronoun 'What item are you referring to?'`

Filed zork#230; fixed by PR #232 (merged). Prod not yet redeployed.

## AB-008 [MEDIUM] Take/drop/put ignores adjectives: 'take the good bedistor' acts on the fused one (examine resolves correctly)  · _filed#244_

- game `planetfall` · area `MECH:planetary-defense-bedistor` · category `parser-pronoun` · target_sha `341a64b`
- command: `take the good bedistor (only fused in scope) -> 'fused to its socket'`

GetItemInScope->HasMatchingNoun uses containment fallback + location-first + no adjective precision; examine's MatchNounAndAdjective resolves correctly, so the paths disagree. Blocks natural puzzle phrasing ('put good bedistor in cube' -> 'you don't have the fused bedistor'). Verified deployed main 341a64b. Filed zork#244.

## AB-009 [MEDIUM] Multi-noun (put/give/slide X in/to Y) still adjective-blind after #244; resolves 'kitchen card' to wrong card  · _filed#246_

- game `planetfall` · area `MECH:access-cards` · category `parser-pronoun` · target_sha `8175684`
- command: `context.HasMatchingNoun('kitchen card') with [shuttle,kitchen] -> Shuttle`

MultiNounEngine.IsItemHere uses raw context/location.HasMatchingNoun (containment, no precise), not the fixed GetItemInScope. #244 patched only single-noun. Confirmed deterministic @8175684. Filed zork#246.

## AB-010 [MEDIUM] Pronoun antecedent too limited: 'them' never resolves to a collection; movement clears 'it' for carried items  · _filed#248_

- game `planetfall` · area `MECH:pronouns` · category `parser-pronoun` · target_sha `8175684`
- command: `take all; drop them -> 'What item are you referring to?'`

context.LastNoun is a single noun string; 'them' only honored for a single IPluralNoun (no collection). MoveEngine/MultiNounEngine clear LastNoun unconditionally, so 'take lantern; east; drop it' fails though still carried. Deterministic @8175684. Filed zork#248.

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

## AB-011 [LOW] GET rehydrate returns previousLocationName=null (POST returns it)  · _filed#250_

- game `zork` · area `MECH:get-post-parity` · category `get-post-parity` · target_sha `8175684`
- command: `POST move E -> prev='Behind House'; GET -> prev=null`

PreviousLocationName is a GameEngine field (private set, set during a turn at GameEngine.cs:257), not on Context, so RestoreGame doesn't restore it and the no-turn GET path returns null. lastMovementDirection (=>Context) is fine. Same family as #230/#238. Confirmed prod 8175684. Filed zork#250.
