using System;

namespace Hooked.Shared.Data
{
    public sealed class Notification
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>The user who receives this notification.</summary>
        public Guid UserId { get; set; }

        public NotificationType Type { get; set; }

        /// <summary>Short display text, e.g. "captainbrook liked your Snapper catch".</summary>
        public string Title { get; set; } = null!;

        /// <summary>Optional additional context (comment preview, achievement description, etc.).</summary>
        public string? Body { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ─── Contextual FKs ───────────────────────────────────────────────────
        public Guid? CatchId { get; set; }
        public Guid? TriggeredByUserId { get; set; }
        public Guid? AchievementId { get; set; }

        // ─── Navigation ───────────────────────────────────────────────────────
        public User? User { get; set; }
        public CatchRecord? Catch { get; set; }
        public User? TriggeredByUser { get; set; }
        public Achievement? Achievement { get; set; }
    }
}
