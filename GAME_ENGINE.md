# Core Engine

## The 4 Coordination Primitives

These primitives are the only things the backend knows how to do. They provide coordination, ordering, and storage. Importantly, they do not provide any game logic.
The backend never applies game rules or validates moves; it only provides coordination primitives and storage. All legality, scoring, win conditions, and resolution live in the frontend.
Implementation note: the primitives are exposed via an `IPrimitivesService` interface so the engine can be mocked in tests and swapped in tooling.

### AcquireToken(gameId, tokenId)
This method allows the game to *lock* a game object. The backend uses Redis to ensure that only one caller can acquire the token at a time. All simultaneous requests are automatically serialized.

**When to use**  
Scenarios where only **one** player is allowed to act on a game object.

**Example**  
A player starts a turn by acquiring the `"turn"` token. While the token is held, no other player can take turn-based actions.
The backend does not decide who is allowed to acquire the token. That logic lives entirely on the frontend.

### ReleaseToken(gameId, tokenId)
Releases a previously acquired token, making the game object available again. Tokens are treated as short-lived leases and may also expire automatically via TTL if a client disconnects.

**When to use**  
When exclusive access to a game object is no longer required.

**Example**  
At the end of a turn, the active player releases the `"turn"` token so the next player may act.
The backend does not validate turn ownership. It only releases the token.

### AppendOrderedEvent(gameId, eventName)
Appends an event to a per-game ordered log. The backend assigns a monotonically increasing sequence number and broadcasts the event to all clients. The backend does not interpret the event or apply any game logic.

Note, the intent for these events is to be ephemeral, not authoritative in the long term.
Every `CommitState` implicitly closes an **event epoch** and will clear out events.

**When to use**  
Scenarios where multiple players may act simultaneously and the outcome depends on **who acted first**.

**Example**  
In a Musical Chairs–style game, each player clicking a square appends a `SquareClicked` event. Clients replay events in sequence order to determine which players successfully claimed a square and which player was eliminated.

EventLog
* ID: 1 - Player A clicked square 1 \[2026:02-07T08:21:24.432\]
* ID: 2 - Player B clicked square 1 \[2026:02-07T08:21:24.532\]
* ID: 3 - Player C clicked square 2 \[2026:02-07T08:21:25.865\]
* ...
### SetData(gameId, data, baseRev)
Stores a full representation of the game data as a JSON blob. The state is only accepted if `baseRev` matches the current revision, preventing out-of-sync updates. On success, the backend increments the revision and broadcasts the new state.

**When to use**  
Scenarios where the game reaches a stable decision point and the current state should be committed atomically.

**Example**  
After a round completes and the loser is determined, the game commits the updated player list and scores as a new state so all clients are synchronized.

### Data vs State
Game **Data** is the persistent JSON blob. Game **State** is the combination of `locks`, `events`, and `data`. The backend never interprets either; it stores and broadcasts them as opaque payloads.

---

## Game Engine Operations
Operations are the the **verbs** that the game engine understands. Each is built on top of the primitives defined earlier.

There are six categories of operation:
* `Exclusive Operations` - "Only one player may do this at at time."
* `Contested Operations` - "Multiple players may act. Order decides."
* `Declarative Operations` - “I am stating my intent.”
* `Barrier Operations` - “Wait until conditions are satisfied.”
* `Commit Operations` - “This is now the official state.”
* `Lifecycle Operations` - “The game is entering or exiting a mode.”

Each operation passes along an operation context:

```ts
type GameId = string
type PlayerId = string
type TokenId = string
type PhaseId = string
type Revision = number
type Timestamp = number

interface OperationContext {
  gameId: GameId
  playerId: PlayerId
  now: Timestamp
}
```

### Exclusive Operations

#### Overview
An **Exclusive Operation** establishes temporary, exclusive control over a shared game resource. While this operation is active, all other players are prevented from performing actions that require the same exclusivity.

