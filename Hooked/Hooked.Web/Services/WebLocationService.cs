using System.Threading.Tasks;
using Hooked.Shared.Services;

namespace Hooked.Web.Services
{
    public class WebLocationService : ILocationService
    {
        public Task<LocationDto?> GetCurrentLocationAsync()
        {
            // For now, return a task that completes with null as a fallback
            return Task.FromResult<LocationDto?>(null);
        }
    }
}
