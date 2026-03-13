using Hooked.Shared.Services.Camera;

namespace Hooked.Web.Services
{
    public sealed class PhotoCaptureService : IPhotoCaptureService
    {
        /// <summary>
        /// Camera capture is not implemented for the web host.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>Always returns <see langword="null"/>.</returns>
        public Task<CapturedPhoto?> CapturePhotoAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CapturedPhoto?>(null);
        }
    }
}
