using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public enum LeaderboardMetric
    {
        MostCaught,
        LargestFish,
        TotalXp
    }

    public enum LeaderboardScope
    {
        AllUsers,
        Friends
    }

    public interface ILeaderboardService
    {
        Task<IReadOnlyList<LeaderboardRowDto>> GetLeaderboardAsync(
            LeaderboardMetric metric,
            LeaderboardScope scope,
            Guid currentUserId,
            int limit = 50,
            CancellationToken cancellationToken = default);

        Task<int> GetUserWeeklyXpAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }

    public sealed record LeaderboardRowDto(
        int Rank,
        Guid UserId,
        string Username,
        string? DisplayName,
        double Score,
        bool IsCurrentUser);
}
