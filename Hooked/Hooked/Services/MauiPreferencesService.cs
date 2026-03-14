using Hooked.Shared.Services;

namespace Hooked.Services
{
    public sealed class MauiPreferencesService : IPreferencesService
    {
        public void Set(string key, string value)
        {
            Microsoft.Maui.Storage.Preferences.Set(key, value);
        }

        public string Get(string key, string defaultValue)
        {
            return Microsoft.Maui.Storage.Preferences.Get(key, defaultValue);
        }

        public string GetDeviceModel()
        {
            return Microsoft.Maui.Devices.DeviceInfo.Model;
        }
    }
}
