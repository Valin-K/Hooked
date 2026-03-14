using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public sealed class Achievement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Key { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
    }
}