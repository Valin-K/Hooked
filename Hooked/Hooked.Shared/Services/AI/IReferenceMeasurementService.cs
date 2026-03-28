using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public interface IReferenceMeasurementService
    {
        /// <summary>
        /// Calculates the fish length in meters from a bounding box using a reference object size.
        /// </summary>
        /// <param name="boundingBox">The fish bounding box detected by Gemini</param>
        /// <param name="referenceObjectSizeMeters">The known size of the reference object in meters</param>
        /// <returns>The estimated fish length in meters</returns>
        double CalculateFishLengthFromBoundingBox(FishBoundingBoxDto boundingBox, double referenceObjectSizeMeters);

        /// <summary>
        /// Estimates fish length based on manual calibration (user provides pixels-per-meter ratio).
        /// </summary>
        double CalculateFishLengthFromManualCalibration(FishBoundingBoxDto boundingBox, double pixelsPerMeter);

        /// <summary>
        /// Calculates fish length using camera focal length and assumed distance.
        /// Uses triangle similarity: distance = (real_width * focal_length) / pixel_width
        /// </summary>
        Task<double> CalculateFishLengthFromCameraAsync(FishBoundingBoxDto boundingBox, double assumedDistanceMeters);
    }
}
