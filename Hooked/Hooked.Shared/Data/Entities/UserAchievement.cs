using System;

namespace Hooked.Shared.Data
{
    public sealed class UserAchievement
    {
        public Guid UserId { get; set; }
        public Guid AchievementId { get; set; }
        public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public Achievement? Achievement { get; set; }
    }
}
