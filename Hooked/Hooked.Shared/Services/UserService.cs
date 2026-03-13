using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public sealed class UserService : IUserService
    {
        private readonly HookedDbContext _db;

        public UserService(HookedDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<Guid> CreateUserAsync(string username, string? displayName = null, string? email = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required", nameof(username));

            var exists = await _db.Users.AnyAsync(u => u.Username == username, cancellationToken).ConfigureAwait(false);
            if (exists) throw new InvalidOperationException("Username already exists");

            var user = new User
            {
                Username = username,
                DisplayName = displayName,
                Email = email
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return user.Id;
        }

        public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
            if (u is null) return null;
            return new UserDto(u.Id, u.Username, u.DisplayName, u.Email, u.CreatedAt);
        }

        public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Username == username, cancellationToken).ConfigureAwait(false);
            if (u is null) return null;
            return new UserDto(u.Id, u.Username, u.DisplayName, u.Email, u.CreatedAt);
        }
    }
}
