# Room and Game Lifecycle

This document defines comprehensive user stories for these lifecycle flows:
1. Create Room
2. Join Room
3. Create Game
4. Join Game
5. Start Game
6. End Game

## Shared Context

### `Actors`
- User: Someone who interacts with House Party rooms and games.
- Frontend: The House Party web app that players use to interact with rooms and games.
- Client: The client library acts as a logic bridge between the frontend and API
- API: The set of backend services that manage rooms, games, and player interactions.

### `Roles`
- Host: A user who creates a room. This `role` has privileges to manage the room and games within it.
- Guest: A user who joins an existing room.
- Player: A user who has joined an active game within the room. This `role` can interact with the game and other players in that game.
- Spectator: A user who is not a `player` while a game is active. This `role` can view ongoing gameplay in a read-only manner.

## Story 1: Create Room

### User Story
As a `host`, I want to create a new room from the home screen so that I can invite other users.

### Acceptance Criteria
* When a `host` clicks "Create Room" on the home screen, a new room is created.
    * An invalid or failed room creation will be displayed to the `host` so they can retry.
* Once the room is created, the `host` is redirected to `/room/{id}`.


## Story 2: Join Room

### User Story
As a `guest`, I want to join a room with a display name so that I can participate in games.

### Acceptance Criteria
* When a `guest` enters a room (e.g. navigates to `/room/{roomId}`), they are presented with a dialog to enter their name.
* When a `guest` enters a valid name and clicks "Join Room", they are added to the room roster and can see other users in the room.
    * Valid name is between 1 and 10 characters
* When a user joins a room and there is no active game, that user is a `guest`.
* A `spectator` role is only assigned during an active game.


## Story 2.1: Mouse Presence in Room

### User Story
As a `guest`, I want to see where other users in the room are pointing so that collaboration feels live and interactive.

### Acceptance Criteria
* A `guest` will see a visual cursor indicator for other users in the room.
* A `guest` can see each active cursor move in near real time as other users move their mouse.
* A `guest` can see the other user name attached to each visible cursor.
* A `guest` will stop seeing a cursor indicator when a user leaves the room.


## Story 3: Create Game

### User Story
As a `host`, I want to create a game in the room so `guests` can join the game.

### Acceptance Criteria
* Game Catalog
    * A `host` will see a game catalog after joining a room.
    * `guest` users will see the game catalog, but cannot interact with it.
    * `guest` users will see a message that says "Waiting on host to choose a game" in the top header area.
* Game Selection and Setup
    * A `host` can select a game from the game catalog.
    * After selecting a game, the `host` will be asked to select the number of players.
    * `guest` users will see the game configuration screen but cannot interact with it.
* Create Game
    * After selecting the number of players, the `host` can click "Create Game" to create the game lobby.
    * `guest` users will see the game lobby once the game is created.


## Story 4: Join Game

### User Story
As a `guest`, I want to join the created game so that I can participate in gameplay.

### Acceptance Criteria
* Once the `host` has created the game, a game lobby screen will be presented to all room users (`host` and `guests`).
* The game lobby will show the game name, a list of `players` who have joined the game, and how many seats are left.
* The game lobby will have a "Join Game" button for `guests` to join the game.
* Once a `guest` joins the game:
    * The `user` becomes a `player` for that game.
    * The "Join Game" button will disappear for that `user`.
    * The `player` name will be shown in the game roster as joined.
    * That `player` will see a message that says "Waiting on host to start the game" in the top header area.


## Story 5: Start Game

### User Story
As a `host`, I want to start the game once players are ready so gameplay can begin.

### Acceptance Criteria
* Once the open seats are filled, A `host` will see a "Start Game" button in the game lobby.
* When a `host` starts the game, all `players` in that game will transition into active gameplay.
* A `guest`, `player`, or `spectator` who is not the `host` cannot start the game and will see a clear message explaining why.
* All `players` will see the game UI and be able to interact with the game.
* All `spectators` will see the game UI to observe gameplay but will not be able to interact with the game.
* All `spectators` will see a message that says "You are spectating this game"
* Any `user` that joins the room after the game has started will be `spectators`


## Story 6: End Game

### User Story
As a `host`, I want to end the current game so that gameplay is finalized and resources can be cleaned up.

### Acceptance Criteria
* A `host` can end an active game.
* A `guest`, `player`, or `spectator` who is not the `host` cannot end the game and will see a clear message explaining why.
* When the game is ended,
    * All `players` in that game will receive a confirmation dialog that says "The host has ended the game" which they can click "Ok" to
    * All `players` will be taken back to the game catalog screen and become `guests`.
    * All `spectators` will see the game catalog screen and become `guests`.
    * The `host` remains `host`.

