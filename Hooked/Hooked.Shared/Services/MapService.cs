using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class MapService : IMapService
    {
        private readonly IDbContextFactory<HookedDbContext> _dbFactory;

        public string MapboxAccessToken { get; }

        public MapService(IDbContextFactory<HookedDbContext> dbFactory, IConfiguration configuration)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            MapboxAccessToken = configuration["Mapbox:AccessToken"] ?? string.Empty;
        }

        public async Task<IReadOnlyList<MapCatchPinDto>> GetCatchPinsAsync(
            CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            var catches = await db.CatchRecords.AsNoTracking()
                .Where(c => c.LocationJson != null)
                .OrderByDescending(c => c.CaughtAt)
                .Take(200)
                .Select(c => new
                {
                    c.Id,
                    c.LocationJson,
                    c.LengthMeters,
                    c.WeightKg,
                    c.PhotoPath,
                    c.CaughtAt,
                    SpeciesName = c.Species != null ? c.Species.CommonName : string.Empty,
                    Username = c.User != null ? c.User.Username : string.Empty,
                    DisplayName = c.User != null ? c.User.DisplayName : null
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var pins = new List<MapCatchPinDto>(catches.Count);

            foreach (var c in catches)
            {
                if (!TryParseLocation(c.LocationJson, out var lat, out var lng))
                    continue;

                pins.Add(new MapCatchPinDto(
                    c.Id,
                    lat,
                    lng,
                    c.SpeciesName,
                    c.Username,
                    c.DisplayName,
                    c.LengthMeters,
                    c.WeightKg,
                    !string.IsNullOrWhiteSpace(c.PhotoPath),
                    c.CaughtAt));
            }

            return pins;
        }

        private static bool TryParseLocation(string? json, out double lat, out double lng)
        {
            lat = 0;
            lng = 0;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("lat", out var latEl) &&
                    root.TryGetProperty("lng", out var lngEl) &&
                    latEl.TryGetDouble(out lat) &&
                    lngEl.TryGetDouble(out lng))
                {
                    return true;
                }

                // Also handle GeoJSON Point: { "type":"Point", "coordinates":[lng,lat] }
                if (root.TryGetProperty("coordinates", out var coords) &&
                    coords.ValueKind == JsonValueKind.Array &&
                    coords.GetArrayLength() >= 2)
                {
                    lng = coords[0].GetDouble();
                    lat = coords[1].GetDouble();
                    return true;
                }
            }
            catch
            {
                // unparseable location � skip
            }

            return false;
        }
    }
}
