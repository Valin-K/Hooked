using Hooked.Shared.Data;
using Hooked.Shared.Services;
using Hooked.Shared.Services.Camera;
using Hooked.Web.Components;
using Hooked.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the Hooked.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<IPhotoCaptureService, PhotoCaptureService>();

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "hooked.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? builder.Environment.ContentRootPath);
builder.Services.AddHookedDatabase(databasePath);

// Register domain services
builder.Services.AddHookedServices(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeHookedDatabaseAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Hooked.Shared._Imports).Assembly);

app.Run();
