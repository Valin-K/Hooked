using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed record SightingDto(
        Guid Id,
        Guid UserId,
        string Username,
        string? DisplayName,
        int SpeciesId,
        string SpeciesCommonName,
        DateTime ReportedAt,
        string? Note,
        string? LocationJson);

    public sealed record ReportSightingRequest(
        int SpeciesId,
        string? Note,
        string? LocationJson);

    public interface ISightingService
    {
        Task<SightingDto> ReportSightingAsync(Guid userId, ReportSightingRequest request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SightingDto>> GetRecentSightingsAsync(int limit = 50, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SightingDto>> GetUserSightingsAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    }
}