Exclusive operations are **time-bounded** and **self-healing**. If the controlling player disconnects or becomes unresponsive, exclusivity is automatically released, preventing permanent deadlocks.

This operation does not validate whether the player _should_ have exclusivity — that responsibility belongs entirely to the frontend game logic.

Built From
- `AcquireToken`
- `ReleaseToken`

#### Operations
* `AcquireControl` - Claims exclusive control over a shared game context or object. While control is held, other players attempting the same operation will be rejected or blocked. Commonly used to start a turn or claim a role. The backend does not decide who should hold control.
* `ReleaseControl` - Releases previously acquired exclusive control, allowing other players to act. This may be triggered explicitly or implicitly when control expires.
* `RevokeControl` - Forcibly removes exclusive control from its current holder when the normal control flow cannot complete.
* `SetActivePlayer` - Declares a single active player for turn-based actions and guards active-player-only actions. While set, other players are blocked from actions that require active-player exclusivity.
* `ReleaseActivePlayer` - Clears the active player designation, allowing another player to become active for exclusive turn-based actions.
* `RevokeActivePlayer` - Forcibly clears the active player designation when normal turn flow cannot complete or a player becomes unresponsive.
* `ClaimRole` - Attempts to become the sole holder of a named role for a period of time (e.g., dealer, judge, narrator). Fails if the role is already occupied.
* `ReleaseRole` - Voluntarily release a role. When a player releases a role, they are declaring that they no longer require the exclusive privileges associated with it, and that another player may now claim the role.
* `RevokeRole`  - Forced revocation of a role by the game engine. Revocation is not a gameplay action. It is a recovery and safety operation used when the normal coordination flow cannot complete. This includes cases where a player disconnects, becomes unresponsive, or otherwise blocks progress.
*  `LockPhase` - Prevents other players from initiating phase changes while a critical operation is underway, such as scoring or resolution.


### Contested Operations

#### Overview
A **Contested Operation** records actions from multiple players when simultaneous input is expected. The engine’s sole responsibility is to impose a global, authoritative ordering on those actions and broadcast them to all clients.

The engine does not interpret the actions, resolve conflicts, or determine winners. Instead, it produces a deterministic timeline that clients can replay to reach the same conclusions independently.

Built From
- `AppendOrderedEvent`

#### Operations
* `SubmitAction` - Records a competitive action that participates in a shared, ordered timeline. The engine guarantees relative ordering but does not interpret meaning.
* `ClaimResource` - Attempts to acquire a shared resource in a competitive context. Multiple claims may be submitted; the earliest valid claim is typically honored by frontend logic.


### Declarative Operations

#### Overview
A **Declarative Operation** records a player’s stated intent without immediately resolving or applying it. These operations exist to support mechanics where decisions are collected first and resolved later.

Declarative operations are often used in groups, where meaning only emerges once enough declarations have been made. They may be overridden, ignored, or invalidated by frontend logic, but the engine treats them as neutral facts.

The key distinction from contested operations is that ordering is usually irrelevant. What matters is _who declared what_, not who declared first.

Built From
- `AppendOrderedEvent`
- Optionally `SetData`

#### Operations
* `DeclareIntent` - States a player’s intended action or choice without resolving it. Often used as a building block for voting, drafting, or simultaneous reveal mechanics.


### Barrier Operations

#### Overview
A **Barrier Operation** represents a synchronization point. It allows players to signal that they have reached a certain stage, and allows the group to observe collective readiness.

Barriers do not automatically advance the game. Instead, they expose coordination state so that frontend logic can decide when and how to proceed.

Built From
- `AppendOrderedEvent`
- Optionally `SetData`

#### Operations
* `ArriveAtBarrier` Signals that a player has reached a synchronization point. Does not imply completion or success.
* `SignalReady` - Indicates readiness to proceed past the barrier. Often aggregated across players.


### Commit Operations

