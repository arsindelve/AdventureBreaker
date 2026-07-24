# AdventureBreaker durable findings

_Generated 2026-07-24T13:57:39Z · 85 finding(s)_

## AB-047 [CRITICAL] Planetfall prod: session fully resets (moves/inventory/time revert to near-initial) after ~14 consecutive wait/idle commands  · _open_

- game `planetfall` · area `MECH:consecutive-wait-session-reset` · category `other` · target_sha `unknown`
- command: `new --game planetfall --target prod; then quiet wait x14 (any idle command repeated)`

Black-box against Planetfall prod (6kvs9n5pj4...): sending consecutive wait-type (idle, no state-changing) commands to a session causes the server to silently and completely reset that session's authoritative state back to near-initial values -- moves reverts to 0, time= drops back close to its session-start value, and any acquired ambient item (the ship's 'brochure', normally picked up automatically a few turns in) disappears from inv=. The response is still HTTP 200 with plausible flavor text ('Time passes...'), so nothing signals to a client/player that a reset occurred. Reproduced deterministically 3 times on independent fresh sessions: (1) 300x identical 'wait' (quiet/noGeneratedResponses) -> resets at request #14, 28, 42, 56 ... 294, i.e. every single multiple of 14 with zero exceptions across 21 cycles; (2) 60x alternating 'wait'/'z' (different literal text, same in-fiction effect) -> resets again at exactly 14, 28, 42, 56, ruling out a naive identical-text dedup/idempotency-key explanation; (3) 24x 'wait' with a forced 4-second gap between every call -> reset still lands at exactly request #14 (72s elapsed, vs ~15-20s elapsed to reach #14 in the unpaced tests), ruling out a simple wall-clock TTL/cache-expiry explanation -- the trigger is the COUNT of consecutive idle commands, not elapsed time. Contrast case: sessions replaying the real walkthrough (movement, taking items, puzzle actions, with only short wait streaks of <=10 interspersed with substantive actions) reached move ~150+ cleanly with no reset anywhere near move 14, so this is not simply 'every Nth HTTP request to any session' -- it specifically implicates long streaks of consecutive wait/no-op commands. This directly collides with legitimate gameplay: Planetfall's own walkthrough requires riding the Alfie/Betty shuttle to reach the Lawanda Complex, which is a scripted sequence of ~20 consecutive 'wait' commands (spine steps ~179-200 in adventurebreaker/spine/planetfall.json) with no opportunity to interleave other actions without breaking the shuttle sequence. Every attempt this session to spine-run through that exact stretch reset back to Deck Nine (score 0, starting inventory) partway through the wait run, consistent with this bug being the root cause of that navigation failure.

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

## AB-046 [HIGH] Meta-verbs (score/look/inventory) incorrectly consume a game turn, accelerating the survival clock into unwarranted deaths  · _open_

- game `planetfall` · area `Engine-wide / turn-clock daemon (affects every room)` · category `turn-accounting` · target_sha `unknown`
- command: `score (or look, or inventory) — any location, narrator off or on`

In authentic Infocom parsers, SCORE, LOOK, and INVENTORY are free meta-verbs that never advance the in-game clock or move counter. In this engine, every one of these commands advances moves by exactly 1 and the in-game time field by exactly 54 ticks, identical to a real action (take/open/movement). Reproduced cleanly in an isolated fresh session (narrator OFF, ruling out narrator involvement): moves=0 time=4558 at game start; after 'score' -> moves=1 time=4671; after 'look' -> moves=2 time=4725 (+54); after 'inventory' -> moves=3 time=4779 (+54); after 'score' again -> moves=4 time=4833 (+54). The in-game clock is confirmed turn-based (fixed +54/turn), not wall-clock, so this directly desyncs pacing from the intended design. Concretely observed harm: replaying the engine's own verified golden walkthrough (extracted from its NUnit test fixtures) command-for-command on live prod, the walkthrough calls 'score' 7 times before a certain point (completely normal player behavior) -- those 7 'free' checks silently burned 7 extra turns (378 extra clock ticks) the golden path never accounts for. The very next scripted command (moving W into the Large Office, which the golden path expects to be safe) instead triggered an unavoidable sleep/hunger death ('...you awake as several ferocious beasts...You have died') that does not occur in the verified walkthrough. A real player who does nothing wrong beyond periodically checking their score/inventory/surroundings can be killed by survival-clock drift entirely outside their control.

## AB-048 [HIGH] Systemic: three call sites re-derive item-possession/interaction-handled with an inconsistent, wrong check instead of trusting the resolver that already got it right  · _filed#362_

- game `planetfall` · area `Systems Corridor West + Robot Shop / systemic: no canonical possession/InteractionHappened check (drop, Floyd-holding-item, nested-container give/show)` · category `take-drop-scope` · target_sha `094a1ed`
- command: `drop board (wrong item resolved); give brush to floyd then examine floyd (short-circuited); show id card to floyd with card still in worn uniform (false denial)`

