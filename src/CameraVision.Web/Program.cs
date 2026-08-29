using System.Globalization;
using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Auth;
using CameraVision.Core.Entities;
using CameraVision.Core.Health;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure;
using CameraVision.Infrastructure.Alerts;
using CameraVision.Infrastructure.Data;
using CameraVision.Web.Components;
using CameraVision.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

// User-facing formatting (dates, numbers) is PT-BR.
var culture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

// Relative storage paths resolve against the project folder (repo: src/CameraVision.Web → ../../).
var contentRoot = builder.Environment.ContentRootPath;
var storage = new StoragePaths(
    DatabasePath: Path.GetFullPath(Path.Combine(contentRoot,
        builder.Configuration["Storage:DatabasePath"] ?? "../../data/database.db")),
    OutputRoot: Path.GetFullPath(Path.Combine(contentRoot,
        builder.Configuration["Storage:OutputRoot"] ?? "../../output")));

builder.Services.AddSingleton(storage);
builder.Services.AddCameraVisionData(storage.DatabasePath);

// Media (videos/thumbnails) is streamed by the API application (SPEC-11).
var mediaBaseUrl = (builder.Configuration["Api:MediaBaseUrl"] ?? "http://localhost:5220").TrimEnd('/');
builder.Services.AddSingleton(new MediaUrls(mediaBaseUrl));

// Public (tokenized) capture links used in alert e-mails — the host names live in
// appsettings.json (CaptureLinks), the secret must match the API's.
builder.Services.AddSingleton(new CaptureLinkOptions
{
    PublicBaseUrl = builder.Configuration["CaptureLinks:PublicBaseUrl"] ?? "",
    MediaBaseUrl = builder.Configuration["CaptureLinks:MediaBaseUrl"] ?? mediaBaseUrl,
    Secret = builder.Configuration["CaptureLinks:Secret"] ?? "",
});
builder.Services.AddSingleton<CaptureLinkService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddSingleton<CameraHealthMonitor>();
builder.Services.AddSingleton<ICameraHealthService>(sp => sp.GetRequiredService<CameraHealthMonitor>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CameraHealthMonitor>());

builder.Services.AddSingleton<HealthAlertNotifier>();
builder.Services.AddSingleton<CameraHealthAlertService>();
builder.Services.AddSingleton<ICameraHealthCycleListener>(sp => sp.GetRequiredService<CameraHealthAlertService>());
builder.Services.AddHostedService<HealthDigestHostedService>();

builder.Services.AddSingleton<ICaptureIndexer, CaptureIndexer>();
builder.Services.AddHostedService<CaptureIndexHostedService>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEvolutionApiClient, EvolutionApiClient>();

builder.Services.AddSingleton<IAlertChannel, EmailAlertChannel>();
builder.Services.AddSingleton<IAlertChannel, WhatsAppAlertChannel>();
builder.Services.AddSingleton<IAlertDispatcher, AlertDispatcher>();
builder.Services.AddHostedService<CaptureAlertDigestHostedService>();

// Authentication: cookie scheme + PasswordHasher over the custom AppUser table
// (full ASP.NET Core Identity is overkill for one LAN admin — see SPEC-08).
// The Data Protection key ring is shared with CameraVision.Api so the same
// cookie authenticates /media streaming there (SPEC-11).
var keysDirectory = Path.GetFullPath(Path.Combine(contentRoot,
    builder.Configuration["Storage:KeysDirectory"] ?? "../../data/keys"));
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("CameraVision");
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.Name = "CameraVision.Auth";
    });
builder.Services.AddAuthorization(options =>
{
    // Admin = manages a tenant; SuperAdmin passes every Admin gate too (SPEC-14).
    options.AddPolicy("Admin", policy => policy.RequireClaim(
        AppClaims.Role, nameof(UserRole.Admin), nameof(UserRole.SuperAdmin)));
    options.AddPolicy("SuperAdmin", policy => policy.RequireClaim(
        AppClaims.Role, nameof(UserRole.SuperAdmin)));
});
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

Directory.CreateDirectory(Path.GetDirectoryName(storage.DatabasePath)!);
Directory.CreateDirectory(storage.OutputRoot);
await DbInitializer.InitializeAsync(
    app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>(),
    app.Services.GetRequiredService<IPasswordHasher<AppUser>>());

var legacyCamerasFile = Path.GetFullPath(Path.Combine(contentRoot,
    builder.Configuration["Storage:LegacyCamerasFile"] ?? "../../data/cameras.json"));
await LegacyCameraImporter.ImportIfEmptyAsync(
    app.Services.GetRequiredService<ICameraRepository>(),
    app.Services.GetRequiredService<ITenantRepository>(), legacyCamerasFile, app.Logger);
await LegacyCameraImporter.EnrichFromLegacyAsync(
    app.Services.GetRequiredService<ICameraRepository>(), legacyCamerasFile, app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        return Results.BadRequest();
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
