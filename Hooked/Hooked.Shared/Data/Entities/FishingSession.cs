using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public sealed class FishingSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; } = true;

        public User? User { get; set; }
        public ICollection<CatchRecord> Catches { get; set; } = new List<CatchRecord>();
    }
}
