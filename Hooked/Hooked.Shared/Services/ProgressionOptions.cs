namespace Hooked.Shared.Services
{
    public sealed class ProgressionOptions
    {
        public const string SectionName = "Progression";

        public int BaseXpPerLevel { get; init; } = 100;
        public double LevelGrowthFactor { get; init; } = 1.15;
        public int MaxLevel { get; init; } = 50;
    }
}
