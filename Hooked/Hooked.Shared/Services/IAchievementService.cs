using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed record AchievementUnlockDto(
        Guid AchievementId,
        string Key,
        string Title,
        string? Description);

    public sealed record UserAchievementDto(
        Guid AchievementId,
        string Key,
        string Title,
        string? Description,
        DateTime EarnedAt);

    public interface IAchievementService
    {
        /// <summary>
        /// Evaluates all achievement rules for the user and awards any that are newly met.
        /// Returns the list of achievements unlocked in this call (empty if none).
        /// </summary>
        Task<IReadOnlyList<AchievementUnlockDto>> CheckAndAwardAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all achievements a user has earned, sorted by earned date.
        /// </summary>
        Task<IReadOnlyList<UserAchievementDto>> GetUserAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all defined achievements, with earned status for the given user.
        /// </summary>
        Task<IReadOnlyList<AchievementStatusDto>> GetAllWithStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    }

    public sealed record AchievementStatusDto(
        Guid AchievementId,
        string Key,
        string Title,
        string? Description,
        bool IsEarned,
        DateTime? EarnedAt);
}
