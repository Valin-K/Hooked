using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public interface IInsightsService
    {
        /// <summary>Fetches current weather + fishing conditions for the given coordinates.</summary>
        Task<FishingConditionsDto> GetConditionsAsync(double lat, double lng, CancellationToken cancellationToken = default);

        /// <summary>Sends a chat message to the Tide AI assistant and returns the reply text (may contain Markdown).</summary>
        Task<string> AskAssistantAsync(string question, double? lat, double? lng, CancellationToken cancellationToken = default);
        Task<string> GeocodeLocationAsync(string query, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FishingSpotDto>> GetNearbyFishingSpotsAsync(double lat, double lng, CancellationToken cancellationToken = default);
        Task<string> GetLocalRegulationsAsync(double lat, double lng, CancellationToken cancellationToken = default);
    }

    public sealed record FishingConditionsDto(
        string LocationLabel,
        double Lat,
        double Lng,
        // Weather
        double TemperatureC,
        double FeelsLikeC,
        double WindSpeedKmh,
        double WindDirectionDeg,
        string WindDirectionLabel,
        double WaveHeightM,
        double WavePeriodS,
        int WeatherCode,
        string WeatherDescription,
        // Derived fishing quality
        FishingQuality OverallQuality,
        string QualityReason,
        // Tide
        string TidePhase,
        string TideLabel,
        // Moon
        string MoonPhase,
        string MoonLabel,
        // Tips
        string BestTimeWindow,
        string StrategyTip,
        string TechniqueTip,
        string RegulationReminder
    );

    public sealed record FishingSpotDto(
        string Name,
        string Type,
        string Distance,
        string TargetSpecies,
        string Tip
    );

    public enum FishingQuality { Excellent, Good, Fair, Poor }
}