#### Overview
A **Commit Operation** publishes a new authoritative game state. It marks a moment of consensus where ephemeral actions, declarations, and observations are collapsed into a single, agreed-upon representation.

Commit operations are atomic and revision-checked. They ensure that all players converge on the same state, or else the commit fails and must be retried with updated context.

This is the _only_ operation that mutates long-lived game state.

Built From
- `SetData`

#### Operations
* `CommitState` - Publishes a new authoritative game state snapshot, replacing the previous state if revisions match.
  * This operation does not validate the contents of the state. It only stores it if the revision matches.


### Lifecycle Operations

#### Overview
A **Lifecycle Operation** marks a transition in the overall structure of the game. These operations define _when_ certain actions are meaningful and _when_ previous actions are no longer relevant.

Lifecycle operations provide **temporal boundaries**. They often accompany state commits, token resets, or event log clearing, but the engine does not assume any specific behavior beyond ordering and visibility.

Built From
- Any combination of primitives

#### Operations
* `StartGame` - Transitions the game from setup to active play. Often initializes state and clears pre-game events.
* `EndGame` - Marks the game as completed and prevents further gameplay operations.
* `EnterPhase` - Transitions the game into a named phase, altering the semantic meaning of actions.
* `ExitPhase` - Closes the current phase and prepares for the next transition.
* `ResetGame` - Returns the game to an initial or neutral state, clearing events, control tokens, and committed data.


---
## Game Archetypes

### Overview

A GameArchetype is a compile-time coordination contract between the game and the engine.

It defines:
- The **coordination model** the game uses
- The **high-level operations** available to the game
- The **legal sequencing** of those operations

A GameArchetype does not:
- Encode game rules
- Define scoring, win conditions, or legality
- Add new backend capabilities
- Change the engine primitives

### Turn-Based Game

Coordination Model
- Serialized authority
- One active player at a time
- Discrete moves
- Explicit turn boundaries

Typical Games
- Chess / Checkers
- Card games
- Async games

#### High-Level Operations

* `startTurn` - Acquires exclusive turn authority and marks the beginning of a turn. Fails if a turn is already active.
* `submitMove` - Records the active player’s move. This is only callable by the turn owner.
* `resolve` - Commits the authoritative state. Can only be called by the active turn holder, once per turn. At the end, it releases the turn lock and clears the active player.

### Real-Time Game

Coordination Model
- Concurrent authority
- Order determines outcome
- Continuous or bursty interaction
- No exclusive ownership

Typical Games
- Reflex games
- Party games
- Action games
- Multiplayer mini-games

#### High-Level Operations

* `submitAction` - Submits an action into a globally ordered stream. Everyone may act at any time
  * Compiles to: `AppendOrderedEvent("action")`
* `waitForSyncPoint` - Signals player readiness within the engine based on a "sync point". Waits until all players have reached this sync point before yielding. Useful for situations where the game needs to wait until all players have taken an action.
* `resolve` - Commits the result of real-time interactions.

---
## Engine Surface Area
The engine exposes coordination primitives via services and controller endpoints. It does not contain game logic.

### Services
- `PrimitivesService` owns raw primitive calls: token, event, and data storage.
  - `primitives.Tokens` -> acquire/release/clear token primitives
  - `primitives.Events` -> append/read/clear ordered event primitives
  - `primitives.Data` -> set/get/clear revisioned data primitives
- `OperationsService` composes primitives into categories: Exclusive, Contested, Declarative, Barrier, Commit, Lifecycle.
  - Current Exclusive operations in code: `ControlObject`, `ReleaseObjectControl`, `RevokeObjectControl`, `SetActivePlayerAsync`, `ReleaseActivePlayerAsync`, `RevokeActivePlayerAsync`.

### State Retrieval
- `GetData` returns only the persistent data blob.
- `GetState` returns the full state payload: `locks`, `events`, and `data`.