Consolidates AB-049 and AB-050 (same root cause, filed separately in error - see their entries). Three independent call sites each have a correct signal available (Repository.GetItemInScope's accessibility walk, or InteractionResult.InteractionHappened) but re-derive the answer with a different, narrower, wrong check: (1) TakeOrDropInteractionProcessor.GetItemsToDrop falls back to the unscoped global Repository.GetItem(noun) instead of GetItemInScope, so single-item drop can resolve to the wrong same-named item (6+ FromitzBoardBase-derived items share generic 'board'/'fromitz board' nouns) - drop all and examine both resolve correctly via GetItemInScope, only single-item drop is broken. (2) Floyd.cs:230-235 checks 'result is not null' instead of 'result.InteractionHappened' when delegating to ItemBeingHeld, so a NoNounMatchInteractionResult (non-null, InteractionHappened=false) from the held item short-circuits Floyd's own examine/search/oil/social-verb handling whenever he holds anything. (3) GiveSomethingToSomeoneDecisionEngine.cs:49 and Floyd.cs:377 (RespondToShow) check flat context.Items.Contains(thing) instead of trusting the accessibility walk GetItemInScope already did, so items nested in a worn container (e.g. the starting ID card) falsely fail GIVE/SHOW. Proposed systemic fix: introduce a canonical Repository.IsItemPossessedBy(item, context) helper (possession-terminates-at-player variant of the existing IsItemAccessible walk) and use it in (1) and (3); fix (2) to check .InteractionHappened, matching the established pattern in SimpleInteractionEngine.cs.

## AB-049 [HIGH] Floyd unresponsive to examine/search/oil/social verbs whenever holding an item  · _duplicate-of-AB-048#362_

- game `planetfall` · area `Robot Shop / Floyd (ItemBeingHeld interaction short-circuit)` · category `character-break` · target_sha `094a1ed`
- command: `give brush to floyd; examine floyd`

Floyd.cs:230-235 short-circuits on 'result is not null' when delegating simple interactions to ItemBeingHeld, but ItemBase.RespondToSimpleInteraction returns a non-null NoNounMatchInteractionResult (InteractionHappened=false) when the noun doesn't match the held item. Floyd treats that negative result as final, skipping his own social responses, search, and ICanBeExamined description. Confirmed by toggling: give item -> broken; take item back -> fixed.

## AB-051 [HIGH] Asking Floyd for the fromitz board again rips it back out of an already-solved planetary-defense panel  · _filed#360_

- game `planetfall` · area `Repair Room + Planetary Defense / Floyd fromitz board retrieval (unconditional re-grant, confirmed un-installs a solved puzzle)` · category `puzzle-step` · target_sha `094a1ed`
- command: `put shiny in panel (solve puzzle, score 42->48); floyd, take board (re-ask) -> board yanked from panel, panel empty, score still 48`

FloydLocationBehaviors.HandleFromitzBoardRetrieval calls context.ItemPlacedHere<ShinyFromitzBoard>() unconditionally, gated only on the narration text, not the state mutation. Take()'s only duplicate guard checks context.Items, not the item's actual CurrentLocation, so it silently relocates the board from wherever it currently is the moment Floyd is re-asked, while the narration ('Floyd already did that') implies nothing happened. CONFIRMED escalation: installed the board into FromitzAccessPanel (put shiny in panel -> puzzle solved, score 42->48), then re-asked Floyd -> the installed board was ripped back into inventory; returning to Planetary Defense confirmed the panel socket now empty (second fromitz board gone from actions@loc) while score stayed at 48 (not reverted) -- a solved, scored puzzle silently un-solved by an ordinary companion interaction. Originally also confirmed for the simpler case: board dropped in a different room teleports back on re-ask.

## AB-052 [HIGH] Floyd's scripted "I'll get the mini card" Bio Lab sequence never fires when he's actually turned on (BioLockStateMachineManager silently dead)  · _filed#365_

- game `planetfall` · area `Bio Lock East / Floyd bio-lab card-retrieval sequence` · category `character-break` · target_sha `unknown`
- command: `god mode go computer room; show/examine to set FloydHasExpressedConcern via real dialogue; walk normally into Bio Lock East; wait x11+ -- NeedToGetCard/LookAMiniCard never fires`

Confirmed live on prod (session floyd-hunt8, continuous single session, no restore involved): entered Bio Lock East normally (walked E from Bio Lock West), with Floyd present, genuinely turned on (verified via successful conversation -- Floyd.OnBeingTalkedTo returns a real AI response only when IsOn is true), and ComputerRoom.FloydHasExpressedConcern already legitimately true (set two rooms earlier via the real 'computer is broken' dialogue firing in Computer Room). Waited 11+ consecutive turns (wait/examine/conversation, moves 282-292) in Bio Lock East -- BioLockStateMachineManager.HandleTurnAction's scripted dialogue (LookAMiniCard / NeedToGetCard, the trigger that tells the player to open the door so Floyd can rush in and retrieve the miniaturization card) never appeared once. 'god mode where floyd' confirmed floyd.CurrentLocation correctly reports Bio Lock East, ruling out a stale-reference location mismatch. White-box: HandleTurnAction's only gate before the dialogue branches is '(!isFloydInLab && !isFloydHereAndOn) return empty' -- both preconditions were independently confirmed true, yet the dialogue still never fires. Root-cause candidate found via test-suite audit: BioLockEast is normally a SEPARATE registered ITurnBasedActor from Floyd (Context.RegisterActor(floyd) fires from FloydPowerManager.Activate whenever a player actually types 'activate floyd', which is how every real game reaches this room), so both actors run in the same ProcessActors() loop every turn in production. But EVERY test that exercises this state machine -- all of BioLockEastTests.cs AND the end-to-end WalkthroughBioLock.cs -- sets floyd.IsOn = true by direct property assignment instead of going through real activation, so Floyd is NEVER actually registered as an actor in any existing test; only BioLockEast is. BioLockEastTests.cs even has an explicit comment admitting the gap: 'The integration has an issue where both Floyd and BioLockEast act on the same turn causing double-incrementing of counters... Individual tests verify correctness' -- i.e. the known interaction bug between the two actors was routed around in tests rather than fixed. Net effect: this scripted, 2-point, dramatic 'Floyd sacrifices himself in the Bio Lab' set piece -- one of Floyd's signature moments in the whole game -- is unreachable via its intended trigger in real play. A player gets no in-fiction prompt to open the door; if they guess to open it blind (the only remaining way to discover the sequence exists), HandleDoorOpening's fallback branch kills them instantly ('Opening the door reveals a Bio-Lab full of horrible mutations... the mutations march into the bio-lock and devour you'). Could not run the C# test suite locally to pin the exact line-level mechanism (no dotnet SDK in this harness environment) -- filing on the strength of the live reproduction plus the corroborating test-suite gap and self-admitted known-issue comment.

## AB-061 [HIGH] Machine Shop dispenser: acid/base buttons unpressable by printed labels (narrator falsely narrates dispense), both collapse to 'clear', 'white' miswired to brown  · _filed#419_

- game `planetfall` · area `Machine Shop` · category `parser-pronoun` · target_sha `68f90e8`
- command: `press acid button`

Machine Shop chemical dispenser: the two white buttons the room labels 'BAAS' (base, square) and 'ASID' (acid, round) can only be pressed by SHAPE (press square/round button), not by their printed labels. 'press acid button'/'press base button'/'press asid'/'press baas'/'press white button' all -> 'no effect' (quiet); with narrator ON, 'press acid button' FALSELY narrates 'the dispenser obliges by releasing a stream of liquid into the flask' while the flask stays empty. Both white buttons map to Click('clear') so base and acid are indistinguishable. 'white' is miswired to the brown button. Root cause: MachineShop.cs:97-109 noun switch (no acid/base/asid/baas cases; line 103 'brown button' or 'white' => Click('brown'); lines 106-107 both square+round => Click('clear')). Same false-narration pattern as AB-058/#412, distinct instance. Also (god-mode artifact, NOT a bug): 'set laser to N' gave no effect under a god-mode-contaminated laser; spine confirms it works in normal play.

## AB-081 [HIGH] Disambiguation 'Which X do you mean?' is unanswerable in prod — pending DisambiguationProcessor not persisted across stateless per-request boundary  · _fixed#472_

- game `planetfall` · area `Engine / disambiguation (all games)` · category `disambiguation-unanswerable` · target_sha `e795f32`
- command: `examine bedistor; good bedistor`

GameEngine.cs:54 _processorInProgress is an in-memory field (set :908), never serialized into session state (grep: no save/restore). Deployed game is stateless (base64 session, engine rebuilt per request) so the pending disambiguation is null on the answer request -> answer parsed as fresh command. Prod e795f32: 'examine bedistor' prompts, then good/good bedistor/good ninety-ohm bedistor all -> 'no effect'; cards: id/id card/kitchen access card fail, kitchen/shuttle -> movement hijack. Control: white-button 'square' identical with/without prompt (processor never consulted). Direct full command works. Affects cards/bedistors/megafuses/'it'-clarification, both games. Fix: persist pending disambiguation in session. ZIL-independent.

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

## AB-040 [MEDIUM] Timber Room west exit silently fails with empty response when player carries any item  · _open_

- game `zork` · area `Timber Room / narrow passage` · category `movement` · target_sha `unknown`
- command: `W`

W exit is listed in the exits envelope for Timber Room (exits=['E','W']) but attempting to go west while carrying any item produces a completely blank response in both narrator-on and narrator-off mode. No 'can't go' message, no 'too narrow' message, nothing. Engine consumes a turn but location doesn't change. Moving west works when the player carries nothing (empty-handed). Reproduced from Mine Entrance → W → N (bat) → Down → Down → W (Timber Room) → W.

## AB-041 [MEDIUM] Timber Room west exit silently fails with empty response when player carries any item  · _filed#309_

- game `zork` · area `Timber Room / narrow passage` · category `movement` · target_sha `unknown`
- command: `W`

W exit listed in exits envelope but produces blank response + consumed turn when player carries anything. Works empty-handed (reaches Drafty Room).

## AB-042 [MEDIUM] Bat Room: vampire bat description appears twice on room entry (engine duplicates NPC text)  · _open_

- game `zork` · area `Bat Room` · category `examine-scenery` · target_sha `unknown`
- command: `N (into Bat Room from Squeaky Room)`

Entering the Bat Room from the south causes the vampire bat description to appear twice in both quiet and narrator-on mode. The line 'In the corner of the room on the ceiling is a large vampire bat who is obviously deranged and holding his nose.' is printed twice consecutively. Reproducible on every entry. The 'look' command with narrator ON shows it only once (narrator rewrites full room description, masking the duplicate), but movement-triggered room entry shows both copies. The garlic held by the player causes the bat to stay (correct mechanic) but the description is still doubled.

## AB-043 [MEDIUM] Bat Room: vampire bat description appears twice on room entry (engine duplicates NPC text)  · _filed#311_

- game `zork` · area `Bat Room` · category `examine-scenery` · target_sha `unknown`
- command: `N (into Bat Room from Squeaky Room)`

Bat NPC description doubled on movement entry. look with narrator ON shows once (masks bug). Narrator-on movement shows doubled text.

## AB-044 [MEDIUM] "look at <noun>" returns room description instead of examining (Zork I)  · _filed#312_

- game `zork` · area `global / look-at parser` · category `examine-scenery` · target_sha `unknown`
- command: `look at mailbox`

look at <single-word noun> collapses to bare room look. look at <two-word noun> works. examine always works. Same root cause as #283 (Planetfall).

## AB-050 [MEDIUM] GIVE/SHOW falsely say 'You don't have the X!' for items nested in a worn container  · _duplicate-of-AB-048#362_

- game `planetfall` · area `Robot Shop / Floyd SHOW + general GIVE (nested-container possession check)` · category `character-break` · target_sha `094a1ed`
- command: `show id card to floyd; give id card to floyd (ID card still inside worn uniform)`

GiveSomethingToSomeoneDecisionEngine.cs:49 and Floyd.cs:377 (RespondToShow) both re-check possession with the flat 'context.Items.Contains(thing)' after Repository.GetItemInScope already resolved the item via the recursive/accessibility-aware GetAllItemsRecursively + IsItemAccessible walk. Any item nested one level inside a worn/carried container (e.g. the starting ID card inside the Patrol uniform) resolves fine for examine/take/read but GIVE/SHOW falsely deny possession. General engine bug (shared GiveSomethingToSomeoneDecisionEngine<T>), most visible via Floyd since he is the primary give/show target.

## AB-054 [MEDIUM] Jumping into the rift never triggers the written death - always misrouted to the generic movement-failure message  · _filed#369_

- game `planetfall` · area `Admin Corridor / Admin Corridor North (rift, RiftLocationBase)` · category `puzzle-step` · target_sha `unknown`
- command: `jump into rift / jump over rift / jump rift / enter rift at Admin Corridor or Admin Corridor North (ladder not placed) -- all give the movement-failure message, never the death`

RiftLocationBase.RespondToSimpleInteraction (Planetfall/Location/Kalamontee/Admin/RiftLocationBase.cs:31-34) has a deliberate death branch for verb=jump/leap + rift-noun, but it is unreachable in live play. Confirmed on prod in both play and quiet mode, from both AdminCorridor and AdminCorridorNorth: 'jump into rift', 'jump over rift', 'jump rift' (bare noun, no preposition), and 'enter rift' all produce the generic 'The rift is too wide to jump across.' movement-failure message instead. That string only exists as AdminCorridor.Map()/AdminCorridorNorth.Map()'s Direction.N/S CustomFailureMessage, proving these inputs are being classified as MoveIntent{Direction=N/S} by the AI complex-intent parser and routed through MoveEngine, never reaching RiftLocationBase's SimpleIntent handler at all. Bare 'jump' (no noun) correctly does NOT trigger death, confirming the misroute is specific to verb+rift-noun combos. Zero test coverage anywhere exercises the jump+rift death branch; the existing 'too wide to jump' test (AdminCorridorTests.CannotGoNorth_WithoutLadder) uses bare 'n' input, not 'jump', so the suite doesn't codify this as intended. No ZIL access this session to confirm original JUMP handling, but the C# itself proves the intent (a full DeathProcessor call sits there unreachable) - this is a routing/parsing defect, not a faithful-cruelty judgment call.

## AB-055 [MEDIUM] Opening an empty canteen gives a completely blank response - no player feedback  · _filed#370_

- game `planetfall` · area `Kitchen / Canteen (canteen-filling mechanic)` · category `container` · target_sha `unknown`
- command: `take canteen (Mess Hall); open canteen -- blank response, no feedback at all, in both play and quiet mode`

Canteen.cs:31-34 overrides NowOpen to unconditionally return string.Empty, and OnOpening (Canteen.cs:36-42) only returns text when the canteen has contents (if Items.Any()) -- for an empty canteen both halves of OpenAndCloseInteractionProcessor.OpenMe's concatenation (NowOpen + OnOpening) are empty, so the response is totally blank even though IsOpen correctly flips to true underneath. Confirmed on prod, reproducible 3x in both play and quiet mode: 'open canteen' on the canteen exactly as taken from Mess Hall (its natural pristine state) returns nothing at all -- the harness oracle auto-flagged it as an empty narrator response. Contrast: opening the SAME canteen once filled with protein liquid correctly shows 'Opening the canteen reveals a quantity of protein-rich liquid.' A sibling container, SurvivalKit.cs:14-20, has the correct pattern (NowOpen returns 'Opened. ' when empty, custom text only when there's something to describe) that Canteen.cs should have followed but doesn't.

