using Hooked.Services;
using Hooked.Shared.Data;
using Hooked.Shared.Services;
using Hooked.Shared.Services.Camera;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hooked
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            AddBundledConfiguration(builder);

            // Add device-specific services used by the Hooked.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<IPhotoCaptureService, PhotoCaptureService>();
            builder.Services.AddSingleton<ILocationService, MauiLocationService>();

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "hooked.db");
            builder.Services.AddHookedDatabase(databasePath);
            builder.Services.AddHookedServices(builder.Configuration);

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();
            app.Services.InitializeHookedDatabaseAsync().GetAwaiter().GetResult();

            return app;
        }

        private static void AddBundledConfiguration(MauiAppBuilder builder)
        {
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
                builder.Configuration.AddJsonStream(stream);
            }
            catch (FileNotFoundException)
            {
            }
        }
    }
}
