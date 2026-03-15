using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public sealed class SocialService : ISocialService
    {
        private readonly IDbContextFactory<HookedDbContext> _dbFactory;
        private const int FeedPageTokenVersion = 1;

        public SocialService(IDbContextFactory<HookedDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public async Task<SocialUserLookupDto> ResolveCurrentUserAsync(string? preferredUsername = null, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            SocialUserLookupDto? user = null;
            if (!string.IsNullOrWhiteSpace(preferredUsername))
            {
                var normalized = preferredUsername.Trim();
                user = await db.Users.AsNoTracking()
                    .Where(u => u.Username == normalized)
                    .Select(u => new SocialUserLookupDto(u.Id, u.Username, u.DisplayName, u.CreatedAt))
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (user is not null)
            {
                return user;
            }

            var fallbackUser = await db.Users.AsNoTracking()
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

            await using var db = _dbFactory.CreateDbContext();
            return await db.Users.AsNoTracking()
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

            await using var db = _dbFactory.CreateDbContext();
            var normalized = username.Trim();
            return await db.Users.AsNoTracking()
                .Where(u => u.Username == normalized)
                .Select(u => new SocialUserLookupDto(u.Id, u.Username, u.DisplayName, u.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SocialUserLookupDto>> LookupUsersAsync(string? query = null, int limit = 20, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            var normalizedLimit = Math.Clamp(limit, 1, 100);
            var users = db.Users.AsNoTracking();

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

            await using var db = _dbFactory.CreateDbContext();
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username.Trim(), cancellationToken)
                .ConfigureAwait(false);
            if (user is null)
            {
                return null;
            }

            return await BuildProfileSummaryAsync(user, viewerUserId, db, cancellationToken).ConfigureAwait(false);
        }

        public async Task<SocialProfileSummaryDto?> GetProfileSummaryByIdAsync(Guid userId, Guid? viewerUserId = null, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return null;
            }

            await using var db = _dbFactory.CreateDbContext();
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                .ConfigureAwait(false);
            if (user is null)
            {
                return null;
            }

            return await BuildProfileSummaryAsync(user, viewerUserId, db, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SocialCatchFeedItemDto>> GetUserFeedAsync(Guid userId, Guid viewerUserId, int limit = 25, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            return await GetFeedAsync(c => c.UserId == userId, viewerUserId, limit, db, cancellationToken);
        }

        public async Task<IReadOnlyList<SocialCatchFeedItemDto>> GetCommunityFeedAsync(Guid viewerUserId, int limit = 25, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            return await GetFeedAsync(c => c.UserId != viewerUserId, viewerUserId, limit, db, cancellationToken);
        }

        public async Task<SocialCommunityFeedPageDto> GetCommunityFeedPageAsync(Guid viewerUserId, string? continuationToken = null, int limit = 25, CancellationToken cancellationToken = default)
        {
            if (viewerUserId == Guid.Empty)
            {
                throw new ArgumentException("Viewer user ID is required.", nameof(viewerUserId));
            }

            var normalizedLimit = Math.Clamp(limit, 1, 100);
            var token = ParseContinuationToken(continuationToken);

            await using var db = _dbFactory.CreateDbContext();
            await EnsureUserExistsAsync(viewerUserId, db, cancellationToken).ConfigureAwait(false);

            var followedUserIds = await db.FriendRelations.AsNoTracking()
                .Where(f => f.UserId == viewerUserId && f.FriendId != viewerUserId)
                .Select(f => f.FriendId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var hasFollowedUsers = followedUserIds.Count > 0;

            var followingItems = new List<FeedItemProjection>(normalizedLimit);
            var recommendedItems = new List<FeedItemProjection>(normalizedLimit);
            var hasMore = false;
            CommunityFeedContinuationToken? nextToken = null;

            if (!token.InRecommendedBucket && hasFollowedUsers)
            {
                var followedBatch = await BuildFeedProjectionQuery(
                        db.CatchRecords.AsNoTracking()
                            .Where(c => followedUserIds.Contains(c.UserId) && c.UserId != viewerUserId && c.CaughtAt <= token.SnapshotUtc)
                            .OrderByDescending(c => c.CaughtAt)
                            .ThenByDescending(c => c.Id),
                        viewerUserId)
                    .Skip(token.FollowingOffset)
                    .Take(normalizedLimit + 1)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var hasMoreFollowing = followedBatch.Count > normalizedLimit;
                followingItems.AddRange(followedBatch.Take(normalizedLimit));

                if (hasMoreFollowing)
                {
                    hasMore = true;
                    nextToken = token with
                    {
                        FollowingOffset = token.FollowingOffset + followingItems.Count,
                        InRecommendedBucket = false
                    };
                }
                else
                {
                    var recommendedTake = normalizedLimit - followingItems.Count;
                    var hasMoreRecommended = false;
                    if (recommendedTake > 0)
                    {
                        var followingCatchIds = followingItems.Select(item => item.CatchId).ToList();
                        var recommendedBatch = await BuildFeedProjectionQuery(
                                BuildRecommendedFeedBaseQuery(db, followedUserIds, viewerUserId, token.SnapshotUtc)
                                    .Where(c => !followingCatchIds.Contains(c.Id)),
                                viewerUserId)
                            .Skip(token.RecommendedOffset)
                            .Take(recommendedTake + 1)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        hasMoreRecommended = recommendedBatch.Count > recommendedTake;
                        recommendedItems.AddRange(recommendedBatch.Take(recommendedTake));
                    }
                    else
                    {
                        hasMoreRecommended = await BuildRecommendedFeedBaseQuery(db, followedUserIds, viewerUserId, token.SnapshotUtc)
                            .Skip(token.RecommendedOffset)
                            .AnyAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }

                    hasMore = hasMoreRecommended;

                    if (hasMore)
                    {
                        nextToken = token with
                        {
                            FollowingOffset = token.FollowingOffset + followingItems.Count,
                            RecommendedOffset = token.RecommendedOffset + recommendedItems.Count,
                            InRecommendedBucket = true
                        };
                    }
                }
            }
            else
            {
                var recommendedBatch = await BuildFeedProjectionQuery(
                        BuildRecommendedFeedBaseQuery(db, followedUserIds, viewerUserId, token.SnapshotUtc),
                        viewerUserId)
                    .Skip(token.RecommendedOffset)
                    .Take(normalizedLimit + 1)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                hasMore = recommendedBatch.Count > normalizedLimit;
                recommendedItems.AddRange(recommendedBatch.Take(normalizedLimit));

                if (hasMore)
                {
                    nextToken = token with
                    {
                        RecommendedOffset = token.RecommendedOffset + recommendedItems.Count,
                        InRecommendedBucket = true
                    };
                }
            }

            var orderedItems = new List<FeedItemProjection>(followingItems.Count + recommendedItems.Count);
            var seenCatchIds = new HashSet<Guid>();
            foreach (var item in followingItems)
            {
                if (seenCatchIds.Add(item.CatchId))
                {
                    orderedItems.Add(item);
                }
            }

            foreach (var item in recommendedItems)
            {
                if (seenCatchIds.Add(item.CatchId))
                {
                    orderedItems.Add(item);
                }
            }

            var feedItems = await MapToFeedItemsAsync(orderedItems, db, cancellationToken).ConfigureAwait(false);
            var nextContinuationToken = nextToken is null ? null : EncodeContinuationToken(nextToken);

            return new SocialCommunityFeedPageDto(
                feedItems,
                nextContinuationToken,
                hasMore,
                followingItems.Count,
                recommendedItems.Count);
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

            await using var db = _dbFactory.CreateDbContext();
            await EnsureUserExistsAsync(userId, db, cancellationToken).ConfigureAwait(false);
            await EnsureUserExistsAsync(targetUserId, db, cancellationToken).ConfigureAwait(false);

            var alreadyFollowing = await db.FriendRelations
                .AnyAsync(f => f.UserId == userId && f.FriendId == targetUserId, cancellationToken)
                .ConfigureAwait(false);
            if (alreadyFollowing)
            {
                return false;
            }

            db.FriendRelations.Add(new FriendRelation
            {
                UserId = userId,
                FriendId = targetUserId,
                Since = DateTime.UtcNow
            });

            var follower = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.Username, u.DisplayName })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var followerName = follower?.DisplayName ?? follower?.Username ?? "Someone";

            db.Notifications.Add(new Notification
            {
                UserId = targetUserId,
                Type = "follow",
                Title = $"{followerName} started following you",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                TriggeredByUserId = userId
            });

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

            await using var db = _dbFactory.CreateDbContext();
            var relation = await db.FriendRelations
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FriendId == targetUserId, cancellationToken)
                .ConfigureAwait(false);
            if (relation is null)
            {
                return false;
            }

            db.FriendRelations.Remove(relation);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

            await using var db = _dbFactory.CreateDbContext();
            await EnsureUserExistsAsync(userId, db, cancellationToken).ConfigureAwait(false);
            await EnsureCatchExistsAsync(catchId, db, cancellationToken).ConfigureAwait(false);

            var existingReaction = await db.CatchReactions
                .FirstOrDefaultAsync(r => r.CatchId == catchId && r.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var isReacted = existingReaction is null;
            if (existingReaction is null)
            {
                db.CatchReactions.Add(new CatchReaction
                {
                    CatchId = catchId,
                    UserId = userId,
                    ReactedAt = DateTime.UtcNow
                });

                var catchInfo = await db.CatchRecords
                    .Where(c => c.Id == catchId)
                    .Select(c => new { c.UserId, SpeciesName = c.Species!.CommonName })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (catchInfo != null && catchInfo.UserId != userId)
                {
                    var reactor = await db.Users
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.Username, u.DisplayName })
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var reactorName = reactor?.DisplayName ?? reactor?.Username ?? "Someone";
                    var speciesText = string.IsNullOrWhiteSpace(catchInfo.SpeciesName) ? "catch" : $"{catchInfo.SpeciesName} catch";

                    db.Notifications.Add(new Notification
                    {
                        UserId = catchInfo.UserId,
                        Type = "reaction",
                        Title = $"{reactorName} liked your {speciesText}",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        CatchId = catchId,
                        TriggeredByUserId = userId
                    });
                }
            }
            else
            {
                db.CatchReactions.Remove(existingReaction);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var reactionCount = await db.CatchReactions
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

            await using var db = _dbFactory.CreateDbContext();
            await EnsureCatchExistsAsync(catchId, db, cancellationToken).ConfigureAwait(false);

            var commenter = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new SocialUserLookupDto(u.Id, u.Username, u.DisplayName, u.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

            var comment = new CatchComment
            {
                CatchId = catchId,
                UserId = userId,
                CommentText = sanitizedComment,
                CommentedAt = DateTime.UtcNow
            };

            db.CatchComments.Add(comment);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var catchInfo = await db.CatchRecords
                .Where(c => c.Id == catchId)
                .Select(c => new { c.UserId, SpeciesName = c.Species!.CommonName })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (catchInfo != null && catchInfo.UserId != userId)
            {
                var commenterName = commenter.DisplayName ?? commenter.Username;
                var speciesText = string.IsNullOrWhiteSpace(catchInfo.SpeciesName) ? "catch" : $"{catchInfo.SpeciesName} catch";
                var preview = sanitizedComment.Length > 100 ? string.Concat(sanitizedComment.AsSpan(0, 97), "…") : sanitizedComment;

                db.Notifications.Add(new Notification
                {
                    UserId = catchInfo.UserId,
                    Type = "comment",
                    Title = $"{commenterName} commented on your {speciesText}",
                    Body = preview,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    CatchId = catchId,
                    TriggeredByUserId = userId
                });

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

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

            await using var db = _dbFactory.CreateDbContext();
            var comment = await db.CatchComments
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
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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

            await using var db = _dbFactory.CreateDbContext();
            var comment = await db.CatchComments
                .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Comment '{commentId}' was not found.");

            if (comment.UserId != requestingUserId)
            {
                throw new UnauthorizedAccessException("You can only delete your own comments.");
            }

            db.CatchComments.Remove(comment);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<SocialProfileSummaryDto> BuildProfileSummaryAsync(User user, Guid? viewerUserId, HookedDbContext db, CancellationToken cancellationToken)
        {
            var catchCount = await db.CatchRecords.CountAsync(c => c.UserId == user.Id, cancellationToken).ConfigureAwait(false);
            var followerCount = await db.FriendRelations.CountAsync(f => f.FriendId == user.Id, cancellationToken).ConfigureAwait(false);
            var followingCount = await db.FriendRelations.CountAsync(f => f.UserId == user.Id, cancellationToken).ConfigureAwait(false);

            var isFollowing = viewerUserId.HasValue && await db.FriendRelations
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
            HookedDbContext db,
            CancellationToken cancellationToken)
        {
            if (viewerUserId == Guid.Empty)
            {
                throw new ArgumentException("Viewer user ID is required.", nameof(viewerUserId));
            }

            var normalizedLimit = Math.Clamp(limit, 1, 100);

            var catches = await BuildFeedProjectionQuery(
                    db.CatchRecords.AsNoTracking()
                        .Where(filter)
                        .OrderByDescending(c => c.CaughtAt)
                        .ThenByDescending(c => c.Id),
                    viewerUserId)
                .Take(normalizedLimit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return await MapToFeedItemsAsync(catches, db, cancellationToken).ConfigureAwait(false);
        }

        private static IQueryable<CatchRecord> BuildRecommendedFeedBaseQuery(
            HookedDbContext db,
            IReadOnlyCollection<Guid> followedUserIds,
            Guid viewerUserId,
            DateTime snapshotUtc)
        {
            var query = db.CatchRecords.AsNoTracking()
                .Where(c => c.UserId != viewerUserId && c.CaughtAt <= snapshotUtc);

            if (followedUserIds.Count > 0)
            {
                query = query.Where(c => !followedUserIds.Contains(c.UserId));
            }

            return query
                .OrderByDescending(c => c.CaughtAt)
                .ThenByDescending(c => c.Id);
        }

        private static IQueryable<FeedItemProjection> BuildFeedProjectionQuery(IQueryable<CatchRecord> query, Guid viewerUserId)
        {
            return query.Select(c => new FeedItemProjection(
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
                c.UserId == viewerUserId && c.IsFavorite));
        }

        private static CommunityFeedContinuationToken ParseContinuationToken(string? continuationToken)
        {
            if (string.IsNullOrWhiteSpace(continuationToken))
            {
                return new CommunityFeedContinuationToken(FeedPageTokenVersion, DateTime.UtcNow, 0, 0, false);
            }

            try
            {
                var payloadBytes = DecodeBase64Url(continuationToken);
                var payload = JsonSerializer.Deserialize<CommunityFeedContinuationToken>(payloadBytes);

                if (payload is null ||
                    payload.Version != FeedPageTokenVersion ||
                    payload.FollowingOffset < 0 ||
                    payload.RecommendedOffset < 0)
                {
                    throw new ArgumentException("Invalid continuation token.", nameof(continuationToken));
                }

                return payload;
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid continuation token.", nameof(continuationToken), ex);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid continuation token.", nameof(continuationToken), ex);
            }
        }

        private static string EncodeContinuationToken(CommunityFeedContinuationToken token)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(token);
            return EncodeBase64Url(payload);
        }

        private static byte[] DecodeBase64Url(string value)
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            var padding = base64.Length % 4;
            if (padding > 0)
            {
                base64 = base64.PadRight(base64.Length + (4 - padding), '=');
            }

            return Convert.FromBase64String(base64);
        }

        private static string EncodeBase64Url(byte[] value)
        {
            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static async Task<IReadOnlyList<SocialCatchFeedItemDto>> MapToFeedItemsAsync(
            IReadOnlyList<FeedItemProjection> catches,
            HookedDbContext db,
            CancellationToken cancellationToken)
        {
            if (catches.Count == 0)
            {
                return Array.Empty<SocialCatchFeedItemDto>();
            }

            var catchIds = catches.Select(c => c.CatchId).ToList();
            var recentComments = await db.CatchComments.AsNoTracking()
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

        private async Task EnsureUserExistsAsync(Guid userId, HookedDbContext db, CancellationToken cancellationToken)
        {
            var exists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                throw new KeyNotFoundException($"User '{userId}' was not found.");
            }
        }

        private async Task EnsureCatchExistsAsync(Guid catchId, HookedDbContext db, CancellationToken cancellationToken)
        {
            var exists = await db.CatchRecords.AnyAsync(c => c.Id == catchId, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                throw new KeyNotFoundException($"Catch '{catchId}' was not found.");
            }
        }

        private sealed record CommunityFeedContinuationToken(
            int Version,
            DateTime SnapshotUtc,
            int FollowingOffset,
            int RecommendedOffset,
            bool InRecommendedBucket);

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