## AB-056 [MEDIUM] Leaving the shuttle control cabin at the arrival moment skips the speed-based crash/death consequence  · _filed#373_

- game `planetfall` · area `Alfie/Betty Control East+West (shared ShuttleControl base class)` · category `puzzle-step` · target_sha `unknown`
- command: `activate shuttle; push lever once, never touch it again; wait through entire tunnel (speed climbs to 120+); on the 'approaching a brightly lit area' turn, next command W (escapes, no consequence) vs any other command e.g. score (retroactive crash/death fires)`

A player who pushes the lever once and never decelerates (ignoring the Limit 45 sign and Begin Deceleration warning, speed climbs unchecked to 120+) should face a speed-based Arrived() consequence on arrival. Confirmed A/B on prod: same reckless setup both times, differing only in the command typed on the turn the 'approaching a brightly lit area' landmark appears. Typing W (leave cabin) as the next command escapes with zero consequence. Typing anything else (score) retroactively triggers the crash/death message on that turn instead. Root cause: ShuttleControl.cs's Move() checks TunnelPosition==EndOfTunnel at its TOP before incrementing, so the turn TunnelPosition goes 23->24 just prints the landmark and increments, WITHOUT calling Arrived() (the actual crash/death computation) - that's deferred to a separate later Act() cycle. But DoorIsClosed becomes false the instant TunnelPosition hits 24, so the door is already open at that moment, and if the player leaves via CanLeave(), OnLeaveLocation() immediately deregisters the shuttle as an actor and resets Activated=false, permanently skipping the pending Arrived() call since movement processing precedes actor processing in the turn pipeline. The developer's own code comment states explicit design intent ('speed... make sure you decelerate to a reasonable speed before you enter the station'), directly contradicted by this exploit. Both AlfieControlEast/West and BettyControlEast/West extend the same generic ShuttleControl<TCabin,TControl> base class, so one root cause affects both shuttle lines. No ZIL access this session, but this is an engine-architecture timing bug (actor deregistration racing a deferred consequence check), not a faithful-cruelty judgment call.

