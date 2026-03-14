using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    /// <summary>
    /// Service for camera calibration and focal length estimation for distance-based measurements.
    /// Uses the triangle similarity method to calculate focal length.
    /// </summary>
    public interface ICameraCalibrationService
    {
        /// <summary>
        /// Stores calibration data for the camera.
        /// </summary>
        Task SaveCalibrationAsync(double focalLengthPixels, double assumedDistanceMeters);

        /// <summary>
        /// Retrieves stored calibration data.
        /// </summary>
        Task<CameraCalibrationData?> GetCalibrationAsync();

        /// <summary>
        /// Calculates focal length in pixels from a calibration image.
        /// Formula: focal_length = (object_width_pixels * known_distance) / real_object_width
        /// </summary>
        double CalculateFocalLength(int objectWidthPixels, double knownDistanceMeters, double realObjectWidthMeters);

        /// <summary>
        /// Checks if the device has been calibrated.
        /// </summary>
        Task<bool> IsCalibrated();
    }

    /// <summary>
    /// Stores camera calibration data.
    /// </summary>
    public sealed record CameraCalibrationData(
        double FocalLengthPixels,
        double CalibrationDistanceMeters,
        string DeviceModel);
}
