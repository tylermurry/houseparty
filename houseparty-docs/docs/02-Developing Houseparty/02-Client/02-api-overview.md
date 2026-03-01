import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# API Overview

## Concepts
### `TState`
`TState` is the game-state type for your app. It is threaded through the SDK so game snapshots are strongly typed everywhere you read state.

Key points:
* `createHousePartyClient<TState>()` sets the state type once for that client instance.
* `GameHandle<TState>.state` and `onStateChange` use that same type.
* `parseState(rawState)` converts backend `unknown` snapshots into your typed `TState`.
* If no parser is provided, the SDK uses a default parser that returns JSON-parsed values when possible, otherwise the raw value.

Example:
```ts
type TicTacToeState = {
  board: Array<'X' | 'O' | null>
  nextPlayer: number
}

const client = createHousePartyClient<TicTacToeState>({
  baseUrl: 'https://my-houseparty-backend.com',
  parseState: (raw) => raw as TicTacToeState,
})
```

## Factory
The client SDK factory is responsible for creating configured client instances.

<Tabs>
<TabItem value="Methods" default>

### `createHousePartyClient<TState>(options)`
Creates a configured client instance with HTTP and realtime support.

<details>

#### Parameters
* `options` - Configuration for API base URL, optional state parser, optional custom fetch, and tracing behavior.

#### Returns
* `HousePartyClient<TState>` - A configured client instance.

#### Example Usage
```js
const client = createHousePartyClient({
  baseUrl: 'https://my-houseparty-backend.com',
  parseState: (rawState) => {
    // Custom parsing logic to transform raw state into TState
    return rawState as TState;
  },
  logLevel: 'debug',
});

// Do something with the client now
client.createRoom().then((room) => {
  console.log('Created room with ID:', room.id);
});
```

</details>

</TabItem>

<TabItem value="Properties">
    None
</TabItem>
</Tabs>

## House Party Client

<Tabs>
<TabItem value="Methods" default>

### `createRoom()`
Creates a room and returns a room handle for the room id

<details>

#### Parameters
None

#### Returns
* `Promise<RoomHandle<TState>>` - A promise that resolves to a room handle

#### Example Usage
```js
const room = await client.createRoom();
console.log('Created room with ID:', room.id);
```
</details>

### `joinRoom(roomId, name, options?)`
Joins a room session and returns the room handle plus caller player handle.

<details>

#### Parameters
* `roomId: string` - The identifier of the room to join.
* `name: string` - The display name of the player joining the room.
* `options?: { playerNumber?: number | null }` - Optional parameters for joining the room, such as a preferred player number.

#### Returns
* `Promise<{ room: RoomHandle<TState>; player: PlayerHandle }>` - A promise that resolves to an object containing the room handle and player handle for the joined session.

#### Example Usage
```js
const { room, player } = await client.joinRoom('my-room-id', 'Alice', { playerNumber: 1 });
console.log('Joined room with ID:', room.id);
console.log('Player handle:', player.id);
```

</details>

</TabItem>

<TabItem value="Properties">
    None
</TabItem>
</Tabs>

## Room
Represents an active room session, including roster state, room event subscriptions, and room-scoped actions.

<Tabs>
<TabItem value="Methods" default>

### `listenForEvents(cb)`
Subscribes to room-level events.

<details>

#### Parameters
* `cb: (event: RoomEvent) => void` - Called when roster, game-event, or game-state-snapshot updates are received.

#### Returns
* `() => void` - Unsubscribe function.

#### Example Usage
```ts
const stopListening = room.listenForEvents((event) => {
  if (event.type === 'playerRosterUpdated') {
    console.log('Roster size:', event.players.length)
  }
})

// Later
stopListening()
```

</details>

### `useMousePresence(target)`
Enables local mouse publishing and remote mouse subscriptions for the room.

<details>

#### Parameters
* `target: Window` - The browser window to track pointer movement from.

#### Returns
* `MousePresenceHandle` - Handle for reading/listening to remote mouse positions and stopping presence.

#### Example Usage
```ts
const mousePresence = room.useMousePresence(window)
const dispose = mousePresence.listen((positions) => {
  console.log('Remote cursors:', positions)
})

// Later
dispose()
mousePresence.stop()
```

</details>

### `createTurnBasedGame(adminPlayer, seatCount)`
Creates a turn-based game from the room.

<details>

#### Parameters
* `adminPlayer: PlayerHandle` - Player creating/administering the game.
* `seatCount: number` - Number of player seats.

#### Returns
* `Promise<GameHandle<TState>>` - Created game handle.

#### Example Usage
```ts
const game = await room.createTurnBasedGame(player, 2)
console.log('Game id:', game.id)
```

</details>

### `dispose()`
Disposes room resources and realtime subscriptions.

<details>

