using CameraVision.Api;
using CameraVision.Core;
using CameraVision.Core.Alerts;
using CameraVision.Core.Auth;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using CameraVision.Infrastructure;
using CameraVision.Infrastructure.Alerts;
using CameraVision.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Same storage layout as the web app (relative paths resolve against this project folder).
var contentRoot = builder.Environment.ContentRootPath;
var storage = new StoragePaths(
    DatabasePath: Path.GetFullPath(Path.Combine(contentRoot,
        builder.Configuration["Storage:DatabasePath"] ?? "../../data/database.db")),
    OutputRoot: Path.GetFullPath(Path.Combine(contentRoot,
        builder.Configuration["Storage:OutputRoot"] ?? "../../output")));

builder.Services.AddSingleton(storage);
builder.Services.AddCameraVisionData(storage.DatabasePath);
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// Public (tokenized) capture links — see CaptureLinkService. The secret must match
// the web app's so the tokens it puts in alert e-mails validate here.
builder.Services.AddSingleton(new CaptureLinkOptions
{
    PublicBaseUrl = builder.Configuration["CaptureLinks:PublicBaseUrl"] ?? "",
    MediaBaseUrl = builder.Configuration["CaptureLinks:MediaBaseUrl"] ?? "",
    Secret = builder.Configuration["CaptureLinks:Secret"] ?? "",
});
builder.Services.AddSingleton<CaptureLinkService>();

// Ingested captures trigger the same rule-based alert dispatch as the web app.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEvolutionApiClient, EvolutionApiClient>();
builder.Services.AddSingleton<IAlertChannel, EmailAlertChannel>();
builder.Services.AddSingleton<IAlertChannel, WhatsAppAlertChannel>();
builder.Services.AddSingleton<IAlertDispatcher, AlertDispatcher>();

// Share the auth cookie with the web app: same cookie name, application name and
// Data Protection key ring (cookies are host-scoped, not port-scoped).
var keysDirectory = Path.GetFullPath(Path.Combine(contentRoot,
    builder.Configuration["Storage:KeysDirectory"] ?? "../../data/keys"));
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("CameraVision");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.Cookie.Name = "CameraVision.Auth");

var app = builder.Build();

Directory.CreateDirectory(Path.GetDirectoryName(storage.DatabasePath)!);
Directory.CreateDirectory(storage.OutputRoot);
await DbInitializer.InitializeAsync(
    app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>(),
    app.Services.GetRequiredService<IPasswordHasher<AppUser>>());

app.UseAuthentication();

// Recorded footage is sensitive — only signed-in web users of the capture's own
// tenant may stream it (SuperAdmin: any), or an alert recipient carrying the
// capture's playback token (?token=…), which is only valid for that one file.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/media", out var relative))
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!await UserOwnsFileAsync(context, relative))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Acesso negado.");
                return;
            }
        }
        else if (!await IsValidCaptureTokenAsync(context, relative))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Não autorizado.");
            return;
        }
    }
    await next();
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storage.OutputRoot),
    RequestPath = "/media",
});

app.MapProcessorEndpoints();

app.Run();

// Tenant ownership: the requested file (video or its .jpg thumbnail) must belong
// to a capture of the signed-in user's tenant. Stale cookies without the tenant
// claim (pre-SPEC-14) are denied — those sessions must sign in again.
static async Task<bool> UserOwnsFileAsync(HttpContext context, PathString relativePath)
{
    if (context.User.IsSuperAdmin())
        return true;

    var tenantId = context.User.GetTenantId();
    if (tenantId == null)
        return false;

    var filePath = relativePath.Value?.TrimStart('/');
    if (string.IsNullOrEmpty(filePath))
        return false;

    filePath = Uri.UnescapeDataString(filePath);
    if (filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        filePath = Path.ChangeExtension(filePath, ".mp4");

    var captures = context.RequestServices.GetRequiredService<ICaptureRepository>();
    var capture = await captures.GetByFilePathAsync(filePath, context.RequestAborted);
    return capture != null && capture.TenantId == tenantId;
}

static async Task<bool> IsValidCaptureTokenAsync(HttpContext context, PathString relativePath)
{
    var token = context.Request.Query[CaptureLinkService.TokenQueryKey].ToString();
    if (string.IsNullOrEmpty(token))
        return false;

    var filePath = relativePath.Value?.TrimStart('/');
    if (string.IsNullOrEmpty(filePath))
        return false;

    var captures = context.RequestServices.GetRequiredService<ICaptureRepository>();
    var capture = await captures.GetByFilePathAsync(Uri.UnescapeDataString(filePath),
        context.RequestAborted);
    if (capture == null)
        return false;

    var links = context.RequestServices.GetRequiredService<CaptureLinkService>();
    return links.IsValidToken(capture.Id, token);
}
