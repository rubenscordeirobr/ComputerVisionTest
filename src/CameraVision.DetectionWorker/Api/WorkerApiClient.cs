using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CameraVision.ApiClient;

public sealed class ApiConfig
{
    public string BaseUrl { get; set; } = "http://localhost:5220";
    public string ApiKey { get; set; } = "";
    public int StatusIntervalSeconds { get; set; } = 30;
}

public sealed record ApiCamera(int Id, string Name, string StreamUrl, string? SubStreamUrl, string PreferredStream);

public sealed record ApiWorkerRule(
    List<string> Classes,
    Dictionary<string, string>? ClassColors,
    double ConfidenceThreshold,
    TimeOnly? ActiveFrom,
    TimeOnly? ActiveTo);

public sealed record ApiCaptureRules(List<ApiWorkerRule> Rules, int MaxSegmentSeconds, double LingerSeconds);

public sealed record ApiCaptureUpload(
    int? CameraId,
    string CameraName,
    string ObjectClass,
    int? TrackId,
    DateTime StartedAt,
    DateTime EndedAt,
    string FilePath,
    bool IsMerged,
    long FileSizeBytes);

/// <summary>Thin client for the CameraVision.Api processor endpoints (X-Api-Key auth).</summary>
public sealed class WorkerApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public WorkerApiClient(ApiConfig config)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15),
        };
        _http.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
    }

    public async Task<List<ApiCamera>> GetCamerasAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<ApiCamera>>("api/processor/cameras", ct) ?? [];

    public async Task<ApiCaptureRules> GetCaptureRulesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<ApiCaptureRules>("api/processor/capture-rules", ct)
        ?? new ApiCaptureRules([], 60, 2.0);

    // TimeOnly round-trips natively via System.Text.Json (both sides are .NET 10).

    public async Task PostStatusAsync(int cameraId, string status, string? detail = null,
        CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"api/processor/cameras/{cameraId}/status", new { status, detail }, JsonOpts, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task PostHeartbeatAsync(DateTime startedAt, string? device, int activeCameras,
        CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/processor/heartbeat", new { startedAt, device, activeCameras }, JsonOpts, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task PostCaptureAsync(ApiCaptureUpload capture, byte[]? thumbnailJpeg,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(JsonSerializer.Serialize(capture, JsonOpts), Encoding.UTF8,
            "application/json"), "capture");
        if (thumbnailJpeg is { Length: > 0 })
        {
            var image = new ByteArrayContent(thumbnailJpeg);
            image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(image, "thumbnail", "thumbnail.jpg");
        }
        using var response = await _http.PostAsync("api/processor/captures", content, ct);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
