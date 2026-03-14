using System;

namespace Hooked.Shared.Data
{
    public sealed class UserFishingQuestProgress
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid QuestId { get; set; }
        public DateTime PeriodStartUtc { get; set; }
        public DateTime PeriodEndUtc { get; set; }
        public int ProgressCount { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? RewardClaimedAtUtc { get; set; }
        public Guid? RewardXpEventId { get; set; }
        public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public FishingQuest? Quest { get; set; }
        public XpEvent? RewardXpEvent { get; set; }
    }
}
