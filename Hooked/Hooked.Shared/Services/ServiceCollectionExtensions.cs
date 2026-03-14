using System;
using Hooked.Shared.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hooked.Shared.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHookedServices(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IFishService, FishService>();
            services.AddScoped<ICatchService, CatchService>();
            services.AddScoped<ISocialService, SocialService>();
            services.AddScoped<ILeaderboardService, LeaderboardService>();
            services.AddScoped<IMapService, MapService>();

            services.AddSingleton<IGeminiFishSpeciesService>(_ =>
                new GeminiFishSpeciesService(configuration["Gemini:ApiKey"]));

            services.AddSingleton<ILeonardoFishImageService>(sp =>
                new LeonardoFishImageService(
                    configuration["LeonardoAI:ApiKey"],
                    configuration["ReferenceImageId"],
                    sp.GetRequiredService<IGeminiFishSpeciesService>()));

            return services;
        }
    }
}