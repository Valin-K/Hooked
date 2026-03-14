using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public interface ILeonardoFishImageService
    {
        Task<string> GenerateFishImageUrlAsync(
            string speciesName,
            Func<string, Task>? onLog = null,
            CancellationToken cancellationToken = default);

        Task<string?> ConvertToTransparentPngDataUrlAsync(
            string imageUrl,
            Func<string, Task>? onLog = null,
            CancellationToken cancellationToken = default);
    }
}
