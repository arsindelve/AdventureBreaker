# Spine Format

A **spine** is an ordered list of commands that drives the game from its starting state into deep, varied territory. The spine isn't trying to win — it's a GPS that gets the adversarial agent to interesting states worth attacking.

Spine files live in `adventurebreaker/spine/` and are referenced by name in `config.py`.

---

## File Schema

```json
{
  "game":             "mygame",
  "source":           ["path/to/original/walkthrough.txt"],
  "count":            42,
  "replayable_count": 40,
  "steps": [
    {
      "cmd":              "open door",
      "setup":            null,
      "expect":           ["The door swings open."],
      "http_replayable":  true
    }
  ]
}
```

### Top-level fields

| Field | Type | Description |
|---|---|---|
| `game` | string | Must match the game key in `config.py` (e.g. `"zork"`, `"mygame"`). |
| `source` | array of strings | Where the walkthrough came from — file path(s), URL, or description. Informational only, not parsed. |
| `count` | integer | Total number of steps. Informational — the harness uses `steps` directly. |
| `replayable_count` | integer | How many steps are safe to replay via HTTP (see `http_replayable` below). |
| `steps` | array | The ordered list of commands. See below. |

### Step fields

| Field | Type | Required | Description |
|---|---|---|---|
| `cmd` | string | yes | The exact player command to send (e.g. `"open mailbox"`, `"N"`, `"take sword"`). Sent verbatim as the `input` field of the play request. |
| `expect` | array of strings | yes | One or more substrings expected to appear in the response. The harness checks these as a golden transcript — if none match, it flags a divergence. Use short, stable substrings that won't change with rephrasing. |
| `setup` | any \| null | no | Backend-specific pre-step setup (e.g. ZorkAI uses this for reflection-based god-mode state injection). Set to `null` if your backend doesn't support this. |
| `http_replayable` | boolean | yes | `true` if this step is safe to replay via HTTP (i.e. the command doesn't depend on state that can't be reconstructed by replaying from turn 1). Set `false` for steps that use `setup` or have backend-side-effects that can't be replayed. |

---

## Minimal Example

A five-step spine for a hypothetical dungeon crawler:

```json
{
  "game":             "dungeon",
  "source":           ["handwritten"],
  "count":            5,
  "replayable_count": 5,
  "steps": [
    {
      "cmd":             "look",
      "setup":           null,
      "expect":          ["You are standing at the entrance"],
      "http_replayable": true
    },
    {
      "cmd":             "take torch",
      "setup":           null,
      "expect":          ["Taken"],
      "http_replayable": true
    },
    {
      "cmd":             "N",
      "setup":           null,
      "expect":          ["Dark Corridor"],
      "http_replayable": true
    },
    {
      "cmd":             "light torch",
      "setup":           null,
      "expect":          ["The corridor is illuminated"],
      "http_replayable": true
    },
    {
      "cmd":             "open chest",
      "setup":           null,
      "expect":          ["Inside the chest"],
      "http_replayable": true
    }
  ]
}
```

---

## Writing a Spine by Hand

You don't need a walkthrough extractor. Any ordered sequence of commands that navigates to interesting game states works. Good spines:

- **Go deep.** Shallow game states (the starting room, trivial commands) aren't interesting to attack. Drive toward complex state: puzzles partially solved, NPCs encountered, inventory loaded up.
- **Prefer breadth over optimality.** A walkthrough that takes the scenic route — visiting more rooms, interacting with more objects — gives the adversarial probes more surface area.
- **Keep `expect` strings short and stable.** Location names, short parser confirmations (`"Taken"`, `"Dropped"`), and opening words of static descriptions hold up well. Avoid full AI-narrator sentences — they'll vary across runs.
- **Set `http_replayable: true` for almost everything.** Only set `false` if the step genuinely cannot be reproduced by replaying the sequence from turn 1 (e.g. a timed random event, or a `setup` injection).

---

## Extracting a Spine from Existing Tests

If your game has automated tests with walkthrough sequences, you can extract them mechanically. AdventureBreaker ships with `tools/extract_spine.py` for ZorkAI's C# NUnit `[TestCase]` format.

For other formats, the extractor needs to:

1. Parse your walkthrough source (test fixtures, text file, JSON, etc.)
2. Emit the spine JSON schema above — one step per command, with `cmd`, `expect`, `setup: null`, and `http_replayable: true`

The output format is simple enough to generate from a shell one-liner if your source is a plain text walkthrough:

```bash
# Example: convert a text walkthrough (one command per line) to spine JSON
python3 - <<'EOF'
import json, sys

commands = [line.strip() for line in open("walkthrough.txt") if line.strip()]
spine = {
    "game": "mygame",
    "source": ["walkthrough.txt"],
    "count": len(commands),
    "replayable_count": len(commands),
    "steps": [
        {"cmd": cmd, "setup": None, "expect": [], "http_replayable": True}
        for cmd in commands
    ]
}
print(json.dumps(spine, indent=2))
EOF
```

Note: `expect` is left empty here — fill it in after a first run against your backend by capturing the responses and inserting stable substrings.

---

## How the Harness Uses the Spine

```
spine-run --count N
```

Advances N steps along the spine. At each step:

1. Sends `cmd` via the play endpoint
2. Checks the response against `expect` substrings (L2 golden transcript oracle)
3. Fires adversarial probes at the current state (the "ribs")
4. Saves/restores around destructive probes so spine progress is never lost
5. Records the transcript and any oracle hits

The spine position is stored in `state.json` so runs can be resumed across sessions.
