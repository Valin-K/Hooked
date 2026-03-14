using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public static class ProgressionSkillCatalog
    {
        public const int CatchMasterySkillId = 1;
        public const int SpeciesMasterySkillId = 2;
        public const int CommunitySkillId = 3;

        public const string CatchMasteryKey = "catch-mastery";
        public const string SpeciesMasteryKey = "species-mastery";
        public const string CommunityKey = "community";

        public static IReadOnlyList<Skill> CreateDefaults(DateTime nowUtc)
        {
            return new List<Skill>
            {
                new()
                {
                    Id = CatchMasterySkillId,
                    Key = CatchMasteryKey,
                    Name = "Catch Mastery",
                    Category = SkillCategory.FishingMethod,
                    Description = "Improve by logging catches and refining your angling practice.",
                    CreatedAt = nowUtc
                },
                new()
                {
                    Id = SpeciesMasterySkillId,
                    Key = SpeciesMasteryKey,
                    Name = "Species Mastery",
                    Category = SkillCategory.SpeciesKnowledge,
                    Description = "Grow your fish knowledge by identifying and catching different species.",
                    CreatedAt = nowUtc
                },
                new()
                {
                    Id = CommunitySkillId,
                    Key = CommunityKey,
                    Name = "Community",
                    Category = SkillCategory.Community,
                    Description = "Earn progression by participating in the social side of Hooked.",
                    CreatedAt = nowUtc
                }
            };
        }
    }
}
