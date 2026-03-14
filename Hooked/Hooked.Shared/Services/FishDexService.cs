using Hooked.Shared.Data;
using Hooked.Shared.Services.AI;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class FishDexService : IFishDexService
    {
        private readonly HookedDbContext _db;
        private readonly IGeminiFishSpeciesService _geminiFishSpeciesService;
        private readonly ILeonardoFishImageService _leonardoFishImageService;

        public FishDexService(
            HookedDbContext db,
            IGeminiFishSpeciesService geminiFishSpeciesService,
            ILeonardoFishImageService leonardoFishImageService)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(geminiFishSpeciesService);
            ArgumentNullException.ThrowIfNull(leonardoFishImageService);

            _db = db;
            _geminiFishSpeciesService = geminiFishSpeciesService;
            _leonardoFishImageService = leonardoFishImageService;
        }

        public async Task<FishScanLogResultDto> ScanAndLogCatchAsync(Guid userId, FishScanLogRequestDto request, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            ArgumentNullException.ThrowIfNull(request);

            if (request.PhotoBytes.Length == 0)
            {
                throw new ArgumentException("Photo bytes are required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.MimeType))
            {
                throw new ArgumentException("MIME type is required.", nameof(request));
            }

            await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);

            var speciesName = await _geminiFishSpeciesService
                .IdentifyFishSpeciesAsync(request.PhotoBytes, request.MimeType, cancellationToken)
                .ConfigureAwait(false);
            var normalizedSpeciesName = NormalizeSpeciesName(speciesName);

            if (normalizedSpeciesName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Scanner could not identify a species from this photo.");
            }

            var fishSpecies = await ResolveSpeciesAsync(normalizedSpeciesName, cancellationToken).ConfigureAwait(false);
            var isNewGlobalSpecies = fishSpecies is null;

            if (fishSpecies is null)
            {
                fishSpecies = new FishSpecies
                {
                    CommonName = normalizedSpeciesName,
                    DiscoveredAt = DateTime.UtcNow,
                    DiscoveredByUserId = userId
                };

                _db.FishSpecies.Add(fishSpecies);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            var wasImageGenerated = false;
            if (string.IsNullOrWhiteSpace(fishSpecies.IllustrationImageUrl))
            {
                fishSpecies.IllustrationImageUrl = await _leonardoFishImageService
                    .GenerateFishImageUrlAsync(fishSpecies.CommonName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                fishSpecies.IllustrationGeneratedAt = DateTime.UtcNow;
                wasImageGenerated = true;
            }
            else
            {
                var transparentImageDataUrl = await _leonardoFishImageService
                    .ConvertToTransparentPngDataUrlAsync(fishSpecies.IllustrationImageUrl, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(transparentImageDataUrl))
                {
                    fishSpecies.IllustrationImageUrl = transparentImageDataUrl;
                    fishSpecies.IllustrationGeneratedAt = DateTime.UtcNow;
                }
            }

            var activeSession = await _db.FishingSessions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var catchRecord = new CatchRecord
            {
                UserId = userId,
                SpeciesId = fishSpecies.Id,
                CaughtAt = now,
                LengthMeters = request.LengthMeters,
                WeightKg = request.WeightKg,
                PhotoPath = request.PhotoPath,
                LocationJson = request.LocationJson,
                FishingSessionId = activeSession?.Id
            };

            _db.CatchRecords.Add(catchRecord);

            var fishDexEntry = await _db.FishDexEntries
                .FirstOrDefaultAsync(fd => fd.UserId == userId && fd.SpeciesId == fishSpecies.Id, cancellationToken)
                .ConfigureAwait(false);

            var isFirstCatchForUser = fishDexEntry is null;
            var isNewPersonalBest = false;

            if (fishDexEntry is null)
            {
                fishDexEntry = new FishDexEntry
                {
                    UserId = userId,
                    SpeciesId = fishSpecies.Id,
                    UnlockedAt = now,
                    CatchCount = 1,
                    PersonalBestLengthMeters = request.LengthMeters,
                    PersonalBestCatch = catchRecord,
                    IsRare = fishSpecies.IsEndangered
                };

                _db.FishDexEntries.Add(fishDexEntry);
                isNewPersonalBest = request.LengthMeters.HasValue;
            }
            else
            {
                fishDexEntry.CatchCount += 1;
                if (request.LengthMeters.HasValue)
                {
                    if (!fishDexEntry.PersonalBestLengthMeters.HasValue ||
                        request.LengthMeters.Value > fishDexEntry.PersonalBestLengthMeters.Value)
                    {
                        fishDexEntry.PersonalBestLengthMeters = request.LengthMeters;
                        fishDexEntry.PersonalBestCatch = catchRecord;
                        isNewPersonalBest = true;
                    }
                }
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new FishScanLogResultDto(
                catchRecord.Id,
                fishSpecies.Id,
                fishSpecies.CommonName,
                fishSpecies.IllustrationImageUrl,
                isNewGlobalSpecies,
                isFirstCatchForUser,
                isNewPersonalBest,
                wasImageGenerated);
        }

        public async Task<FishDexOverviewDto> GetFishDexOverviewAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);
            await NormalizeExistingIllustrationsAsync(cancellationToken).ConfigureAwait(false);

            var discoveredSpecies = await _db.FishSpecies.AsNoTracking()
                .Where(species => species.Catches.Any())
                .OrderBy(species => species.CommonName)
                .Select(species => new
                {
                    species.Id,
                    species.CommonName,
                    species.ScientificName,
                    species.IllustrationImageUrl
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var userFishDexEntries = await _db.FishDexEntries.AsNoTracking()
                .Where(entry => entry.UserId == userId)
                .Select(entry => new
                {
                    entry.SpeciesId,
                    entry.UnlockedAt,
                    entry.CatchCount,
                    entry.PersonalBestLengthMeters
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var speciesIds = discoveredSpecies.Select(species => species.Id).ToArray();
            var userCatches = await _db.CatchRecords.AsNoTracking()
                .Where(catchRecord => catchRecord.UserId == userId && speciesIds.Contains(catchRecord.SpeciesId))
                .OrderByDescending(catchRecord => catchRecord.CaughtAt)
                .Select(catchRecord => new
                {
                    catchRecord.Id,
                    catchRecord.SpeciesId,
                    catchRecord.CaughtAt,
                    catchRecord.LengthMeters,
                    catchRecord.WeightKg,
                    catchRecord.PhotoPath
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var fishDexEntryBySpeciesId = userFishDexEntries.ToDictionary(entry => entry.SpeciesId);
            var catchesBySpeciesId = userCatches
                .GroupBy(catchRecord => catchRecord.SpeciesId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<FishDexCatchDto>)group
                        .Select(catchRecord => new FishDexCatchDto(
                            catchRecord.Id,
                            catchRecord.CaughtAt,
                            catchRecord.LengthMeters,
                            catchRecord.WeightKg,
                            catchRecord.PhotoPath))
                        .ToList());

            var fishDexSpecies = discoveredSpecies
                .Select(species =>
                {
                    var hasEntry = fishDexEntryBySpeciesId.TryGetValue(species.Id, out var entry);

                    return new FishDexSpeciesCardDto(
                        species.Id,
                        species.CommonName,
                        species.ScientificName,
                        species.IllustrationImageUrl,
                        hasEntry,
                        hasEntry ? entry!.UnlockedAt : null,
                        hasEntry ? entry!.CatchCount : 0,
                        hasEntry ? entry!.PersonalBestLengthMeters : null,
                        catchesBySpeciesId.TryGetValue(species.Id, out var catches)
                            ? catches
                            : Array.Empty<FishDexCatchDto>());
                })
                .ToList();

            return new FishDexOverviewDto(userId, fishDexSpecies);
        }

        private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var userExists = await _db.Users.AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken)
                .ConfigureAwait(false);
            if (!userExists)
            {
                throw new KeyNotFoundException($"User '{userId}' was not found.");
            }
        }

        private async Task<FishSpecies?> ResolveSpeciesAsync(string speciesName, CancellationToken cancellationToken)
        {
            var exactMatch = await _db.FishSpecies
                .FirstOrDefaultAsync(species => species.CommonName == speciesName, cancellationToken)
                .ConfigureAwait(false);
            if (exactMatch is not null)
            {
                return exactMatch;
            }

            return await _db.FishSpecies
                .FirstOrDefaultAsync(species => species.CommonName.ToLower() == speciesName.ToLower(), cancellationToken)
                .ConfigureAwait(false);
        }

        private static string NormalizeSpeciesName(string speciesName)
        {
            if (string.IsNullOrWhiteSpace(speciesName))
            {
                throw new InvalidOperationException("Scanner could not identify a species from this photo.");
            }

            var normalized = speciesName.Trim();
            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            return textInfo.ToTitleCase(normalized.ToLowerInvariant());
        }

        private async Task NormalizeExistingIllustrationsAsync(CancellationToken cancellationToken)
        {
            var speciesWithIllustrations = await _db.FishSpecies
                .Where(species => species.Catches.Any()
                    && !string.IsNullOrWhiteSpace(species.IllustrationImageUrl))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (speciesWithIllustrations.Count == 0)
            {
                return;
            }

            var hasChanges = false;
            foreach (var species in speciesWithIllustrations)
            {
                var transparentImageDataUrl = await _leonardoFishImageService
                    .ConvertToTransparentPngDataUrlAsync(species.IllustrationImageUrl!, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(transparentImageDataUrl))
                {
                    continue;
                }

                species.IllustrationImageUrl = transparentImageDataUrl;
                species.IllustrationGeneratedAt = DateTime.UtcNow;
                hasChanges = true;
            }

            if (hasChanges)
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

    }
}
