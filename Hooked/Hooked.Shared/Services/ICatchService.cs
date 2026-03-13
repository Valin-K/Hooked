using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public interface ICatchService
    {
        Task<Guid> AddCatchAsync(Guid userId, int speciesId, double? lengthMeters = null, double? weightKg = null, string? photoPath = null, string? locationJson = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<CatchRecord>> GetRecentCatchesAsync(int limit = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<CatchRecord>> GetUserCatchesAsync(Guid userId, int limit = 100, CancellationToken cancellationToken = default);
    }
}
