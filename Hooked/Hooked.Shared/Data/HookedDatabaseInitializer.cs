namespace Hooked.Shared.Data
{
    internal sealed class HookedDatabaseInitializer : IHookedDatabaseInitializer
    {
        private readonly HookedDbContext _dbContext;

        public HookedDatabaseInitializer(HookedDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            _dbContext = dbContext;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
    }
}
