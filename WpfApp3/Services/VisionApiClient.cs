using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed class VisionApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    public VisionApiClient(HttpClient? httpClient = null) =>
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
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

    public Task<RobotHealthResponse> GetHealthAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<RobotHealthResponse>(
            "health",
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

    public Task<RobotCommandResponse> HomeAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<RobotCommandResponse>(
            "robot/home",
            new { },
            cancellationToken);

    public Task<RobotStatusResponse> GetRobotStatusAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<RobotStatusResponse>(
            "robot/status",
            cancellationToken);

    public Task<RobotServosResponse> GetServosAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<RobotServosResponse>(
            "robot/servos",
            cancellationToken);

    public Task<RobotCommandResponse> SetServoAsync(
        int servoId,
        double angle,
        int moveTimeMs,
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

    public Task<RobotCommandResponse> ResetRobotAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<RobotCommandResponse>(
            "robot/reset",
            new { },
            cancellationToken);

    public Task<RobotJobResponse> SendDirectPickAsync(
        string className,
        string inspectionId,
        CancellationToken cancellationToken = default) =>
        PostAsync<RobotJobResponse>(
            "robot/pick",
            new
            {
                class_name = className,
                job_id = inspectionId,
                wrist_angle = (double?)null,
                start_delay_ms = 0,
                // The checkpoint caller verifies vision_ready before posting.
                // The backend can therefore skip duplicate HOME, gripper-open
                // and PICK_ABOVE commands and move directly to PICK_DOWN.
                prepositioned = true,
                source = "arduino_checkpoint"
            },
            cancellationToken);

    public Task<RobotJobResponse> GetJobAsync(
        string jobId, CancellationToken cancellationToken = default) =>
        GetAsync<RobotJobResponse>(
            "robot/jobs/" + Uri.EscapeDataString(jobId), cancellationToken);

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

    public async IAsyncEnumerable<byte[]> StreamPreviewFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildRequestUri("vision/stream.mjpg"));
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        string? boundary = response.Content.Headers.ContentType?
            .Parameters
            .FirstOrDefault(parameter =>
                string.Equals(
                    parameter.Name,
                    "boundary",
                    StringComparison.OrdinalIgnoreCase))?
            .Value?
            .Trim('"');
        if (string.IsNullOrWhiteSpace(boundary))
        {
            throw new InvalidOperationException(
                "MJPEG response does not provide a boundary.");
        }

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken);
        string expectedBoundary = "--" + boundary;

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            do
            {
                line = await ReadAsciiLineAsync(stream, cancellationToken);
                if (line is null)
                    yield break;
            }
            while (!string.Equals(
                line,
                expectedBoundary,
                StringComparison.Ordinal));

            int contentLength = 0;
            while (true)
            {
                line = await ReadAsciiLineAsync(stream, cancellationToken);
                if (line is null)
                    yield break;
                if (line.Length == 0)
                    break;

                const string contentLengthHeader = "Content-Length:";
                if (line.StartsWith(
                        contentLengthHeader,
                        StringComparison.OrdinalIgnoreCase) &&
                    !int.TryParse(
                        line[contentLengthHeader.Length..].Trim(),
                        out contentLength))
                {
                    throw new InvalidOperationException(
                        "MJPEG content length is invalid.");
                }
            }

            if (contentLength <= 0 || contentLength > 2 * 1024 * 1024)
            {
                throw new InvalidOperationException(
                    "MJPEG frame length is outside the allowed range.");
            }

            byte[] jpeg = new byte[contentLength];
            await ReadExactlyAsync(stream, jpeg, cancellationToken);
            yield return jpeg;
        }
    }

    private static async Task<string?> ReadAsciiLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>(128);
        byte[] next = new byte[1];
        while (true)
        {
            int count = await stream.ReadAsync(next, cancellationToken);
            if (count == 0)
                return bytes.Count == 0 ? null : Encoding.ASCII.GetString([.. bytes]);

            if (next[0] == (byte)'\n')
            {
                if (bytes.Count > 0 && bytes[^1] == (byte)'\r')
                    bytes.RemoveAt(bytes.Count - 1);
                return Encoding.ASCII.GetString([.. bytes]);
            }

            if (bytes.Count >= 8192)
                throw new InvalidOperationException("MJPEG header line is too long.");
            bytes.Add(next[0]);
        }
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int count = await stream.ReadAsync(
                buffer.AsMemory(offset),
                cancellationToken);
            if (count == 0)
                throw new EndOfStreamException(
                    "MJPEG stream ended inside a frame.");
            offset += count;
        }
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
