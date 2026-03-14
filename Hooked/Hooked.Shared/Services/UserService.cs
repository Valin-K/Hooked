using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public sealed class UserService : IUserService
    {
        private readonly IDbContextFactory<HookedDbContext> _dbFactory;

        public UserService(IDbContextFactory<HookedDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public async Task<Guid> CreateUserAsync(string username, string? displayName = null, string? email = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required", nameof(username));

            await using var db = _dbFactory.CreateDbContext();
            var exists = await db.Users.AnyAsync(u => u.Username == username, cancellationToken).ConfigureAwait(false);
            if (exists) throw new InvalidOperationException("Username already exists");

            var user = new User
            {
                Username = username,
                DisplayName = displayName,
                Email = email
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return user.Id;
        }

        public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
            if (u is null) return null;
            return new UserDto(u.Id, u.Username, u.DisplayName, u.Email, u.CreatedAt);
        }

        public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            await using var db = _dbFactory.CreateDbContext();
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Username == username, cancellationToken).ConfigureAwait(false);
            if (u is null) return null;
            return new UserDto(u.Id, u.Username, u.DisplayName, u.Email, u.CreatedAt);
        }
        public async Task UpdateProfileAsync(Guid userId, string? displayName, string? email, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));

            await using var db = _dbFactory.CreateDbContext();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
            if (user is null) throw new KeyNotFoundException($"User '{userId}' was not found.");

            user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
            user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
