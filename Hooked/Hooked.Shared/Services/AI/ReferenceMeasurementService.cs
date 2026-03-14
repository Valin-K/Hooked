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
        /// Calculates the fish length in meters from a bounding box using a reference object size.
        /// The reference object is assumed to have a known diagonal size.
        /// </summary>
        public double CalculateFishLengthFromBoundingBox(FishBoundingBoxDto boundingBox, double referenceObjectSizeMeters)
        {
            ArgumentNullException.ThrowIfNull(boundingBox);

            if (referenceObjectSizeMeters <= 0)
            {
                throw new ArgumentException("Reference object size must be greater than zero.", nameof(referenceObjectSizeMeters));
            }

            // Calculate fish diagonal length in normalized coordinates (0-1000 scale)
            var fishDiagonalNormalized = Math.Sqrt(
                Math.Pow(boundingBox.Y1 - boundingBox.Y0, 2) +
                Math.Pow(boundingBox.X1 - boundingBox.X0, 2));

            // Convert normalized coordinates to pixels
            var fishDiagonalPixels = (fishDiagonalNormalized / 1000.0) *
                Math.Sqrt(Math.Pow(boundingBox.ImageWidth, 2) + Math.Pow(boundingBox.ImageHeight, 2));

            // Use a standard reference: assuming a typical smartphone camera field of view
            // and that the reference object (e.g., a credit card at 8.5cm or a ruler) is placed near the fish
            // For simplicity, we'll assume the reference is roughly 20% of the image diagonal
            var referencePixelEstimate = Math.Sqrt(Math.Pow(boundingBox.ImageWidth, 2) + Math.Pow(boundingBox.ImageHeight, 2)) * 0.2;

            // Calculate pixels per meter
            var pixelsPerMeter = referencePixelEstimate / referenceObjectSizeMeters;

            // Convert fish diagonal pixels to meters
            var fishLengthMeters = fishDiagonalPixels / pixelsPerMeter;

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
