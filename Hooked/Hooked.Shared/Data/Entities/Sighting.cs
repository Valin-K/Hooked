using System;

namespace Hooked.Shared.Data
{
    public sealed class Sighting
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public int SpeciesId { get; set; }
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
        public string? Note { get; set; }
        public string? LocationJson { get; set; }

        public User? User { get; set; }
        public FishSpecies? Species { get; set; }
    }
}