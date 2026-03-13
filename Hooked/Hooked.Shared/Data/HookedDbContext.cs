using Microsoft.EntityFrameworkCore;

namespace Hooked.Shared.Data
{
    public sealed class HookedDbContext : DbContext
    {
        public HookedDbContext(DbContextOptions<HookedDbContext> options)
            : base(options)
        {
        }
    }
}
