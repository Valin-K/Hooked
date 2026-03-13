using System;

namespace Hooked.Shared.Data
{
    public sealed class FriendRelation
    {
        public Guid UserId { get; set; }
        public Guid FriendId { get; set; }
        public DateTime Since { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public User? Friend { get; set; }
    }
}