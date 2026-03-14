using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public interface IUserService
    {
        Task<Guid> CreateUserAsync(string username, string? displayName = null, string? email = null, CancellationToken cancellationToken = default);
        Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task UpdateProfileAsync(Guid userId, string? displayName, string? email, CancellationToken cancellationToken = default);
    }

    public sealed record UserDto(Guid Id, string Username, string? DisplayName, string? Email, DateTime CreatedAt);
}
