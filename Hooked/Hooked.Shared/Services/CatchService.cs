using Hooked.Shared.Data;
using Hooked.Shared.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class CatchService : ICatchService
    {
        private const int CatchLogXpAmount = 50;
        private const int SpeciesDiscoveryXpAmount = 100;

        private readonly IDbContextFactory<HookedDbContext> _dbFactory;
        private readonly IElasticSearchService? _elastic;
        private readonly ILogger<CatchService> _logger;
        private readonly IProgressionService _progressionService;
        private readonly IFishingQuestService _fishingQuestService;

        public CatchService(
            IDbContextFactory<HookedDbContext> dbFactory,
            ILogger<CatchService> logger,
            IProgressionService progressionService,
            IFishingQuestService fishingQuestService,
            IElasticSearchService? elastic = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progressionService = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
            _fishingQuestService = fishingQuestService ?? throw new ArgumentNullException(nameof(fishingQuestService));
            _elastic = elastic;
        }

        public async Task<Guid> AddCatchAsync(
            Guid userId,
            int speciesId,
            double? lengthMeters = null,
            double? weightKg = null,
            string? photoPath = null,
            string? locationJson = null,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            if (speciesId <= 0)
            {
                throw new ArgumentException("Species ID must be greater than zero.", nameof(speciesId));
            }

            await using var db = _dbFactory.CreateDbContext();

            var userExists = await db.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken)
                .ConfigureAwait(false);
            if (!userExists)
            {
                throw new KeyNotFoundException($"User '{userId}' was not found.");
            }

            var speciesExists = await db.FishSpecies
                .AsNoTracking()
                .AnyAsync(species => species.Id == speciesId, cancellationToken)
                .ConfigureAwait(false);
            if (!speciesExists)
            {
                throw new KeyNotFoundException($"Species '{speciesId}' was not found.");
            }

            var hasPreviousCatchForSpecies = await db.CatchRecords
                .AsNoTracking()
                .AnyAsync(catchRecord => catchRecord.UserId == userId && catchRecord.SpeciesId == speciesId, cancellationToken)
                .ConfigureAwait(false);

            var activeSession = await db.FishingSessions
                .FirstOrDefaultAsync(session => session.UserId == userId && session.IsActive, cancellationToken)
                .ConfigureAwait(false);

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

            db.CatchRecords.Add(catchRec);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (_elastic is not null)
            {
                try
                {
                    var species = await db.FishSpecies.FindAsync([speciesId], cancellationToken).ConfigureAwait(false);
                    var user = await db.Users.FindAsync([userId], cancellationToken).ConfigureAwait(false);
                    if (species is not null && user is not null)
                    {
                        await _elastic.IndexCatchAsync(catchRec, species, user, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Elasticsearch indexing failed for catch {CatchId} — continuing without search index", catchRec.Id);
                }
            }

            var activeSkillIds = await db.Skills.AsNoTracking()
                .Where(skill =>
                    skill.IsActive &&
                    (skill.Key == ProgressionSkillCatalog.CatchMasteryKey
                     || skill.Key == ProgressionSkillCatalog.SpeciesMasteryKey))
                .Select(skill => new { skill.Key, skill.Id })
                .ToDictionaryAsync(skill => skill.Key, skill => skill.Id, cancellationToken)
                .ConfigureAwait(false);

            if (activeSkillIds.TryGetValue(ProgressionSkillCatalog.CatchMasteryKey, out var catchMasterySkillId))
            {
                await _progressionService.AwardXpAsync(
                    new ProgressionAwardRequest(
                        userId,
                        catchMasterySkillId,
                        CatchLogXpAmount,
                        $"catch:{catchRec.Id}",
                        "Recorded catch",
                        catchRec.Id),
                    cancellationToken).ConfigureAwait(false);
            }

            if (!hasPreviousCatchForSpecies
                && activeSkillIds.TryGetValue(ProgressionSkillCatalog.SpeciesMasteryKey, out var speciesMasterySkillId))
            {
                await _progressionService.AwardXpAsync(
                    new ProgressionAwardRequest(
                        userId,
                        speciesMasterySkillId,
                        SpeciesDiscoveryXpAmount,
                        $"species-discovery:{userId:N}:{speciesId}",
                        "Discovered species",
                        catchRec.Id,
                        $"{{\"speciesId\":{speciesId}}}"),
                    cancellationToken).ConfigureAwait(false);
            }

            await _fishingQuestService
                .RecordCatchProgressAsync(userId, catchRec.Id, catchRec.CaughtAt, cancellationToken)
                .ConfigureAwait(false);

            return catchRec.Id;
        }

        public async Task<IEnumerable<CatchRecord>> GetRecentCatchesAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            var normalizedLimit = Math.Clamp(limit, 1, 500);
            await using var db = _dbFactory.CreateDbContext();
            return await db.CatchRecords.AsNoTracking()
                .OrderByDescending(c => c.CaughtAt)
                .Take(normalizedLimit)
                .Include(c => c.Species)
                .Include(c => c.User)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<CatchRecord>> GetUserCatchesAsync(Guid userId, int limit = 100, CancellationToken cancellationToken = default)
        {
            var normalizedLimit = Math.Clamp(limit, 1, 500);
            await using var db = _dbFactory.CreateDbContext();
            return await db.CatchRecords.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CaughtAt)
                .Take(normalizedLimit)
                .Include(c => c.Species)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<CatchDetailDto?> GetCatchDetailAsync(Guid catchId, Guid viewerUserId, CancellationToken cancellationToken = default)
        {
            if (catchId == Guid.Empty)
            {
                return null;
            }

            await using var db = _dbFactory.CreateDbContext();
            var catchRecord = await db.CatchRecords.AsNoTracking()
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

            var comments = await db.CatchComments.AsNoTracking()
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

        public async Task<bool> SetCatchFavoriteAsync(Guid catchId, Guid userId, bool isFavorite, CancellationToken cancellationToken = default)
        {
            if (catchId == Guid.Empty)
            {
                throw new ArgumentException("Catch ID is required.", nameof(catchId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            await using var db = _dbFactory.CreateDbContext();
            var catchRecord = await db.CatchRecords
                .FirstOrDefaultAsync(c => c.Id == catchId, cancellationToken)
                .ConfigureAwait(false);
            if (catchRecord is null)
            {
                throw new KeyNotFoundException($"Catch '{catchId}' was not found.");
            }

            if (catchRecord.UserId != userId)
            {
                throw new UnauthorizedAccessException("Only the catch owner can manage favorites.");
            }

            if (catchRecord.IsFavorite == isFavorite)
            {
                return false;
            }

            catchRecord.IsFavorite = isFavorite;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
