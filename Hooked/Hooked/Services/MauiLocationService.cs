using System;
using System.Linq;
using System.Threading.Tasks;
using Hooked.Shared.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace Hooked.Services
{
    public class MauiLocationService : ILocationService
    {
        public async Task<LocationDto?> GetCurrentLocationAsync()
        {
            try
            {
                var status = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var currentStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

                    if (currentStatus != PermissionStatus.Granted)
                    {
                        currentStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    }

                    return currentStatus;
                });

                if (status != PermissionStatus.Granted)
                {
                    return null;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                {
                    // Optionally reverse geocode to get a description for Gemini
                    string? description = null;
                    try
                    {
                        var placemarks = await Geocoding.Default.GetPlacemarksAsync(location.Latitude, location.Longitude);
                        var placemark = placemarks?.FirstOrDefault();
                        if (placemark != null)
                        {
                            description = $"{placemark.Locality}, {placemark.AdminArea}, {placemark.CountryName}";
                        }
                    }
                    catch
                    {
                        // Ignore geocoding errors
                    }

                    return new LocationDto(location.Latitude, location.Longitude, description);
                }
            }
            catch (Exception)
            {
                // Unable to get location
            }

            return null;
        }
    }
}
