using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Diagnostics;

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
            try
            {
                await RequestLocationPermissionOnFirstOpenAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Location permission startup check failed: {ex}");
            }
        }

        private static async Task RequestLocationPermissionOnFirstOpenAsync()
        {
            if (Preferences.Default.Get(LocationPermissionPromptedKey, false))
            {
                return;
            }

            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                Preferences.Default.Set(LocationPermissionPromptedKey, true);
            }
            catch (PermissionException ex)
            {
                Debug.WriteLine($"Location permission metadata is missing or invalid: {ex.Message}");
            }
        }
    }
}
