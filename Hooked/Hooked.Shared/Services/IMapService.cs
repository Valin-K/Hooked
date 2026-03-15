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

        /// <summary>Returns the 10 most recent catches (with location) from users the viewer follows.</summary>
        Task<IReadOnlyList<MapCatchPinDto>> GetFriendCatchPinsAsync(
            Guid viewerUserId,
            CancellationToken cancellationToken = default);

        /// <summary>Returns all catches (with location) for a specific species — friends and public.</summary>
        Task<IReadOnlyList<MapCatchPinDto>> GetCatchPinsBySpeciesAsync(
            string speciesName,
            CancellationToken cancellationToken = default);

        /// <summary>Returns distinct species names that have at least one catch with location data.</summary>
        Task<IReadOnlyList<string>> GetSpeciesWithCatchesAsync(
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
        DateTime CaughtAt,
        bool IsFriend = false);
}
