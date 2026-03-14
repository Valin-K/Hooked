# XP, Leveling, and Quests (Hooked)

Hooked now has an integrated progression system built around XP events, skill leveling, and recurring quests.

## XP Awards

- **Catch XP:** Logging a catch awards XP to **Catch Mastery**.
- **Species Discovery XP:** The first time a user catches a species, they receive bonus XP to **Species Mastery**.
- XP awards are recorded as idempotent `XpEvent` entries (event-key based), so duplicate processing doesn’t double-award.

## Leveling

- Each skill tracks:
  - `CurrentLevel`
  - `CurrentXp` (toward next level)
  - `TotalXpEarned`
- XP requirements scale by configurable progression options (`BaseXpPerLevel`, `LevelGrowthFactor`, `MaxLevel`).
- Profile now shows an **Overall XP** card with level progress.

## Quests

- Quests are cadence-based: **Daily**, **Weekly**, and **Monthly**.
- Catch activity updates quest progress.
- Completed quests can be claimed for reward XP.
- Profile now includes a quest board grouped by cadence, with progress bars and claim actions.

## UX Integrations Added

- **XP Toast Popups:** Global, auto-dismiss notifications appear whenever XP is awarded, including level-up indication.
- **XP Leaderboard:** Leaderboard page now supports sorting by total XP.
- **Home XP This Week:** Home page shows a 7-day XP total card (`XP this week`).

## Services and Data Touchpoints

- Progression service handles XP awarding, level math, and overview read models.
- Catch service applies catch XP and first-species bonus XP.
- Leaderboard service now supports XP ranking and weekly XP totals.
- Core entities: `UserSkill`, `XpEvent`, `FishingQuest`, `UserFishingQuestProgress`.
