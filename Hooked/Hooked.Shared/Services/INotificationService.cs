using Hooked.Shared.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public record NotificationDto(
        Guid Id,
        NotificationType Type,
        string Title,
        string? Body,
        bool IsRead,
        DateTime CreatedAt,
        Guid? CatchId,
        Guid? TriggeredByUserId,
        string? TriggeredByUsername,
        string? TriggeredByDisplayName,
        Guid? AchievementId);

    public interface INotificationService
    {
        Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            Guid userId,
            int limit = 50,
            CancellationToken cancellationToken = default);

        Task<int> GetUnreadCountAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task MarkAsReadAsync(
            Guid notificationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task MarkAllAsReadAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
