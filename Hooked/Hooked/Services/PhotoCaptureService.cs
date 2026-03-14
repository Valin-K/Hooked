using Hooked.Shared.Services.Camera;

namespace Hooked.Services
{
    public sealed class PhotoCaptureService : IPhotoCaptureService
    {
        /// <summary>
        /// Opens the device camera and returns the captured photo.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel waiting for camera capture.</param>
        /// <returns>The captured photo when successful; otherwise <see langword="null"/>.</returns>
        public async Task<CapturedPhoto?> CapturePhotoAsync(CancellationToken cancellationToken = default)
        {
            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    throw new InvalidOperationException("Camera permission is required to capture photos.");
                }
            }

            FileResult? fileResult;

            try
            {
                fileResult = await MediaPicker.Default
                    .CapturePhotoAsync(new MediaPickerOptions { Title = "Fish photo" })
                    .WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (fileResult is null)
            {
                return null;
            }

            await using var stream = await fileResult.OpenReadAsync().ConfigureAwait(false);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

            var mimeType = string.IsNullOrWhiteSpace(fileResult.ContentType)
                ? "image/jpeg"
                : fileResult.ContentType;

            return new CapturedPhoto(memoryStream.ToArray(), mimeType);
        }
    }
}
