using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public interface ISocialService
    {
        Task<SocialUserLookupDto> ResolveCurrentUserAsync(string? preferredUsername = null, CancellationToken cancellationToken = default);
        Task<SocialUserLookupDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<SocialUserLookupDto?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SocialUserLookupDto>> LookupUsersAsync(string? query = null, int limit = 20, CancellationToken cancellationToken = default);
        Task<SocialProfileSummaryDto?> GetProfileSummaryByUsernameAsync(string username, Guid? viewerUserId = null, CancellationToken cancellationToken = default);
        Task<SocialProfileSummaryDto?> GetProfileSummaryByIdAsync(Guid userId, Guid? viewerUserId = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SocialCatchFeedItemDto>> GetUserFeedAsync(Guid userId, Guid viewerUserId, int limit = 25, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SocialCatchFeedItemDto>> GetCommunityFeedAsync(Guid viewerUserId, int limit = 25, CancellationToken cancellationToken = default);
        Task<bool> FollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default);
        Task<bool> UnfollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default);
        Task<SocialReactionToggleDto> ToggleReactionAsync(Guid catchId, Guid userId, CancellationToken cancellationToken = default);
        Task<SocialCommentDto> AddCommentAsync(Guid catchId, Guid userId, string commentText, CancellationToken cancellationToken = default);
        Task<SocialCommentDto> EditCommentAsync(Guid commentId, Guid requestingUserId, string newText, CancellationToken cancellationToken = default);
        Task DeleteCommentAsync(Guid commentId, Guid requestingUserId, CancellationToken cancellationToken = default);
    }

    public sealed record SocialUserLookupDto(Guid Id, string Username, string? DisplayName, DateTime CreatedAt);

    public sealed record SocialProfileSummaryDto(
        Guid UserId,
        string Username,
        string? DisplayName,
        DateTime CreatedAt,
        int CatchCount,
        int FollowerCount,
        int FollowingCount,
        bool IsFollowing);

    public sealed record SocialCommentDto(
        Guid Id,
        Guid CatchId,
        Guid UserId,
        string Username,
        string? DisplayName,
        string CommentText,
        DateTime CommentedAt,
        DateTime? EditedAt);

    public sealed record SocialCatchFeedItemDto(
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
        bool IsFavorite,
        IReadOnlyList<SocialCommentDto> RecentComments);

    public sealed record SocialReactionToggleDto(Guid CatchId, Guid UserId, bool IsReacted, int ReactionCount);
}
