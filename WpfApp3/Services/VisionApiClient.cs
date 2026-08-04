using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class VisionApiClient : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    private Uri? _baseUri;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(string baseUrl)
    {
        if (!Uri.TryCreate(
                baseUrl.Trim().TrimEnd('/') + "/",
                UriKind.Absolute,
                out Uri? uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "Địa chỉ Vision API không hợp lệ.",
                nameof(baseUrl));
        }

        _baseUri = uri;
    }

    public Task<VisionStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<VisionStatusResponse>(
            "vision/status",
            cancellationToken);

    public Task<VisionStatusResponse> StartAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<VisionStatusResponse>(
            "vision/start",
            new { },
            cancellationToken);

    public Task<VisionStatusResponse> StopAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<VisionStatusResponse>(
            "vision/stop",
            new { },
            cancellationToken);

    public Task<VisionStatusResponse> ResetTriggerAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<VisionStatusResponse>(
            "vision/reset-trigger",
            new { },
            cancellationToken);

    public Task<RobotCommandResponse> VisionHomeAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<RobotCommandResponse>(
            "robot/vision-home",
            new { },
            cancellationToken);

    public Task<VisionConfigResponse> UpdateConfigAsync(
        VisionConfig config,
        CancellationToken cancellationToken = default) =>
        PostAsync<VisionConfigResponse>(
            "vision/config",
            config,
            cancellationToken);

    public Task<VisionClassificationResponse> SubmitClassificationAsync(
        string className,
        double confidence,
        string? inspectionId = null,
        CancellationToken cancellationToken = default) =>
        PostAsync<VisionClassificationResponse>(
            "vision/classifications",
            new
            {
                class_name = className,
                confidence,
                inspection_id = inspectionId ?? Guid.NewGuid().ToString(),
                source = "wpf_test"
            },
            cancellationToken);

    public async Task<VisionClassificationResponse> ClearClassificationsAsync(
        CancellationToken cancellationToken = default)
    {
        Uri requestUri = BuildRequestUri("vision/classifications");
        using HttpResponseMessage response = await _httpClient.DeleteAsync(
            requestUri,
            cancellationToken);
        return await ReadResponseAsync<VisionClassificationResponse>(
            response,
            cancellationToken);
    }

    public async Task<byte[]> GetLatestFrameAsync(
        CancellationToken cancellationToken = default)
    {
        Uri requestUri =
            BuildRequestUri("vision/frame.jpg");
        using HttpResponseMessage response = await _httpClient.GetAsync(
            requestUri,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        Uri requestUri = BuildRequestUri(path);
        using HttpResponseMessage response = await _httpClient.GetAsync(
            requestUri,
            cancellationToken);
        return await ReadResponseAsync<T>(
            response,
            cancellationToken);
    }

    private async Task<T> PostAsync<T>(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        Uri requestUri = BuildRequestUri(path);
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                requestUri,
                payload,
                cancellationToken);
        return await ReadResponseAsync<T>(
            response,
            cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string detail = body;
            try
            {
                var error = JsonSerializer.Deserialize<RobotCommandResponse>(
                    body,
                    JsonOptions);
                if (!string.IsNullOrWhiteSpace(error?.Error))
                    detail = error.Error;
            }
            catch (JsonException)
            {
                // Preserve the raw response for diagnostics.
            }

            throw new HttpRequestException(
                $"Vision API trả về {(int)response.StatusCode}: {detail}");
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException(
                "Vision API trả về dữ liệu rỗng.");
    }

    private Uri BuildRequestUri(string path)
    {
        if (_baseUri == null)
            throw new InvalidOperationException(
                "Vision API chưa được cấu hình.");
        return new Uri(_baseUri, path);
    }

    public void Dispose() => _httpClient.Dispose();
}
