namespace Hooked.Shared.Services
{
    /// <summary>
    /// Platform-agnostic service for storing and retrieving preferences.
    /// </summary>
    public interface IPreferencesService
    {
        void Set(string key, string value);
        string Get(string key, string defaultValue);
        string GetDeviceModel();
    }
}
