using System.Threading.Tasks;

namespace Hooked.Shared.Services
{
    public interface ILocationService
    {
        Task<LocationDto?> GetCurrentLocationAsync();
    }

    public sealed record LocationDto(double Latitude, double Longitude, string? Description = null);
}
