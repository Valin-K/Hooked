using System;

namespace Hooked.Shared.Data
{
    public sealed class XpEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventKey { get; set; } = null!;
        public Guid UserId { get; set; }
        public int SkillId { get; set; }
        public int XpDelta { get; set; }
        public int PreviousLevel { get; set; }
        public int NewLevel { get; set; }
        public int PreviousXp { get; set; }
        public int NewXp { get; set; }
        public int LevelsGained { get; set; }
        public string? Reason { get; set; }
        public string? Metadata { get; set; }
        public Guid? CatchRecordId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public Skill? Skill { get; set; }
        public CatchRecord? CatchRecord { get; set; }
    }
}
