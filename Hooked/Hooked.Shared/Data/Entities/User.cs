using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public sealed class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CatchRecord> Catches { get; set; } = new List<CatchRecord>();
        public ICollection<Sighting> Sightings { get; set; } = new List<Sighting>();
        public ICollection<FriendRelation> Friends { get; set; } = new List<FriendRelation>();
        public ICollection<CatchReaction> CatchReactions { get; set; } = new List<CatchReaction>();
        public ICollection<CatchComment> CatchComments { get; set; } = new List<CatchComment>();
        public ICollection<FishDexEntry> FishDexEntries { get; set; } = new List<FishDexEntry>();
    }
}
