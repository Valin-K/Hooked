using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public interface ILeonardoFishImageService
    {
        /// <summary>
        /// Generates a fish illustration URL for a species using the configured Leonardo reference image.
        /// </summary>
        Task<string> GenerateFishImageUrlAsync(
            string speciesName,
            Func<string, Task>? onLog = null,
            CancellationToken cancellationToken = default);
    }
}
