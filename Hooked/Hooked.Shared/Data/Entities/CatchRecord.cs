using System;

namespace Hooked.Shared.Data
{
    public sealed class CatchRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public int SpeciesId { get; set; }
        public DateTime CaughtAt { get; set; } = DateTime.UtcNow;
        public double? LengthMeters { get; set; }
        public double? WeightKg { get; set; }
        public string? PhotoPath { get; set; }
        public string? LocationJson { get; set; } // GeoJSON or simple lat/lon stored as JSON

        public User? User { get; set; }
        public FishSpecies? Species { get; set; }
    }
}