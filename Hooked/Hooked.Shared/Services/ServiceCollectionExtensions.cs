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
            services.AddSingleton<IGeminiFishSpeciesService>(_ =>
                new GeminiFishSpeciesService(configuration["Gemini:ApiKey"]));

            // Additional services (leaderboards, stats, AR scanner integration) can be added later
            return services;
        }
    }
}