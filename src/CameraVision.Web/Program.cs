using System.Globalization;
using CameraVision.Core;
using CameraVision.Core.Alerts;
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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddSingleton<CameraHealthMonitor>();
builder.Services.AddSingleton<ICameraHealthService>(sp => sp.GetRequiredService<CameraHealthMonitor>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CameraHealthMonitor>());

builder.Services.AddSingleton<ICaptureIndexer, CaptureIndexer>();
builder.Services.AddHostedService<CaptureIndexHostedService>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEvolutionApiClient, EvolutionApiClient>();

builder.Services.AddSingleton<IAlertChannel, EmailAlertChannel>();
builder.Services.AddSingleton<IAlertChannel, WhatsAppAlertChannel>();
builder.Services.AddSingleton<IAlertDispatcher, AlertDispatcher>();

// Authentication: cookie scheme + PasswordHasher over the custom AppUser table
// (full ASP.NET Core Identity is overkill for one LAN admin — see SPEC-08).
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
    options.AddPolicy("Admin", policy => policy.RequireClaim("is_admin", "true")));
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

// Recorded videos and thumbnails are sensitive footage — require a signed-in user.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/media") &&
        context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Não autorizado.");
        return;
    }
    await next();
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storage.OutputRoot),
    RequestPath = "/media",
});

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
