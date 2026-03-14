using System;

namespace Hooked.Shared.Services
{
    public interface IXpNotificationService
    {
        event EventHandler<XpAwardNotification>? NotificationPublished;

        void Publish(XpAwardNotification notification);
    }

    public sealed class XpNotificationService : IXpNotificationService
    {
        public event EventHandler<XpAwardNotification>? NotificationPublished;

        public void Publish(XpAwardNotification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);
            NotificationPublished?.Invoke(this, notification);
        }
    }

    public sealed record XpAwardNotification(
        Guid Id,
        Guid UserId,
        int SkillId,
        int AwardedXp,
        string? Reason,
        string? Context,
        int NewLevel,
        int LevelsGained,
        DateTimeOffset AwardedAt)
    {
        public bool LeveledUp => LevelsGained > 0;
    }
}
