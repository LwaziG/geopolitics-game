# Rule interpretations

The brief leaves some rules open. These are the interpretations the engine implements. Each one is covered by unit tests.

| Topic | Interpretation | Basis in brief |
|---|---|---|
| Population bands | Upper limits are inclusive ("up to 5 million" means ≤ 5,000,000) | Table on p. 2 |
| Population data | Scraped once from Worldometers into a seed file with its capture date; never scraped at runtime | p. 1 link |
| Dice count | `min(attacker − 1, defender)`, no cap at 3 | 10 vs 2 → 2 dice; 3 vs 5 → 2 dice; 2 vs 5 → 1 die |
| Dice pairing | In roll order (die 1 vs die 1), not sorted | "Soldier 1 vs. Soldier 1" |
| Ties | Neither soldier dies | p. 3 |
| Conquest bonus | `max(2, floor(soldierValue / 2))`, where soldier value is the population-based starting value; placed on the conquered country | India 9 → 4; Vatican City 1 → 2 |
| Post-conquest move | Player may move extra soldiers from the attacking country, leaving at least 1 behind | p. 3 |
| Attacks per turn | One. Conquering ends the attack but not the turn; stopping an attack ends the turn | p. 2, p. 4 |
| Transfers | Allowed between owned countries when no attack is in progress; each country keeps at least 1 soldier | p. 2 |
| Reinforcements | From round 2: 2 soldiers per country owned, at the start of the turn, allocated freely | p. 4 |
| Round | Complete when every non-eliminated player has taken one turn; tracked explicitly | p. 4 |
| Elimination | Player with no countries is skipped for the rest of the game | p. 4 |
| Win | One player owns all 10 countries | p. 4 |

## Test cases taken from the brief

- Dice counts: (10 vs 2) → 2, (3 vs 5) → 2, (2 vs 5) → 1
- Roll `[1, 4]` vs `[2, 3]` → each side loses 1; attacker 10 → 9, defender 2 → 1
- Conquest bonus: India (value 9) → 4; Vatican City (value 1) → 2
- Reinforcements: 1 country → 2, 3 countries → 6, 5 countries → 10
