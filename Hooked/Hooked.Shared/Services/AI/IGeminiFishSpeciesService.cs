using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public interface IGeminiFishSpeciesService
    {
        Task<string> IdentifyFishSpeciesAsync(byte[] imageBytes, string mimeType = "image/jpeg", CancellationToken cancellationToken = default);

        Task<string> DescribeFishSpeciesAsync(string speciesName, CancellationToken cancellationToken = default);

        Task<string> GetEnvironmentalImpactAsync(string speciesName, double? lengthMeters, string? locationJson, CancellationToken cancellationToken = default);
    }
}
