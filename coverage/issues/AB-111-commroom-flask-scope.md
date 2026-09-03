## Bug

Two independent defects in `Planetfall/Location/Kalamontee/Tower/CommRoom.cs`. The first is the substantive one; the second is a small state-vs-description contradiction in the same file, included here so a fix touches the file once.

### 1. `pour fluid into hole` ignores flask possession and scope entirely

The Comm Room's coolant-override pour resolves the flask through a repository-global singleton lookup with no possession, room, or scope check. A player standing in the Comm Room **empty-handed** can drain — and consume — a flask sitting anywhere else on the map, and thereby either solve or permanently destroy the 6-point transmission puzzle without ever touching it.

Both symptoms reproduced deterministically on **live prod**, narrator **OFF** (`noGeneratedResponses=true`, so this is engine behaviour, not the narrator).

**Repro A — solve the puzzle without carrying the flask**

Standing in Tower Core holding the flask with the correct (black) fluid, enunciator showing a black light:

```
> drop flask
Dropped
    [Tower Core] inv=[... no flask ...]

> NE
    [Comm Room]  A black colored light is flashing on the enunciator panel.
    inv=['chronometer', 'patrol uniform', ...]      <-- flask NOT carried

> pour fluid into hole
The liquid disappears into the hole. The lights on the enunciator panel blink
rapidly and all go off except one, a gray light.
```

The puzzle advanced a stage. Walking back confirms the remote flask was silently drained:

```
> SW
    [Tower Core]
> examine flask
The flask has a wide mouth and looks large enough to hold one or two liters.
It is made of glass, or perhaps some tough plastic .        <-- empty
```

**Repro B — permanently destroy the puzzle without carrying the flask**