#### Parameters
None

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await room.dispose()
```

</details>

</TabItem>

<TabItem value="Properties">

### `id`
* Type: `string`
* The room identifier.

### `players`
* Type: `readonly PlayerSummary[]`
* Latest roster known to this room handle.

</TabItem>
</Tabs>

## Player
Represents the local caller identity and exposes player-scoped game commands.

<Tabs>
<TabItem value="Methods" default>

### `join(game)`
Joins a game as this player.

<details>

#### Parameters
* `game: GameHandle<TState>` - Target game handle.

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await player.join(game)
```

</details>

### `startTurn(game)`
Starts this player's turn.

<details>

#### Parameters
* `game: GameHandle<TState>` - Target game handle.

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await player.startTurn(game)
```

</details>

### `makeMove(game, movePayload)`
Submits a move payload for the current turn.

<details>

#### Parameters
* `game: GameHandle<TState>` - Target game handle.
* `movePayload: string` - Serialized move payload expected by backend game logic.

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await player.makeMove(game, JSON.stringify({ cell: 4 }))
```

</details>

### `endTurn(game, statePayload)`
Ends this player's turn and submits the state snapshot payload.

<details>

#### Parameters
* `game: GameHandle<TState>` - Target game handle.
* `statePayload: string` - Serialized game state snapshot.

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await player.endTurn(game, JSON.stringify({ board: ['X', null, null] }))
```

</details>

</TabItem>

<TabItem value="Properties">

### `id`
* Type: `string`
* Stable SDK player id (`player-{number}`).

### `number`
* Type: `number`
* Numeric player slot in the room/game.

### `name`
* Type: `string`
* Display name for this player.

</TabItem>
</Tabs>

## Game
Represents one game projection, including lifecycle commands, event history, and parsed state.

<Tabs>
<TabItem value="Methods" default>

### `start(adminPlayer)`
Starts the game.

<details>

#### Parameters
* `adminPlayer: PlayerHandle` - The admin/host player.

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await game.start(player)
```

</details>

### `end(adminPlayer)`
Ends the game.

<details>

#### Parameters
* `adminPlayer: PlayerHandle` - The admin/host player.

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await game.end(player)
```

</details>

### `onEvent(cb)`
Subscribes to ingested game events.

<details>

#### Parameters
* `cb: (event: GameEvent) => void` - Called for each parsed game event.

#### Returns
* `() => void` - Unsubscribe function.

#### Example Usage
```ts
const stop = game.onEvent((event) => {
  console.log('Game event:', event.name)
})
```

</details>

### `onStateChange(cb)`
Subscribes to parsed game-state snapshot changes.

<details>

#### Parameters
* `cb: (state: TState | null) => void` - Called when `state` updates.

#### Returns
* `() => void` - Unsubscribe function.

#### Example Usage
```ts
const stop = game.onStateChange((state) => {
  console.log('New state:', state)
})
```

</details>

### `onObjectsChange(cb)`
Subscribes to derived object-lock projection changes.

<details>

#### Parameters
* `cb: (objects: readonly GameObjectLock[]) => void` - Called when lock projection updates.

#### Returns
* `() => void` - Unsubscribe function.

#### Example Usage
```ts
const stop = game.onObjectsChange((objects) => {
  console.log('Locked objects:', objects.length)
})
```

</details>

### `dispose()`
Disposes game subscriptions and in-memory emitters.

<details>

#### Parameters
None

#### Returns
* `Promise<void>`

#### Example Usage
```ts
await game.dispose()
```

</details>

</TabItem>

<TabItem value="Properties">

### `id`
* Type: `string`
* The game identifier.

### `events`
* Type: `readonly GameEvent[]`
* In-memory history of ingested game events.

### `state`
* Type: `TState | null`
* Last parsed game-state snapshot.

### `objects`
* Type: `readonly GameObjectLock[]`
* Derived object-lock projection computed from event history.

</TabItem>
</Tabs>

## Mouse Presence
Represents room mouse-presence state and subscriptions.

<Tabs>
<TabItem value="Methods" default>

### `listen(cb)`
Subscribes to remote mouse-position updates.

<details>

#### Parameters
* `cb: (mousePositions: readonly MousePosition[]) => void` - Called on each presence update.

#### Returns
* `() => void` - Unsubscribe function.

#### Example Usage
```ts
const stop = mousePresence.listen((mousePositions) => {
  console.log(mousePositions)
})
```

</details>

### `stop()`
Stops tracking and publishing presence for this handle.

<details>

#### Parameters
None

#### Returns
* `void`

#### Example Usage
```ts
mousePresence.stop()
```

</details>

</TabItem>

<TabItem value="Properties">

### `mousePositions`
* Type: `readonly MousePosition[]`
* Latest pixel coordinates and metadata for remote players.

</TabItem>
</Tabs>
