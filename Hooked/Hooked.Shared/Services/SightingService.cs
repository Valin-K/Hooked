using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class SightingService : ISightingService
    {
        private readonly IDbContextFactory<HookedDbContext> _dbFactory;

        public SightingService(IDbContextFactory<HookedDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public async Task<SightingDto> ReportSightingAsync(Guid userId, ReportSightingRequest request, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));
            ArgumentNullException.ThrowIfNull(request);

            await using var db = _dbFactory.CreateDbContext();

            var userExists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
            if (!userExists) throw new KeyNotFoundException($"User '{userId}' was not found.");

            var speciesExists = await db.FishSpecies.AsNoTracking().AnyAsync(s => s.Id == request.SpeciesId, cancellationToken).ConfigureAwait(false);
            if (!speciesExists) throw new KeyNotFoundException($"Species '{request.SpeciesId}' was not found.");

            var sighting = new Sighting
            {
                UserId = userId,
                SpeciesId = request.SpeciesId,
                ReportedAt = DateTime.UtcNow,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                LocationJson = request.LocationJson
            };

            db.Sightings.Add(sighting);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var result = await db.Sightings.AsNoTracking()
                .Where(s => s.Id == sighting.Id)
                .Select(s => new SightingDto(
                    s.Id,
                    s.UserId,
                    s.User!.Username,
                    s.User.DisplayName,
                    s.SpeciesId,
                    s.Species!.CommonName,
                    s.ReportedAt,
                    s.Note,
                    s.LocationJson))
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);

            return result;
        }

        public async Task<IReadOnlyList<SightingDto>> GetRecentSightingsAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            return await db.Sightings.AsNoTracking()
                .OrderByDescending(s => s.ReportedAt)
                .Take(Math.Clamp(limit, 1, 200))
                .Select(s => new SightingDto(
                    s.Id,
                    s.UserId,
                    s.User!.Username,
                    s.User.DisplayName,
                    s.SpeciesId,
                    s.Species!.CommonName,
                    s.ReportedAt,
                    s.Note,
                    s.LocationJson))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SightingDto>> GetUserSightingsAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty) return [];

            await using var db = _dbFactory.CreateDbContext();
            return await db.Sightings.AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.ReportedAt)
                .Take(Math.Clamp(limit, 1, 200))
                .Select(s => new SightingDto(
                    s.Id,
                    s.UserId,
                    s.User!.Username,
                    s.User.DisplayName,
                    s.SpeciesId,
                    s.Species!.CommonName,
                    s.ReportedAt,
                    s.Note,
                    s.LocationJson))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
