using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hooked.Shared.Data
{
    public static class HookedDatabaseServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the shared SQLite database services.
        /// </summary>
        public static IServiceCollection AddHookedDatabase(this IServiceCollection services, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path cannot be null, empty, or whitespace.", nameof(databasePath));
            }

            services.AddSingleton(new HookedDatabaseOptions(databasePath));
            services.AddDbContext<HookedDbContext>((serviceProvider, options) =>
            {
                var dbOptions = serviceProvider.GetRequiredService<HookedDatabaseOptions>();
                options.UseSqlite($"Data Source={dbOptions.DatabasePath}");
            });
            services.AddScoped<IHookedDatabaseInitializer, HookedDatabaseInitializer>();

            return services;
        }

        /// <summary>
        /// Recreates the shared SQLite database for the current app run.
        /// </summary>
        public static async Task InitializeHookedDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            using var scope = serviceProvider.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IHookedDatabaseInitializer>();
            await initializer.InitializeAsync(cancellationToken);
        }
    }
}
