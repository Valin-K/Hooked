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

            // Elasticsearch-powered catch search (falls back to 404 when ES is not configured)
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
        public sealed record FollowRequest(Guid UserId, Guid TargetUserId);
        public sealed record FollowResponse(bool Success);
        public sealed record ToggleReactionRequest(Guid CatchId, Guid UserId);
        public sealed record ToggleReactionResponse(SocialReactionToggleDto Reaction);
        public sealed record AddCommentRequest(Guid CatchId, Guid UserId, string CommentText);
        public sealed record AddCommentResponse(SocialCommentDto Comment);
        public sealed record ErrorResponse(string Message);
    }
}
