using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public sealed class SessionService : ISessionService
    {
        private readonly IDbContextFactory<HookedDbContext> _dbFactory;
        private readonly IAchievementService _achievementService;

        public SessionService(IDbContextFactory<HookedDbContext> dbFactory, IAchievementService achievementService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _achievementService = achievementService ?? throw new ArgumentNullException(nameof(achievementService));
        }

        public async Task<FishingSession> StartSessionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty) throw new ArgumentException("UserId required");

            await using var db = _dbFactory.CreateDbContext();
            var active = await db.FishingSessions
                .Include(s => s.Catches)
                .ThenInclude(c => c.Species)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken);

            if (active != null)
            {
                return active;
            }

            var session = new FishingSession
            {
                UserId = userId,
                StartTime = DateTime.UtcNow,
                IsActive = true
            };

            db.FishingSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task<FishingSession?> GetActiveSessionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            return await db.FishingSessions
                .Include(s => s.Catches)
                .ThenInclude(c => c.Species)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken);
        }

        public async Task<FishingSession> EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            var session = await db.FishingSessions
                .Include(s => s.Catches)
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

            if (session == null) throw new InvalidOperationException("Session not found");

            session.IsActive = false;
            session.EndTime = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            await _achievementService
                .CheckAndAwardAsync(session.UserId, cancellationToken)
                .ConfigureAwait(false);

            return session;
        }

        public async Task<Post> CreatePostFromSessionAsync(Guid sessionId, string title, string? body, string? locationName, IReadOnlyList<string>? selectedPhotos, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            var session = await db.FishingSessions
                .Include(s => s.Catches)
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

            if (session == null) throw new InvalidOperationException("Session not found");

            var post = new Post
            {
                UserId = session.UserId,
                FishingSessionId = session.Id,
                Title = title,
                Body = body ?? string.Empty,
                LocationName = locationName,
                CreatedAt = DateTime.UtcNow,
            };

            if (selectedPhotos != null)
            {
                foreach (var photoUrl in selectedPhotos)
                {
                    post.Photos.Add(new PostPhoto { PhotoPath = photoUrl });
                }
            }

            db.Posts.Add(post);
            await db.SaveChangesAsync(cancellationToken);
            return post;
        }

        public async Task<IReadOnlyList<Post>> GetPostsAsync(int limit = 20, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            return await db.Posts
                .Include(p => p.User)
                .Include(p => p.Photos)
                .Include(p => p.FishingSession)
                .ThenInclude(fs => fs!.Catches)
                .ThenInclude(c => c.Species)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
