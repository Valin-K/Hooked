using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hooked.Shared.Data;

namespace Hooked.Shared.Services
{
    public sealed class FishService : IFishService
    {
        private readonly HookedDbContext _db;

        public FishService(HookedDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<int> AddSpeciesAsync(FishSpecies species, CancellationToken cancellationToken = default)
        {
            if (species is null) throw new ArgumentNullException(nameof(species));
            if (string.IsNullOrWhiteSpace(species.CommonName)) throw new ArgumentException("Common name is required", nameof(species));

            _db.FishSpecies.Add(species);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return species.Id;
        }

        public async Task<FishSpecies?> GetSpeciesByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _db.FishSpecies.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<FishSpecies>> SearchSpeciesAsync(string? query = null, CancellationToken cancellationToken = default)
        {
            var q = _db.FishSpecies.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query))
            {
                q = q.Where(s => s.CommonName.Contains(query) || (s.ScientificName != null && s.ScientificName.Contains(query)));
            }

            return await q.OrderBy(s => s.CommonName).Take(100).ToListAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
