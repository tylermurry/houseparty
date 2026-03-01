# Overview

## House Party in 5 Minutes
HouseParty is a realtime multiplayer coordination platform for lightweight social games.

Its purpose is to take all the hard work out of developing turn-based and real-time social games. 
By providing easy-to-use frameworks for room management, player presence, role handling, and networking, HouseParty lets developers ship games quickly.

House Party is not a game engine. Instead, it's game host and coordination platform where developers can integrate their own games.

In practice, HouseParty provides:
- A shared room where people gather before and after gameplay.
- Host-driven game setup (choose game and seat count).
- Clear participant roles (`host`, `guest`, `player`, `spectator`) with predictable permissions.
- Canonical backend state and realtime event delivery so all clients stay in sync.

In all of this HouseParty delivers a common-sense API that handles the hard stuff so developers can focus on building great games.

### Architecture
HouseParty is split into clear layers with explicit ownership:

- Frontend: route-level UI, interaction flows, and role-aware rendering.
- Client: typed API/realtime abstraction and connection lifecycle management.
- Backend: authoritative room/game coordination and canonical state transitions.

#### House Party Framework
The framework is the shared coordination system that game UIs build on top of:

- Room lifecycle: create room, join room, roster updates, leave/disconnect handling.
- Game lifecycle: create game, join seat, start game, end game.
- Role model: `host`, `guest`, `player`, `spectator`.
- Realtime sync: room and game events flow through backend + SignalR to connected clients.
- State authority: backend is the source of truth; clients render and react to canonical events.

#### House Party Games
Games plug into the framework lifecycle instead of reimplementing networking and coordination:

- The host selects a game and seat count.
- Guests join available seats to become players.
- Once started, players can interact; non-players spectate in read-only mode.
- Ending a game returns users to the room/game catalog flow, ready for the next session.

## Components of a House Party Experience
Every session combines these components:

- Room: shared space where users gather before and after gameplay.
- Game lobby: pre-game setup area where seats are filled.
- Active game: interactive game phase for players, view-only phase for spectators.
- Realtime presence: roster and cursor/presence updates across connected users.
- Coordination API: backend commands and events that keep everyone in sync.

### Actors
These actors participate in every lifecycle flow:

- User: person interacting with HouseParty.
- Frontend: web app used by users to trigger actions and render state.
- Client: integration layer between frontend and backend contracts.
- API: backend services that validate commands and emit canonical room/game events.

### Roles
Roles are contextual and can change during a session:

- Host: user who created the room; can create/start/end games.
- Guest: room member who has not joined the active game.
- Player: user who joined the current game and can interact with gameplay.
- Spectator: room member observing an active game without gameplay interaction.
