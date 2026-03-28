using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public interface IGeminiFishSpeciesService
    {
        Task<string> IdentifyFishSpeciesAsync(byte[] imageBytes, string mimeType = "image/jpeg", CancellationToken cancellationToken = default);

        Task<string> DescribeFishSpeciesAsync(string speciesName, CancellationToken cancellationToken = default);

        Task<string> GetEnvironmentalImpactAsync(string speciesName, double? lengthMeters, string? locationJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Detects the bounding box of a specific object in an image.
        /// </summary>
        /// <param name="imageBytes">The image data.</param>
        /// <param name="mimeType">The MIME type of the image.</param>
        /// <param name="objectHint">
        /// A short description of the object to locate, e.g. "fish" or "ruler or straight reference object".
        /// Defaults to "fish".
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task<FishBoundingBoxDto?> DetectObjectBoundingBoxAsync(byte[] imageBytes, string mimeType = "image/jpeg", string objectHint = "fish", CancellationToken cancellationToken = default);
    }

    public sealed record FishBoundingBoxDto(
        int Y0,
        int X0,
        int Y1,
        int X1,
        int ImageWidth,
        int ImageHeight);
}
