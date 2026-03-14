using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public static class FishingQuestCatalog
    {
        public const string DailyCatchQuestKey = "daily-catch";
        public const string WeeklyCatchQuestKey = "weekly-catch";
        public const string MonthlyCatchQuestKey = "monthly-catch";

        public static IReadOnlyList<FishingQuest> CreateDefaults(DateTime nowUtc)
        {
            return new List<FishingQuest>
            {
                new()
                {
                    Key = DailyCatchQuestKey,
                    Name = "Daily Catch",
                    Description = "Log 1 catch today.",
                    Cadence = QuestCadence.Daily,
                    TargetCount = 1,
                    RewardXp = 30,
                    SkillId = ProgressionSkillCatalog.CatchMasterySkillId,
                    CreatedAt = nowUtc
                },
                new()
                {
                    Key = WeeklyCatchQuestKey,
                    Name = "Weekly Catch Streak",
                    Description = "Log 5 catches this week.",
                    Cadence = QuestCadence.Weekly,
                    TargetCount = 5,
                    RewardXp = 180,
                    SkillId = ProgressionSkillCatalog.CatchMasterySkillId,
                    CreatedAt = nowUtc
                },
                new()
                {
                    Key = MonthlyCatchQuestKey,
                    Name = "Monthly Trophy Hunt",
                    Description = "Log 20 catches this month.",
                    Cadence = QuestCadence.Monthly,
                    TargetCount = 20,
                    RewardXp = 800,
                    SkillId = ProgressionSkillCatalog.CatchMasterySkillId,
                    CreatedAt = nowUtc
                }
            };
        }
    }
}
