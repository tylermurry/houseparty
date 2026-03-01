# Overview

## AI and this Repo
AI was heavily used in the creation of this project and will continue to be heavily used going forward.
Therefore, it is critical that we treat AI as a first-class citizen during the development lifecycle of this project.
The AGENT.md file and documentation are essential to maintain well as they influence the approach of the AI greatly.

As such, great care should be taken to maintain high quality within this codebase. **AI-slop will not be tolerated.**

## High-Level Layer Responsibilities

HouseParty is split into clear layers with explicit ownership:
- Frontend: Owns user experience, route-level UI, and view orchestration.
- Client: Owns transport abstraction (HTTP + realtime), connection lifecycle, and typed client operations.
- Backend API: Owns authoritative coordination, authorization, and canonical room/game state transitions.

## Repo Overview

### /HouseParty.Server
This is the backend server that hosts the coordination plane of HouseParty.
It is responsible for managing the coordination of game sessions, enforcing policies, and providing a robust infrastructure for game developers to build and deploy their games.

### /HouseParty.GameEngine
This project contains the core game engine used by HouseParty.

### /houseparty-client
This project is the SDK for interacting with the houseparty server. It aims to abstract the difficult parts away so that developers can focus on building their game and let house party handle the coordination.

### /houseparty-frontend
This is the web frontend used by room participants to create/join rooms, create/join games, and play/spectate in realtime.

### /HouseParty.E2E

### /HouseParty.AppHost
