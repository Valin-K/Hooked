using System;

namespace Hooked.Shared.Data
{
    public sealed class CatchComment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CatchId { get; set; }
        public Guid UserId { get; set; }
        public string CommentText { get; set; } = null!;
        public DateTime CommentedAt { get; set; } = DateTime.UtcNow;

        public CatchRecord? Catch { get; set; }
        public User? User { get; set; }
    }
}
