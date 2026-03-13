using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.Camera
{
    public interface IPhotoCaptureService
    {
        /// <summary>
        /// Opens the platform camera UI and returns the captured photo.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>The captured photo when successful; otherwise <see langword="null"/>.</returns>
        Task<CapturedPhoto?> CapturePhotoAsync(CancellationToken cancellationToken = default);
    }
}
