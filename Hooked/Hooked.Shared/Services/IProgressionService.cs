using Hooked.Shared.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public interface IProgressionService
    {
        Task<ProgressionAwardResult> AwardXpAsync(ProgressionAwardRequest request, CancellationToken cancellationToken = default);
        Task<ProgressionOverviewDto> GetProgressionOverviewAsync(Guid userId, int primarySkillId = ProgressionSkillCatalog.CatchMasterySkillId, CancellationToken cancellationToken = default);
        Task<UserSkill?> GetUserSkillAsync(Guid userId, int skillId, CancellationToken cancellationToken = default);
        int GetXpRequiredForNextLevel(int currentLevel);
    }

    public sealed record ProgressionAwardRequest(
        Guid UserId,
        int SkillId,
        int XpDelta,
        string EventKey,
        string? Reason = null,
        Guid? CatchRecordId = null,
        string? Metadata = null);

    public sealed record ProgressionAwardResult(
        Guid UserId,
        int SkillId,
        string EventKey,
        int AwardedXp,
        bool WasApplied,
        bool WasDuplicate,
        int PreviousLevel,
        int NewLevel,
        int PreviousXp,
        int NewXp,
        int TotalXpEarned,
        int LevelsGained,
        Guid XpEventId)
    {
        public bool LeveledUp => LevelsGained > 0;
    }

    public sealed record ProgressionOverviewDto(
        Guid UserId,
        int PrimarySkillId,
        string PrimarySkillKey,
        string PrimarySkillName,
        int CurrentLevel,
        int CurrentXp,
        int XpRequiredForNextLevel,
        int SkillTotalXpEarned,
        int OverallTotalXpEarned,
        int MaxLevel)
    {
        public bool IsAtMaxLevel => CurrentLevel >= MaxLevel || XpRequiredForNextLevel <= 0;
    }
}
