# Overview

## Responsibility
The client abstracts transport and coordination details so application code can use stable, domain-oriented operations.

It is responsible for:
- Managing HTTP and realtime connection orchestration.
- Negotiating/joining room sessions.
- Exposing stable commands for room and game lifecycle operations.
- Subscribing to canonical backend events and translating them into predictable client-side updates.

## Contract Ownership
Every command should define:
- Input contract.
- Success output shape.
- Error outputs and failure semantics.
- Emitted or expected realtime side effects.

Every consumed realtime event should define:
- Event name.
- Payload contract.
- Idempotent handling expectation on the client.