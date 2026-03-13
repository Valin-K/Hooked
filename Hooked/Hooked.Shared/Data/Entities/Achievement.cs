using System;

namespace Hooked.Shared.Data
{
    public sealed class Achievement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Key { get; set; } = null!; // unique key for achievement
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}