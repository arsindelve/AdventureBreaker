# Game API Contract

This document defines the HTTP API a game backend must implement to work with AdventureBreaker. The ZorkAI backend is the reference implementation.

---

## Base URL and Endpoints

Every game is configured with a `base_url` and an `endpoint` path (see `config.py`). All requests are made to `{base_url}{endpoint}` or sub-paths under it.

```
base_url  = https://your-backend.example.com
endpoint  = /YourGame

full URL  = https://your-backend.example.com/YourGame
```

---

## Required Endpoints

### 1. Init — start or resume a session

```
GET {base_url}{endpoint}?sessionId={session_id}
```

- `session_id` — a UUID string identifying the player's session. If the session already exists on the backend, this resumes it. If not, a new game is started.
- Returns: a [Game Response](#game-response-envelope) with the opening text.

---

### 2. Play — send a player command

```
POST {base_url}{endpoint}
Content-Type: application/json

{
  "input":               "open mailbox",
  "sessionId":           "550e8400-e29b-41d4-a716-446655440000",
  "noGeneratedResponses": false
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `input` | string | yes | The raw player command text |
| `sessionId` | string | yes | Session UUID from the init call |
| `noGeneratedResponses` | boolean | yes | When `true`, the backend should return a plain parser response with no AI narrator prose. Used by the A/B oracle to distinguish engine bugs from narrator bugs. |

- Returns: a [Game Response](#game-response-envelope).

---

## Optional Endpoints (Save/Restore)

These endpoints enable the **fork-via-save/restore** mechanic that lets the harness checkpoint before adversarial probes. If your backend doesn't support persistence, the harness still works — save/restore commands will simply fail gracefully.

### Save

```
POST {base_url}{endpoint}/saveGame
Content-Type: application/json

{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "clientId":  "client-abc123",
  "name":      "checkpoint-1",
  "id":        null
}
```

`id` can be `null` to let the backend assign an ID, or a UUID to overwrite a specific save slot.

Returns: JSON with at minimum an `"id"` field containing the save's UUID.

---

### List Saves

```
GET {base_url}{endpoint}/saveGame?sessionId={client_id}
```

Note: the query parameter is `sessionId` but the value passed is the `clientId`, matching the ZorkAI web client convention.

Returns: a JSON array of save objects, each with at minimum `"id"` and `"name"`.

---

### Restore

```
POST {base_url}{endpoint}/restoreGame
Content-Type: application/json

{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "clientId":  "client-abc123",
  "id":        "save-uuid-here"
}
```

Returns: a [Game Response](#game-response-envelope) reflecting the restored state.

---

### Delete Save

```
DELETE {base_url}{endpoint}/saveGame/{save_id}?sessionId={session_id}
```

Returns: any 2xx response on success.

---

## Game Response Envelope

Both the `GET` (init) and `POST` (play) endpoints must return a JSON object with the following fields. All fields are optional in the sense that missing ones default to zero/empty — but the richer the envelope, the more oracle coverage you get for free.

```json
{
  "response":                   "Opening the small mailbox reveals a leaflet.",
  "locationName":               "West of House",
  "moves":                      1,
  "score":                      0,
  "time":                       1,
  "previousLocationName":       null,
  "lastMovementDirection":      null,
  "inventory":                  [],
  "exits":                      [0, 2, 3, 9],
  "actionsAvailableFromLocation": {
    "leaflet": ["take", "read", "examine"]
  },
  "actionsAvailableFromInventory": {}
}
```

### Field reference

| Field | Type | Description |
|---|---|---|
| `response` | string | The text shown to the player — either plain parser output or AI narrator prose depending on `noGeneratedResponses`. |
| `locationName` | string | Name of the player's current location. Used by L1 to verify movement. |
| `moves` | integer | Total move count. Must be monotonically non-decreasing. |
| `score` | integer | Current score. Must be in `[0, max_score]` (configured per game). |
| `time` | integer | In-game time counter. Game-specific meaning, used for transcript only. |
| `previousLocationName` | string \| null | Location before the last move. Used by L1 to catch `previousLocationName` mismatches. Optional. |
| `lastMovementDirection` | string \| null | Direction of the last move (e.g. `"N"`, `"SE"`). Optional. |
| `inventory` | array of strings | Item names currently held by the player. L1 checks this against "Taken"/"Dropped" prose. |
| `exits` | array | Available exits from the current location. See [Direction Encoding](#direction-encoding) below. |
| `actionsAvailableFromLocation` | object | Map of `{ itemName: [verb, ...] }` for items visible in the current location. Optional — enriches L3 judging. |
| `actionsAvailableFromInventory` | object | Map of `{ itemName: [verb, ...] }` for items in the player's inventory. Optional — enriches L3 judging. |

### Direction Encoding

`exits` can be encoded as either **integers** or **strings**. Both are supported:

| Integer | String | Direction |
|---|---|---|
| 0 | `"N"` | North |
| 1 | `"S"` | South |
| 2 | `"E"` | East |
| 3 | `"W"` | West |
| 4 | `"NE"` | Northeast |
| 5 | `"NW"` | Northwest |
| 6 | `"SW"` | Southwest |
| 7 | `"SE"` | Southeast |
| 8 | `"In"` | In |
| 9 | `"Out"` | Out |
| 10 | `"Up"` | Up |
| 11 | `"Down"` | Down |
| 12 | `"Unknown"` | Unknown |

String form is recommended for new implementations — it's self-documenting and avoids the integer mapping dependency.

---

## Oracle Coverage by Field

The more envelope fields you populate, the more free bug detection you get:

| Oracle | Fields required | What it catches |
|---|---|---|
| L0 contract | HTTP status, JSON validity, `score`, `moves` | Server errors, malformed responses, score out of bounds |
| L0 empty response | `response` | Narrator returned nothing |
| L1 movement | `locationName`, `previousLocationName` | Move consumed a turn but location didn't change |
| L1 inventory | `inventory` | "Taken"/"Dropped" prose contradicts inventory state |
| L1 score/move | `score`, `moves` | Regressions |
| L2 narrator leak | `response` | Narrator broke character or leaked meta-prompt |
| L3 critic | all of the above | Hallucinations, state contradictions, spoilers |

---

## Configuring a New Game

Once your backend implements the contract above, register it in `adventurebreaker/config.py`:

```python
GAME_BACKENDS = {
    # ... existing games ...

    "mygame": {
        "name":          "My Game",
        "endpoint":      "/MyGame",
        "targets": {
            "prod":  "https://your-backend.example.com",
            "local": "http://localhost:5000",
        },
        "max_score":     100,
        "target_score":  100,
        "spine":         "mygame.json",
    },
}
```

Then write a [spine file](spine-format.md) at `adventurebreaker/spine/mygame.json` and run:

```bash
python3 -m adventurebreaker.harness new --game mygame
```

No other code changes required — the harness, oracles, and ledger all pick up the new game automatically.

---

## Notes for New Implementations

**On `noGeneratedResponses`:** this flag is the key to the A/B oracle. When `true`, return the raw parser response without AI narration. If your game doesn't have a narrator/LLM layer, set it to `true` always (or ignore the flag) — L3 judging simply won't apply, but L0–L2 still give full coverage.

**On `actionsAvailableFrom*`:** these are optional but significantly improve L3 judging — Claude can check whether the narrator describes actions or objects that aren't in the action set. Even a partial list helps.

**On save/restore:** not required. If absent, omit `saveGame`/`restoreGame` endpoints entirely. The harness `save` and `restore` commands will error but won't crash the session.
