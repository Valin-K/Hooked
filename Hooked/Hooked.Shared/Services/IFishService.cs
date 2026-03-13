using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public interface IFishService
    {
        Task<int> AddSpeciesAsync(FishSpecies species, CancellationToken cancellationToken = default);
        Task<FishSpecies?> GetSpeciesByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<FishSpecies>> SearchSpeciesAsync(string? query = null, CancellationToken cancellationToken = default);
    }
}
