using Microsoft.AspNetCore.Builder;
using Hooked.Shared.Services;
using Hooked.Shared.Services.Search;

namespace Hooked.Web.Api
{
    public static class HookedApiExtensions
    {
        public static WebApplication MapHookedApi(this WebApplication app)
        {
            var group = app.MapGroup("/api");

            group.MapGet("/species", async (IFishService fishService) => Results.Ok(await fishService.SearchSpeciesAsync(null))).WithName("GetSpecies");

            group.MapPost("/users", async (IUserService userService, CreateUserRequest req) =>
            {
                var id = await userService.CreateUserAsync(req.Username, req.DisplayName, req.Email);
                return Results.Created($"/api/users/{id}", new { id });
            });

            group.MapGet("/catches/recent", async (ICatchService catchService) => Results.Ok(await catchService.GetRecentCatchesAsync())).WithName("GetRecentCatches");
            group.MapPost("/catches/favorite", async (ICatchService catchService, SetCatchFavoriteRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var changed = await catchService.SetCatchFavoriteAsync(request.CatchId, request.UserId, request.IsFavorite, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(new SetCatchFavoriteResponse(request.CatchId, request.IsFavorite, changed));
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new ErrorResponse(ex.Message));
                }
                catch (UnauthorizedAccessException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("SetCatchFavorite");

            var quests = group.MapGroup("/quests");

            quests.MapGet("/{userId:guid}", async (IFishingQuestService fishingQuestService, Guid userId, DateTime? asOfUtc, CancellationToken cancellationToken) =>
            {
                try
                {
                    var activeQuests = await fishingQuestService.GetActiveQuestsAsync(userId, asOfUtc, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(new FishingQuestListResponse(activeQuests));
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new ErrorResponse(ex.Message));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("GetActiveFishingQuests");

            quests.MapPost("/claim", async (IFishingQuestService fishingQuestService, ClaimQuestRewardRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var claimResult = await fishingQuestService.ClaimRewardAsync(request.UserId, request.UserQuestProgressId, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(new ClaimQuestRewardResponse(claimResult));
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new ErrorResponse(ex.Message));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("ClaimFishingQuestReward");

            // Admin: force reindex all catches into Elasticsearch
            group.MapPost("/search/reindex", async (
                IElasticSearchService? elasticSearchService,
                Hooked.Shared.Data.HookedDbContext db,
                CancellationToken cancellationToken) =>
            {
                if (elasticSearchService is null)
                    return Results.Problem("Elasticsearch is not configured.", statusCode: 503);

                var catches = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(db.CatchRecords, cancellationToken).ConfigureAwait(false);
                await elasticSearchService.BulkReindexAsync(catches, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new { reindexed = catches.Count });
            }).WithName("ReindexCatches");


            group.MapGet("/search/catches", async (
                IElasticSearchService? elasticSearchService,
                string? q,
                double? lat,
                double? lon,
                double? radiusKm,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                if (elasticSearchService is null)
                    return Results.Problem("Elasticsearch is not configured.", statusCode: 503);

                var results = await elasticSearchService.SearchCatchesAsync(
                    q, lat, lon, radiusKm, limit ?? 25, cancellationToken).ConfigureAwait(false);
                return Results.Ok(results);
            }).WithName("SearchCatches");

            var social = group.MapGroup("/social");

            social.MapGet("/current-user", async (ISocialService socialService, string? preferredUsername, CancellationToken cancellationToken) =>
            {
                var user = await socialService.ResolveCurrentUserAsync(preferredUsername, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new ResolveCurrentUserResponse(user));
            }).WithName("ResolveCurrentUser");

            social.MapGet("/users", async (ISocialService socialService, string? query, int? limit, CancellationToken cancellationToken) =>
            {
                var users = await socialService.LookupUsersAsync(query, limit ?? 20, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new LookupUsersResponse(users));
            }).WithName("LookupUsers");

            social.MapGet("/profile/by-id/{userId:guid}", async (ISocialService socialService, Guid userId, Guid? viewerUserId, CancellationToken cancellationToken) =>
            {
                var profile = await socialService.GetProfileSummaryByIdAsync(userId, viewerUserId, cancellationToken).ConfigureAwait(false);
                return profile is null ? Results.NotFound() : Results.Ok(new ProfileSummaryResponse(profile));
            }).WithName("GetProfileById");

            social.MapGet("/profile/{username}", async (ISocialService socialService, string username, Guid? viewerUserId, CancellationToken cancellationToken) =>
            {
                var profile = await socialService.GetProfileSummaryByUsernameAsync(username, viewerUserId, cancellationToken).ConfigureAwait(false);
                return profile is null ? Results.NotFound() : Results.Ok(new ProfileSummaryResponse(profile));
            }).WithName("GetProfileByUsername");

            social.MapGet("/feed/user/{userId:guid}", async (ISocialService socialService, Guid userId, Guid viewerUserId, int? limit, CancellationToken cancellationToken) =>
            {
                var items = await socialService.GetUserFeedAsync(userId, viewerUserId, limit ?? 25, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new FeedResponse(items));
            }).WithName("GetUserFeed");

            social.MapGet("/feed/community", async (ISocialService socialService, Guid viewerUserId, int? limit, CancellationToken cancellationToken) =>
            {
                var items = await socialService.GetCommunityFeedAsync(viewerUserId, limit ?? 25, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new FeedResponse(items));
            }).WithName("GetCommunityFeed");

            social.MapGet("/feed/community/page", async (ISocialService socialService, Guid viewerUserId, string? continuationToken, int? limit, CancellationToken cancellationToken) =>
            {
                try
                {
                    var page = await socialService.GetCommunityFeedPageAsync(viewerUserId, continuationToken, limit ?? 25, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(new CommunityFeedPageResponse(page));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("GetCommunityFeedPage");

            social.MapPost("/follow", async (ISocialService socialService, FollowRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var followed = await socialService.FollowAsync(request.UserId, request.TargetUserId, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(new FollowResponse(followed));
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new ErrorResponse(ex.Message));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("FollowUser");

            social.MapPost("/unfollow", async (ISocialService socialService, FollowRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var unfollowed = await socialService.UnfollowAsync(request.UserId, request.TargetUserId, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(new FollowResponse(unfollowed));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("UnfollowUser");

            social.MapPost("/reactions/toggle", async (ISocialService socialService, ToggleReactionRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await socialService.ToggleReactionAsync(request.CatchId, request.UserId, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(new ToggleReactionResponse(result));
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new ErrorResponse(ex.Message));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("ToggleCatchReaction");

            social.MapPost("/comments", async (ISocialService socialService, AddCommentRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var comment = await socialService.AddCommentAsync(request.CatchId, request.UserId, request.CommentText, cancellationToken).ConfigureAwait(false);
                    return Results.Created($"/api/social/comments/{comment.Id}", new AddCommentResponse(comment));
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new ErrorResponse(ex.Message));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErrorResponse(ex.Message));
                }
            }).WithName("AddCatchComment");

            return app;
        }

        public sealed record CreateUserRequest(string Username, string? DisplayName, string? Email);
        public sealed record ResolveCurrentUserResponse(SocialUserLookupDto User);
        public sealed record LookupUsersResponse(IReadOnlyList<SocialUserLookupDto> Users);
        public sealed record ProfileSummaryResponse(SocialProfileSummaryDto Profile);
        public sealed record FeedResponse(IReadOnlyList<SocialCatchFeedItemDto> Items);
        public sealed record CommunityFeedPageResponse(SocialCommunityFeedPageDto Feed);
        public sealed record SetCatchFavoriteRequest(Guid CatchId, Guid UserId, bool IsFavorite);
        public sealed record SetCatchFavoriteResponse(Guid CatchId, bool IsFavorite, bool Changed);
        public sealed record FishingQuestListResponse(IReadOnlyList<FishingQuestProgressDto> Quests);
        public sealed record ClaimQuestRewardRequest(Guid UserId, Guid UserQuestProgressId);
        public sealed record ClaimQuestRewardResponse(FishingQuestClaimResultDto Result);
        public sealed record FollowRequest(Guid UserId, Guid TargetUserId);
        public sealed record FollowResponse(bool Success);
        public sealed record ToggleReactionRequest(Guid CatchId, Guid UserId);
        public sealed record ToggleReactionResponse(SocialReactionToggleDto Reaction);
        public sealed record AddCommentRequest(Guid CatchId, Guid UserId, string CommentText);
        public sealed record AddCommentResponse(SocialCommentDto Comment);
        public sealed record ErrorResponse(string Message);
    }
}
