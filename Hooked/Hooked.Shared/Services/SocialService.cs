using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class SocialService : ISocialService
    {
        private readonly HookedDbContext _db;

        public SocialService(HookedDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<SocialUserLookupDto> ResolveCurrentUserAsync(string? preferredUsername = null, CancellationToken cancellationToken = default)
        {
            SocialUserLookupDto? user = null;
            if (!string.IsNullOrWhiteSpace(preferredUsername))
            {
                user = await GetUserByUsernameAsync(preferredUsername, cancellationToken).ConfigureAwait(false);
            }

            if (user is not null)
            {
                return user;
            }

            var fallbackUser = await _db.Users.AsNoTracking()
                .OrderBy(u => u.CreatedAt)
                .ThenBy(u => u.Username)
                .Select(u => new SocialUserLookupDto(u.Id, u.Username, u.DisplayName, u.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return fallbackUser ?? throw new InvalidOperationException("No users exist in the database.");
        }

        public async Task<SocialUserLookupDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return null;
            }

            return await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new SocialUserLookupDto(u.Id, u.Username, u.DisplayName, u.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<SocialUserLookupDto?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var normalized = username.Trim();
            return await _db.Users.AsNoTracking()
                .Where(u => u.Username == normalized)
                .Select(u => new SocialUserLookupDto(u.Id, u.Username, u.DisplayName, u.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SocialUserLookupDto>> LookupUsersAsync(string? query = null, int limit = 20, CancellationToken cancellationToken = default)
        {
            var normalizedLimit = Math.Clamp(limit, 1, 100);
            var users = _db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var likePattern = $"%{query.Trim()}%";
                users = users.Where(u =>
                    EF.Functions.Like(u.Username, likePattern) ||
                    (u.DisplayName != null && EF.Functions.Like(u.DisplayName, likePattern)));
            }

            return await users
                .OrderBy(u => u.Username)
                .Take(normalizedLimit)
                .Select(u => new SocialUserLookupDto(u.Id, u.Username, u.DisplayName, u.CreatedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<SocialProfileSummaryDto?> GetProfileSummaryByUsernameAsync(string username, Guid? viewerUserId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username is required.", nameof(username));
            }

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username.Trim(), cancellationToken)
                .ConfigureAwait(false);
            if (user is null)
            {
                return null;
            }

            return await BuildProfileSummaryAsync(user, viewerUserId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<SocialProfileSummaryDto?> GetProfileSummaryByIdAsync(Guid userId, Guid? viewerUserId = null, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return null;
            }

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                .ConfigureAwait(false);
            if (user is null)
            {
                return null;
            }

            return await BuildProfileSummaryAsync(user, viewerUserId, cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<SocialCatchFeedItemDto>> GetUserFeedAsync(Guid userId, Guid viewerUserId, int limit = 25, CancellationToken cancellationToken = default)
        {
            return GetFeedAsync(c => c.UserId == userId, viewerUserId, limit, cancellationToken);
        }

        public Task<IReadOnlyList<SocialCatchFeedItemDto>> GetCommunityFeedAsync(Guid viewerUserId, int limit = 25, CancellationToken cancellationToken = default)
        {
            return GetFeedAsync(c => c.UserId != viewerUserId, viewerUserId, limit, cancellationToken);
        }

        public async Task<bool> FollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            if (targetUserId == Guid.Empty)
            {
                throw new ArgumentException("Target user ID is required.", nameof(targetUserId));
            }

            if (userId == targetUserId)
            {
                return false;
            }

            await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);
            await EnsureUserExistsAsync(targetUserId, cancellationToken).ConfigureAwait(false);

            var alreadyFollowing = await _db.FriendRelations
                .AnyAsync(f => f.UserId == userId && f.FriendId == targetUserId, cancellationToken)
                .ConfigureAwait(false);
            if (alreadyFollowing)
            {
                return false;
            }

            _db.FriendRelations.Add(new FriendRelation
            {
                UserId = userId,
                FriendId = targetUserId,
                Since = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> UnfollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            if (targetUserId == Guid.Empty)
            {
                throw new ArgumentException("Target user ID is required.", nameof(targetUserId));
            }

            var relation = await _db.FriendRelations
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FriendId == targetUserId, cancellationToken)
                .ConfigureAwait(false);
            if (relation is null)
            {
                return false;
            }

            _db.FriendRelations.Remove(relation);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<SocialReactionToggleDto> ToggleReactionAsync(Guid catchId, Guid userId, CancellationToken cancellationToken = default)
        {
            if (catchId == Guid.Empty)
            {
                throw new ArgumentException("Catch ID is required.", nameof(catchId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);
            await EnsureCatchExistsAsync(catchId, cancellationToken).ConfigureAwait(false);

            var existingReaction = await _db.CatchReactions
                .FirstOrDefaultAsync(r => r.CatchId == catchId && r.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var isReacted = existingReaction is null;
            if (existingReaction is null)
            {
                _db.CatchReactions.Add(new CatchReaction
                {
                    CatchId = catchId,
                    UserId = userId,
                    ReactedAt = DateTime.UtcNow
                });
            }
            else
            {
                _db.CatchReactions.Remove(existingReaction);
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var reactionCount = await _db.CatchReactions
                .CountAsync(r => r.CatchId == catchId, cancellationToken)
                .ConfigureAwait(false);

            return new SocialReactionToggleDto(catchId, userId, isReacted, reactionCount);
        }

        public async Task<SocialCommentDto> AddCommentAsync(Guid catchId, Guid userId, string commentText, CancellationToken cancellationToken = default)
        {
            if (catchId == Guid.Empty)
            {
                throw new ArgumentException("Catch ID is required.", nameof(catchId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(commentText))
            {
                throw new ArgumentException("Comment text is required.", nameof(commentText));
            }

            var sanitizedComment = commentText.Trim();
            if (sanitizedComment.Length > 500)
            {
                throw new ArgumentException("Comment text cannot exceed 500 characters.", nameof(commentText));
            }

            await EnsureCatchExistsAsync(catchId, cancellationToken).ConfigureAwait(false);
            var commenter = await GetUserByIdAsync(userId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

            var comment = new CatchComment
            {
                CatchId = catchId,
                UserId = userId,
                CommentText = sanitizedComment,
                CommentedAt = DateTime.UtcNow
            };

            _db.CatchComments.Add(comment);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new SocialCommentDto(
                comment.Id,
                comment.CatchId,
                comment.UserId,
                commenter.Username,
                commenter.DisplayName,
                comment.CommentText,
                comment.CommentedAt,
                comment.EditedAt);
        }

        public async Task<SocialCommentDto> EditCommentAsync(Guid commentId, Guid requestingUserId, string newText, CancellationToken cancellationToken = default)
        {
            if (commentId == Guid.Empty)
            {
                throw new ArgumentException("Comment ID is required.", nameof(commentId));
            }

            if (requestingUserId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(requestingUserId));
            }

            if (string.IsNullOrWhiteSpace(newText))
            {
                throw new ArgumentException("Comment text is required.", nameof(newText));
            }

            var sanitized = newText.Trim();
            if (sanitized.Length > 500)
            {
                throw new ArgumentException("Comment text cannot exceed 500 characters.", nameof(newText));
            }

            var comment = await _db.CatchComments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Comment '{commentId}' was not found.");

            if (comment.UserId != requestingUserId)
            {
                throw new UnauthorizedAccessException("You can only edit your own comments.");
            }

            comment.CommentText = sanitized;
            comment.EditedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var username = comment.User?.Username ?? string.Empty;
            var displayName = comment.User?.DisplayName;
            return new SocialCommentDto(comment.Id, comment.CatchId, comment.UserId, username, displayName, comment.CommentText, comment.CommentedAt, comment.EditedAt);
        }

        public async Task DeleteCommentAsync(Guid commentId, Guid requestingUserId, CancellationToken cancellationToken = default)
        {
            if (commentId == Guid.Empty)
            {
                throw new ArgumentException("Comment ID is required.", nameof(commentId));
            }

            if (requestingUserId == Guid.Empty)
            {
                throw new ArgumentException("User ID is required.", nameof(requestingUserId));
            }

            var comment = await _db.CatchComments
                .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Comment '{commentId}' was not found.");

            if (comment.UserId != requestingUserId)
            {
                throw new UnauthorizedAccessException("You can only delete your own comments.");
            }

            _db.CatchComments.Remove(comment);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<SocialProfileSummaryDto> BuildProfileSummaryAsync(User user, Guid? viewerUserId, CancellationToken cancellationToken)
        {
            var catchCount = await _db.CatchRecords.CountAsync(c => c.UserId == user.Id, cancellationToken).ConfigureAwait(false);
            var followerCount = await _db.FriendRelations.CountAsync(f => f.FriendId == user.Id, cancellationToken).ConfigureAwait(false);
            var followingCount = await _db.FriendRelations.CountAsync(f => f.UserId == user.Id, cancellationToken).ConfigureAwait(false);

            var isFollowing = viewerUserId.HasValue && await _db.FriendRelations
                .AnyAsync(f => f.UserId == viewerUserId.Value && f.FriendId == user.Id, cancellationToken)
                .ConfigureAwait(false);

            return new SocialProfileSummaryDto(
                user.Id,
                user.Username,
                user.DisplayName,
                user.CreatedAt,
                catchCount,
                followerCount,
                followingCount,
                isFollowing);
        }

        private async Task<IReadOnlyList<SocialCatchFeedItemDto>> GetFeedAsync(
            System.Linq.Expressions.Expression<Func<CatchRecord, bool>> filter,
            Guid viewerUserId,
            int limit,
            CancellationToken cancellationToken)
        {
            if (viewerUserId == Guid.Empty)
            {
                throw new ArgumentException("Viewer user ID is required.", nameof(viewerUserId));
            }

            var normalizedLimit = Math.Clamp(limit, 1, 100);

            var catches = await _db.CatchRecords.AsNoTracking()
                .Where(filter)
                .OrderByDescending(c => c.CaughtAt)
                .Take(normalizedLimit)
                .Select(c => new FeedItemProjection(
                    c.Id,
                    c.UserId,
                    c.User != null ? c.User.Username : string.Empty,
                    c.User != null ? c.User.DisplayName : null,
                    c.CaughtAt,
                    c.SpeciesId,
                    c.Species != null ? c.Species.CommonName : string.Empty,
                    c.LengthMeters,
                    c.WeightKg,
                    c.PhotoPath,
                    c.LocationJson,
                    c.Reactions.Count(),
                    c.Comments.Count(),
                    c.Reactions.Any(r => r.UserId == viewerUserId),
                    c.UserId == viewerUserId && c.IsFavorite))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (catches.Count == 0)
            {
                return Array.Empty<SocialCatchFeedItemDto>();
            }

            var catchIds = catches.Select(c => c.CatchId).ToList();
            var recentComments = await _db.CatchComments.AsNoTracking()
                .Where(c => catchIds.Contains(c.CatchId))
                .OrderByDescending(c => c.CommentedAt)
                .Select(c => new FeedCommentProjection(
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

            var commentsByCatch = recentComments
                .GroupBy(c => c.CatchId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<SocialCommentDto>)g
                        .Take(3)
                        .OrderBy(c => c.CommentedAt)
                        .Select(c => new SocialCommentDto(c.Id, c.CatchId, c.UserId, c.Username, c.DisplayName, c.CommentText, c.CommentedAt, c.EditedAt))
                        .ToList());

            return catches
                .Select(c => new SocialCatchFeedItemDto(
                    c.CatchId,
                    c.UserId,
                    c.Username,
                    c.DisplayName,
                    c.CaughtAt,
                    c.SpeciesId,
                    c.SpeciesCommonName,
                    c.LengthMeters,
                    c.WeightKg,
                    c.PhotoPath,
                    c.LocationJson,
                    c.ReactionCount,
                    c.CommentCount,
                    c.ViewerHasReacted,
                    c.IsFavorite,
                    commentsByCatch.TryGetValue(c.CatchId, out var comments) ? comments : Array.Empty<SocialCommentDto>()))
                .ToList();
        }

        private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var exists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                throw new KeyNotFoundException($"User '{userId}' was not found.");
            }
        }

        private async Task EnsureCatchExistsAsync(Guid catchId, CancellationToken cancellationToken)
        {
            var exists = await _db.CatchRecords.AnyAsync(c => c.Id == catchId, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                throw new KeyNotFoundException($"Catch '{catchId}' was not found.");
            }
        }

        private sealed record FeedItemProjection(
            Guid CatchId,
            Guid UserId,
            string Username,
            string? DisplayName,
            DateTime CaughtAt,
            int SpeciesId,
            string SpeciesCommonName,
            double? LengthMeters,
            double? WeightKg,
            string? PhotoPath,
            string? LocationJson,
            int ReactionCount,
            int CommentCount,
            bool ViewerHasReacted,
            bool IsFavorite);

        private sealed record FeedCommentProjection(
            Guid Id,
            Guid CatchId,
            Guid UserId,
            string Username,
            string? DisplayName,
            string CommentText,
            DateTime CommentedAt,
            DateTime? EditedAt);
    }
}
