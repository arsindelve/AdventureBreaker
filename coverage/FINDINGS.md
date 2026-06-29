# AdventureBreaker durable findings

_Generated 2026-06-29T22:29:08Z · 41 finding(s)_

## AB-007 [HIGH] god mode (LoadAllItems/LoadAllLocations) rebuilds the repository without Init(), returning empty containers and discarding live state  · _open_

- game `planetfall` · area `MECH:god-mode` · category `puzzle-step` · target_sha `c31e9ec`
- command: `god mode take cardboard box -> empty box`

Repository.LoadAllItems and LoadAllLocations do '_allItems = new Dictionary<...>()' then create each instance via Activator.CreateInstance WITHOUT calling Init(). god mode Take/Go call these, so: (1) items come back missing their Init() state - 'god mode take cardboard box' yields an EMPTY box on a fresh prod run, though CardboardBox.Init StartWithItemInside GoodBedistor + K/BSeriesMegafuse + CrackedFromitzBoard; (2) replacing the dict discards the restored state of all other items/locations, scrambling an in-progress session. This also caused the false-alarm 'bedistor noun collision' (god mode's Repository.GetItem lacks the precise-matching/disambiguation the real SimpleInteractionEngine parser uses, so it grabbed the fused bedistor). Blocks god-mode-based testing of any Init-dependent item/container. Fix: call Init() on created instances and/or only add missing types instead of replacing the dict (mirror GetLocation<T> which Init()s on creation). Verified in origin/main c31e9ec.

## AB-015 [HIGH] Production HTTP 500 on multi-clause input 'look examine bulkhead open bulkhead'  · _open_

- game `planetfall` · area `Deck Nine / multi-clause command parsing (production)` · category `other` · target_sha `unknown`
- command: `look examine bulkhead open bulkhead`

Black-box: against Planetfall prod (6kvs9n5pj4...), the single input 'look examine bulkhead open bulkhead' returns HTTP 500 (no body) from a FRESH session at move 0, reproducibly. The server should return a graceful parse failure, not a 500. Near-variants all return 200: 'examine bulkhead open bulkhead', 'look open bulkhead', 'look examine bulkhead open', 'x bulkhead open bulkhead', 'look examine examine examine' -> all 200. Trigger is the specific 3-clause shape look + examine<noun> + open<noun>.

## AB-019 [HIGH] Addressing an absent talkable NPC (Floyd/Blather/Ambassador) leaks into player actions instead of 'not here'  · _open_

- game `planetfall` · area `ConversationHandler / absent talkable NPC direct-address` · category `npc-conversation` · target_sha `unknown`
- command: `floyd|blather|ambassador, <go up|drop diary|take brush> while absent`

ConversationHandler.CollectTalkableEntities only gathers ICanBeTalkedTo characters from inventory + current room. When the named NPC is absent, FindTargetCharacter returns null, the whole input falls through to normal command parsing, and the PLAYER executes the command. State-affecting: 'floyd/blather/ambassador, go up' moves the PLAYER; 'X, drop diary' drops the PLAYER's item; 'X, take brush' -> 'You already have that!'; 'floyd, sing' even hallucinates Floyd singing while absent. The game should always know these named characters and, if addressed while absent, say 'X isn't here.' This is the absent-case gap left by #182 (which handles direct-address only when present). Confirmed for all three ICanBeTalkedTo NPCs.

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

## AB-014 [MEDIUM] Bio-lock: 'Floyd is dead' message repeats every turn after he dies trapped in the lab  · _open_

- game `planetfall` · area `Bio Lock East / Floyd lab sacrifice (non-solution: never reopen door)` · category `puzzle-step` · target_sha `unknown`
- command: `wait (x3) in BioLockEast with LabSequenceState=NeedToReopenDoor`

If the player never reopens the bio-lock door after Floyd knocks (NeedToReopenDoor state), Floyd dies. But the state stays NeedToReopenDoor and BioLockEast remains a registered actor, so the death announcement 'You hear a final metallic scream... Floyd is dead.' re-fires on EVERY subsequent turn forever. Player-death non-solution paths are clean (DeathProcessor resets); only this Floyd-dies path leaks.

## AB-018 [MEDIUM] 'enter pod' (valid noun) routed to generic refusal/mock instead of 'bulkhead is closed'  · _open_

- game `planetfall` · area `Deck Nine / enter-pod routing (EnterSubLocationEngine)` · category `parser-pronoun` · target_sha `unknown`
- command: `enter pod / board pod (Deck Nine)`

The escape pod is a valid object: BulkheadDoor.NounsForMatching includes 'pod'. But 'enter/board pod' is classified as an EnterSubLocationIntent, and EnterSubLocationEngine only enters ISubLocation items. 'pod' resolves (via GetItemInScope) to BulkheadDoor, which is NOT an ISubLocation, so the engine emits a generic 'cannot go that way' -> the narrator renders it as a mock of a valid command. With the bulkhead closed it should say 'The escape pod bulkhead is closed.' The 'escape pod door' phrasing works; bare 'pod' does not.

## AB-020 [MEDIUM] 'it' loses its antecedent across movement in production (Planetfall 1.6.2) despite #248  · _open_

- game `planetfall` · area `Production pronoun resolution / it-across-movement (Planetfall)` · category `parser-pronoun` · target_sha `unknown`
- command: `take X; examine it; <move>; drop it`

In prod 1.6.2: take <item>; examine it (resolves correctly); <move>; drop it -> 'You don't have that!' / 'invisible <room>' - the carried-item antecedent is lost after a move. #248/#253 claimed to fix exactly this and its deterministic UnitTest (PronounResolutionTests.It_AfterMove_StillResolves_ForCarriedItem, Zork lantern) PASSES on main - so the engine seam works, but the production path (AI pronoun resolver and/or per-turn processing) does not honor the preserved LastNoun. Reproduced 3x cleanly (Feinstein intro AND calm Mech area). 'it' WITHOUT a move resolves fine (take brush; examine it; drop it -> Dropped).

## AB-023 [MEDIUM] Physical verbs on the bulkhead (knock/hit/kick/bang) return an EMPTY response  · _open_

- game `planetfall` · area `Deck Nine / bulkhead unhandled-verb empty response` · category `other` · target_sha `unknown`
- command: `knock on the bulkhead`

On Deck Nine, 'knock on the bulkhead', 'bang on the bulkhead', 'hit the bulkhead', 'kick the bulkhead' all return a completely empty response (blank line) - in prod AND white-box (len=0). Bare 'knock' works ('Your knuckles rap against the air...') and 'knock on the wall' works (narrator flavor). The difference: 'bulkhead' resolves to the real BulkheadDoor object, so the engine routes the verb to it, gets an empty/handled-but-blank result, and returns that instead of falling through to the narrator the way the no-object 'wall' case does. The bulkhead is the prominently-described escape-pod door, so this is a very natural thing for a new player to type.

## AB-024 [MEDIUM] 'look at <noun>' returns the room description instead of examining the noun  · _open_

- game `planetfall` · area `Deck Nine / global` · category `parser-routing` · target_sha `unknown`
- command: `look at uniform`

## AB-025 [MEDIUM] look-at issue filed  · _filed#283_

- game `planetfall` · area `global` · category `parser-routing` · target_sha `unknown`
- command: `look at uniform`

## AB-026 [MEDIUM] Bare quoted speech and untargeted 'say' don't route to the present ITalkable NPC  · _open_

- game `planetfall` · area `Reactor Lobby / conversation` · category `conversation-routing` · target_sha `unknown`
- command: `"you are a fool"`

## AB-027 [MEDIUM] AB-026 filed  · _filed#284_

- game `planetfall` · area `conversation` · category `conversation-routing` · target_sha `unknown`
- command: `"you are a fool"`

## AB-032 [MEDIUM] Admin Corridor South: examining ANY object returns the crevice description (catch-all swallows all examines)  · _open_

- game `planetfall` · area `Planetfall/Admin Corridor South` · category `location-examine-override` · target_sha `unknown`
- command: `examine chronometer`

## AB-033 [MEDIUM] Admin Corridor South examine catch-all — filed  · _filed#291_

- game `planetfall` · area `Planetfall/Admin Corridor South` · category `location-examine-override` · target_sha `unknown`
- command: `examine chronometer`

## AB-036 [MEDIUM] Rift: 'place ladder across rift' narrates losing the ladder even when you don't have one  · _open_

- game `planetfall` · area `Planetfall/Admin Corridor (rift)` · category `stale-state-message` · target_sha `unknown`
- command: `place ladder across rift`

## AB-037 [MEDIUM] Rift bridging: 'place/put ladder across rift' narrates losing a ladder that isn't there  · _open_

- game `planetfall` · area `Admin Corridor` · category `stale-state-message` · target_sha `unknown`
- command: `place ladder across rift (with no ladder in game)`

At Admin Corridor (rift), with NO ladder anywhere (not in inventory, not in room - confirmed via look), 'place ladder across rift' AND 'put ladder across rift' both print 'The ladder, far too short to reach the other edge of the rift, plunges into the rift and is lost forever.' By contrast 'extend ladder' correctly says it's not here, and 'take ladder' correctly refuses. The rift-bridge handler runs its too-short/lost branch unconditionally without checking ladder presence. Also: 'throw ladder across rift' loses a ground (un-held) ladder, and the un-extended ladder is lost permanently on place with no warning (rift is on critical path = softlock).

## AB-039 [MEDIUM] Magnet puzzle: canonical 'get key with magnet' unrecognized; narrator falsely calls the steel key 'non-magnetic', steering players off the solution  · _open_

- game `planetfall` · area `Admin Corridor South` · category `narrator-contradiction` · target_sha `unknown`
- command: `get key with magnet`

At Admin Corridor South, only 'put/place/hold magnet on/over/beside crevice' solves the key puzzle (AdminCorridorSouth.cs:80-81). The original's canonical solve 'get/take/attract key WITH magnet' (key as target, magnet as tool; ZIL KEY-F compone.zil:980-982) is unrecognized and falls through to the AI narrator, which improvises a refusal asserting the key is 'stubbornly non-magnetic' - a direct CONTRADICTION of the puzzle's own success text ('a piece of metal leaps from the crevice and affixes itself to the magnet. It is a steel key!'). Also 'put magnet in crevice' (natural phrasing of the actual solution) gets an AI refusal calling it useless. Net: the narrator tells the player the correct approach won't work and states a false fact about the key.

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

## AB-012 [LOW] Library computer terminal processes keypresses while turned off (menu navigates with screen dark)  · _filed#252_

- game `planetfall` · area `MECH:computer-terminal` · category `puzzle-step` · target_sha `8175684`
- command: `terminal off; type 1 -> MenuState MainMenu->HistoryMenu (IsOn still false)`

ProcessKeyPress + both RespondTo* entry points have no IsOn guard, unlike Examination/ReadDescription. Verified by object inspection. Diary (15-page read/advance/rewind) and spool reader (insert/read green&red/already-a-spool/doesn't-fit) are clean. Deployed main 8175684. Filed zork#252.

## AB-013 [LOW] Floyd activation points re-awarded each turn during wake-up countdown  · _filed#254_

- game `planetfall` · area `RobotShop / Floyd (power activation scoring)` · category `puzzle-step` · target_sha `unknown`
- command: `activate floyd; activate floyd; activate floyd (at RobotShop)`

Turning Floyd on is worth 2 points once, but the award was guarded on HasEverBeenOn, which only flips when Floyd finishes his 3-turn wake-up countdown. Each repeated 'activate floyd' during the countdown re-awarded +2 (score 2->4->6), letting a player farm +4. Fixed with a dedicated one-shot HasAwardedActivationPoints flag.

## AB-017 [LOW] Narrator breaks Infocom character on failed actions (anachronistic/meta, invents 'Podthat')  · _open_

- game `planetfall` · area `Reactor Lobby / Brig (narrator character-break)` · category `character-break` · target_sha `unknown`
- command: `enter pod (Reactor Lobby); press red button (Brig)`

The AI narrator (product promise: 'never breaks character') produces anachronistic, fourth-wall/meta responses for failed actions instead of the terse 1983 Infocom register. 'enter pod' at Reactor Lobby (no pod present) yields: 'trying to teleport are we? your sci-fi wizardry is malfunctioning', and on repeats invents a capitalized fictional object 'the mysterious and elusive Podthat' / 'the mythical Podthat'. 'press red button' in the Brig yields 'as much effect as a space heater on a sun'. A period-correct engine would say e.g. 'I do not see any pod here.'

## AB-021 [LOW] 'show printout to floyd' should set Floyd's computer concern (gates bio-lab foray) - missing in C#  · _open_

- game `planetfall` · area `Lawanda / Floyd computer concern (bio-lab foray gate)` · category `puzzle-step` · target_sha `unknown`
- command: `show printout to floyd`

ZIL gates Floyd's offer to fetch the mini-card (the bio-lab sacrifice) on COMPUTER-FLAG (comptwo.zil:1751-1758). COMPUTER-FLAG is set by COMPUTER-ACTION (comptwo.zil:1514, Floyd's 'Uh oh. Computer is broken' line) from TWO triggers: (1) Floyd present in the Computer Room (compone.zil:2291-2293) and (2) 'show printout to floyd' (compone.zil:2024-2026, SHOW verb on PRINT-OUT). The C# only implements trigger (1) - FloydHasExpressedConcern is set solely in ComputerRoom.cs:64. The printout IS modeled (ComputerOutput, nouns output/computer output/printout), but showing it to Floyd does nothing, so a player using the original's show-printout path can't unlock the foray. Primary room-visit gate is faithful.

## AB-028 [LOW] Intermittent: named direct-address to present ITalkable deflects to narrator (~3%, nondeterministic)  · _open_

- game `planetfall` · area `Reactor Lobby / conversation` · category `conversation-routing` · target_sha `unknown`
- command: `blather, what should i do now`

## AB-029 [LOW] AB-028 filed  · _filed#286_

- game `planetfall` · area `conversation` · category `conversation-routing` · target_sha `unknown`
- command: `blather, what should i do now`

## AB-034 [LOW] Magnet/crevice: 'put magnet IN crevice/crack' rejected; only on/over/beside/next-to work  · _open_

- game `planetfall` · area `Planetfall/Admin Corridor South` · category `parser-preposition` · target_sha `unknown`
- command: `put magnet in crack`

## AB-035 [LOW] Magnet/crevice: 'put magnet IN crevice/crack' rejected; only on/over/beside/next-to work  · _open_

- game `planetfall` · area `Planetfall/Admin Corridor South` · category `parser-preposition` · target_sha `unknown`
- command: `put magnet in crack`

## AB-038 [LOW] Rift: 'place ladder across rift' when it already spans re-narrates success and re-adds the ladder to Admin Corridor North  · _open_

- game `planetfall` · area `Admin Corridor` · category `state-integrity` · target_sha `unknown`
- command: `place ladder across rift (when already spanning)`

AdminCorridor.cs RespondToMultiNounInteraction has no 'already spans the rift' guard (ZIL compone.zil:699 checks LADDER-FLAG first). Placing the ladder again when IsAcrossRift==true re-runs the extended branch: prints the success message again AND calls GetLocation<AdminCorridorNorth>().Items.Add(ladder) a second time, duplicating the ladder in that room's Items list. ZIL says 'The ladder already spans the rift.'

## AB-040 [LOW] Aragain Falls DOWN uses generic movement failure instead of canonical long-way message  · _filed#335_

- game `zork` · area `Aragain Falls` · category `movement` · target_sha `unknown`
- command: `down`

At Aragain Falls in Zork prod, DOWN returns generic "You cannot go that way." instead of the original room-specific "It's a long way...". Root cause: ZorkOne/Location/AragainFalls.cs maps only N and conditional W, so Direction.Down falls through to generic movement failure. Original ZIL zork1/1dungeon.zil:2319-2328 defines DOWN "It's a long way...".

## AB-041 [LOW] Rainbow local-global object is missing LOOK-UNDER and THROUGH behavior  · _filed#336_

- game `zork` · area `Aragain Falls` · category `examine-scenery` · target_sha `unknown`
- command: `look under rainbow`

At Aragain Falls in Zork prod, the room description exposes a rainbow and cross rainbow is handled, but look under rainbow returns generic no-effect and through/go through rainbow returns generic movement failure. Root cause: ZorkOne/Location/AragainFalls.cs hard-codes only cross rainbow, with no Rainbow local/global object/action handler. Original ZIL defines RAINBOW in LOCAL-GLOBALS, includes it in ARAGAIN-FALLS, and RAINBOW-FCN handles LOOK-UNDER plus CROSS/THROUGH.

## AB-016 [INFO] UNREPRODUCED: harness session showed moves reset 11->0 (Deck Nine) after 'drop brush'  · _open_

- game `planetfall` · area `Gangway / session-state durability (prod)` · category `other` · target_sha `unknown`
- command: `drop brush (after a session that ate multiple 500s)`

During a prodplay session that had absorbed several HTTP 500s, a 'drop brush' turn at Gangway came back as a fresh game (Deck Nine, moves=0). Could NOT reproduce in controlled fresh sessions: drop brush alone -> 'Dropped' clean; firing the 500 mid-session did NOT corrupt/reset state. Logging as an unreproduced anomaly to watch; NOT filing a GH issue without a deterministic repro.

## AB-022 [INFO] Testing limitation: god-mode 'go' bypasses BeforeEnterLocation (no actor registration / Floyd follow); unstable during intro  · _open_

- game `planetfall` · area `God mode / actor registration (testing affordance)` · category `other` · target_sha `unknown`
- command: `god mode go bio lock east; wait (no state machine)`

GodModeProcessor.Go just sets context.CurrentLocation; it never calls BeforeEnterLocation, so the destination's ITurnBasedActor isn't registered and Floyd is not moved. Actor-driven sequences can't be reached by teleport alone: BioLockEast's sacrifice state machine never runs and ComputerRoom never sets FloydHasExpressedConcern. Also, teleporting during the unfinished Feinstein intro is unstable - the explosion timer is still queued and snaps the player back toward Deck Nine (also corrupts a save taken in that state). Fromitz works via teleport because it's driven by Floyd conversation metadata, not a location actor. Reliable prod path for the sacrifice = narrator-on walkthrough replay (survival off). Possible affordance: have god-mode 'go' run BeforeEnterLocation/actor registration + bring followers.

## AB-030 [INFO] 1.6.4 verified in prod: #286 (no-comma NPC address) + #284 (nameless speech routing) FIXED  · _filed#286_

- game `zork` · area `Planetfall/ConversationHandler` · category `regression-verify` · target_sha `unknown`

## AB-031 [INFO] 1.6.4 verified: #285 egg force-open with weapon (prod) + #281 Stream rooms (white-box)  · _filed#285_

- game `zork` · area `ZorkOne/Reservoir+egg` · category `regression-verify` · target_sha `unknown`
