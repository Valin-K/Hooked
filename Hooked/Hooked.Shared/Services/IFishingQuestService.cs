using Hooked.Shared.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public interface IFishingQuestService
    {
        Task<IReadOnlyList<FishingQuestProgressDto>> GetActiveQuestsAsync(Guid userId, DateTime? asOfUtc = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FishingQuestProgressDto>> RecordCatchProgressAsync(Guid userId, Guid catchRecordId, DateTime caughtAtUtc, CancellationToken cancellationToken = default);
        Task<FishingQuestClaimResultDto> ClaimRewardAsync(Guid userId, Guid userQuestProgressId, CancellationToken cancellationToken = default);
    }

    public sealed record FishingQuestProgressDto(
        Guid UserQuestProgressId,
        Guid QuestId,
        string QuestKey,
        string QuestName,
        string? QuestDescription,
        QuestCadence Cadence,
        int TargetCount,
        int ProgressCount,
        bool IsCompleted,
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        int RewardXp,
        int SkillId,
        DateTime? CompletedAtUtc,
        DateTime? RewardClaimedAtUtc,
        Guid? RewardXpEventId)
    {
        public bool CanClaimReward => IsCompleted && RewardClaimedAtUtc is null;
    }

    public sealed record FishingQuestClaimResultDto(
        Guid UserQuestProgressId,
        Guid QuestId,
        string QuestKey,
        int RewardXp,
        bool WasApplied,
        bool WasDuplicate,
        Guid XpEventId,
        DateTime? RewardClaimedAtUtc);
}
