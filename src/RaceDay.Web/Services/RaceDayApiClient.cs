using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>
/// Typed client for the RaceDay API. Every page in the MVC app gets and saves its data through this class;
/// the MVC project has no database access of its own.
/// </summary>
public class RaceDayApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly UserSession _session;
    private readonly ILogger<RaceDayApiClient> _logger;

    public RaceDayApiClient(HttpClient http, UserSession session, ILogger<RaceDayApiClient> logger)
    {
        _http = http;
        _session = session;
        _logger = logger;
    }

    // ---------- Authentication ----------

    public Task<ApiResult<UserProfileDto>> RegisterAsync(string fullName, string email, string password, string role, string? phoneNumber) =>
        SendAsync<UserProfileDto>(HttpMethod.Post, "api/auth/register", new { fullName, email, password, role, phoneNumber });

    public Task<ApiResult<LoginResponse>> LoginAsync(string email, string password) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new { email, password });

    public Task<ApiResult> LogoutAsync() => SendAsync(HttpMethod.Post, "api/auth/logout");

    // ---------- Profile ----------

    public Task<ApiResult<UserProfileDto>> GetProfileAsync() => SendAsync<UserProfileDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserProfileDto>> UpdateProfileAsync(string fullName, string? phoneNumber) =>
        SendAsync<UserProfileDto>(HttpMethod.Put, "api/users/me", new { fullName, phoneNumber });

    public Task<ApiResult<UserProfileDto>> UploadProfilePictureAsync(IFormFile file) =>
        UploadAsync<UserProfileDto>("api/users/me/photo", file);

    public Task<ApiImage?> GetProfilePictureAsync() => GetImageAsync("api/users/me/photo");

    // ---------- Events ----------

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(EventFilter filter)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add($"search={Uri.EscapeDataString(filter.Search.Trim())}");
        if (filter.EventType.HasValue) query.Add($"eventType={filter.EventType}");
        if (filter.From.HasValue) query.Add($"from={filter.From.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filter.To.HasValue) query.Add($"to={filter.To.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (filter.IncludePast) query.Add("includePast=true");
        var url = query.Count == 0 ? "api/events" : $"api/events?{string.Join('&', query)}";
        return SendAsync<List<EventDto>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventDto>> GetEventAsync(int eventId) => SendAsync<EventDto>(HttpMethod.Get, $"api/events/{eventId}");

    public Task<ApiResult<List<OrganiserEventSummaryDto>>> GetMyEventsAsync() =>
        SendAsync<List<OrganiserEventSummaryDto>>(HttpMethod.Get, "api/users/me/events");

    public Task<ApiResult<EventDto>> CreateEventAsync(object request) => SendAsync<EventDto>(HttpMethod.Post, "api/events", request);

    public Task<ApiResult<EventDto>> UpdateEventAsync(int eventId, object request) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{eventId}", request);

    public Task<ApiResult> DeleteEventAsync(int eventId) => SendAsync(HttpMethod.Delete, $"api/events/{eventId}");

    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) =>
        UploadAsync<EventDto>($"api/events/{eventId}/banner", file);

    public Task<ApiImage?> GetBannerAsync(int eventId) => GetImageAsync($"api/events/{eventId}/banner");

    // ---------- Categories ----------

    public Task<ApiResult<List<CategoryDto>>> GetCategoriesAsync(int eventId) =>
        SendAsync<List<CategoryDto>>(HttpMethod.Get, $"api/events/{eventId}/categories");

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, object request) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", request);

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, object request) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", request);

    public Task<ApiResult> DeleteCategoryAsync(int categoryId) => SendAsync(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ---------- Enrolments ----------

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, $"api/categories/{categoryId}/enrol");

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/users/me/enrolments");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult<EnrolmentDto>> UpdateEnrolmentStatusAsync(int enrolmentId, string status) =>
        SendAsync<EnrolmentDto>(HttpMethod.Put, $"api/enrolments/{enrolmentId}/status", new { status });

    public Task<ApiResult> WithdrawAsync(int enrolmentId) => SendAsync(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    // ---------- Results ----------

    public Task<ApiResult<ResultDto>> CaptureResultAsync(int enrolmentId, string finishTime, int position) =>
        SendAsync<ResultDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/result", new { finishTime, position });

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, "api/users/me/results");

    public Task<ApiResult<List<ResultDto>>> GetCategoryResultsAsync(int categoryId) =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/categories/{categoryId}/results");

    // ---------- Plumbing ----------

    private async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await SendRawAsync(request);
        return response.IsSuccessStatusCode
            ? new ApiResult { Succeeded = true, StatusCode = response.StatusCode }
            : await FailureAsync<object>(response);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await SendRawAsync(request);
        if (!response.IsSuccessStatusCode) return await FailureAsync<T>(response);

        var data = await response.Content.ReadFromJsonAsync<T>(Json);
        return new ApiResult<T> { Succeeded = true, StatusCode = response.StatusCode, Data = data };
    }

    /// <summary>Sends an uploaded file to the API as multipart/form-data in a field named "file".</summary>
    private async Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
            ? type
            : new MediaTypeHeaderValue("application/octet-stream");
        using var form = new MultipartFormDataContent { { content, "file", Path.GetFileName(file.FileName) } };
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = form };

        using var response = await SendRawAsync(request);
        if (!response.IsSuccessStatusCode) return await FailureAsync<T>(response);
        var data = await response.Content.ReadFromJsonAsync<T>(Json);
        return new ApiResult<T> { Succeeded = true, StatusCode = response.StatusCode, Data = data };
    }

    /// <summary>Downloads an image through the API, or returns null when there is none (or storage is down).</summary>
    private async Task<ApiImage?> GetImageAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendRawAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new ApiImage(bytes, contentType);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "RaceDay API call {Method} {Url} failed", request.Method, request.RequestUri);
            throw new ApiUnavailableException(ex);
        }

        // A logged-in user getting 401 means the API session ended (timeout or API restart).
        if (response.StatusCode == HttpStatusCode.Unauthorized && _session.IsLoggedIn)
        {
            response.Dispose();
            throw new ApiSessionExpiredException();
        }
        return response;
    }

    /// <summary>Turns an error response (MessageResponse or ValidationProblemDetails) into a friendly result.</summary>
    private async Task<ApiResult<T>> FailureAsync<T>(HttpResponseMessage response)
    {
        string? message = null;
        var fieldErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    message = m.GetString();
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        var messages = field.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                        fieldErrors[field.Name.TrimStart('$', '.')] = messages;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: fall back to a message based on the status code.
        }

        message ??= fieldErrors.Count > 0
            ? "Please correct the highlighted fields."
            : response.StatusCode switch
            {
                HttpStatusCode.NotFound => "That item could not be found.",
                HttpStatusCode.Forbidden => "You do not have permission to do that.",
                HttpStatusCode.ServiceUnavailable => "That service is temporarily unavailable. Please try again shortly.",
                _ => "Something went wrong. Please try again."
            };

        return new ApiResult<T> { Succeeded = false, StatusCode = response.StatusCode, ErrorMessage = message, FieldErrors = fieldErrors };
    }
}
