using System;

namespace Hooked.Shared.Data
{
    public sealed class UserSkill
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public int SkillId { get; set; }
        public int CurrentLevel { get; set; } = 1;
        public int CurrentXp { get; set; }
        public int TotalXpEarned { get; set; }
        public DateTime FirstAchievedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public Skill? Skill { get; set; }
    }
}
