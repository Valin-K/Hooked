using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

            services.AddPooledDbContextFactory<HookedDbContext>((serviceProvider, options) =>
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

                    var optimizedConnectionString = BuildOptimizedSupabaseConnectionString(connectionString);
                    options.UseNpgsql(optimizedConnectionString, npgsqlOptions =>
                        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null));
                }
                else
                {
                    options.UseSqlite($"Data Source={databasePath}");
                }
            }, poolSize: 128);

            services.AddScoped<HookedDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<HookedDbContext>>().CreateDbContext());

            services.AddScoped<IHookedDatabaseInitializer, HookedDatabaseInitializer>();

            return services;
        }

        private static string BuildOptimizedSupabaseConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null, empty, or whitespace.", nameof(connectionString));
            }

            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Pooling = true,
                MinPoolSize = 5,
                MaxPoolSize = 100,
                Timeout = 15,
                CommandTimeout = 30
            };

            if (builder.KeepAlive <= 0)
            {
                builder.KeepAlive = 30;
            }

            return builder.ConnectionString;
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
