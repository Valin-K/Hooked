using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public interface ICatchService
    {
        Task<Guid> AddCatchAsync(Guid userId, int speciesId, double? lengthMeters = null, double? weightKg = null, string? photoPath = null, string? locationJson = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<CatchRecord>> GetRecentCatchesAsync(int limit = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<CatchRecord>> GetUserCatchesAsync(Guid userId, int limit = 100, CancellationToken cancellationToken = default);
        Task<CatchDetailDto?> GetCatchDetailAsync(Guid catchId, Guid viewerUserId, CancellationToken cancellationToken = default);
        Task<bool> SetCatchFavoriteAsync(Guid catchId, Guid userId, bool isFavorite, CancellationToken cancellationToken = default);
    }

    public sealed record CatchDetailDto(
        Guid CatchId,
        Guid UserId,
        string Username,
        string? DisplayName,
        DateTime CaughtAt,
        int SpeciesId,
        string SpeciesCommonName,
        string? SpeciesScientificName,
        string? SpeciesIllustrationUrl,
        string? ConservationStatus,
        bool IsEndangered,
        bool IsInvasive,
        double? LengthMeters,
        double? WeightKg,
        string? PhotoPath,
        string? LocationJson,
        int ReactionCount,
        bool ViewerHasReacted,
        IReadOnlyList<SocialCommentDto> Comments);
}
