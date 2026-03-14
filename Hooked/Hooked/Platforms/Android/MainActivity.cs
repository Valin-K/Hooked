using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Hooked
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (Window != null)
            {
                AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, true);
            }
        }
    }
}
