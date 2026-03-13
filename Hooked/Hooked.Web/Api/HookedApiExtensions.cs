using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Hooked.Shared.Services;

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

            return app;
        }

        public sealed record CreateUserRequest(string Username, string? DisplayName, string? Email);
    }
}
