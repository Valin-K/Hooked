using Microsoft.EntityFrameworkCore;

namespace Hooked.Shared.Data
{
    internal sealed class HookedDatabaseInitializer : IHookedDatabaseInitializer
    {
        private readonly HookedDbContext _dbContext;

        public HookedDatabaseInitializer(HookedDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            _dbContext = dbContext;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
            await SeedDemoDataAsync(cancellationToken);
        }

        private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
        {
            if (await _dbContext.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var users = new List<User>
            {
                new() { Username = "captainbrook", DisplayName = "Captain Brook", Email = "brook@hooked.demo", CreatedAt = now.AddMonths(-8) },
                new() { Username = "riverrose", DisplayName = "River Rose", Email = "rose@hooked.demo", CreatedAt = now.AddMonths(-7) },
                new() { Username = "docksidejay", DisplayName = "Dockside Jay", Email = "jay@hooked.demo", CreatedAt = now.AddMonths(-6) },
                new() { Username = "kelpkate", DisplayName = "Kelp Kate", Email = "kate@hooked.demo", CreatedAt = now.AddMonths(-5) },
                new() { Username = "anglermax", DisplayName = "Angler Max", Email = "max@hooked.demo", CreatedAt = now.AddMonths(-4) }
            };

            var species = new List<FishSpecies>
            {
                new() { CommonName = "Largemouth Bass", ScientificName = "Micropterus salmoides", ConservationStatus = "Least Concern", IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Rainbow Trout", ScientificName = "Oncorhynchus mykiss", ConservationStatus = "Least Concern", IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Red Drum", ScientificName = "Sciaenops ocellatus", ConservationStatus = "Least Concern", IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Yellowfin Tuna", ScientificName = "Thunnus albacares", ConservationStatus = "Near Threatened", IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Northern Pike", ScientificName = "Esox lucius", ConservationStatus = "Least Concern", IsInvasive = false, IsEndangered = false },
                new() { CommonName = "Bluegill", ScientificName = "Lepomis macrochirus", ConservationStatus = "Least Concern", IsInvasive = false, IsEndangered = false }
            };

            _dbContext.Users.AddRange(users);
            _dbContext.FishSpecies.AddRange(species);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var catches = new List<CatchRecord>
            {
                new() { UserId = users[0].Id, SpeciesId = species[0].Id, CaughtAt = now.AddHours(-3), LengthMeters = 0.58, WeightKg = 2.9, PhotoPath = "/seed/catches/bass-1.jpg", LocationJson = "{\"lat\":34.7465,\"lng\":-92.2896}" },
                new() { UserId = users[0].Id, SpeciesId = species[1].Id, CaughtAt = now.AddDays(-1), LengthMeters = 0.42, WeightKg = 1.4, PhotoPath = "/seed/catches/trout-1.jpg", LocationJson = "{\"lat\":39.7392,\"lng\":-104.9903}" },
                new() { UserId = users[1].Id, SpeciesId = species[2].Id, CaughtAt = now.AddHours(-7), LengthMeters = 0.67, WeightKg = 3.6, PhotoPath = "/seed/catches/reddrum-1.jpg", LocationJson = "{\"lat\":29.7604,\"lng\":-95.3698}" },
                new() { UserId = users[1].Id, SpeciesId = species[5].Id, CaughtAt = now.AddDays(-2), LengthMeters = 0.31, WeightKg = 0.7, PhotoPath = "/seed/catches/bluegill-1.jpg", LocationJson = "{\"lat\":41.8781,\"lng\":-87.6298}" },
                new() { UserId = users[2].Id, SpeciesId = species[4].Id, CaughtAt = now.AddHours(-9), LengthMeters = 0.72, WeightKg = 4.2, PhotoPath = "/seed/catches/pike-1.jpg", LocationJson = "{\"lat\":44.9537,\"lng\":-93.0900}" },
                new() { UserId = users[2].Id, SpeciesId = species[1].Id, CaughtAt = now.AddDays(-3), LengthMeters = 0.39, WeightKg = 1.2, PhotoPath = "/seed/catches/trout-2.jpg", LocationJson = "{\"lat\":45.5152,\"lng\":-122.6784}" },
                new() { UserId = users[3].Id, SpeciesId = species[3].Id, CaughtAt = now.AddHours(-12), LengthMeters = 1.08, WeightKg = 17.1, PhotoPath = "/seed/catches/tuna-1.jpg", LocationJson = "{\"lat\":21.3069,\"lng\":-157.8583}" },
                new() { UserId = users[4].Id, SpeciesId = species[0].Id, CaughtAt = now.AddHours(-20), LengthMeters = 0.61, WeightKg = 3.3, PhotoPath = "/seed/catches/bass-2.jpg", LocationJson = "{\"lat\":33.7490,\"lng\":-84.3880}" }
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
                new() { CatchId = catches[0].Id, UserId = users[1].Id, ReactedAt = now.AddHours(-2) },
                new() { CatchId = catches[0].Id, UserId = users[2].Id, ReactedAt = now.AddHours(-2) },
                new() { CatchId = catches[0].Id, UserId = users[3].Id, ReactedAt = now.AddHours(-1) },
                new() { CatchId = catches[2].Id, UserId = users[0].Id, ReactedAt = now.AddHours(-6) },
                new() { CatchId = catches[2].Id, UserId = users[4].Id, ReactedAt = now.AddHours(-5) },
                new() { CatchId = catches[4].Id, UserId = users[0].Id, ReactedAt = now.AddHours(-8) },
                new() { CatchId = catches[6].Id, UserId = users[1].Id, ReactedAt = now.AddHours(-10) },
                new() { CatchId = catches[6].Id, UserId = users[2].Id, ReactedAt = now.AddHours(-10) },
                new() { CatchId = catches[7].Id, UserId = users[0].Id, ReactedAt = now.AddHours(-18) }
            };

            var comments = new List<CatchComment>
            {
                new() { CatchId = catches[0].Id, UserId = users[1].Id, CommentText = "That bass is a tank. Nice pull!", CommentedAt = now.AddHours(-2) },
                new() { CatchId = catches[0].Id, UserId = users[2].Id, CommentText = "What lure did you use there?", CommentedAt = now.AddHours(-1) },
                new() { CatchId = catches[2].Id, UserId = users[0].Id, CommentText = "Love that color on the red drum.", CommentedAt = now.AddHours(-6) },
                new() { CatchId = catches[4].Id, UserId = users[0].Id, CommentText = "Pike season is on fire.", CommentedAt = now.AddHours(-8) },
                new() { CatchId = catches[6].Id, UserId = users[1].Id, CommentText = "Absolute offshore monster!", CommentedAt = now.AddHours(-10) },
                new() { CatchId = catches[7].Id, UserId = users[3].Id, CommentText = "Great healthy bass, solid release.", CommentedAt = now.AddHours(-18) }
            };

            _dbContext.CatchRecords.AddRange(catches);
            _dbContext.FriendRelations.AddRange(followRelations);
            _dbContext.CatchReactions.AddRange(reactions);
            _dbContext.CatchComments.AddRange(comments);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
