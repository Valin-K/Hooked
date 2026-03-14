using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Hooked
{
    public partial class MainPage : ContentPage
    {
        private const string LocationPermissionPromptedKey = "location_permission_prompted";

        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            Loaded -= OnLoaded;
            await RequestLocationPermissionOnFirstOpenAsync();
        }

        private static async Task RequestLocationPermissionOnFirstOpenAsync()
        {
            if (Preferences.Default.Get(LocationPermissionPromptedKey, false))
            {
                return;
            }

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            Preferences.Default.Set(LocationPermissionPromptedKey, true);
        }
    }
}