The natural version of the same mistake: fill the flask and forget the `take flask` step (the walkthrough's own `put flask under spout` sequence sets this state up). With the enunciator requiring **gray**, the flask left **under the spout in the Machine Shop** — roughly eleven rooms and an elevator ride away — holding the wrong (red) fluid:

```
    [Machine Shop]
> put flask under spout
The glass flask is now sitting under the spout.
> press red button
The flask fills with some red chemical fluid. The fluid gradually turns milky white.
    inv=[... no flask ...]                                   <-- deliberately left behind

    ... walk N N N N E N / slide upper access card / press up button / wait wait / S / NE ...

    [Comm Room]  inv=['chronometer', 'patrol uniform', ...]  <-- flask NOT carried
> pour fluid into hole
An alarm sounds briefly, and a sign flashes "Kuulint Sistum Imbalins Kritikul --
Shuteeng Down Awl Sistumz." A moment later, the lights in the room dim and the
send console shuts down.
```

The send console is now permanently shut down and the 6 points are unreachable — triggered by a flask the player never held, in a room they were not standing in.

### 2. `CriticalDescription` drops the funnel-hole sentence

After the shutdown above, the funnel-shaped hole disappears from the room description while still responding to commands (`PourLiquid` returns *"The liquid disappears into the hole, but nothing happens; the send console has shut down."* for the critical state). Prod, after a wrong pour:

```
> look
...The console on the right side of the room is labelled "Send Staashun."
A screen on the console displays a message. Next to the screen is a flashing sign
which says "Kuulint Sistum Imbalins Kritikul -- Shuteeng Down Awl Sistumz." Next to
this console is an enunciator whose lights are all dark.
```

The `Malfunkshun` (broken) and `Tranzmishun in pragres` (fixed) states both print the hole; only the critical state omits it.

## Root cause

**Defect 1** — `Planetfall/Location/Kalamontee/Tower/CommRoom.cs:38-52`. The handler matches the verb/noun pair and then goes straight to the item, with no guard between:

```csharp
if (!action.Match(verbs, liquidNouns, holeNouns, prepositions))
    return base.RespondToMultiNounInteraction(action, context);

if (string.IsNullOrEmpty(GetItem<Flask>().LiquidColor))   // :48  global lookup
    return base.RespondToMultiNounInteraction(action, context);

return PourLiquid(context);                                // :51
```

and `PourLiquid` mutates that same global instance (`CommRoom.cs:56-57`):

```csharp
var flaskColor = GetItem<Flask>().LiquidColor!;
GetItem<Flask>().LiquidColor = null;
```

`GetItem<T>()` here is `LocationBase.GetItem<T>()` (`GameEngine/Location/LocationBase.cs:459-462`), a bare passthrough to the static `Repository.GetItem<T>()` (`GameEngine/Repository.cs:49`) — a type-keyed singleton lookup that carries no location or possession semantics whatsoever. Nothing on this path ever asks where the flask is.

That the omission is unintentional is settled by the sibling handler for the *same item in the same subsystem*, which guards possession **twice** before mutating (`Planetfall/Location/Kalamontee/Mech/MachineShop.cs:36-44`):

```csharp
if (flask.IsHereButNotInInventory(context))
    return Task.FromResult<InteractionResult?>(new PositiveInteractionResult(
        "You don't have the flask. "));

// Container-aware possession (issue #503) ...
if (!context.IsCarrying<Flask>())
    return Task.FromResult<InteractionResult?>(new NoNounMatchInteractionResult());
```

This is the "a guard the simple path has but the multi-noun path lacks" class, compounded by an unscoped global lookup.

**Defect 2** — `CommRoom.cs:31-35`. `CriticalDescription` ends at the enunciator, while `FixedDescription` (`:10-13`) and `BrokenDescription` (`:25-29`) both close with the funnel-hole sentence:

```csharp
private string CriticalDescription =>
    "A screen on the console displays a message. Next to the screen is " +
    "a flashing sign which says \"Kuulint Sistum Imbalins Kritikul -- " +
    "Shuteeng Down Awl Sistumz.\" Next to this console is an enunciator " +
    "whose lights are all dark. ";               // <-- funnel-hole sentence missing
```

## Original behavior (ZIL — ground truth)

**Defect 1.** The possession check is the *first* thing the `PUT`/`POUR` branch does, before any state mutation — `planetfall-source/compone.zil:2982-2988`:

```
(<VERB? PUT POUR>
 <COND (<NOT <HELD? ,FLASK>>
        <TELL "You're not holding the flask." CR>
        <RTRUE>)
       (<EQUAL? ,PRSI ,CANTEEN>
        <WORTHLESS-ACTION>
        <RTRUE>)>
 <REMOVE ,CHEMICAL-FLUID>
```

The original author guarded this on every chemical-fluid path — the identical `<NOT <HELD? ,FLASK>>` check also opens the neighbouring branch at `compone.zil:2975-2977`. In the original it is simply not possible to pour a flask you are not holding; the coolant sequence and the shutdown at `compone.zil:3024-3033` are both unreachable without it in hand.

**Defect 2.** The funnel-hole sentence is printed unconditionally in all three states; only the "whose lights are all dark" clause is conditional — `planetfall-source/compone.zil:2835-2846`:

```
<COND (,COMM-SHUTDOWN
       <SHUTDOWN>)
      (,COMM-FIXED
       <TELL "\"Tranzmishun in pragres.\"">)
      (T
       <TELL "\"Malfunkshun in Sendeeng Kuulint Sistum.\"">)>
<TELL " Next to this console is an enunciator">
<COND (<OR ,COMM-FIXED ,COMM-SHUTDOWN>
       <TELL " whose lights are all dark">)>
<TELL
". On the console next to the enunciator panel is a funnel-shaped hole
labelled \"Kuulint Sistum Manyuuwul Oovuriid.\"" CR>)
```

## Impact

- **Puzzle bypass.** The Comm Room fetch loop — Machine Shop → Tower → Comm Room, twice — is the entire design of this puzzle. It can be completed with the flask parked anywhere on the map, so the intended carrying constraint does not exist.
- **Silent item mutation at a distance.** A flask left somewhere deliberately is drained with no message referring to the flask at all. The player has no way to connect the empty flask they later find to the command they typed rooms away.
- **Permanent, unrecoverable score loss on a natural mistake.** Forgetting `take flask` after filling it is an ordinary slip that the walkthrough's own `put flask under spout` step sets up. If the remaining fluid is the wrong colour, a single `pour fluid into hole` in the Comm Room shuts the send console down for good (`PermanentlyBroken()`, `CommRoom.cs:105-112`), costing 6 points and putting the maximum score out of reach with no warning and no workaround.
- **Description contradicts state.** After a shutdown the game answers commands aimed at a funnel hole it no longer mentions.

Severity **medium** — the everyday trigger is benign-looking and the wrong-colour case is a permanent scoring loss, but reaching it requires the player not to be carrying the flask.

**Why CI is green.** Every existing pour test in `Planetfall.Tests/CommRoomAndMachineRoomTests.cs` calls `Take<Flask>()` before `StartHere<CommRoom>()` — see `PourLiquid_Black` (`:444`), `PourLiquid_Red` (`:463`), `PourLiquid_Gray` (`:482`). The not-carrying path is never exercised, so the missing guard cannot be caught. `MachineShop`'s equivalent path *is* covered (`PutFlaskUnderSpout_Success`, `PutFlaskUnderSpout_FlaskInUniformPocket_Success`), which is consistent with the guard existing there and not here.

## Suggested fix

Guard possession in `CommRoom.RespondToMultiNounInteraction` before touching `GetItem<Flask>()`, mirroring the pattern already established in `MachineShop.cs:36-44` rather than inventing a new one:

1. After the `action.Match(...)` succeeds and **before** the `LiquidColor` read at `:48`, reject when the flask is not carried. Use the container-aware `context.IsCarrying<Flask>()` (not the flat `HasItem<T>()`) so the flask still works from the Patrol uniform pocket — the same correction made for issue #503.
2. When the flask is visible in the room but not held, return the ZIL-faithful refusal *"You're not holding the flask."* — matching both `compone.zil:2983-2985` and the wording `MachineShop` already uses for the analogous case. When it is nowhere in scope, return `NoNounMatchInteractionResult` so the turn falls through normally.
3. Keep the existing `SystemIsCritical` / `IsFixed` terminal guards (`:67-73`) exactly as they are; the new check belongs ahead of them, since possession should be settled before any state branch runs.

For defect 2, append the funnel-hole sentence to `CriticalDescription` (`CommRoom.cs:31-35`) so all three states describe the hole, as ZIL does. The three descriptions share two long literal fragments; extracting the trailing funnel-hole sentence into one `private const string` used by all three would stop the states drifting apart again.

Do not port the ZIL verbatim — the above is a description of intent only.

## TDD plan

Add to `Planetfall.Tests/CommRoomAndMachineRoomTests.cs`, alongside the existing `PourLiquid_*` tests. Each must fail on the current code and pass after the fix.

1. `PourLiquid_FlaskNotCarried_Refuses` — red before / green after, defect 1's core:
   ```csharp
   var target = GetTarget();
   StartHere<CommRoom>();
   GetItem<Flask>().LiquidColor = "black";      // NOTE: no Take<Flask>()
   var response = await target.GetResponse("pour fluid into hole");
   response.Should().Contain("You're not holding the flask.");
   GetItem<Flask>().LiquidColor.Should().Be("black");            // not drained
   GetLocation<CommRoom>().CurrentColor.Should().Be("black");    // puzzle did not advance
   GetLocation<CommRoom>().IsFixed.Should().BeFalse();
   ```
   Currently returns the gray-light success text, nulls `LiquidColor`, and advances `CurrentColor`.

2. `PourLiquid_FlaskLeftUnderSpout_DoesNotShutDownConsole` — defect 1's destructive path, the regression that actually costs the player the game:
   ```csharp
   var target = GetTarget();
   Take<Flask>();
   StartHere<MachineShop>();
   await target.GetResponse("put flask under spout");   // flask leaves inventory
   GetItem<Flask>().LiquidColor = "red";                // wrong colour
   StartHere<CommRoom>();
   var response = await target.GetResponse("pour fluid into hole");
   response.Should().NotContain("the send console shuts down");
   GetLocation<CommRoom>().SystemIsCritical.Should().BeFalse();
   target.Context.Score.Should().Be(0);
   ```

3. `PourLiquid_FlaskInUniformPocket_Success` — pins the container-aware guard so the fix does not regress issue #503, mirroring `PutFlaskUnderSpout_FlaskInUniformPocket_Success`:
   ```csharp
   var target = GetTarget();
   Pocket<Flask>();
   StartHere<CommRoom>();
   GetItem<Flask>().LiquidColor = "black";
   var response = await target.GetResponse("pour fluid into hole");
   response.Should().Contain("and all go off except one, a gray light");
   GetLocation<CommRoom>().CurrentColor.Should().Be("gray");
   ```
   Guards against "fixing" this with a flat `HasItem<Flask>()`, which would break the pocket case.

4. `CommRoom_CriticalDescription_MentionsFunnelHole` — defect 2:
   ```csharp
   var target = GetTarget();
   StartHere<CommRoom>();
   GetLocation<CommRoom>().SystemIsCritical = true;
   var response = await target.GetResponse("look");
   response.Should().Contain("funnel-shaped hole");
   response.Should().Contain("Kuulint Sistum Manyuuwul Oovuriid");
   ```

The existing `PourLiquid_Black` / `_Red` / `_Gray` tests must stay green unchanged — they all `Take<Flask>()`, so the new guard is a no-op for them.

---

Found via the AdventureBreaker harness (black-box prod + white-box/ZIL confirmation). `AB-111` — filed as arsindelve/zorkai#547

Verified against `origin/main` `6d85ba6`; both repros observed on live prod (`Prod/Planetfall`) with the narrator disabled.

---
_Generated by [Claude Code](https://claude.ai/code)_
