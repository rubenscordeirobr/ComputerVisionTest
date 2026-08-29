using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CameraVision.Core;
using CameraVision.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CameraVision.Infrastructure;

/// <summary>
/// Evolution API v2 pairing flow: GET /instance/connect/{name} returns the QR
/// (creating the instance on 404), GET /instance/connectionState/{name} reports
/// open | connecting | close.
/// </summary>
public sealed class EvolutionApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<EvolutionApiClient> logger) : IEvolutionApiClient
{
    public async Task<EvolutionQr> ConnectAsync(SystemSettings settings, CancellationToken ct = default)
    {
        if (Validate(settings) is { } configError)
            return new EvolutionQr(null, null, configError);

        try
        {
            using var client = CreateClient(settings);
            var instance = Uri.EscapeDataString(settings.EvolutionInstanceName.Trim());

            var response = await client.GetAsync($"instance/connect/{instance}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var created = await client.PostAsJsonAsync("instance/create", new
                {
                    instanceName = settings.EvolutionInstanceName.Trim(),
                    qrcode = true,
                    integration = "WHATSAPP-BAILEYS",
                }, ct);
                if (!created.IsSuccessStatusCode)
                    return new EvolutionQr(null, null,
                        $"Falha ao criar a instância (HTTP {(int)created.StatusCode}).");
                response = await client.GetAsync($"instance/connect/{instance}", ct);
            }

            if (!response.IsSuccessStatusCode)
                return new EvolutionQr(null, null,
                    $"A Evolution API retornou HTTP {(int)response.StatusCode}.");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var base64 = GetString(json, "base64") ?? GetString(json, "qrcode", "base64");
            var pairingCode = GetString(json, "pairingCode") ?? GetString(json, "qrcode", "pairingCode");

            if (base64 == null && pairingCode == null)
                return new EvolutionQr(null, null,
                    "A API não retornou um QR Code — a instância talvez já esteja conectada.");
            return new EvolutionQr(base64, pairingCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Evolution API connect failed.");
            return new EvolutionQr(null, null, $"Falha ao contatar a Evolution API: {ex.Message}");
        }
    }

    public async Task<EvolutionState> GetStateAsync(SystemSettings settings, CancellationToken ct = default)
    {
        if (Validate(settings) is { } configError)
            return new EvolutionState(EvolutionConnection.Error, configError);

        try
        {
            using var client = CreateClient(settings);
            var instance = Uri.EscapeDataString(settings.EvolutionInstanceName.Trim());
            var response = await client.GetAsync($"instance/connectionState/{instance}", ct);
            if (!response.IsSuccessStatusCode)
                return new EvolutionState(EvolutionConnection.Error,
                    $"A Evolution API retornou HTTP {(int)response.StatusCode}.");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var state = GetString(json, "instance", "state") ?? GetString(json, "state");

            return state?.ToLowerInvariant() switch
            {
                "open" => new EvolutionState(EvolutionConnection.Open),
                "connecting" => new EvolutionState(EvolutionConnection.Connecting),
                "close" or "closed" => new EvolutionState(EvolutionConnection.Closed),
                _ => new EvolutionState(EvolutionConnection.Error,
                    $"Estado desconhecido retornado pela API: \"{state}\"."),
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Evolution API state check failed.");
            return new EvolutionState(EvolutionConnection.Error,
                $"Falha ao contatar a Evolution API: {ex.Message}");
        }
    }

    private HttpClient CreateClient(SystemSettings settings)
    {
        var client = httpClientFactory.CreateClient("evolution-api");
        client.BaseAddress = new Uri(settings.EvolutionBaseUrl.Trim().TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
        if (!string.IsNullOrWhiteSpace(settings.EvolutionApiKey))
            client.DefaultRequestHeaders.Add("apikey", settings.EvolutionApiKey.Trim());
        return client;
    }

    private static string? Validate(SystemSettings settings)
    {
        if (!Uri.TryCreate(settings.EvolutionBaseUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return "Configure uma URL base válida para a Evolution API.";
        if (string.IsNullOrWhiteSpace(settings.EvolutionInstanceName))
            return "Configure o nome da instância.";
        return null;
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(segment, out element))
                return null;
        }
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