## AB-057 [MEDIUM] Fatigue warning while already in a dorm bunk never triggers sleep; 'sleep' says 'Civilized members of society usually sleep in beds' while in a bed  · _filed#392_

- game `planetfall` · area `MECH:sleep` · category `other` · target_sha `128cae8`
- command: `lie down (while WellRested) -> wait until weary -> sleep`

If the player lies in a dorm bunk BEFORE getting tired ('You are now in bed'), then becomes weary in bed, the fatigue escalation emits the generic 'find a nice safe place to sleep' warning and never queues fall-asleep, so 'sleep' refuses with 'Civilized members of society usually sleep in beds' while the player is in the In Bed sublocation. A/B: get up + lie down while weary ('soft and comfortable') -> sleep works. Root cause: PlanetfallContext.cs:226-245 / SleepNotifications.GetNotification (SleepNotifications.cs:90-106) have no 'CurrentLocation is BedLocation' branch; SleepProcessor.cs:28-34 keys off FallAsleepQueued which this path never sets.

## AB-058 [MEDIUM] Comm Room message-playback button only matches bare 'button'; label 'Mesij Plaabak' + descriptors fall to narrator, which falsely claims playback  · _filed#412_

- game `planetfall` · area `Comm Room` · category `narrator-hallucination` · target_sha `68f90e8`
- command: `press mesij plaabak`

