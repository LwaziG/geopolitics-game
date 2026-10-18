# ADR 0001: Modular monolith over microservices

- Status: Accepted
- Date: 2026-10-18

## Context

The project is built by one developer in under three weeks and is deployed as a single web application.
The key architecture characteristics are correctness of the game rules, real-time visibility for spectators,
security of player actions, and maintainability (the marksheet rewards layering, MVC and OO design).
Scalability and independent deployability are not significant drivers.

## Decision

Build a modular monolith (Richards & Ford, *Fundamentals of Software Architecture*, 2nd ed., Ch. 11), split into
Domain, Application, Infrastructure and Web projects. Patterns from the microservices literature are applied inside
the monolith where they help:

- **Aggregate** (Williams, Ch. 2): `Game` owns all board state and enforces its invariants.
- **Light CQRS** (Williams, Ch. 6): commands change state; queries read board, list and replay views.
- **Event log** (Williams, Ch. 7; Garofolo, Ch. 2–5): every state change is appended to `gameEvents`, which drives the
  battle log, spectator updates, replay and analytics.
- **Optimistic concurrency** (Rocha, Ch. 6): a `version` field on the game document, plus a per-game in-memory lock.

## Consequences

- One deployable unit, one database, simple hosting.
- The server is authoritative: dice are rolled and persisted on the server, which gives access control and
  anti-cheat behaviour by design.
- Module boundaries are enforced by project references rather than network calls.
- Horizontal scale-out would need a SignalR backplane and a distributed lock; this is out of scope.
