# API Overview

## Rooms and Realtime Bootstrapping

### `POST /api/rooms`
Creates a new room id.

<details>

Request:
```http
POST /api/rooms HTTP/1.1
Content-Type: application/json

```

Response (`200 OK`):
```json
{
  "id": "room-123"
}
```
</details>

#### `POST /api/signalr/negotiate`
Returns SignalR connection metadata for the client.

<details>

Request:
```http
POST /api/signalr/negotiate HTTP/1.1
Content-Type: application/json

```

Response (`200 OK`):
```json
{
  "url": "https://example.service.signalr.net/client/?hub=roomhub",
  "accessToken": "eyJ..."
}
```
</details>

#### `POST /api/rooms/{roomId}/join`
Adds user to room membership and returns assigned player plus full roster.

<details>

Path Parameters:
- `roomId: string`

Request:
```http
POST /api/rooms/room-123/join HTTP/1.1
Content-Type: application/json

{
  "connectionId": "connection-abc",
  "name": "Taylor",
  "playerNumber": 1
}
```

Response (`200 OK`):
```json
{
  "player": {
    "id": "player-1",
    "name": "Taylor",
    "role": "host",
    "playerNumber": 1
  },
  "players": [
    {
      "id": "player-1",
      "name": "Taylor",
      "role": "host",
      "playerNumber": 1
    }
  ]
}
```
</details>

#### `POST /api/rooms/{roomId}/mouse`
Broadcasts caller mouse presence to room subscribers.

<details>

Path Parameters:
- `roomId: string`

Request:
```http
POST /api/rooms/room-123/mouse HTTP/1.1
Content-Type: application/json

{
  "playerNumber": 1,
  "name": "Taylor",
  "x": 744,
  "y": 312
}
```

Response (`202 Accepted`):
```json
{}
```
</details>

### Turn-Based Game Commands

#### `POST /api/engine/turn-based-game/create-game`
Attempts to create a game lobby from host/admin player context.

<details>

Request:
```http
POST /api/engine/turn-based-game/create-game HTTP/1.1
Content-Type: application/json

{
  "playerId": "player-1",
  "seatCount": 4
}
```

Response (`200 OK`):
```json
{
  "gameCreated": true,
  "gameId": "game-abc",
  "errorMessage": null
}
```
</details>

#### `POST /api/engine/turn-based-game/join-game`
Attempts to join the player to the target game.

<details>

Request:
```http
POST /api/engine/turn-based-game/join-game HTTP/1.1
Content-Type: application/json

{
  "gameId": "game-abc",
  "playerId": "player-2"
}
```

Response (`200 OK`):
```json
{
  "joined": true,
  "errorMessage": null
}
```
</details>

#### `POST /api/engine/turn-based-game/start-game`
Attempts to transition the game to active state.

<details>

Request:
```http
POST /api/engine/turn-based-game/start-game HTTP/1.1
Content-Type: application/json

{
  "gameId": "game-abc",
  "playerId": "player-1"
}
```

Response (`200 OK`):
```json
{
  "gameStarted": true,
  "gameId": "game-abc",
  "errorMessage": null
}
```
</details>

#### `POST /api/engine/turn-based-game/end-game`
Attempts to end an active game.

<details>

Request:
```http
POST /api/engine/turn-based-game/end-game HTTP/1.1
Content-Type: application/json

{
  "gameId": "game-abc",
  "playerId": "player-1"
}
```

Response (`200 OK`):
```json
{
  "gameEnded": true,
  "errorMessage": null
}
```
</details>

#### `POST /api/engine/turn-based-game/start-turn`
Attempts to start the current player turn.

<details>

Request:
```http
POST /api/engine/turn-based-game/start-turn HTTP/1.1
Content-Type: application/json

{
  "gameId": "game-abc",
  "playerId": "player-1"
}
```

Response (`200 OK`):
```json
{
  "turnStarted": true,
  "errorMessage": null
}
```
</details>

#### `POST /api/engine/turn-based-game/end-turn`
Ends the turn, persists state payload, and returns the accepted state snapshot.

<details>

Request:
```http
POST /api/engine/turn-based-game/end-turn HTTP/1.1
Content-Type: application/json

{
  "gameId": "game-abc",
  "playerId": "player-1",
  "statePayload": "{\"round\":2,\"activePlayer\":\"player-2\"}"
}
```

Response (`200 OK`):
```json
{
  "turnEnded": true,
  "statePayload": "{\"round\":2,\"activePlayer\":\"player-2\"}",
  "errorMessage": null
}
```
</details>

#### `POST /api/engine/turn-based-game/make-move`
Attempts to apply one move payload in current game context.

<details>

Request:
```http
POST /api/engine/turn-based-game/make-move HTTP/1.1
Content-Type: application/json

{
  "gameId": "game-abc",
  "playerId": "player-1",
  "movePayload": "{\"action\":\"draw-card\"}"
}
```

Response (`200 OK`):
```json
{
  "moveAccepted": true,
  "errorMessage": null
}
```
</details>

## Validation and Failure Expectations
- `POST /api/rooms/{roomId}/join` returns `400` when `connectionId` or `name` is missing.
- `POST /api/rooms/{roomId}/mouse` returns `400` for invalid `playerNumber`, missing `name`, or coordinates outside `0..2047`.
- Turn-based command endpoints return `200` with success booleans and optional `errorMessage` when a domain command is rejected.

## Public Realtime Interface (SignalR)
The backend broadcasts these events to room group subscribers:
- `playerRosterUpdated` with payload `RoomPlayer[]`
- `mousePresenceUpdated` with payload `MousePresenceUpdate`
- `gameEvent` with payload `GameEvent` object
- `gameStateSnapshot` with payload state snapshot object/string

## Authorization and Role Expectations
- Only `host` may create/start/end games.
- Non-host attempts are rejected by game command processing and surfaced as unsuccessful command responses.
- Role transitions for `guest`/`player`/`spectator` are authoritative in backend state and reflected to clients through events/snapshots.
