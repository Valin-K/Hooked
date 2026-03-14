using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;
using Hooked.Shared.Services.Search;
using Microsoft.Extensions.Logging;

namespace Hooked.Shared.Services
{
    public sealed class CatchService : ICatchService
    {
        private readonly HookedDbContext _db;
        private readonly IElasticSearchService? _elastic;
        private readonly ILogger<CatchService> _logger;

        public CatchService(HookedDbContext db, ILogger<CatchService> logger, IElasticSearchService? elastic = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _elastic = elastic;
        }

        public async Task<Guid> AddCatchAsync(Guid userId, int speciesId, double? lengthMeters = null, double? weightKg = null, string? photoPath = null, string? locationJson = null, CancellationToken cancellationToken = default)
        {
            var activeSession = await _db.FishingSessions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken);

            var catchRec = new CatchRecord
            {
                UserId = userId,
                SpeciesId = speciesId,
                LengthMeters = lengthMeters,
                WeightKg = weightKg,
                PhotoPath = photoPath,
                LocationJson = locationJson,
                CaughtAt = DateTime.UtcNow,
                FishingSessionId = activeSession?.Id
            };

            _db.CatchRecords.Add(catchRec);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (_elastic is not null)
            {
                try
                {
                    var species = await _db.FishSpecies.FindAsync([speciesId], cancellationToken).ConfigureAwait(false);
                    var user = await _db.Users.FindAsync([userId], cancellationToken).ConfigureAwait(false);
                    if (species is not null && user is not null)
                        await _elastic.IndexCatchAsync(catchRec, species, user, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Elasticsearch indexing failed for catch {CatchId} — continuing without search index", catchRec.Id);
                }
            }

            return catchRec.Id;
        }

        public async Task<IEnumerable<CatchRecord>> GetRecentCatchesAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            return await _db.CatchRecords.AsNoTracking()
                .OrderByDescending(c => c.CaughtAt)
                .Take(limit)
                .Include(c => c.Species)
                .Include(c => c.User)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<CatchRecord>> GetUserCatchesAsync(Guid userId, int limit = 100, CancellationToken cancellationToken = default)
        {
            return await _db.CatchRecords.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CaughtAt)
                .Take(limit)
                .Include(c => c.Species)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
