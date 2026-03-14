using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public sealed class CameraCalibrationService : ICameraCalibrationService
    {
        private const string CalibrationKey = "camera_calibration_data";
        private readonly IPreferencesService _preferencesService;
        private CameraCalibrationData? _cachedCalibration;

        public CameraCalibrationService(IPreferencesService preferencesService)
        {
            _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        }

        public Task SaveCalibrationAsync(double focalLengthPixels, double assumedDistanceMeters)
        {
            var deviceModel = _preferencesService.GetDeviceModel();
            var calibration = new CameraCalibrationData(focalLengthPixels, assumedDistanceMeters, deviceModel);

            var json = JsonSerializer.Serialize(calibration);

            _preferencesService.Set(CalibrationKey, json);

            _cachedCalibration = calibration;

            return Task.CompletedTask;
        }

        public Task<CameraCalibrationData?> GetCalibrationAsync()
        {
            if (_cachedCalibration is not null)
            {
                return Task.FromResult<CameraCalibrationData?>(_cachedCalibration);
            }

            var json = _preferencesService.Get(CalibrationKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
                return Task.FromResult<CameraCalibrationData?>(null);
            }

            try
            {
                _cachedCalibration = JsonSerializer.Deserialize<CameraCalibrationData>(json);
                return Task.FromResult<CameraCalibrationData?>(_cachedCalibration);
            }
            catch
            {
                return Task.FromResult<CameraCalibrationData?>(null);
            }
        }

        public double CalculateFocalLength(int objectWidthPixels, double knownDistanceMeters, double realObjectWidthMeters)
        {
            if (objectWidthPixels <= 0)
            {
                throw new ArgumentException("Object width in pixels must be greater than zero.", nameof(objectWidthPixels));
            }

            if (knownDistanceMeters <= 0)
            {
                throw new ArgumentException("Known distance must be greater than zero.", nameof(knownDistanceMeters));
            }

            if (realObjectWidthMeters <= 0)
            {
                throw new ArgumentException("Real object width must be greater than zero.", nameof(realObjectWidthMeters));
            }

            // Triangle similarity: focal_length = (object_width_pixels * distance) / real_width
            return (objectWidthPixels * knownDistanceMeters) / realObjectWidthMeters;
        }

        public async Task<bool> IsCalibrated()
        {
            var calibration = await GetCalibrationAsync();
            return calibration is not null;
        }
    }
}
