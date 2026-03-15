using System;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public sealed class ReferenceMeasurementService : IReferenceMeasurementService
    {
        private readonly ICameraCalibrationService _calibrationService;

        public ReferenceMeasurementService(ICameraCalibrationService calibrationService)
        {
            _calibrationService = calibrationService ?? throw new ArgumentNullException(nameof(calibrationService));
        }
        /// <summary>
        /// Estimates fish length from its bounding box using a reference object of known size placed next to the fish.
        /// <para>
        /// Limitation: we only have the fish bounding box, not the reference object's bounding box, so the
        /// pixel-to-metre scale is derived from the assumption that the reference object fills roughly the same
        /// depth plane as the fish. For a proper scale you would need a second bounding box for the reference;
        /// see <see cref="CalculateFishLengthFromCameraAsync"/> for the more reliable sensor-based path.
        /// </para>
        /// </summary>
        public double CalculateFishLengthFromBoundingBox(FishBoundingBoxDto boundingBox, double referenceObjectSizeMeters)
        {
            ArgumentNullException.ThrowIfNull(boundingBox);

            if (referenceObjectSizeMeters <= 0)
            {
                throw new ArgumentException("Reference object size must be greater than zero.", nameof(referenceObjectSizeMeters));
            }

            // Use the longer axis of the bounding box — fish are elongated horizontally,
            // so Max(width, height) is a much better proxy for length than the diagonal.
            var fishWidthNormalized  = (double)(boundingBox.X1 - boundingBox.X0);
            var fishHeightNormalized = (double)(boundingBox.Y1 - boundingBox.Y0);

            var fishLengthNormalized = Math.Max(fishWidthNormalized, fishHeightNormalized);

            // Denormalise from the 0-1000 Gemini coordinate space to pixels
            var fishLengthPixels = (fishLengthNormalized / 1000.0) * Math.Max(boundingBox.ImageWidth, boundingBox.ImageHeight);

            // We don't have the reference object's bounding box, so we estimate its pixel size by
            // assuming it is centred in the frame and occupies a proportional share of the image.
            // Better accuracy requires a dedicated reference bounding box from a second detection call.
            var imageDiagonalPixels = Math.Sqrt(
                Math.Pow(boundingBox.ImageWidth, 2) + Math.Pow(boundingBox.ImageHeight, 2));

            // referenceObjectSizeMeters maps to this many pixels (rough scene-scale assumption)
            var referencePixels = imageDiagonalPixels * (referenceObjectSizeMeters / 2.0);

            var pixelsPerMeter = referencePixels / referenceObjectSizeMeters; // simplifies to imageDiagonal / 2

            var fishLengthMeters = fishLengthPixels / pixelsPerMeter;

            return Math.Round(fishLengthMeters, 2);
        }

        /// <summary>
        /// Estimates fish length based on manual calibration.
        /// </summary>
        public double CalculateFishLengthFromManualCalibration(FishBoundingBoxDto boundingBox, double pixelsPerMeter)
        {
            ArgumentNullException.ThrowIfNull(boundingBox);

            if (pixelsPerMeter <= 0)
            {
                throw new ArgumentException("Pixels per meter must be greater than zero.", nameof(pixelsPerMeter));
            }

            // Calculate fish diagonal length in normalized coordinates (0-1000 scale)
            var fishDiagonalNormalized = Math.Sqrt(
                Math.Pow(boundingBox.Y1 - boundingBox.Y0, 2) +
                Math.Pow(boundingBox.X1 - boundingBox.X0, 2));

            // Convert normalized coordinates to pixels
            var fishDiagonalPixels = (fishDiagonalNormalized / 1000.0) *
                Math.Sqrt(Math.Pow(boundingBox.ImageWidth, 2) + Math.Pow(boundingBox.ImageHeight, 2));

            // Convert to meters
            var fishLengthMeters = fishDiagonalPixels / pixelsPerMeter;

            return Math.Round(fishLengthMeters, 2);
        }

        /// <summary>
        /// Calculates fish length using calibrated camera focal length and assumed distance from camera to fish.
        /// Formula: real_size = (pixel_size * distance) / focal_length
        /// </summary>
        public async Task<double> CalculateFishLengthFromCameraAsync(FishBoundingBoxDto boundingBox, double assumedDistanceMeters)
        {
            ArgumentNullException.ThrowIfNull(boundingBox);

            if (assumedDistanceMeters <= 0)
            {
                throw new ArgumentException("Assumed distance must be greater than zero.", nameof(assumedDistanceMeters));
            }

            // Get calibration data
            var calibration = await _calibrationService.GetCalibrationAsync();
            if (calibration is null)
            {
                throw new InvalidOperationException("Camera is not calibrated. Please calibrate the camera first.");
            }

            // Calculate fish width in pixels (denormalize from 0-1000 scale)
            var fishWidthNormalized = boundingBox.X1 - boundingBox.X0;
            var fishWidthPixels = (fishWidthNormalized / 1000.0) * boundingBox.ImageWidth;

            // Calculate fish height in pixels
            var fishHeightNormalized = boundingBox.Y1 - boundingBox.Y0;
            var fishHeightPixels = (fishHeightNormalized / 1000.0) * boundingBox.ImageHeight;

            // Use the larger dimension (width or height) as the fish length
            var fishLengthPixels = Math.Max(fishWidthPixels, fishHeightPixels);

            // Apply triangle similarity formula
            // real_size = (pixel_size * distance) / focal_length
            var fishLengthMeters = (fishLengthPixels * assumedDistanceMeters) / calibration.FocalLengthPixels;

            return Math.Round(fishLengthMeters, 2);
        }
    }
}
