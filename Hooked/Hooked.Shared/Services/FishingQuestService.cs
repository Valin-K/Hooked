using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class FishingQuestService : IFishingQuestService
    {
        private readonly HookedDbContext _db;
        private readonly IProgressionService _progressionService;

        public FishingQuestService(HookedDbContext db, IProgressionService progressionService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _progressionService = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
        }

        public async Task<IReadOnlyList<FishingQuestProgressDto>> GetActiveQuestsAsync(Guid userId, DateTime? asOfUtc = null, CancellationToken cancellationToken = default)
        {
            ValidateUserId(userId);
            await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);

            var questProgress = await GetOrCreateProgressRowsAsync(
                userId,
                NormalizeToUtc(asOfUtc ?? DateTime.UtcNow),
                cancellationToken).ConfigureAwait(false);

            return questProgress
                .Select(state => MapToDto(state.Quest, state.Progress))
                .ToList();
        }

        public async Task<IReadOnlyList<FishingQuestProgressDto>> RecordCatchProgressAsync(Guid userId, Guid catchRecordId, DateTime caughtAtUtc, CancellationToken cancellationToken = default)
        {
            ValidateUserId(userId);
            if (catchRecordId == Guid.Empty)
            {
                throw new ArgumentException("Catch record ID is required.", nameof(catchRecordId));
            }

            var catchExists = await _db.CatchRecords
                .AsNoTracking()
                .AnyAsync(catchRecord => catchRecord.Id == catchRecordId && catchRecord.UserId == userId, cancellationToken)
                .ConfigureAwait(false);
            if (!catchExists)
            {
                throw new KeyNotFoundException($"Catch '{catchRecordId}' was not found for user '{userId}'.");
            }

            var questProgress = await GetOrCreateProgressRowsAsync(
                userId,
                NormalizeToUtc(caughtAtUtc),
                cancellationToken).ConfigureAwait(false);

            if (questProgress.Count == 0)
            {
                return Array.Empty<FishingQuestProgressDto>();
            }

            var now = DateTime.UtcNow;
            foreach (var state in questProgress)
            {
                var progress = state.Progress;
                if (progress.ProgressCount < state.Quest.TargetCount)
                {
                    progress.ProgressCount++;
                }

                if (!progress.IsCompleted && progress.ProgressCount >= state.Quest.TargetCount)
                {
                    progress.IsCompleted = true;
                    progress.CompletedAtUtc = now;
                }

                progress.LastUpdatedAtUtc = now;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return questProgress
                .Select(state => MapToDto(state.Quest, state.Progress))
                .ToList();
        }

        public async Task<FishingQuestClaimResultDto> ClaimRewardAsync(Guid userId, Guid userQuestProgressId, CancellationToken cancellationToken = default)
        {
            ValidateUserId(userId);
            if (userQuestProgressId == Guid.Empty)
            {
                throw new ArgumentException("Quest progress ID is required.", nameof(userQuestProgressId));
            }

            var progress = await _db.UserFishingQuestProgresses
                .Include(userProgress => userProgress.Quest)
                .FirstOrDefaultAsync(
                    userProgress => userProgress.Id == userQuestProgressId && userProgress.UserId == userId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (progress is null || progress.Quest is null)
            {
                throw new KeyNotFoundException($"Quest progress '{userQuestProgressId}' was not found for user '{userId}'.");
            }

            if (!progress.IsCompleted)
            {
                throw new InvalidOperationException("Quest must be completed before claiming its reward.");
            }

            if (progress.RewardClaimedAtUtc.HasValue && progress.RewardXpEventId.HasValue)
            {
                return new FishingQuestClaimResultDto(
                    progress.Id,
                    progress.QuestId,
                    progress.Quest.Key,
                    RewardXp: 0,
                    WasApplied: false,
                    WasDuplicate: true,
                    progress.RewardXpEventId.Value,
                    progress.RewardClaimedAtUtc);
            }

            if (progress.Quest.RewardXp < 0)
            {
                throw new InvalidOperationException("Quest reward XP cannot be negative.");
            }

            var eventKey = $"quest:{progress.Id}:reward";
            var metadata = $"{{\"questKey\":\"{progress.Quest.Key}\",\"periodStartUtc\":\"{progress.PeriodStartUtc:O}\"}}";

            var awardResult = await _progressionService.AwardXpAsync(
                new ProgressionAwardRequest(
                    userId,
                    progress.Quest.SkillId,
                    progress.Quest.RewardXp,
                    eventKey,
                    $"Quest reward: {progress.Quest.Name}",
                    Metadata: metadata),
                cancellationToken).ConfigureAwait(false);

            if (progress.RewardClaimedAtUtc is null)
            {
                progress.RewardClaimedAtUtc = DateTime.UtcNow;
            }

            if (!progress.RewardXpEventId.HasValue)
            {
                progress.RewardXpEventId = awardResult.XpEventId;
            }

            progress.LastUpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new FishingQuestClaimResultDto(
                progress.Id,
                progress.QuestId,
                progress.Quest.Key,
                awardResult.AwardedXp,
                awardResult.WasApplied,
                awardResult.WasDuplicate,
                awardResult.XpEventId,
                progress.RewardClaimedAtUtc);
        }

        private async Task<List<QuestProgressState>> GetOrCreateProgressRowsAsync(
            Guid userId,
            DateTime evaluationUtc,
            CancellationToken cancellationToken,
            bool allowRetry = true)
        {
            var quests = await _db.FishingQuests
                .Where(quest => quest.IsActive && quest.TargetCount > 0)
                .OrderBy(quest => quest.Cadence)
                .ThenBy(quest => quest.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (quests.Count == 0)
            {
                return new List<QuestProgressState>();
            }

            var windowsByQuestId = quests.ToDictionary(quest => quest.Id, quest => BuildWindow(quest.Cadence, evaluationUtc));
            var questIds = quests.Select(quest => quest.Id).ToList();
            var periodStarts = windowsByQuestId.Values.Select(window => window.PeriodStartUtc).Distinct().ToList();

            var progressRows = await _db.UserFishingQuestProgresses
                .Where(progress => progress.UserId == userId && questIds.Contains(progress.QuestId) && periodStarts.Contains(progress.PeriodStartUtc))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var progressByQuestId = progressRows.ToDictionary(progress => progress.QuestId);
            var now = DateTime.UtcNow;
            var hasAddedRows = false;

            foreach (var quest in quests)
            {
                if (progressByQuestId.ContainsKey(quest.Id))
                {
                    continue;
                }

                var window = windowsByQuestId[quest.Id];
                var newProgress = new UserFishingQuestProgress
                {
                    UserId = userId,
                    QuestId = quest.Id,
                    PeriodStartUtc = window.PeriodStartUtc,
                    PeriodEndUtc = window.PeriodEndUtc,
                    ProgressCount = 0,
                    IsCompleted = false,
                    LastUpdatedAtUtc = now
                };

                _db.UserFishingQuestProgresses.Add(newProgress);
                progressByQuestId[quest.Id] = newProgress;
                hasAddedRows = true;
            }

            if (hasAddedRows)
            {
                try
                {
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateException) when (allowRetry)
                {
                    _db.ChangeTracker.Clear();
                    return await GetOrCreateProgressRowsAsync(userId, evaluationUtc, cancellationToken, allowRetry: false).ConfigureAwait(false);
                }
            }

            return quests
                .Select(quest => new QuestProgressState(quest, progressByQuestId[quest.Id]))
                .ToList();
        }

        private static QuestWindow BuildWindow(QuestCadence cadence, DateTime asOfUtc)
        {
            var normalized = NormalizeToUtc(asOfUtc);
            var start = normalized.Date;

            switch (cadence)
            {
                case QuestCadence.Daily:
                    return new QuestWindow(start, start.AddDays(1));
                case QuestCadence.Weekly:
                    var diff = ((7 + (int)start.DayOfWeek - (int)DayOfWeek.Monday) % 7);
                    var weeklyStart = start.AddDays(-diff);
                    return new QuestWindow(weeklyStart, weeklyStart.AddDays(7));
                case QuestCadence.Monthly:
                    var monthlyStart = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    return new QuestWindow(monthlyStart, monthlyStart.AddMonths(1));
                default:
                    throw new ArgumentOutOfRangeException(nameof(cadence), cadence, "Unsupported quest cadence.");
            }
        }

        private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var userExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken)
                .ConfigureAwait(false);
            if (!userExists)
            {
                throw new KeyNotFoundException($"User '{userId}' was not found.");
            }
        }

        private static void ValidateUserId(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }
        }

        private static DateTime NormalizeToUtc(DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
        }

        private static FishingQuestProgressDto MapToDto(FishingQuest quest, UserFishingQuestProgress progress)
        {
            return new FishingQuestProgressDto(
                progress.Id,
                quest.Id,
                quest.Key,
                quest.Name,
                quest.Description,
                quest.Cadence,
                quest.TargetCount,
                progress.ProgressCount,
                progress.IsCompleted,
                progress.PeriodStartUtc,
                progress.PeriodEndUtc,
                quest.RewardXp,
                quest.SkillId,
                progress.CompletedAtUtc,
                progress.RewardClaimedAtUtc,
                progress.RewardXpEventId);
        }

        private sealed record QuestWindow(DateTime PeriodStartUtc, DateTime PeriodEndUtc);
        private sealed record QuestProgressState(FishingQuest Quest, UserFishingQuestProgress Progress);
    }
}
