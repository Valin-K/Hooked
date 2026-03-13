using Hooked.Services;
using Hooked.Shared.Data;
using Hooked.Shared.Services;
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

            // Add device-specific services used by the Hooked.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();

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
    }
}
