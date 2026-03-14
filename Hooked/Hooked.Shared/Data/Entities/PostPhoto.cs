using System;

namespace Hooked.Shared.Data
{
    public sealed class PostPhoto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PostId { get; set; }
        public string PhotoPath { get; set; } = string.Empty;

        public Post? Post { get; set; }
    }
}
