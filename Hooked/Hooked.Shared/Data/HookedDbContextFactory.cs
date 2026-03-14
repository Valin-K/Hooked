using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hooked.Shared.Data
{
    // Used by EF Core tools (dotnet ef migrations add, dotnet ef database update, etc.)
    public sealed class HookedDbContextFactory : IDesignTimeDbContextFactory<HookedDbContext>
    {
        public HookedDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<HookedDbContext>()
                .UseNpgsql("Host=localhost;Database=hooked_design;Username=postgres;Password=postgres")
                .Options;
            return new HookedDbContext(options);
        }
    }
}
