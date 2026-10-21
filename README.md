# Geopolitics

A single-player, Risk-style web game built for the UCT INF4001N 2027 Honours entrance exam.

**Live site:** https://geopolitics-ken5.onrender.com (health check: `/health`)
You pick a country, 9 others are drawn at random and played by NPCs, and you win by conquering all 10.

## Stack

| Concern | Choice |
|---|---|
| Web framework | ASP.NET Core (.NET 10) MVC with Razor views |
| Real-time | ASP.NET Core SignalR (one group per game) |
| Database | MongoDB Atlas (NoSQL) |
| AI | Server-side LLM call for battle commentary, with template fallback |
| Hosting | Docker container on Render or Azure App Service (WebSockets enabled) |

## Solution layout

```
src/
  Geopolitics.Domain/          Pure C#, no I/O: Game aggregate, Country, Player, BattleEngine, NPC strategy
  Geopolitics.Application/     Use cases (commands + queries), repository and service interfaces
  Geopolitics.Infrastructure/  MongoDB repositories, LLM client, population seed data
  Geopolitics.Web/             MVC controllers, views, SignalR hub, authentication
tests/
  Geopolitics.Tests/           Engine tests with a scripted dice roller
docs/
  adr/                         Architecture Decision Records
  rules.md                     How ambiguous rules in the brief are interpreted
  plan.md                      Build schedule to 6 November 2026
```

Dependencies point inward: `Web → Application → Domain`, and `Infrastructure` implements interfaces defined in `Application`.

## Running locally

Requirements: .NET SDK 10.0.x (pinned in `global.json`) and a MongoDB connection string.

```powershell
dotnet user-secrets --project src/Geopolitics.Web set "Mongo:ConnectionString" "<atlas-connection-string>"
dotnet user-secrets --project src/Geopolitics.Web set "Llm:ApiKey" "<api-key>"
dotnet run --project src/Geopolitics.Web
dotnet test
```

Secrets are never committed. In production they are supplied as environment variables
(`Mongo__ConnectionString`, `Llm__ApiKey`).

## Game engine and tests

All game rules live in `Geopolitics.Domain`, which has no dependencies on the database, web framework or AI.
The `Game` aggregate exposes one method per move (`PlaceReinforcements`, `Transfer`, `StartAttack`, `Roll`,
`StopAttack`, `MoveAfterConquest`, `EndTurn`). Each checks the turn, phase, ownership and soldier rules before
changing anything, and records a domain event for every change.

`dotnet test` runs:

- **Rule tests** for every worked example in the brief (dice counts, the `[1,4]` vs `[2,3]` roll, India and
  Vatican City conquest bonuses, reinforcement amounts) and every rejected move. Dice are scripted so outcomes are exact.
- **A 500-game simulation** in which every player makes random legal moves until the game is won or lost.
  After every move it checks that no owned country has fewer than 1 soldier, eliminated players own nothing,
  the game state is consistent, and replaying the event log rebuilds exactly the same board.

## Submission checklist

- [x] Deployed URL (Render, Frankfurt; move to a paid instance before submission so it doesn't sleep)
- [ ] Demo app username and password
- [ ] Examiners added as read-only Atlas project members: ammarcanani@gmail.com, mc.tanner@uct.ac.za, ridewaan.hanslo@uct.ac.za
- [ ] Source zip
- [ ] This README completed with run instructions, assumptions and architecture diagram
- [ ] Uploaded to the INF4001 Amathuba assignment folder
