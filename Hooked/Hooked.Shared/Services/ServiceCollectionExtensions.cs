using Microsoft.Extensions.DependencyInjection;

namespace Hooked.Shared.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHookedServices(this IServiceCollection services)
        {
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IFishService, FishService>();
            services.AddScoped<ICatchService, CatchService>();

            // Additional services (leaderboards, stats, AR scanner integration) can be added later
            return services;
        }
    }
}