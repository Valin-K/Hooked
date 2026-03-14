using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace Hooked.Shared.Services
{
    public sealed class NotificationService : INotificationService
    {
        private readonly HookedDbContext _db;

        public NotificationService(HookedDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            Guid userId,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return [];
            }

            return await _db.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .Select(n => new NotificationDto(
                    n.Id,
                    n.Type,
                    n.Title,
                    n.Body,
                    n.IsRead,
                    n.CreatedAt,
                    n.CatchId,
                    n.TriggeredByUserId,
                    n.TriggeredByUser != null ? n.TriggeredByUser.Username : null,
                    n.TriggeredByUser != null ? n.TriggeredByUser.DisplayName : null,
                    n.AchievementId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<int> GetUnreadCountAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return 0;
            }

            return await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task MarkAsReadAsync(
            Guid notificationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (notification is { IsRead: false })
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task MarkAllAsReadAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return;
            }

            var unread = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (unread.Count == 0)
            {
                return;
            }

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
