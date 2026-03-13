namespace Hooked.Shared.Data
{
    public interface IHookedDatabaseInitializer
    {
        /// <summary>
        /// Recreates the database from scratch for the current app run.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
