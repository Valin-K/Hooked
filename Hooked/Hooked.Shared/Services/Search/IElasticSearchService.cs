using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services.Search
{
    public interface IElasticSearchService
    {
        /// <summary>Ensures the catches index exists with correct mappings.</summary>
        Task EnsureIndexAsync(CancellationToken cancellationToken = default);

        /// <summary>Indexes a single catch document.</summary>
        Task IndexCatchAsync(CatchRecord catchRecord, FishSpecies species, User user, CancellationToken cancellationToken = default);

        /// <summary>Full-text + optional geo search across all catches.</summary>
        Task<ElasticSearchResultDto> SearchCatchesAsync(
            string? query,
            double? lat = null,
            double? lon = null,
            double? radiusKm = null,
            int limit = 25,
            CancellationToken cancellationToken = default);

        /// <summary>Bulk reindex all catches from the database (used on startup/seed).</summary>
        Task BulkReindexAsync(IEnumerable<CatchRecord> catches, CancellationToken cancellationToken = default);
    }
}
