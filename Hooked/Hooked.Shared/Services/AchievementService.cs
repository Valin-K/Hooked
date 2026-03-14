using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace Hooked.Shared.Services
{
    public sealed class AchievementService : IAchievementService
    {
        private readonly HookedDbContext _db;

        public AchievementService(HookedDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IReadOnlyList<AchievementUnlockDto>> CheckAndAwardAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return [];
            }

            var allAchievements = await _db.Achievements
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (allAchievements.Count == 0)
            {
                return [];
            }

            var alreadyEarned = await _db.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.AchievementId)
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);

            var unearnedAchievements = allAchievements
                .Where(a => !alreadyEarned.Contains(a.Id))
                .ToList();

            if (unearnedAchievements.Count == 0)
            {
                return [];
            }

            // Load the stats we need to evaluate rules
            var catchCount = await _db.CatchRecords
                .CountAsync(c => c.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var fishDexCount = await _db.FishDexEntries
                .CountAsync(fd => fd.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var completedSessionCount = await _db.FishingSessions
                .CountAsync(s => s.UserId == userId && !s.IsActive, cancellationToken)
                .ConfigureAwait(false);

            var followingCount = await _db.FriendRelations
                .CountAsync(f => f.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var hasGlobalDiscovery = await _db.FishSpecies
                .AnyAsync(s => s.DiscoveredByUserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var hasBigCatch = await _db.CatchRecords
                .AnyAsync(c => c.UserId == userId && c.LengthMeters >= 1.0, cancellationToken)
                .ConfigureAwait(false);

            var hasPersonalBest = await _db.FishDexEntries
                .AnyAsync(fd => fd.UserId == userId && fd.PersonalBestLengthMeters != null, cancellationToken)
                .ConfigureAwait(false);

            var totalSpeciesCount = await _db.FishSpecies.CountAsync(cancellationToken).ConfigureAwait(false);
            var fishdexComplete = totalSpeciesCount > 0 && fishDexCount >= totalSpeciesCount;

            var now = DateTime.UtcNow;
            var newlyUnlocked = new List<AchievementUnlockDto>();

            foreach (var achievement in unearnedAchievements)
            {
                var shouldAward = achievement.Key switch
                {
                    "first-catch"       => catchCount >= 1,
                    "catch-5"           => catchCount >= 5,
                    "catch-25"          => catchCount >= 25,
                    "catch-100"         => catchCount >= 100,
                    "species-3"         => fishDexCount >= 3,
                    "species-10"        => fishDexCount >= 10,
                    "fishdex-complete"  => fishdexComplete,
                    "big-catch"         => hasBigCatch,
                    "personal-best"     => hasPersonalBest,
                    "global-discovery"  => hasGlobalDiscovery,
                    "session-complete"  => completedSessionCount >= 1,
                    "session-3"         => completedSessionCount >= 3,
                    "social-butterfly"  => followingCount >= 3,
                    _                   => false
                };

                if (!shouldAward)
                {
                    continue;
                }

                _db.UserAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = achievement.Id,
                    EarnedAt = now
                });

                newlyUnlocked.Add(new AchievementUnlockDto(
                    achievement.Id,
                    achievement.Key,
                    achievement.Title,
                    achievement.Description));
            }

            if (newlyUnlocked.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return newlyUnlocked;
        }

        public async Task<IReadOnlyList<UserAchievementDto>> GetUserAchievementsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _db.UserAchievements
                .AsNoTracking()
                .Where(ua => ua.UserId == userId)
                .OrderBy(ua => ua.EarnedAt)
                .Select(ua => new UserAchievementDto(
                    ua.AchievementId,
                    ua.Achievement!.Key,
                    ua.Achievement.Title,
                    ua.Achievement.Description,
                    ua.EarnedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<AchievementStatusDto>> GetAllWithStatusAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var allAchievements = await _db.Achievements
                .AsNoTracking()
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var earned = await _db.UserAchievements
                .AsNoTracking()
                .Where(ua => ua.UserId == userId)
                .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.EarnedAt, cancellationToken)
                .ConfigureAwait(false);

            return allAchievements
                .Select(a =>
                {
                    var isEarned = earned.TryGetValue(a.Id, out var earnedAt);
                    return new AchievementStatusDto(
                        a.Id,
                        a.Key,
                        a.Title,
                        a.Description,
                        isEarned,
                        isEarned ? earnedAt : null);
                })
                .ToList();
        }
    }
}
