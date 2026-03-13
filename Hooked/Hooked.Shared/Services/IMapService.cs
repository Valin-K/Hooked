using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public interface IMapService
    {
        string MapboxAccessToken { get; }

        Task<IReadOnlyList<MapCatchPinDto>> GetCatchPinsAsync(
            CancellationToken cancellationToken = default);
    }

    public sealed record MapCatchPinDto(
        Guid CatchId,
        double Lat,
        double Lng,
        string SpeciesName,
        string Username,
        string? DisplayName,
        double? LengthMeters,
        double? WeightKg,
        bool HasPhoto,
        DateTime CaughtAt);
}
