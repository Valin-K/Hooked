using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hooked.Shared.Data
{
    public static class HookedDatabaseServiceCollectionExtensions
    {
        /// <summary>
        /// Registers database services (SQLite or PostgreSQL via Supabase based on configuration).
        /// </summary>
        public static IServiceCollection AddHookedDatabase(this IServiceCollection services, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path cannot be null, empty, or whitespace.", nameof(databasePath));
            }

            services.AddDbContextFactory<HookedDbContext>((serviceProvider, options) =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var useSupabase = configuration.GetValue<bool>("DatabaseConfiguration:UseSupabase");
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                if (useSupabase)
                {
                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured when UseSupabase is true.");
                    }

                    options.UseNpgsql(connectionString);
                }
                else
                {
                    options.UseSqlite($"Data Source={databasePath}");
                }
            });

            services.AddScoped<HookedDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<HookedDbContext>>().CreateDbContext());

            services.AddScoped<IHookedDatabaseInitializer, HookedDatabaseInitializer>();

            return services;
        }

        /// <summary>
        /// Initializes the database (creates schema and seeds data).
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
