# Overview

## Responsibility
The frontend owns user experience and presentation logic for room and game lifecycle flows.

It is responsible for:
- Creating and joining rooms.
- Creating, joining, starting, and ending games.
- Rendering live presence, roster changes, and game state updates.
- Enforcing role-aware UI behavior (`host`, `guest`, `player`, `spectator`) through visible/disabled actions.

## Frontend Layering Standard

### Views
Own:
- Route-level composition, layout, and template structure.
- UI interactions (button clicks, form submits, dialogs).
- View-only animations/timers/transitions.

Do not own:
- API calls.
- Business orchestration.
- Response normalization.

Examples:
- Room creation form with validation and feedback.
- Game join button that disables when room is full.
- Spectator-only UI elements that are hidden from players.

### Composables
Own:
- Feature orchestration and side effects.
- Lifecycle wiring (`onMounted`, `watch`, cleanup).
- State transitions for loading, success, empty, and error states.

Do not own:
- DOM rendering concerns.
- Network protocol details.

Examples:
- Orchestrating the flow of joining a game: validating input, orchestrating client calls, handling success/failure, and updating view state.
- Managing the lifecycle of a game session: starting timers, subscribing to realtime updates, and cleaning up on unmount.