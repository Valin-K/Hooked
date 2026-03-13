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
        private readonly HookedDbContext _db;

        public LeaderboardService(HookedDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IReadOnlyList<LeaderboardRowDto>> GetLeaderboardAsync(
            LeaderboardMetric metric,
            LeaderboardScope scope,
            Guid currentUserId,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            var normalizedLimit = Math.Clamp(limit, 1, 200);

            IQueryable<CatchRecord> query = _db.CatchRecords.AsNoTracking();

            if (scope == LeaderboardScope.Friends && currentUserId != Guid.Empty)
            {
                var friendIds = await _db.FriendRelations.AsNoTracking()
                    .Where(f => f.UserId == currentUserId)
                    .Select(f => f.FriendId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                friendIds.Add(currentUserId);
                query = query.Where(c => friendIds.Contains(c.UserId));
            }

            List<LeaderboardRowDto> rows;

            if (metric == LeaderboardMetric.MostCaught)
            {
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
            else
            {
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

            return rows;
        }
    }
}
