using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public enum QuestCadence
    {
        Daily = 0,
        Weekly = 1,
        Monthly = 2
    }

    public sealed class FishingQuest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public QuestCadence Cadence { get; set; }
        public int TargetCount { get; set; }
        public int RewardXp { get; set; }
        public int SkillId { get; set; } = ProgressionSkillCatalog.CatchMasterySkillId;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Skill? Skill { get; set; }
        public ICollection<UserFishingQuestProgress> UserProgress { get; set; } = new List<UserFishingQuestProgress>();
    }
}
