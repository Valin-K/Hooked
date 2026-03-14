using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Services;

namespace Hooked.Shared.Services
{
    public interface IFishDexService
    {
        /// <summary>
        /// Scans a fish photo, ensures the species exists, logs the catch, and updates user FishDex progress.
        /// </summary>
        Task<FishScanLogResultDto> ScanAndLogCatchAsync(Guid userId, FishScanLogRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all globally discovered species and the requesting user's FishDex unlock state.
        /// </summary>
        Task<FishDexOverviewDto> GetFishDexOverviewAsync(Guid userId, CancellationToken cancellationToken = default);
    }

    public sealed record FishScanLogRequestDto(
        byte[] PhotoBytes,
        string MimeType,
        double? LengthMeters,
        double? WeightKg,
        string? PhotoPath,
        string? LocationJson);

    public sealed record FishScanLogResultDto(
        Guid CatchId,
        int SpeciesId,
        string SpeciesName,
        string? SpeciesImageUrl,
        bool IsNewGlobalSpecies,
        bool IsFirstCatchForUser,
        bool IsNewPersonalBest,
        bool WasImageGenerated,
        bool IsInvasive,
        IReadOnlyList<AchievementUnlockDto> NewlyUnlockedAchievements);

    public sealed record FishDexOverviewDto(
        Guid UserId,
        IReadOnlyList<FishDexSpeciesCardDto> Species);

    public sealed record FishDexSpeciesCardDto(
        int SpeciesId,
        string CommonName,
        string? ScientificName,
        string? IllustrationImageUrl,
        bool IsUnlocked,
        DateTime? UnlockedAt,
        int CatchCount,
        double? PersonalBestLengthMeters,
        bool IsInvasive,
        IReadOnlyList<FishDexCatchDto> Catches);

    public sealed record FishDexCatchDto(
        Guid CatchId,
        DateTime CaughtAt,
        double? LengthMeters,
        double? WeightKg,
        string? PhotoPath);
}
