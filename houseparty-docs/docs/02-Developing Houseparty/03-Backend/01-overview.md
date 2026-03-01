# Overview

## Responsibility
The backend is the authoritative coordination layer for rooms and games.

It is responsible for:
- Room and game lifecycle state transitions.
- Membership and seat assignment.
- Authorization and role-based action enforcement.
- Broadcasting canonical room/game events to connected clients.

It is not responsible for:
- Frontend rendering concerns.
- Client-specific UX behavior.
- Game logic.

## State and Event Guarantees
- Backend state is the source of truth for room membership, game state, and role outcomes.
- Events must represent canonical state changes (not speculative client state).
- Commands that violate state constraints (full game, invalid transition, stale operation) should fail deterministically.
