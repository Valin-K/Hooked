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

        public async Task<CatchDetailDto?> GetCatchDetailAsync(Guid catchId, Guid viewerUserId, CancellationToken cancellationToken = default)
        {
            if (catchId == Guid.Empty)
            {
                return null;
            }

            var catchRecord = await _db.CatchRecords.AsNoTracking()
                .Where(c => c.Id == catchId)
                .Select(c => new
                {
                    c.Id,
                    c.UserId,
                    Username = c.User != null ? c.User.Username : string.Empty,
                    DisplayName = c.User != null ? c.User.DisplayName : null,
                    c.CaughtAt,
                    c.SpeciesId,
                    SpeciesCommonName = c.Species != null ? c.Species.CommonName : string.Empty,
                    SpeciesScientificName = c.Species != null ? c.Species.ScientificName : null,
                    SpeciesIllustrationUrl = c.Species != null ? c.Species.IllustrationImageUrl : null,
                    ConservationStatus = c.Species != null ? c.Species.ConservationStatus : null,
                    IsEndangered = c.Species != null && c.Species.IsEndangered,
                    IsInvasive = c.Species != null && c.Species.IsInvasive,
                    c.LengthMeters,
                    c.WeightKg,
                    c.PhotoPath,
                    c.LocationJson,
                    ReactionCount = c.Reactions.Count(),
                    ViewerHasReacted = c.Reactions.Any(r => r.UserId == viewerUserId)
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (catchRecord is null)
            {
                return null;
            }

            var comments = await _db.CatchComments.AsNoTracking()
                .Where(c => c.CatchId == catchId)
                .OrderBy(c => c.CommentedAt)
                .Take(100)
                .Select(c => new SocialCommentDto(
                    c.Id,
                    c.CatchId,
                    c.UserId,
                    c.User != null ? c.User.Username : string.Empty,
                    c.User != null ? c.User.DisplayName : null,
                    c.CommentText,
                    c.CommentedAt,
                    c.EditedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new CatchDetailDto(
                catchRecord.Id,
                catchRecord.UserId,
                catchRecord.Username,
                catchRecord.DisplayName,
                catchRecord.CaughtAt,
                catchRecord.SpeciesId,
                catchRecord.SpeciesCommonName,
                catchRecord.SpeciesScientificName,
                catchRecord.SpeciesIllustrationUrl,
                catchRecord.ConservationStatus,
                catchRecord.IsEndangered,
                catchRecord.IsInvasive,
                catchRecord.LengthMeters,
                catchRecord.WeightKg,
                catchRecord.PhotoPath,
                catchRecord.LocationJson,
                catchRecord.ReactionCount,
                catchRecord.ViewerHasReacted,
                comments);
        }
    }
}
