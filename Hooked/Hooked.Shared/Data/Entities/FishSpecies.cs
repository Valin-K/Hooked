using System.Collections.Generic;
using System;

namespace Hooked.Shared.Data
{
    public sealed class FishSpecies
    {
        public int Id { get; set; }
        public string CommonName { get; set; } = null!;
        public string? ScientificName { get; set; }
        public string? ConservationStatus { get; set; }
        public bool IsInvasive { get; set; }
        public bool IsEndangered { get; set; }

        public ICollection<CatchRecord> Catches { get; set; } = new List<CatchRecord>();
        public ICollection<Sighting> Sightings { get; set; } = new List<Sighting>();
        public ICollection<FishDexEntry> FishDexEntries { get; set; } = new List<FishDexEntry>();
    }
}