using Microsoft.EntityFrameworkCore;

namespace Hooked.Shared.Data
{
    public sealed class HookedDbContext : DbContext
    {
        public HookedDbContext(DbContextOptions<HookedDbContext> options)
            : base(options)
        {
        }

        // Users of the app
        public DbSet<User> Users { get; set; } = null!;

        // Known fish species catalog
        public DbSet<FishSpecies> FishSpecies { get; set; } = null!;

        // Records of user catches
        public DbSet<CatchRecord> CatchRecords { get; set; } = null!;
        public DbSet<CatchReaction> CatchReactions { get; set; } = null!;
        public DbSet<CatchComment> CatchComments { get; set; } = null!;

        // Community sightings (not necessarily caught)
        public DbSet<Sighting> Sightings { get; set; } = null!;

        // Friendship relationships
        public DbSet<FriendRelation> FriendRelations { get; set; } = null!;

        // Achievements and badges
        public DbSet<Achievement> Achievements { get; set; } = null!;

        // Per-user collection entries (FishDex)
        public DbSet<FishDexEntry> FishDexEntries { get; set; } = null!;

        // Fishing Sessions and Posts
        public DbSet<FishingSession> FishingSessions { get; set; } = null!;
        public DbSet<Post> Posts { get; set; } = null!;
        public DbSet<PostPhoto> PostPhotos { get; set; } = null!;

        // Per-user earned achievements
        public DbSet<UserAchievement> UserAchievements { get; set; } = null!;

        // Cached leaderboard entries (optional)
        public DbSet<LeaderboardEntry> LeaderboardEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.HasIndex(u => u.Username).IsUnique();
                b.Property(u => u.DisplayName).HasMaxLength(100);
                b.Property(u => u.Email).HasMaxLength(254);
            });

            modelBuilder.Entity<FishSpecies>(b =>
            {
                b.HasKey(s => s.Id);
                b.HasIndex(s => s.CommonName);
                b.Property(s => s.ScientificName).HasMaxLength(200);
                b.Property(s => s.CommonName).HasMaxLength(200);
                b.Property(s => s.IllustrationImageUrl).HasMaxLength(1024);
                b.HasOne(s => s.DiscoveredByUser)
                    .WithMany()
                    .HasForeignKey(s => s.DiscoveredByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CatchRecord>(b =>
            {
                b.HasKey(c => c.Id);
                b.HasOne(c => c.User).WithMany(u => u.Catches).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(c => c.Species).WithMany(s => s.Catches).HasForeignKey(c => c.SpeciesId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(c => c.FishingSession).WithMany(s => s.Catches).HasForeignKey(c => c.FishingSessionId).OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(c => c.UserId);
                b.HasIndex(c => c.SpeciesId);
                b.HasIndex(c => c.CaughtAt);

                b.Property(c => c.PhotoPath).HasMaxLength(512);
            });

            modelBuilder.Entity<CatchReaction>(b =>
            {
                b.HasKey(r => new { r.CatchId, r.UserId });
                b.HasOne(r => r.Catch).WithMany(c => c.Reactions).HasForeignKey(r => r.CatchId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(r => r.User).WithMany(u => u.CatchReactions).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(r => r.UserId);
                b.HasIndex(r => r.ReactedAt);
            });

            modelBuilder.Entity<CatchComment>(b =>
            {
                b.HasKey(c => c.Id);
                b.HasOne(c => c.Catch).WithMany(cr => cr.Comments).HasForeignKey(c => c.CatchId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(c => c.User).WithMany(u => u.CatchComments).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);

                b.Property(c => c.CommentText).HasMaxLength(500).IsRequired();
                b.HasIndex(c => c.CatchId);
                b.HasIndex(c => c.UserId);
                b.HasIndex(c => c.CommentedAt);
            });

            modelBuilder.Entity<Sighting>(b =>
            {
                b.HasKey(s => s.Id);
                b.HasOne(s => s.User).WithMany(u => u.Sightings).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(s => s.Species).WithMany(s => s.Sightings).HasForeignKey(s => s.SpeciesId).OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(s => s.SpeciesId);
                b.HasIndex(s => s.ReportedAt);
            });

            modelBuilder.Entity<FriendRelation>(b =>
            {
                b.HasKey(f => new { f.UserId, f.FriendId });
                b.HasOne(f => f.User).WithMany(u => u.Friends).HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(f => f.Friend).WithMany().HasForeignKey(f => f.FriendId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Achievement>(b =>
            {
                b.HasKey(a => a.Id);
                b.HasIndex(a => a.Key).IsUnique();
                b.Property(a => a.Title).HasMaxLength(200);
                b.Property(a => a.Description).HasMaxLength(1000);
            });

            modelBuilder.Entity<FishDexEntry>(b =>
            {
                b.HasKey(fd => new { fd.UserId, fd.SpeciesId });
                b.HasOne(fd => fd.User).WithMany(u => u.FishDexEntries).HasForeignKey(fd => fd.UserId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(fd => fd.Species).WithMany(s => s.FishDexEntries).HasForeignKey(fd => fd.SpeciesId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(fd => fd.PersonalBestCatch).WithMany().HasForeignKey(fd => fd.PersonalBestCatchId).OnDelete(DeleteBehavior.SetNull);
                b.HasIndex(fd => fd.UnlockedAt);
            });

            modelBuilder.Entity<FishingSession>(b =>
            {
                b.HasKey(s => s.Id);
                b.HasOne(s => s.User).WithMany(u => u.FishingSessions).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(s => s.UserId);
                b.HasIndex(s => s.StartTime);
            });

            modelBuilder.Entity<Post>(b =>
            {
                b.HasKey(p => p.Id);
                b.HasOne(p => p.User).WithMany(u => u.Posts).HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(p => p.FishingSession).WithMany().HasForeignKey(p => p.FishingSessionId).OnDelete(DeleteBehavior.Restrict);

                b.Property(p => p.Title).HasMaxLength(200);
                b.Property(p => p.LocationName).HasMaxLength(200);
                b.HasIndex(p => p.UserId);
                b.HasIndex(p => p.CreatedAt);
            });

            modelBuilder.Entity<PostPhoto>(b =>
            {
                b.HasKey(p => p.Id);
                b.HasOne(p => p.Post).WithMany(post => post.Photos).HasForeignKey(p => p.PostId).OnDelete(DeleteBehavior.Cascade);
                b.Property(p => p.PhotoPath).HasMaxLength(512).IsRequired();
            });

            modelBuilder.Entity<UserAchievement>(b =>
            {
                b.HasKey(ua => new { ua.UserId, ua.AchievementId });
                b.HasOne(ua => ua.User).WithMany(u => u.UserAchievements).HasForeignKey(ua => ua.UserId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(ua => ua.Achievement).WithMany(a => a.UserAchievements).HasForeignKey(ua => ua.AchievementId).OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(ua => ua.UserId);
                b.HasIndex(ua => ua.EarnedAt);
            });

            modelBuilder.Entity<LeaderboardEntry>(b =>
            {
                b.HasKey(l => l.Id);
                b.HasIndex(l => new { l.Category, l.Score });
                b.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // Seed minimal species entries could be added later via initializer
        }
    }
}
