using Hooked.Shared.Services;

namespace Hooked.Web.Services
{
    public sealed class WebPreferencesService : IPreferencesService
    {
        private readonly Dictionary<string, string> _storage = new();

        public void Set(string key, string value)
        {
            _storage[key] = value;
        }

        public string Get(string key, string defaultValue)
        {
            return _storage.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public string GetDeviceModel()
        {
            return "WebBrowser";
        }
    }
}
