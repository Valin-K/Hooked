using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public interface ISessionService
    {
        Task<FishingSession> StartSessionAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<FishingSession?> GetActiveSessionAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<FishingSession> EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
        Task<Post> CreatePostFromSessionAsync(Guid sessionId, string title, string? body, string? locationName, IReadOnlyList<string>? selectedPhotos, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Post>> GetPostsAsync(int limit = 20, CancellationToken cancellationToken = default);
    }
}
