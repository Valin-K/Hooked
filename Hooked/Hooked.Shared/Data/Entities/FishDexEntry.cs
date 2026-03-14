using System;

namespace Hooked.Shared.Data
{
    public sealed class FishDexEntry
    {
        public Guid UserId { get; set; }
        public int SpeciesId { get; set; }
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
        public bool IsRare { get; set; }
        public int CatchCount { get; set; }
        public double? PersonalBestLengthMeters { get; set; }
        public Guid? PersonalBestCatchId { get; set; }

        public User? User { get; set; }
        public FishSpecies? Species { get; set; }
        public CatchRecord? PersonalBestCatch { get; set; }
    }
}