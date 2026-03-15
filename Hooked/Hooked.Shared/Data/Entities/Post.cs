using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public sealed class Post
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid FishingSessionId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public FishingSession? FishingSession { get; set; }
        public ICollection<PostPhoto> Photos { get; set; } = new List<PostPhoto>();
    }
}
