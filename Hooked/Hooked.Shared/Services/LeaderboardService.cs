using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class LeaderboardService : ILeaderboardService
    {
        private const int WeeklyXpWindowDays = 7;
        private readonly IDbContextFactory<HookedDbContext> _dbFactory;

        public LeaderboardService(IDbContextFactory<HookedDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public async Task<IReadOnlyList<LeaderboardRowDto>> GetLeaderboardAsync(
            LeaderboardMetric metric,
            LeaderboardScope scope,
            Guid currentUserId,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            var normalizedLimit = Math.Clamp(limit, 1, 200);

            List<Guid>? scopedUserIds = null;
            if (scope == LeaderboardScope.Friends && currentUserId != Guid.Empty)
            {
                scopedUserIds = await db.FriendRelations.AsNoTracking()
                    .Where(f => f.UserId == currentUserId)
                    .Select(f => f.FriendId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                scopedUserIds.Add(currentUserId);
            }

            List<LeaderboardRowDto> rows;
            if (metric == LeaderboardMetric.MostCaught)
            {
                IQueryable<CatchRecord> query = db.CatchRecords.AsNoTracking();
                if (scopedUserIds is not null)
                {
                    query = query.Where(c => scopedUserIds.Contains(c.UserId));
                }

                var grouped = await query
                    .GroupBy(c => new { c.UserId, c.User!.Username, c.User.DisplayName })
                    .Select(g => new
                    {
                        g.Key.UserId,
                        g.Key.Username,
                        g.Key.DisplayName,
                        Score = (double)g.Count()
                    })
                    .OrderByDescending(x => x.Score)
                    .Take(normalizedLimit)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                rows = grouped
                    .Select((x, i) => new LeaderboardRowDto(
                        i + 1,
                        x.UserId,
                        x.Username,
                        x.DisplayName,
                        x.Score,
                        x.UserId == currentUserId))
                    .ToList();
            }
            else if (metric == LeaderboardMetric.LargestFish)
            {
                IQueryable<CatchRecord> query = db.CatchRecords.AsNoTracking();
                if (scopedUserIds is not null)
                {
                    query = query.Where(c => scopedUserIds.Contains(c.UserId));
                }

                var grouped = await query
                    .Where(c => c.LengthMeters.HasValue)
                    .GroupBy(c => new { c.UserId, c.User!.Username, c.User.DisplayName })
                    .Select(g => new
                    {
                        g.Key.UserId,
                        g.Key.Username,
                        g.Key.DisplayName,
                        Score = g.Max(c => c.LengthMeters!.Value)
                    })
                    .OrderByDescending(x => x.Score)
                    .Take(normalizedLimit)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                rows = grouped
                    .Select((x, i) => new LeaderboardRowDto(
                        i + 1,
                        x.UserId,
                        x.Username,
                        x.DisplayName,
                        x.Score,
                        x.UserId == currentUserId))
                    .ToList();
            }
            else
            {
                IQueryable<UserSkill> query = db.UserSkills.AsNoTracking();
                if (scopedUserIds is not null)
                {
                    query = query.Where(us => scopedUserIds.Contains(us.UserId));
                }

                var grouped = await query
                    .GroupBy(us => new { us.UserId, us.User!.Username, us.User.DisplayName })
                    .Select(g => new
                    {
                        g.Key.UserId,
                        g.Key.Username,
                        g.Key.DisplayName,
                        Score = (double)g.Sum(us => us.TotalXpEarned)
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Take(normalizedLimit)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                rows = grouped
                    .Select((x, i) => new LeaderboardRowDto(
                        i + 1,
                        x.UserId,
                        x.Username,
                        x.DisplayName,
                        x.Score,
                        x.UserId == currentUserId))
                    .ToList();
            }

            return rows;
        }

        public async Task<int> GetUserWeeklyXpAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            await using var db = _dbFactory.CreateDbContext();
            var windowStartUtc = DateTime.UtcNow.AddDays(-WeeklyXpWindowDays);
            return await db.XpEvents
                .AsNoTracking()
                .Where(xpEvent => xpEvent.UserId == userId && xpEvent.CreatedAt >= windowStartUtc)
                .Select(xpEvent => (int?)xpEvent.XpDelta)
                .SumAsync(cancellationToken)
                .ConfigureAwait(false) ?? 0;
        }
    }
}
