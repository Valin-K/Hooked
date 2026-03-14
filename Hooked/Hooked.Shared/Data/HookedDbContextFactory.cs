using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hooked.Shared.Data
{
    // Used by EF Core tools (dotnet ef migrations add, dotnet ef database update, etc.)
    // Reads connection settings from appsettings.json (found by walking up from CWD)
    // so migrations can be applied to whichever database the app is configured to use.
    // You can also override with environment variable: ConnectionStrings__DefaultConnection
    public sealed class HookedDbContextFactory : IDesignTimeDbContextFactory<HookedDbContext>
    {
        public HookedDbContext CreateDbContext(string[] args)
        {
            var connectionString = ResolveConnectionString();
            var optionsBuilder = new DbContextOptionsBuilder<HookedDbContext>()
                .UseNpgsql(connectionString);
            return new HookedDbContext(optionsBuilder.Options);
        }

        private static string ResolveConnectionString()
        {
            // 1. Environment variable takes priority (useful in CI/CD)
            var envCs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (!string.IsNullOrWhiteSpace(envCs))
                return envCs;

            // 2. Read from appsettings.json in the Web project
            var appSettingsPath = FindAppSettings();
            if (appSettingsPath is not null)
            {
                using var stream = File.OpenRead(appSettingsPath);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                    cs.TryGetProperty("DefaultConnection", out var val))
                {
                    var s = val.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }

            // 3. Fallback for brand-new local dev setup
            return "Host=localhost;Database=hooked_design;Username=postgres;Password=postgres";
        }

        private static string? FindAppSettings()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir is not null)
            {
                // Repo-root layout: Hooked/Hooked.Web/appsettings.json
                var candidate = Path.Combine(dir.FullName, "Hooked", "Hooked.Web", "appsettings.json");
                if (File.Exists(candidate))
                    return candidate;

                // When CWD is already inside Hooked/Hooked.Shared (common with dotnet-ef)
                var sibling = Path.GetFullPath(Path.Combine(dir.FullName, "..", "Hooked.Web", "appsettings.json"));
                if (File.Exists(sibling))
                    return sibling;

                dir = dir.Parent;
            }
            return null;
        }
    }
}