The Comm Room's glowing 'Message Playback' button (CommRoom.cs:109) matches only the bare noun 'button'. Its printed label 'Mesij Plaabak' (CommRoom.cs:131), plus 'playback button' / 'message playback button' / 'glowing button', all miss the handler and fall through to the AI narrator, which falsely narrates success ('The console obligingly plays back the message') without ever delivering the Feinstein transmission. Only 'press button' works. Contrast: the pour handler (CommRoom.cs:40-41) accepts rich synonyms, showing the intended pattern. ZIL sub-checkout absent -> internal-consistency bug provable from the engine's own presented text.

## AB-059 [MEDIUM] Holding both elevator access cards: drop upper/lower card fails 'You don't have that!' (take/drop resolution ignores adjective)  · _filed#414_

- game `planetfall` · area `Plan Room` · category `parser-pronoun` · target_sha `68f90e8`
- command: `drop upper elevator access card`

With both upper+lower elevator access cards in scope (a normal state per closed #119: upper from Small Office desk, lower from Floyd), 'drop upper elevator access card' AND 'drop lower elevator access card' both return 'You don't have that!' despite both being held; nothing drops. 'examine <full card name>' resolves correctly, so the take/drop path specifically mis-handles the ambiguous-but-specific noun. Cards share nouns incl. 'card'/'access card' (Upper/LowerElevatorAccessCard.cs:7-11); ElevatorAccessCard.cs:9-11 strips 'elevator access card' from NounsForPreciseMatching to force specificity, but retained 'card' still matches both; GetPreciseMatchInScope (Repository.cs:159-177) returns room-first wrong card, DropIt (TakeOrDropInteractionProcessor.cs:189-190) then says 'You don't have that!'. Did NOT test elevator-slide impact.

## AB-062 [MEDIUM] Bio Lock East: 'look through window' shows the room instead of the Bio Lab view, though the handler explicitly tries to support it  · _filed#423_

- game `planetfall` · area `Bio Lock East` · category `parser-pronoun` · target_sha `68f90e8`
- command: `look through window`

At Bio Lock East, 'look through window' (the natural window command + the walkthrough's step-301 command) shows the ROOM description instead of the view into the Bio Lab (mutants + magnetic-striped mini card). 'examine window'/'look at window'/'look in window' all correctly show the Bio Lab view. Root cause: BioLockEast.cs:23-30 explicitly tries to support 'look through window' (checks OriginalInput.Contains('through')) but gates it on action.Match(LookVerbs, ['window']), which the prod parse of 'look through window' doesn't satisfy (the 'through' preposition disrupts verb/noun extraction), so it falls to base -> bare look -> room. Second branch (ExamineVerbs) catches examine/look-at/look-in. Same pattern at RadiationLab.cs:36 (crack) -- possibly systemic. Setup note: navigation reached Bio Lock East legitimately (chronometer reset at 176 for Alfie); run ended at score 42 vs spine ~54 (a divergence during the long drive) but the window handler is unaffected -- examine window shows the correct view -- so the finding is not a state confound.

## AB-064 [MEDIUM] Rift throw handler resolves target via global lookup: deletes out-of-scope items across the map + re-narrates already-lost items  · _filed#429_

- game `planetfall` · area `Admin Corridor North (rift)` · category `state-scope` · target_sha `unknown`
- command: `throw laser into rift`

RiftLocationBase.cs:11-26 'throw <item> into rift' resolves nounOne via global Repository.GetItem with no possession/scope/state guard. Divergence A: 'throw laser into rift' (laser in Mech) / 'throw canteen into rift' (canteen in Kitchen) delete those items across the map with false success (softlock vector for critical items). Divergence B: re-throwing an already-lost item (diary, CurrentLocation=null) re-narrates 'sails gracefully into the rift'. Same bug class #297 fixed for the sibling ladder-place handler in AdminCorridor.cs, never applied to this shared throw routine. Prod-confirmed via harness.

## AB-065 [MEDIUM] Cryo-Elevator button fires on push-verb alone: 'push wall'/'push floor' operates the elevator (instant death after arrival)  · _filed#430_

- game `planetfall` · area `Cryo-Elevator (Lawanda LabOffice)` · category `noun-guard` · target_sha `unknown`
- command: `push wall`

CryoElevatorButton.cs:17-21 gates on MatchVerb(PushVerbs) only, no noun check. Room contains only this button; LocationBase offers the action to it regardless of noun, so 'push wall' starts the escape countdown ('The elevator door closes just as the monsters reach it!...') and 'push floor' reaches the same button (CountdownActive -> 'Nothing happens'). After AlreadyArrived, any push routes to the hilarious-death ending. Sibling RedButton/WhiteButton/BlackButton correctly guard with MatchNounAndAdjective(NounsForMatching). Prod-confirmed.

## AB-070 [MEDIUM] Admin Corridor South: 'You don't have the curved metal bar' fires for ANY two-noun command when the magnet is on the floor there  · _filed#436_

- game `planetfall` · area `Admin Corridor South` · category `match-on-text-not-state` · target_sha `unknown`
- command: `put brush in uniform (magnet on floor here)`

AdminCorridorSouth.cs:68-77 RespondToMultiNounInteraction returns 'You don't have the curved metal bar.' whenever !HasItem<Magnet> && Magnet.CurrentLocation==this, BEFORE and independent of the magnet/key-fishing match logic (lines 89+). No verb/noun gate, so every MultiNounIntent is swallowed. Prod-confirmed: hold magnet -> 'put brush in uniform' handled normally; drop magnet; 'put brush in uniform' -> 'You don't have the curved metal bar.' Multi-noun analog of already-fixed examine catch-all #291 (same file, RespondToSimpleInteraction).

## AB-071 [MEDIUM] Laser depression accepts TWO batteries (SpaceForItems defaults to 2); firing uses FirstOrDefault so a fresh battery added without removing the old is ignored  · _filed#437_

- game `planetfall` · area `Tool Room (laser capacity)` · category `container-false-capacity` · target_sha `unknown`
- command: `put fresh battery in laser (old still inside)`

Laser.cs has CanOnlyHoldTheseTypes=[BatteryBase] but no SpaceForItems override -> ContainerBase default 2; batteries are Size 1, so HaveRoomForItem admits a second. Prod-confirmed: put old battery in laser, put fresh battery in laser both 'resting in the depression'; inventory lists 'The laser contains: An old battery, A new battery'. TryFireLaser reads Items.FirstOrDefault (old, depleted in real play) so inserting fresh without removing old leaves the laser non-functional. Fix: SpaceForItems => 1. Sibling of #434 (same item, examine hardcode).

## AB-077 [MEDIUM] Metal cube socket accepts a 2nd bedistor: good bedistor drops in beside the fused one, 'fixing' course control with the broken part still socketed (pliers step skipped)  · _fixed#462_

- game `planetfall` · area `Course Control (LargeMetalCube)` · category `container-capacity` · target_sha `e795f32`
- command: `put fused bedistor in cube; put good bedistor in cube`

LargeMetalCube.cs: CanOnlyHoldTheseTypes=[BedistorBase] but NO SpaceForItems override -> ContainerBase default 2; ships StartWithItemInside<FusedBedistor>; placing a GoodBedistor fires CourseControl.ItIsFixed. Both bedistors size 1 so fused(1)+good(1)<=2. Prod e795f32: re-seat fused, 'put good bedistor in cube'->'Done. The warning lights go out...'; examine cube shows BOTH fused+good. Course control reads fixed with the broken part still in; pliers-removal sub-puzzle skippable. Sibling FromitzAccessPanel caps at 4; laser #437 capped to 1. Fix: SpaceForItems=>1. Anti-pattern #6. ZIL-independent.

## AB-078 [MEDIUM] 'Permanently broken' send console is recoverable: after wrong-color shutdown, correct black->gray pour still fixes it (+6, sends distress call)  · _filed#463_

- game `planetfall` · area `Comm Room (coolant send console)` · category `matching-command-not-state` · target_sha `e795f32`
- command: `pour flask into hole (wrong); then black; then gray`

CommRoom.cs PourLiquid (~L54) has NO guard on SystemIsCritical/IsFixed; PermanentlyBroken (~L89) sets SystemIsCritical=true but never changes CurrentColor, so black->gray progression still runs NextColor->Fixed (+6, MarkCommunicationsFixed). Prod e795f32: pour red->'Shuteeng Down Awl Sistumz...send console shuts down' (room 'dark'); pour black->'all go off except one, a gray light'; pour gray->'help message is now being sent' (+6). Permanent shutdown recovers. Also: PermanentlyBroken re-narrates on repeat wrong pour (no guard); CriticalDescription('dark') vs 'gray light' progress contradiction. Anti-pattern #1/#2. ZIL-independent.

## AB-079 [MEDIUM] Betty exit destination keyed off AlfieControlEast (wrong car) not BettyControlEast — exiting Betty after Alfie moves teleports you to the wrong station  · _filed#468_

- game `planetfall` · area `Shuttle Car Betty` · category `disambiguation-wrong-target` · target_sha `e795f32`
- command: `activate alfie east; nudge; go lawanda platform; north; north`

ShuttleCarBetty.cs:17 reads AlfieControlEast.TunnelPosition (Alfie's control) to pick Lawanda/Kalamontee; should read BettyControlEast. Cars move independently; AlfieControlEast.Arrived resets only West so East stays non-zero after a trip. Prod e795f32: nudge Alfie East off 0, board Betty at Lawanda, exit north -> Kalamontee Platform (wrong, no ride). Alfie's own exit (ShuttleCarAlfie.cs:17) correctly reads AlfieControlEast. Copy-paste wiring error. Related #461. ZIL-independent.

## AB-080 [MEDIUM] FlaskUnderSpout set once, never reset: after taking the flask back, room still shows it under the spout and pressing a button fills the flask in-hand  · _fixed#469_

- game `planetfall` · area `Machine Shop (flask/spout)` · category `state-flag-not-reset` · target_sha `e795f32`
- command: `put flask under spout; take flask; look; press red button`

MachineShop.cs FlaskUnderSpout written true only at :44 (put-under-spout handler), never set false anywhere (grep-confirmed); Flask.cs has no OnBeingTaken. So flag sticks true. Prod e795f32: put flask under spout -> take flask -> look still 'Sitting under the spout is a glass flask' while flask in inventory; press red button -> 'flask fills with red' in-hand (Click :162 gates on flag not flask position; desc :183 same). Reachable in normal play (put then take). Fix: reset flag on take, or check flask.CurrentLocation is MachineShop. ZIL-independent.

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

## AB-045 [LOW] Narrator falsely denies existence of keyboard in Miniaturization Booth (scenery-noun false-negative pattern)  · _filed#315_

- game `planetfall` · area `MECH:miniaturization` · category `narrator-hallucination` · target_sha `unknown`
- command: `examine keyboard`

Room description says 'a keyboard with numeric keys' is there. Narrator says 'the legendary invisible keyboard—often mistaken for air. Keep searching, Ensign.' Engine has no examine handler (quiet=no effect); narrator generates false-absent response instead of neutral scenery description.

## AB-053 [LOW] Examining Achilles' body gives generic "nothing special" fallback despite Floyd's emotional backstory moment  · _filed#367_

- game `planetfall` · area `Repair Room / Achilles corpse (BrokenRobot)` · category `examine-scenery` · target_sha `unknown`
- command: `N into Repair Room (Achilles reveal fires); examine damaged robot`

BrokenRobot.cs (the Achilles corpse in Repair Room) implements NeverPickedUpDescription/GenericDescription (used for room-listing text) but does NOT implement ICanBeExamined at all, so 'examine damaged robot' / 'examine robot' / 'examine broken robot' falls through to the engine's generic default ('There is nothing special about the damaged robot.'). This is jarring because entering the room triggers Floyd's own scripted reaction (RepairRoom.cs's Act(), same pattern family as Infirmary's Lazarus scene) where he narrates a specific, named, emotionally-loaded backstory: 'That's Achilles. He was in charge of repairing machinery. He repaired Floyd once... Looks like he fell down the stairs... A Planner-person once told me that's why they named him Achilles.' A player who examines the very corpse Floyd just eulogized gets a completely generic non-answer instead of anything acknowledging what Floyd just said (limping foot, repair role, etc). Confirmed live on prod immediately after the Achilles reveal fired correctly.

## AB-060 [LOW] Empty microfilm reader falsely says 'already a spool' when putting an oversized non-spool item (put checks room before type)  · _filed#417_

- game `planetfall` · area `Library` · category `container` · target_sha `68f90e8`
- command: `put brush in reader`

With the microfilm reader EMPTY, 'put brush in reader' returns 'There's already a spool in the reader.' (false; brush not consumed). PutProcessor.cs:63-67 checks HaveRoomForItem (size) before the type check; SpoolReader SpaceForItems=1 (SpoolReader.cs:12), HaveRoomForItem=Items.Sum(Size)+item.Size<=SpaceForItems (ContainerBase.cs:107), so an oversized non-spool fails the size check even when empty and emits SpoolReader.NoRoomMessage (SpoolReader.cs:39) which hardcodes occupancy. Smaller non-spools (diary/chronometer) correctly get 'It doesn't fit in the circular opening.' Fix: type-check before room-check in PutProcessor. Also observed (NOT filed - could not root-cause): 'read reader' on empty reader falls to narrator while 'read screen'/'examine reader' say 'The screen is blank.'

## AB-063 [LOW] Kitchen dispenser: 'put canteen under spout' not handled (only 'put canteen in niche' works)  · _filed#424_

- game `planetfall` · area `Kitchen` · category `parser-pronoun` · target_sha `68f90e8`
- command: `put canteen under spout`

Kitchen food dispenser accepts 'put canteen in niche' but not 'put canteen under spout' (room says 'octagonal niche beneath a spout'; Machine Shop dispenser supports 'put flask under spout'). KitchenMachine.cs has 'spout' absent from NounsForMatching and a '// TODO: put canteen under spout' marker (line 63).

## AB-066 [LOW] Padlock 'unlock padlock with key' re-narrates 'springs open' when already unlocked (multi-noun path missing !Locked guard the simple path has)  · _filed#431_

- game `planetfall` · area `Mess Corridor (padlock)` · category `state-guard` · target_sha `unknown`
- command: `unlock padlock with key`

Padlock.cs multi-noun RespondToMultiNounInteraction sets Locked=false unconditionally and returns 'The padlock springs open.' with no !Locked guard (lines 76-81). The simple-interaction path in the same class DOES guard: 'if (!Locked) return The padlock is already open.' (lines 53-54). So identical state (already unlocked) yields different responses by phrasing. Prod-confirmed: 'unlock padlock with key' twice -> both 'springs open'. Floyd comment does not double-fire (CommentOnAction guards).

## AB-067 [LOW] Miniaturization Booth keyboard fires on type/press/push verb alone: 'push slot'/'press wall' hit booth logic instead of the noun  · _filed#433_

- game `planetfall` · area `Miniaturization Booth (Lawanda)` · category `noun-guard` · target_sha `unknown`
- command: `push slot`

MiniaturizationBooth.cs:63-94 gates on MatchVerb(TypeVerbs + press/push/key) with no noun check, so any push/press/type on any noun enters the keyboard block. Not-activated: 'push slot'/'press wall' -> 'Internal computer repair booth not activated.'; activated: non-numeric noun -> 'The keyboard only has numeric keys.', shadowing the room's slot item. Same noun-guard omission class as #430 (Cryo-Elevator button), different room/item. Prod-confirmed.

## AB-068 [LOW] Laser examine hardcodes 'depression contains an old battery' — stale after removing the battery or swapping in the fresh one  · _filed#434_

- game `planetfall` · area `Tool Room (laser)` · category `description-vs-state` · target_sha `unknown`
- command: `examine laser`

Laser.cs:64-68 ExaminationDescription interpolates only the dial Setting; the battery clause 'which contains an old battery' is a constant that ignores Items. Prod-confirmed: examine laser -> 'contains an old battery'; take battery -> 'Taken.' (depression now empty); examine laser -> still 'contains an old battery'. Same staleness after the golden-path old->fresh swap (fresh battery still described as 'old'). Laser is a transparent ContainerBase; firing reads Items.FirstOrDefault but examine never consults it.

## AB-069 [LOW] ProjCon Office description starts with doubled letter 'TThis office looks like a headquarters'  · _filed#435_

- game `planetfall` · area `ProjCon Office (Lawanda)` · category `typo` · target_sha `unknown`
- command: `look`

ProjConOffice.cs:69, AnnouncmentHasBeenMade==false branch of GetContextBasedDescription has literal typo 'TThis office...' (doubled T); the true-branch line 66 correctly reads 'This office...'. Shown on entry/look; ProjCon Office is the only route to the Cryo-Elevator. Prod-confirmed.

## AB-072 [LOW] Mess Hall description hardcodes 'A door to the south is closed' even when the kitchen door is open  · _filed#438_

- game `planetfall` · area `Mess Hall` · category `description-vs-state` · target_sha `unknown`
- command: `look (after sliding kitchen access card)`

MessHall.cs:44-49 GetContextBasedDescription returns a constant string with 'A door to the south is closed.' — never consulting KitchenDoor.IsOpen. Prod-confirmed: slide kitchen access card through slot -> 'The kitchen door quietly slides open.'; look -> 'A door to the south is closed.'; examine kitchen door -> 'The door is open.' (contradiction). Sibling RecArea.cs:61 interpolates Door.IsOpen correctly. (Note: door auto-closes at TurnsOpen==3, so check within window.)

## AB-073 [LOW] Radiation Lab 'look through crack' intermittently (5/8 prod) shows the room instead of the Bio Lab view  · _filed#447_

- game `planetfall` · area `Radiation Lab` · category `parser-pronoun` · target_sha `68f90e8`
- command: `look through crack`

RadiationLab.cs:36 gates the Bio Lab view on Match(LookVerbs,[crack]) && OriginalInput.Contains(through) - the same fragile parse dependence #423 diagnosed for the BioLockEast window and explicitly flagged for RadiationLab.cs:36. Prod (68f90e8) 8-run measurement: 5/8 room (bug), 3/8 view. No fallback: examine/look-at/look-in crack all give 'too small', so the view is intermittently unreachable. origin/main still carries the fragile pattern in both files. Sibling of #423.

## AB-074 [LOW] Escape pod 'sit on <object>' hijacked to 'You're already in the safety web' (SafetyWeb matches verb alone, no noun guard)  · _filed#448_

- game `planetfall` · area `Escape Pod / safety web` · category `verb-only-firing` · target_sha `68f90e8`
- command: `sit on the control panel`

EscapePod.cs:50 (seated) & :53 (standing) MatchVerb([get,rest,sit] / [leave,exit,get]) with NO noun guard; SafetyWeb is a pod Item so its handler runs on every command. Prod 68f90e8: seated in web, 'sit on the control panel' and 'sit on the floor' both return 'You're already in the safety web.' (noun ignored). Contrast 'take brochure'->normal, 'get slime'->generic. Side effect of #376 web-entry fix. Parser masks get-branch; sit-on-X reliably reproduces. Anti-pattern #5. ZIL-independent.

## AB-075 [LOW] Moving-elevator room description hardcodes 'sliding door which is open' while door is closed (contradicts 'door slides shut' same turn)  · _filed#450_

- game `planetfall` · area `Upper/Lower Elevator (ElevatorBase)` · category `hardcoded-description-vs-state` · target_sha `68f90e8`
- command: `press up button; look`

ElevatorBase.cs:162 GetContextBasedDescription hardcodes '...which is open' ignoring GetItem<TDoor>().IsOpen, which Move() (:124) sets false when the car departs. Prod 68f90e8 (Lower Elevator): 'press up button'->'The elevator door slides shut...movement.'; 'look'->'...sliding door to the north which is open.'; 'examine lower elevator door'->'The door is closed.' Contradiction persists ~3 moving turns (reopens at TurnsSinceMoving==4). Affects both cars (shared base). Sibling of #438 (MessHall) / #430; RecArea.cs:61 is the correct interpolated pattern. god-mode used only to reach the car; contradiction is Move()-driven. Anti-pattern #6. ZIL-independent.

## AB-076 [LOW] Shuttle Car Betty cabin says platform doorway is 'south' but the exit is Direction.N (north) — 'go south' fails  · _fixed#461_

- game `planetfall` · area `Shuttle Car Betty` · category `hardcoded-description-vs-state` · target_sha `e795f32`
- command: `god mode go lawanda platform; north; south`

ShuttleCabin.cs:17 renders '...platform to the {Exit}'; ShuttleCarBetty.cs:7 Exit='south' but Map:16 wires the platform exit to Direction.N. Prod e795f32: board Betty via 'north' from Lawanda Platform; cabin says 'platform to the south'; 'south'->'You cannot go that way.'; 'north'->Lawanda Platform. Platform maps N->Betty so platform is genuinely south of Betty => description correct, Map direction (N) is the bug (should be Direction.S). Alfie is the consistent control (Exit='north' + Direction.N). Non-Euclidean north-to-board + north-to-leave. Anti-pattern #4. ZIL-independent internal contradiction.

## AB-082 [LOW] Three connector rooms drop first-visit description: BeforeEnterLocation returns transition text without base (VisitCount never increments)  · _fixed#473_

- game `planetfall` · area `Kalamontee corridors (Dorm/Junction/Admin)` · category `lifecycle-base-not-called` · target_sha `e795f32`
- command: `god mode go corridor junction; west; look`

DormCorridor.cs:24, CorridorJunction.cs:39, AdminCorridor.cs:45 early-return the walk-transition string without base.BeforeEnterLocation, so VisitCount++ (and OnFirstTimeEnterLocation) never fire; LookProcessor shows the full desc only when VisitCount==1 in Brief (default). Prod e795f32: go corridor junction -> west -> 'You walk down...' + title 'Dorm Corridor' but NO description; 'look' then shows 'wide, east-west hallway'. AdminCorridorNorth.cs:49 does it right (prepend + base). Fix: prepend transition to base return. ZIL-independent.

## AB-083 [LOW] Bio Lock East window reports magnetic-striped card on floor after it is retrieved (missing MINI-CARD guard)  · _filed#477_

- game `planetfall` · area `Bio Lock East (window)` · category `hardcoded-description-vs-state` · target_sha `e795f32`
- command: `examine window`

BioLockEast.cs:28-30 returns the card sentence unconditionally (no state guard). Original globals.zil WINDOW-F guards it on <NOT <FSET? MINI-CARD TOUCHBIT>>, omitting it once the card is taken. Reproduced live on prod e795f32: card confirmed in inventory (read miniaturization card) while window still says it is on the floor. Distinct from #423 (through-parsing). Same class as #438.

## AB-084 [LOW] Lab uniform pocket seeded with 2 items but SpaceForItems=1 (cannot re-insert a removed item)  · _fixed#478_

- game `planetfall` · area `Lab Storage (lab uniform pocket)` · category `container-capacity` · target_sha `e795f32`
- command: `put teleportation card in lab pocket`

LabUniformPocket.cs:10 SpaceForItems=>1 but Init (:25-29) seeds TeleportationAccessCard + PieceOfPaper (both size 1). ContainerBase.HaveRoomForItem (:107) size-sum <= 1 rejects the 2nd. ContainerBase default is 2; the =>1 override is the bug (copied from PatrolUniformPocket which seeds 1). ZIL comptwo.zil:1657 original holds both. Reproduced live prod e795f32.

## AB-085 [LOW] Shuttle cabin "station ahead" window view is dead code (gated on unreachable TunnelPosition 190/195; should be 23)  · _fixed#479_

- game `planetfall` · area `Shuttle control cabin (window view)` · category `dead-code-threshold` · target_sha `e795f32`
- command: `look (in shuttle control cabin at tunnel position 23)`

ShuttleControl.cs:75-81 OutTheWindow gates the "brightly-lit station ahead" view on TunnelPosition==190 or 195, but TunnelPosition runs 0..24 (EndOfTunnel=24), so it is unreachable. Original globals.zil:1701 DESCRIBE-VIEW shows it at SHUTTLE-COUNTER==23 (moving); the C# landmark table already uses 23. Reproduced live prod e795f32 by driving Alfie to pos 23.

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
