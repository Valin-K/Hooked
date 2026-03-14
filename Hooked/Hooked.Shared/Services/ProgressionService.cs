using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class ProgressionService : IProgressionService
    {
        private const int MaxEventKeyLength = 200;
        private const int MaxReasonLength = 300;
        private const int MaxMetadataLength = 4000;
        private readonly HookedDbContext _db;
        private readonly ProgressionOptions _options;
        private readonly IXpNotificationService _xpNotificationService;

        public ProgressionService(
            HookedDbContext db,
            IOptions<ProgressionOptions> options,
            IXpNotificationService xpNotificationService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _xpNotificationService = xpNotificationService ?? throw new ArgumentNullException(nameof(xpNotificationService));

            ValidateOptions(_options);
        }

        public int GetXpRequiredForNextLevel(int currentLevel)
        {
            if (currentLevel < 1)
            {
                throw new ArgumentException("Current level must be at least 1.", nameof(currentLevel));
            }

            if (currentLevel >= _options.MaxLevel)
            {
                return int.MaxValue;
            }

            var required = _options.BaseXpPerLevel * Math.Pow(_options.LevelGrowthFactor, currentLevel - 1);
            if (!double.IsFinite(required) || required >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(1, (int)Math.Ceiling(required));
        }

        public async Task<UserSkill?> GetUserSkillAsync(Guid userId, int skillId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            if (skillId <= 0)
            {
                throw new ArgumentException("Skill ID must be greater than zero.", nameof(skillId));
            }

            return await _db.UserSkills
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    us => us.UserId == userId && us.SkillId == skillId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<ProgressionOverviewDto> GetProgressionOverviewAsync(
            Guid userId,
            int primarySkillId = ProgressionSkillCatalog.CatchMasterySkillId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            if (primarySkillId <= 0)
            {
                throw new ArgumentException("Skill ID must be greater than zero.", nameof(primarySkillId));
            }

            await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);

            var skill = await _db.Skills
                .AsNoTracking()
                .Where(s => s.Id == primarySkillId && s.IsActive)
                .Select(s => new { s.Id, s.Key, s.Name })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (skill is null)
            {
                throw new KeyNotFoundException($"Skill '{primarySkillId}' was not found.");
            }

            var userSkill = await _db.UserSkills
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    us => us.UserId == userId && us.SkillId == primarySkillId,
                    cancellationToken)
                .ConfigureAwait(false);

            var overallTotalXp = await _db.UserSkills
                .AsNoTracking()
                .Where(us => us.UserId == userId)
                .Select(us => (int?)us.TotalXpEarned)
                .SumAsync(cancellationToken)
                .ConfigureAwait(false) ?? 0;

            var level = userSkill?.CurrentLevel ?? 1;
            var isAtMaxLevel = level >= _options.MaxLevel;
            var currentXp = isAtMaxLevel ? 0 : userSkill?.CurrentXp ?? 0;
            var xpRequired = isAtMaxLevel ? 0 : GetXpRequiredForNextLevel(level);

            return new ProgressionOverviewDto(
                userId,
                skill.Id,
                skill.Key,
                skill.Name,
                level,
                currentXp,
                xpRequired,
                userSkill?.TotalXpEarned ?? 0,
                overallTotalXp,
                _options.MaxLevel);
        }

        public async Task<ProgressionAwardResult> AwardXpAsync(ProgressionAwardRequest request, CancellationToken cancellationToken = default)
        {
            ValidateRequest(request);

            var normalizedEventKey = request.EventKey.Trim();
            var existingEvent = await _db.XpEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventKey == normalizedEventKey, cancellationToken)
                .ConfigureAwait(false);

            if (existingEvent is not null)
            {
                if (existingEvent.UserId != request.UserId || existingEvent.SkillId != request.SkillId)
                {
                    throw new InvalidOperationException("This event key has already been used for a different user/skill.");
                }

                return await BuildDuplicateResultAsync(existingEvent, request.UserId, request.SkillId, cancellationToken).ConfigureAwait(false);
            }

            await EnsureUserAndSkillExistAsync(request.UserId, request.SkillId, cancellationToken).ConfigureAwait(false);

            var userSkill = await _db.UserSkills
                .FirstOrDefaultAsync(
                    us => us.UserId == request.UserId && us.SkillId == request.SkillId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (userSkill is null)
            {
                userSkill = new UserSkill
                {
                    UserId = request.UserId,
                    SkillId = request.SkillId,
                    CurrentLevel = 1,
                    CurrentXp = 0,
                    TotalXpEarned = 0,
                    FirstAchievedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                _db.UserSkills.Add(userSkill);
            }

            var previousLevel = userSkill.CurrentLevel;
            var previousXp = userSkill.CurrentXp;
            var levelsGained = ApplyXp(userSkill, request.XpDelta);

            var xpEvent = new XpEvent
            {
                EventKey = normalizedEventKey,
                UserId = request.UserId,
                SkillId = request.SkillId,
                XpDelta = request.XpDelta,
                PreviousLevel = previousLevel,
                NewLevel = userSkill.CurrentLevel,
                PreviousXp = previousXp,
                NewXp = userSkill.CurrentXp,
                LevelsGained = levelsGained,
                Reason = request.Reason,
                CatchRecordId = request.CatchRecordId,
                Metadata = request.Metadata,
                CreatedAt = DateTime.UtcNow
            };

            _db.XpEvents.Add(xpEvent);

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
                var duplicateEvent = await _db.XpEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EventKey == normalizedEventKey, cancellationToken)
                    .ConfigureAwait(false);

                if (duplicateEvent is null)
                {
                    throw;
                }

                return await BuildDuplicateResultAsync(duplicateEvent, request.UserId, request.SkillId, cancellationToken).ConfigureAwait(false);
            }

            if (request.XpDelta > 0)
            {
                _xpNotificationService.Publish(
                    new XpAwardNotification(
                        Guid.NewGuid(),
                        request.UserId,
                        request.SkillId,
                        request.XpDelta,
                        CleanOptionalText(request.Reason),
                        BuildContextText(request),
                        userSkill.CurrentLevel,
                        levelsGained,
                        DateTimeOffset.UtcNow));
            }

            return new ProgressionAwardResult(
                request.UserId,
                request.SkillId,
                normalizedEventKey,
                request.XpDelta,
                WasApplied: true,
                WasDuplicate: false,
                previousLevel,
                userSkill.CurrentLevel,
                previousXp,
                userSkill.CurrentXp,
                userSkill.TotalXpEarned,
                levelsGained,
                xpEvent.Id);
        }

        private async Task EnsureUserAndSkillExistAsync(Guid userId, int skillId, CancellationToken cancellationToken)
        {
            await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);

            var skillExists = await _db.Skills
                .AsNoTracking()
                .AnyAsync(s => s.Id == skillId && s.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!skillExists)
            {
                throw new KeyNotFoundException($"Skill '{skillId}' was not found.");
            }
        }

        private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var userExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId, cancellationToken)
                .ConfigureAwait(false);
            if (!userExists)
            {
                throw new KeyNotFoundException($"User '{userId}' was not found.");
            }
        }

        private int ApplyXp(UserSkill userSkill, int xpDelta)
        {
            var now = DateTime.UtcNow;
            userSkill.LastUpdatedAt = now;

            var totalXp = (long)userSkill.TotalXpEarned + xpDelta;
            userSkill.TotalXpEarned = totalXp > int.MaxValue ? int.MaxValue : (int)totalXp;

            if (xpDelta == 0)
            {
                return 0;
            }

            var currentXp = (long)userSkill.CurrentXp + xpDelta;
            userSkill.CurrentXp = currentXp > int.MaxValue ? int.MaxValue : (int)currentXp;

            var levelsGained = 0;
            while (userSkill.CurrentLevel < _options.MaxLevel)
            {
                var xpRequired = GetXpRequiredForNextLevel(userSkill.CurrentLevel);
                if (userSkill.CurrentXp < xpRequired)
                {
                    break;
                }

                userSkill.CurrentXp -= xpRequired;
                userSkill.CurrentLevel++;
                levelsGained++;
            }

            if (userSkill.CurrentLevel >= _options.MaxLevel)
            {
                userSkill.CurrentXp = 0;
            }

            return levelsGained;
        }

        private static void ValidateOptions(ProgressionOptions options)
        {
            if (options.BaseXpPerLevel <= 0)
            {
                throw new InvalidOperationException("Progression BaseXpPerLevel must be greater than zero.");
            }

            if (options.LevelGrowthFactor <= 0)
            {
                throw new InvalidOperationException("Progression LevelGrowthFactor must be greater than zero.");
            }

            if (options.MaxLevel <= 1)
            {
                throw new InvalidOperationException("Progression MaxLevel must be greater than 1.");
            }
        }

        private static void ValidateRequest(ProgressionAwardRequest request)
        {
            if (request.UserId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(request));
            }

            if (request.SkillId <= 0)
            {
                throw new ArgumentException("Skill ID must be greater than zero.", nameof(request));
            }

            if (request.XpDelta < 0)
            {
                throw new ArgumentException("XP delta cannot be negative.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.EventKey))
            {
                throw new ArgumentException("Event key is required.", nameof(request));
            }

            if (request.EventKey.Trim().Length > MaxEventKeyLength)
            {
                throw new ArgumentException($"Event key cannot exceed {MaxEventKeyLength} characters.", nameof(request));
            }

            if (!string.IsNullOrEmpty(request.Reason) && request.Reason.Length > MaxReasonLength)
            {
                throw new ArgumentException($"Reason cannot exceed {MaxReasonLength} characters.", nameof(request));
            }

            if (!string.IsNullOrEmpty(request.Metadata) && request.Metadata.Length > MaxMetadataLength)
            {
                throw new ArgumentException($"Metadata cannot exceed {MaxMetadataLength} characters.", nameof(request));
            }
        }

        private async Task<ProgressionAwardResult> BuildDuplicateResultAsync(
            XpEvent duplicateEvent,
            Guid requestUserId,
            int requestSkillId,
            CancellationToken cancellationToken)
        {
            if (duplicateEvent.UserId != requestUserId || duplicateEvent.SkillId != requestSkillId)
            {
                throw new InvalidOperationException("This event key has already been used for a different user/skill.");
            }

            var totalXp = await _db.UserSkills
                .AsNoTracking()
                .Where(us => us.UserId == duplicateEvent.UserId && us.SkillId == duplicateEvent.SkillId)
                .Select(us => us.TotalXpEarned)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return new ProgressionAwardResult(
                duplicateEvent.UserId,
                duplicateEvent.SkillId,
                duplicateEvent.EventKey,
                0,
                WasApplied: false,
                WasDuplicate: true,
                duplicateEvent.PreviousLevel,
                duplicateEvent.NewLevel,
                duplicateEvent.PreviousXp,
                duplicateEvent.NewXp,
                totalXp,
                duplicateEvent.LevelsGained,
                duplicateEvent.Id);
        }

        private static string? BuildContextText(ProgressionAwardRequest request)
        {
            if (request.CatchRecordId is Guid catchRecordId && catchRecordId != Guid.Empty)
            {
                return $"Catch reward ({catchRecordId.ToString("N")[..8]})";
            }

            return CleanOptionalText(request.Metadata, maxLength: 140);
        }

        private static string? CleanOptionalText(string? value, int maxLength = 100)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return $"{trimmed[..maxLength]}…";
        }
    }
}
