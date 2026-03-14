using System;
using System.Collections.Generic;

namespace Hooked.Shared.Data
{
    public enum SkillCategory
    {
        FishingMethod = 0,
        SpeciesKnowledge = 1,
        Community = 2,
        Exploration = 3,
        SpeciesExpertise = SpeciesKnowledge,
        Region = Exploration,
        General = 4
    }

    public sealed class Skill
    {
        public int Id { get; set; }
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public SkillCategory Category { get; set; } = SkillCategory.General;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
        public ICollection<XpEvent> XpEvents { get; set; } = new List<XpEvent>();
    }
}
