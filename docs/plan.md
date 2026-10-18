# Build plan

Deadline: Friday 6 November 2026.

| Dates | Phase | Done when |
|---|---|---|
| Sun 18 – Mon 19 Oct | Walking skeleton | Repo, solution projects, Atlas cluster, first deployment, ADR 0001 |
| Tue 20 – Fri 23 Oct | Domain engine and tests | Setup, soldier allocation, battles, conquest, reinforcement, elimination, win; all brief examples pass |
| Sat 24 – Sun 25 Oct | Persistence | Repositories, `gameEvents` log, version check, country seed data |
| Mon 26 – Tue 27 Oct | Auth and game lifecycle | Sign-up/login, start game, "my games" list with search and filter, public game page |
| Wed 28 – Fri 30 Oct | Real-time board and battle view | SignalR groups, all countries and soldier counts visible, dice animation, battle log |
| Sat 31 Oct – Mon 2 Nov | NPC AI | Turn loop, fast-forward, win-probability targeting, visible justifications |
| Tue 3 Nov | LLM commentary | Server-side calls, timeout, template fallback |
| Wed 4 Nov | Bonus and security | Battle replay and game history (bonus #5); security pass |
| Thu 5 Nov | Release prep | Test on deployed site, README, examiner Atlas access, source zip |
| Fri 6 Nov | Buffer | Submit to Amathuba |

## MongoDB collections

- `users`: account data, hashed passwords
- `countries`: name, population, soldier value, source capture date
- `games`: status, owner, round, current player, phase, 10 countries with owner and soldiers, attack in progress, `version`
- `gameEvents`: `{ gameId, seq, type, payload, commentary, createdAt }`, unique index on `(gameId, seq)`

Event types: `GameStarted`, `TurnStarted`, `Reinforced`, `AttackStarted`, `DiceRolled`, `CountryConquered`,
`AttackStopped`, `SoldiersTransferred`, `NpcDecision`, `PlayerEliminated`, `GameWon`.

## Turn state machine

`Reinforce → Action → AttackInProgress → PostConquest → End`. Transfers are rejected during `AttackInProgress`.

## NPC strategy

Each pair of dice is fair (15/36 attacker, 15/36 defender, 6/36 tie), so the chance of conquest depends only on soldier
counts and can be precomputed. The NPC concentrates troops on one attack stack, targets the country with the best
win probability × conquest bonus (weighted toward eliminations and the leader), stops when win probability falls below
about 30%, and records a structured reason for every decision.
