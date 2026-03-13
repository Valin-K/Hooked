using System;

namespace Hooked.Shared.Data
{
    public sealed class LeaderboardEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string Category { get; set; } = null!; // e.g. "most_caught", "biggest"
        public long Score { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}