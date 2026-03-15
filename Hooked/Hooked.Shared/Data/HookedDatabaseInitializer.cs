using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hooked.Shared.Services.Search;
using Npgsql;
using System.Net.Sockets;

namespace Hooked.Shared.Data
{
    internal sealed class HookedDatabaseInitializer : IHookedDatabaseInitializer
    {
        private readonly HookedDbContext _dbContext;
        private readonly IElasticSearchService? _elastic;
        private readonly ILogger<HookedDatabaseInitializer> _logger;

        public HookedDatabaseInitializer(
            HookedDbContext dbContext,
            ILogger<HookedDatabaseInitializer> logger,
            IElasticSearchService? elastic = null)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(logger);
            _dbContext = dbContext;
            _logger = logger;
            _elastic = elastic;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // MigrateAsync is idempotent: creates the DB if missing, applies any pending
            // migrations, and is a no-op when already up-to-date.
            // For SQLite (local dev) we use EnsureCreated — the EF migrations are Postgres-
            // specific (uuid, timestamp with time zone) so MigrateAsync would fail on SQLite.
            var providerName = _dbContext.Database.ProviderName ?? string.Empty;
            if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Applying EF migrations to PostgreSQL (Supabase)…");
                try
                {
                    var applied = await ApplyPostgresMigrationsAsync(cancellationToken).ConfigureAwait(false);
                    if (applied)
                    {
                        _logger.LogInformation("Database migrations applied successfully.");
                    }
                    else
                    {
                        _logger.LogWarning("Database migrations skipped for this run because only Supabase pooler connectivity is available.");
                    }
                }
                catch (NpgsqlException ex)
                {
                    _logger.LogCritical(ex, "Failed to apply database migrations. " +
                        "Ensure the connection string points to a direct (non-pooler) Supabase connection. " +
                        "You can also run: dotnet ef database update --project Hooked/Hooked.Shared --startup-project Hooked/Hooked.Web");
                    throw;
                }
                catch (TimeoutException ex)
                {
                    _logger.LogCritical(ex, "Timed out while applying database migrations. " +
                        "Verify Supabase connectivity and run migrations manually if needed.");
                    throw;
                }
            }
            else
            {
                await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            var seeded = await SeedDemoDataAsync(cancellationToken);

            // Bulk reindex into Elasticsearch after a fresh seed so all demo catches are searchable
            if (seeded && _elastic is not null)
            {
                try
                {
                    var catches = await _dbContext.CatchRecords.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
                    await _elastic.BulkReindexAsync(catches, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Non-fatal — the app still works without Elasticsearch
                    _logger.LogWarning(ex, "[Elasticsearch] Bulk reindex failed.");
                }
            }
        }

        /// <returns>true if data was seeded, false if already seeded.</returns>
        private async Task<bool> SeedDemoDataAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            if (!await _dbContext.Skills.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                _dbContext.Skills.AddRange(ProgressionSkillCatalog.CreateDefaults(now));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!await _dbContext.FishingQuests.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                _dbContext.FishingQuests.AddRange(FishingQuestCatalog.CreateDefaults(now));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (await _dbContext.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            await SeedAchievementsAsync(cancellationToken).ConfigureAwait(false);
            var users = new List<User>
            {
                new() { Username = "captainbrook", DisplayName = "Captain Brook", Email = "brook@hooked.demo", CreatedAt = now.AddMonths(-8) },
                new() { Username = "riverrose",    DisplayName = "River Rose",    Email = "rose@hooked.demo",  CreatedAt = now.AddMonths(-7) },
                new() { Username = "docksidejay",  DisplayName = "Dockside Jay",  Email = "jay@hooked.demo",   CreatedAt = now.AddMonths(-6) },
                new() { Username = "kelpkate",     DisplayName = "Kelp Kate",     Email = "kate@hooked.demo",  CreatedAt = now.AddMonths(-5) },
                new() { Username = "anglermax",    DisplayName = "Angler Max",    Email = "max@hooked.demo",   CreatedAt = now.AddMonths(-4) }
            };

            // Species mix for demo feed coverage
            var species = new List<FishSpecies>
            {
                new() { CommonName = "Australian Bass",    ScientificName = "Macquaria novemaculeata", ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Murray Cod",         ScientificName = "Maccullochella peelii",   ConservationStatus = "Vulnerable",        IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Dusky Flathead",     ScientificName = "Platycephalus fuscus",    ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Yellowfin Tuna",     ScientificName = "Thunnus albacares",       ConservationStatus = "Near Threatened",  IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Mulloway",           ScientificName = "Argyrosomus japonicus",   ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Yellowtail Kingfish",ScientificName = "Seriola lalandi",         ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Snapper",            ScientificName = "Chrysophrys auratus",     ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Bream",              ScientificName = "Acanthopagrus brama",     ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Brown Trout",        ScientificName = "Salmo trutta",            ConservationStatus = "Least Concern",    IsInvasive = true,  IsEndangered = false },
                new() { CommonName = "Rainbow Trout",      ScientificName = "Oncorhynchus mykiss",     ConservationStatus = "Least Concern",    IsInvasive = true,  IsEndangered = false },
                new() { CommonName = "Common Carp",        ScientificName = "Cyprinus carpio",         ConservationStatus = "Vulnerable",       IsInvasive = true,  IsEndangered = false },
                new() { CommonName = "Prussian Carp",      ScientificName = "Carassius gibelio",       ConservationStatus = "Least Concern",    IsInvasive = true,  IsEndangered = false },
                new() { CommonName = "Sardine",            ScientificName = "Sardinops sagax",         ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Sailfish",           ScientificName = "Istiophorus platypterus", ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Giant Trevally",     ScientificName = "Caranx ignobilis",        ConservationStatus = "Least Concern",    IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Sheepshead",         ScientificName = "Archosargus probatocephalus", ConservationStatus = "Least Concern", IsInvasive = false, IsEndangered = false }
            };

            _dbContext.Users.AddRange(users);
            _dbContext.FishSpecies.AddRange(species);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Catches spread across real NSW fishing spots
            var catches = new List<CatchRecord>
            {
                // Sydney Harbour (Circular Quay) � Australian Bass
                new() { UserId = users[0].Id, SpeciesId = species[0].Id, CaughtAt = now.AddHours(-3),  LengthMeters = 0.42, WeightKg = 1.8,  IsFavorite = true,  PhotoPath = "/seed/catches/bass-1.jpg",     LocationJson = "{\"lat\":-33.8568,\"lng\":151.2153}" },
                // Hawkesbury River (Windsor Bridge) � Murray Cod
                new() { UserId = users[0].Id, SpeciesId = species[1].Id, CaughtAt = now.AddDays(-1),   LengthMeters = 0.86, WeightKg = 8.2,  PhotoPath = "/seed/catches/cod-1.jpg",      LocationJson = "{\"lat\":-33.6133,\"lng\":150.8183}" },
                // Jervis Bay (bay entrance, in water) � Dusky Flathead
                new() { UserId = users[1].Id, SpeciesId = species[2].Id, CaughtAt = now.AddHours(-7),  LengthMeters = 0.71, WeightKg = 3.4,  IsFavorite = true,  PhotoPath = "/seed/catches/flathead-1.jpg", LocationJson = "{\"lat\":-35.0667,\"lng\":150.7883}" },
                // Lake Macquarie (lake centre) � Bream
                new() { UserId = users[1].Id, SpeciesId = species[7].Id, CaughtAt = now.AddDays(-2),   LengthMeters = 0.34, WeightKg = 0.9,  PhotoPath = "/seed/catches/bream-1.jpg",    LocationJson = "{\"lat\":-33.0833,\"lng\":151.5667}" },
                // Shoalhaven River (Nowra, in river) � Mulloway
                new() { UserId = users[2].Id, SpeciesId = species[4].Id, CaughtAt = now.AddHours(-9),  LengthMeters = 0.93, WeightKg = 7.6,  IsFavorite = true,  PhotoPath = "/seed/catches/mulloway-1.jpg", LocationJson = "{\"lat\":-34.8750,\"lng\":150.6017}" },
                // Lake Jindabyne (lake centre) � Australian Bass
                new() { UserId = users[2].Id, SpeciesId = species[0].Id, CaughtAt = now.AddDays(-3),   LengthMeters = 0.38, WeightKg = 1.3,  PhotoPath = "/seed/catches/bass-2.jpg",     LocationJson = "{\"lat\":-36.4300,\"lng\":148.6483}" },
                // Offshore Sydney (30 km east) � Yellowfin Tuna
                new() { UserId = users[3].Id, SpeciesId = species[3].Id, CaughtAt = now.AddHours(-12), LengthMeters = 1.14, WeightKg = 22.4, IsFavorite = true, PhotoPath = "/seed/catches/tuna-1.jpg",     LocationJson = "{\"lat\":-33.9500,\"lng\":152.0833}" },
                // Port Stephens (bay centre) � Yellowtail Kingfish
                new() { UserId = users[4].Id, SpeciesId = species[5].Id, CaughtAt = now.AddHours(-20), LengthMeters = 0.95, WeightKg = 9.8,  IsFavorite = true,  PhotoPath = "/seed/catches/kingfish-1.jpg", LocationJson = "{\"lat\":-32.7383,\"lng\":152.0917}" },
                // Broken Bay (Pittwater entrance) � Snapper
                new() { UserId = users[3].Id, SpeciesId = species[6].Id, CaughtAt = now.AddDays(-4),   LengthMeters = 0.55, WeightKg = 2.6,  PhotoPath = "/seed/catches/snapper-1.jpg",  LocationJson = "{\"lat\":-33.5617,\"lng\":151.3250}" },
                // Botany Bay (bay centre, in water) � Dusky Flathead
                new() { UserId = users[4].Id, SpeciesId = species[2].Id, CaughtAt = now.AddDays(-5),   LengthMeters = 0.62, WeightKg = 2.9,  PhotoPath = "/seed/catches/flathead-2.jpg", LocationJson = "{\"lat\":-34.0100,\"lng\":151.2217}" },
                // Tuggerah Lake (lake centre) � Bream
                new() { UserId = users[0].Id, SpeciesId = species[7].Id, CaughtAt = now.AddDays(-6),   LengthMeters = 0.29, WeightKg = 0.7,  PhotoPath = "/seed/catches/bream-2.jpg",    LocationJson = "{\"lat\":-33.3233,\"lng\":151.5117}" },
                // Clarence River mouth (Yamba) � Murray Cod
                new() { UserId = users[1].Id, SpeciesId = species[1].Id, CaughtAt = now.AddDays(-7),   LengthMeters = 0.78, WeightKg = 6.1,  PhotoPath = "/seed/catches/cod-2.jpg",      LocationJson = "{\"lat\":-29.4367,\"lng\":153.3617}" },
                // Thredbo River � Brown Trout
                new() { UserId = users[2].Id, SpeciesId = species[8].Id, CaughtAt = now.AddHours(-30), LengthMeters = 0.47, WeightKg = 1.7, IsFavorite = true,  LocationJson = "{\"lat\":-36.5000,\"lng\":148.3000}" },
                // Lake Eucumbene � Rainbow Trout
                new() { UserId = users[3].Id, SpeciesId = species[9].Id, CaughtAt = now.AddHours(-34), LengthMeters = 0.52, WeightKg = 2.1, LocationJson = "{\"lat\":-36.1167,\"lng\":148.6667}" },
                // Murray River near Echuca � Common Carp
                new() { UserId = users[4].Id, SpeciesId = species[10].Id, CaughtAt = now.AddDays(-8),  LengthMeters = 0.66, WeightKg = 4.9, IsFavorite = true,  LocationJson = "{\"lat\":-36.1333,\"lng\":144.7500}" },
                // Lachlan River � Prussian Carp
                new() { UserId = users[0].Id, SpeciesId = species[11].Id, CaughtAt = now.AddDays(-9),  LengthMeters = 0.31, WeightKg = 0.8, LocationJson = "{\"lat\":-33.7833,\"lng\":148.1667}" },
                // Offshore Eden � Sardine
                new() { UserId = users[1].Id, SpeciesId = species[12].Id, CaughtAt = now.AddDays(-10), LengthMeters = 0.21, WeightKg = 0.2, LocationJson = "{\"lat\":-37.0667,\"lng\":149.9000}" },
                // Coral Sea offshore � Sailfish
                new() { UserId = users[4].Id, SpeciesId = species[13].Id, CaughtAt = now.AddDays(-11), LengthMeters = 2.34, WeightKg = 44.2, IsFavorite = true,  LocationJson = "{\"lat\":-16.2000,\"lng\":146.1000}" },
                // Ningaloo Reef edge � Giant Trevally
                new() { UserId = users[3].Id, SpeciesId = species[14].Id, CaughtAt = now.AddDays(-12), LengthMeters = 1.08, WeightKg = 24.5, IsFavorite = true,  LocationJson = "{\"lat\":-22.1000,\"lng\":113.9000}" },
                // Gulf Coast jetty � Sheepshead
                new() { UserId = users[2].Id, SpeciesId = species[15].Id, CaughtAt = now.AddDays(-13), LengthMeters = 0.44, WeightKg = 1.6, LocationJson = "{\"lat\":29.8000,\"lng\":-93.9000}" }
            };

            var followRelations = new List<FriendRelation>
            {
                new() { UserId = users[0].Id, FriendId = users[1].Id, Since = now.AddMonths(-5) },
                new() { UserId = users[0].Id, FriendId = users[2].Id, Since = now.AddMonths(-4) },
                new() { UserId = users[1].Id, FriendId = users[0].Id, Since = now.AddMonths(-4) },
                new() { UserId = users[1].Id, FriendId = users[3].Id, Since = now.AddMonths(-3) },
                new() { UserId = users[2].Id, FriendId = users[0].Id, Since = now.AddMonths(-2) },
                new() { UserId = users[3].Id, FriendId = users[0].Id, Since = now.AddMonths(-1) },
                new() { UserId = users[4].Id, FriendId = users[1].Id, Since = now.AddMonths(-1) }
            };

            var reactions = new List<CatchReaction>
            {
                new() { CatchId = catches[0].Id,  UserId = users[1].Id, ReactedAt = now.AddHours(-2) },
                new() { CatchId = catches[0].Id,  UserId = users[2].Id, ReactedAt = now.AddHours(-2) },
                new() { CatchId = catches[0].Id,  UserId = users[3].Id, ReactedAt = now.AddHours(-1) },
                new() { CatchId = catches[2].Id,  UserId = users[0].Id, ReactedAt = now.AddHours(-6) },
                new() { CatchId = catches[2].Id,  UserId = users[4].Id, ReactedAt = now.AddHours(-5) },
                new() { CatchId = catches[4].Id,  UserId = users[0].Id, ReactedAt = now.AddHours(-8) },
                new() { CatchId = catches[6].Id,  UserId = users[1].Id, ReactedAt = now.AddHours(-10) },
                new() { CatchId = catches[6].Id,  UserId = users[2].Id, ReactedAt = now.AddHours(-10) },
                new() { CatchId = catches[7].Id,  UserId = users[0].Id, ReactedAt = now.AddHours(-18) },
                new() { CatchId = catches[8].Id,  UserId = users[4].Id, ReactedAt = now.AddHours(-22) },
                new() { CatchId = catches[11].Id, UserId = users[0].Id, ReactedAt = now.AddDays(-6) },
                new() { CatchId = catches[12].Id, UserId = users[1].Id, ReactedAt = now.AddDays(-1) },
                new() { CatchId = catches[13].Id, UserId = users[2].Id, ReactedAt = now.AddDays(-1) },
                new() { CatchId = catches[14].Id, UserId = users[3].Id, ReactedAt = now.AddDays(-2) },
                new() { CatchId = catches[15].Id, UserId = users[4].Id, ReactedAt = now.AddDays(-2) },
                new() { CatchId = catches[16].Id, UserId = users[0].Id, ReactedAt = now.AddDays(-3) },
                new() { CatchId = catches[17].Id, UserId = users[1].Id, ReactedAt = now.AddDays(-3) },
                new() { CatchId = catches[18].Id, UserId = users[2].Id, ReactedAt = now.AddDays(-4) },
                new() { CatchId = catches[19].Id, UserId = users[3].Id, ReactedAt = now.AddDays(-4) }
            };

            var comments = new List<CatchComment>
            {
                new() { CatchId = catches[0].Id,  UserId = users[1].Id, CommentText = "Sydney Harbour bass are back! Ripper catch.", CommentedAt = now.AddHours(-2) },
                new() { CatchId = catches[0].Id,  UserId = users[2].Id, CommentText = "What soft plastic were you on?",              CommentedAt = now.AddHours(-1) },
                new() { CatchId = catches[1].Id,  UserId = users[3].Id, CommentText = "Hawkesbury cod are on the chew this season.", CommentedAt = now.AddDays(-1) },
                new() { CatchId = catches[2].Id,  UserId = users[0].Id, CommentText = "Jervis Bay flathead never disappoint.",       CommentedAt = now.AddHours(-6) },
                new() { CatchId = catches[4].Id,  UserId = users[0].Id, CommentText = "Monster mulloway from the Shoalhaven!",       CommentedAt = now.AddHours(-8) },
                new() { CatchId = catches[6].Id,  UserId = users[1].Id, CommentText = "Offshore Sydney tuna is absolutely firing.",  CommentedAt = now.AddHours(-10) },
                new() { CatchId = catches[7].Id,  UserId = users[3].Id, CommentText = "Port Stephens kingies are nuts right now.",   CommentedAt = now.AddHours(-18) },
                new() { CatchId = catches[8].Id,  UserId = users[2].Id, CommentText = "Beautiful snapper from Broken Bay.",          CommentedAt = now.AddDays(-4) },
                new() { CatchId = catches[11].Id, UserId = users[4].Id, CommentText = "Clarence River cod are massive this year.",   CommentedAt = now.AddDays(-7) },
                new() { CatchId = catches[12].Id, UserId = users[4].Id, CommentText = "Great trout colours — clean release too.",     CommentedAt = now.AddDays(-1) },
                new() { CatchId = catches[13].Id, UserId = users[0].Id, CommentText = "Snowy Mountains rainbow trout are looking healthy.", CommentedAt = now.AddDays(-1) },
                new() { CatchId = catches[14].Id, UserId = users[1].Id, CommentText = "That carp is a tank. Nice control on the net.", CommentedAt = now.AddDays(-2) },
                new() { CatchId = catches[15].Id, UserId = users[2].Id, CommentText = "Prussian carp in the Lachlan again — great report.", CommentedAt = now.AddDays(-2) },
                new() { CatchId = catches[16].Id, UserId = users[3].Id, CommentText = "Sardine schools are thick offshore right now.", CommentedAt = now.AddDays(-3) },
                new() { CatchId = catches[17].Id, UserId = users[2].Id, CommentText = "That sailfish run must have been wild!",       CommentedAt = now.AddDays(-3) },
                new() { CatchId = catches[18].Id, UserId = users[4].Id, CommentText = "Giant trevally hit like a truck. Epic catch.", CommentedAt = now.AddDays(-4) },
                new() { CatchId = catches[19].Id, UserId = users[1].Id, CommentText = "Sheepshead patterns are so distinctive.",      CommentedAt = now.AddDays(-4) }
            };

            _dbContext.CatchRecords.AddRange(catches);
            _dbContext.FriendRelations.AddRange(followRelations);
            _dbContext.CatchReactions.AddRange(reactions);
            _dbContext.CatchComments.AddRange(comments);

            var discoveryBySpecies = catches
                .GroupBy(c => c.SpeciesId)
                .Select(group => group
                    .OrderBy(c => c.CaughtAt)
                    .ThenBy(c => c.Id)
                    .First())
                .ToDictionary(c => c.SpeciesId);

            foreach (var fishSpecies in species)
            {
                if (!discoveryBySpecies.TryGetValue(fishSpecies.Id, out var firstCatch))
                {
                    continue;
                }

                fishSpecies.DiscoveredAt = firstCatch.CaughtAt;
                fishSpecies.DiscoveredByUserId = firstCatch.UserId;
            }

            var fishDexEntries = catches
                .GroupBy(c => new { c.UserId, c.SpeciesId })
                .Select(group =>
                {
                    var firstCatch = group
                        .OrderBy(c => c.CaughtAt)
                        .ThenBy(c => c.Id)
                        .First();

                    var personalBestCatch = group
                        .OrderByDescending(c => c.LengthMeters ?? 0)
                        .ThenByDescending(c => c.CaughtAt)
                        .First();

                    return new FishDexEntry
                    {
                        UserId = group.Key.UserId,
                        SpeciesId = group.Key.SpeciesId,
                        UnlockedAt = firstCatch.CaughtAt,
                        CatchCount = group.Count(),
                        PersonalBestLengthMeters = personalBestCatch.LengthMeters,
                        PersonalBestCatchId = personalBestCatch.Id,
                        IsRare = false
                    };
                })
                .ToList();

            _dbContext.FishDexEntries.AddRange(fishDexEntries);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> ApplyPostgresMigrationsAsync(CancellationToken cancellationToken)
        {
            var connectionString = _dbContext.Database.GetConnectionString();
            if (TryBuildSupabaseDirectConnectionString(connectionString, out var directConnectionString)
                || IsSupabaseDirectConnection(connectionString, out directConnectionString))
            {
                _logger.LogInformation("Running EF migrations using Supabase direct connection host.");

                await RunMigrationsWithRetryAsync(directConnectionString, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (IsSupabasePoolerConnection(connectionString))
            {
                throw new InvalidOperationException(
                    "Supabase pooler connection was configured for startup migrations. Configure ConnectionStrings:DefaultConnection with direct host (db.<project-ref>.supabase.co) and postgres username.");
            }

            await _dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task RunMigrationsWithRetryAsync(string connectionString, CancellationToken cancellationToken)
        {
            const int maxAttempts = 4;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var options = new DbContextOptionsBuilder<HookedDbContext>()
                        .UseNpgsql(connectionString)
                        .Options;

                    await using var migrationContext = new HookedDbContext(options);
                    await migrationContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (NpgsqlException ex) when (IsTransientDnsResolutionFailure(ex) && attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    _logger.LogWarning(ex,
                        "Transient DNS resolution failure while connecting to Supabase direct host for migrations (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                        attempt,
                        maxAttempts,
                        delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static bool TryBuildSupabaseDirectConnectionString(string? connectionString, out string directConnectionString)
        {
            directConnectionString = string.Empty;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host) || !builder.Host.Contains(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(builder.Username) || !builder.Username.StartsWith("postgres.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var projectRef = builder.Username["postgres.".Length..];
            if (string.IsNullOrWhiteSpace(projectRef))
            {
                return false;
            }

            builder.Host = $"db.{projectRef}.supabase.co";
            builder.Port = 5432;
            builder.Username = "postgres";
            builder.Pooling = false;

            directConnectionString = builder.ConnectionString;
            return true;
        }

        private static bool IsTransientDnsResolutionFailure(NpgsqlException ex)
        {
            var socketEx = FindSocketException(ex);
            if (socketEx is not null)
            {
                return socketEx.SocketErrorCode is SocketError.NoData
                    or SocketError.HostNotFound
                    or SocketError.TryAgain;
            }

            return ex.Message.Contains("requested name is valid", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("requested type", StringComparison.OrdinalIgnoreCase);
        }

        private static SocketException? FindSocketException(Exception ex)
        {
            Exception? current = ex;
            while (current is not null)
            {
                if (current is SocketException socketException)
                {
                    return socketException;
                }

                current = current.InnerException;
            }

            return null;
        }

        private static bool IsSupabasePoolerConnection(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return !string.IsNullOrWhiteSpace(builder.Host)
                   && builder.Host.Contains(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupabaseDirectConnection(string? connectionString, out string directConnectionString)
        {
            directConnectionString = string.Empty;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host)
                || !builder.Host.StartsWith("db.", StringComparison.OrdinalIgnoreCase)
                || !builder.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            directConnectionString = builder.ConnectionString;
            return true;
        }

        private async Task SeedAchievementsAsync(CancellationToken cancellationToken)
        {
            if (await _dbContext.Achievements.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            var now = DateTime.UtcNow;
            _dbContext.Achievements.AddRange(
                new Achievement { Key = "first-catch",      Title = "First Cast",         Description = "Log your very first catch.",                          CreatedAt = now },
                new Achievement { Key = "catch-5",          Title = "On the Hook",         Description = "Log 5 catches.",                                      CreatedAt = now },
                new Achievement { Key = "catch-25",         Title = "Dedicated Angler",    Description = "Log 25 catches.",                                     CreatedAt = now },
                new Achievement { Key = "catch-100",        Title = "Master Angler",       Description = "Log 100 catches.",                                    CreatedAt = now },
                new Achievement { Key = "species-3",        Title = "Species Explorer",    Description = "Unlock 3 species in your FishDex.",                   CreatedAt = now },
                new Achievement { Key = "species-10",       Title = "Species Hunter",      Description = "Unlock 10 species in your FishDex.",                  CreatedAt = now },
                new Achievement { Key = "fishdex-complete", Title = "FishDex Master",      Description = "Complete the entire FishDex.",                        CreatedAt = now },
                new Achievement { Key = "big-catch",        Title = "Trophy Fish",         Description = "Catch a fish over 1 metre long.",                     CreatedAt = now },
                new Achievement { Key = "personal-best",    Title = "New Record",          Description = "Set a personal best length for any species.",         CreatedAt = now },
                new Achievement { Key = "global-discovery", Title = "Pioneer",             Description = "Discover a species new to the global FishDex.",       CreatedAt = now },
                new Achievement { Key = "session-complete", Title = "Gone Fishin'",        Description = "Complete your first fishing session.",                 CreatedAt = now },
                new Achievement { Key = "session-3",        Title = "Regular Outing",      Description = "Complete 3 fishing sessions.",                        CreatedAt = now },
                new Achievement { Key = "social-butterfly", Title = "Social Angler",       Description = "Follow 3 or more fellow anglers.",                    CreatedAt = now }
            );

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
