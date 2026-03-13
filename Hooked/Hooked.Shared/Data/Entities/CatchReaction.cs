using System;

namespace Hooked.Shared.Data
{
    public sealed class CatchReaction
    {
        public Guid CatchId { get; set; }
        public Guid UserId { get; set; }
        public DateTime ReactedAt { get; set; } = DateTime.UtcNow;

        public CatchRecord? Catch { get; set; }
        public User? User { get; set; }
    }
}
