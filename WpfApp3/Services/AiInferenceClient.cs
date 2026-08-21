using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfApp3.Services;

public sealed class AiInferenceClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public AiInferenceClient(string serviceUrl = "http://127.0.0.1:7100")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serviceUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<AiHealthResponse> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            "health",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiHealthResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "AI service returned an empty health response.");
    }

    public async Task<AiPredictionResponse> PredictAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var request = new AiPredictionRequest
        {
            ImagePath = imagePath,
            InspectionId = Guid.NewGuid().ToString("N")
        };
        using HttpResponseMessage response = await PostJsonAsync(
            "predict",
            request,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string details = await response.Content.ReadAsStringAsync(
                cancellationToken);
            throw new HttpRequestException(
                $"AI service returned {(int)response.StatusCode}: {details}");
        }

        return await response.Content.ReadFromJsonAsync<AiPredictionResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "AI service returned an empty prediction response.");
    }

    private async Task<HttpResponseMessage> PostJsonAsync<T>(
        string endpoint,
        T request,
        CancellationToken cancellationToken)
    {
        // ByteArrayContent always supplies Content-Length. The Python service
        // also accepts chunked requests, but an explicit length keeps this
        // request compatible with simple local HTTP servers.
        byte[] payload = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(request));
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new(
            "application/json",
            "utf-8");
        return await _httpClient.PostAsync(
            endpoint,
            content,
            cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed class AiPredictionRequest
{
    [JsonPropertyName("image_path")]
    public string ImagePath { get; set; } = "";

    [JsonPropertyName("inspection_id")]
    public string InspectionId { get; set; } = "";
}

public sealed class AiHealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("model_path")]
    public string ModelPath { get; set; } = "";
}

public sealed class AiPredictionResponse
{
    [JsonPropertyName("inspection_id")]
    public string InspectionId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("detected_class")]
    public string DetectedClass { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("recommended_robot_cycle")]
    public string RecommendedRobotCycle { get; set; } = "";

    [JsonPropertyName("inference_time_ms")]
    public double InferenceTimeMs { get; set; }

    [JsonPropertyName("inspection_time")]
    public DateTime InspectionTime { get; set; }

    [JsonPropertyName("image_path")]
    public string ImagePath { get; set; } = "";

    [JsonPropertyName("detections")]
    public List<AiDetectionResponse> Detections { get; set; } = [];
}

public sealed class AiDetectionResponse
{
    [JsonPropertyName("class_name")]
    public string ClassName { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}
