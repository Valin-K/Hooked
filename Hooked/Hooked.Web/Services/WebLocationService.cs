using System;
using System.Threading.Tasks;
using Hooked.Shared.Services;
using Microsoft.JSInterop;

namespace Hooked.Web.Services
{
    public class WebLocationService : ILocationService
    {
        private readonly IJSRuntime _js;

        public WebLocationService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<LocationDto?> GetCurrentLocationAsync()
        {
            try
            {
                var pos = await _js.InvokeAsync<GeoPosition>("hookedGeo.getPosition");
                return new LocationDto(pos.Lat, pos.Lng);
            }
            catch
            {
                return null;
            }
        }

        private sealed class GeoPosition
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
        }
    }
}
