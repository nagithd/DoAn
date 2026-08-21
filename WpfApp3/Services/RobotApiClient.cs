using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class RobotApiClient : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private Uri? _baseUri;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Địa chỉ Robot API không hợp lệ.", nameof(baseUrl));
        }

        _baseUri = uri;
    }

    public Task<RobotHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) =>
        GetAsync<RobotHealthResponse>("health", cancellationToken);

    public Task<RobotStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default) =>
        GetAsync<RobotStatusResponse>("robot/status", cancellationToken);

    public Task<RobotServosResponse> GetServosAsync(CancellationToken cancellationToken = default) =>
        GetAsync<RobotServosResponse>("robot/servos", cancellationToken);

    public Task<RobotCommandResponse> SetServoAsync(
        int servoId,
        double angle,
        int moveTimeMs = 500,
        CancellationToken cancellationToken = default) =>
        PostAsync<RobotCommandResponse>(
            "robot/servo",
            new
            {
                servo_id = servoId,
                angle,
                move_time_ms = moveTimeMs
            },
            cancellationToken);

    public Task<RobotCommandResponse> HomeAsync(CancellationToken cancellationToken = default) =>
        PostAsync<RobotCommandResponse>("robot/home", new { }, cancellationToken);

    public Task<RobotCommandResponse> ResetAsync(CancellationToken cancellationToken = default) =>
        PostAsync<RobotCommandResponse>("robot/reset", new { }, cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        Uri requestUri = BuildRequestUri(path);
        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                requestUri,
                cancellationToken);
        return await ReadResponseAsync<T>(response, cancellationToken);
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
        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string detail = body;
            try
            {
                var error = JsonSerializer.Deserialize<RobotCommandResponse>(body, JsonOptions);
                if (!string.IsNullOrWhiteSpace(error?.Error))
                    detail = error.Error;
            }
            catch (JsonException)
            {
                // Keep the raw response as the diagnostic message.
            }

            throw new HttpRequestException(
                $"Robot API trả về {(int)response.StatusCode}: {detail}");
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException("Robot API trả về dữ liệu rỗng.");
    }

    private Uri BuildRequestUri(string path)
    {
        if (_baseUri == null)
            throw new InvalidOperationException("Robot API chưa được cấu hình.");
        return new Uri(_baseUri, path);
    }

    public void Dispose() => _httpClient.Dispose();
}
