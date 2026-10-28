using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RaceDay.Web.Services;

/// <summary>
/// The only place the MVC app talks to anything: every call goes to the Part 2 API.
/// The API's login cookie is stored in the MVC session and forwarded on each request.
/// </summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly string _publicBase;

    public ApiClient(HttpClient http, IHttpContextAccessor accessor, IConfiguration config)
    {
        _http = http;
        _accessor = accessor;
        _publicBase = (config["Api:PublicBaseUrl"] ?? config["Api:BaseUrl"] ?? string.Empty).TrimEnd('/');
    }

    /// <summary>Turns an API image path (/uploads/...) or an Azure URL into something the browser can load.</summary>
    public string? ImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : _publicBase + url;
    }

    // ---------------- Auth ----------------

    public async Task<ApiResult<LoginOutcome>> LoginAsync(string email, string password)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("api/auth/login", new { email, password }, Json);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Email or password is incorrect."
                    : ExtractError(response.StatusCode, body);
                return ApiResult<LoginOutcome>.Fail((int)response.StatusCode, message);
            }

            var auth = JsonSerializer.Deserialize<AuthResponse>(body, Json);
            var cookie = ReadAuthCookie(response);
            if (auth is null || cookie is null)
                return ApiResult<LoginOutcome>.Fail(500, "We couldn't sign you in. Please try again.");
            return ApiResult<LoginOutcome>.Ok(new LoginOutcome(auth, cookie));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiUnavailableException();
        }
    }

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterViewModel m) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", JsonContent.Create(new
        {
            fullName = m.FullName,
            email = m.Email,
            password = m.Password,
            role = m.Role,
            contactNumber = m.ContactNumber,
            organisationName = m.Role == "Organiser" ? m.OrganisationName : null,
            dateOfBirth = m.Role == "Participant" ? m.DateOfBirth : null
        }, options: Json));

    public Task<ApiResult<object>> LogoutAsync() => SendAsync<object>(HttpMethod.Post, "api/auth/logout", null, true);

    // ---------------- Profile ----------------

    public Task<ApiResult<ProfileResponse>> GetProfileAsync() =>
        SendAsync<ProfileResponse>(HttpMethod.Get, "api/users/me", null, true);

    public Task<ApiResult<ProfileResponse>> UpdateProfileAsync(ProfileFormModel m) =>
        SendAsync<ProfileResponse>(HttpMethod.Put, "api/users/me", JsonContent.Create(new
        {
            fullName = m.FullName,
            contactNumber = m.ContactNumber,
            organisationName = m.OrganisationName,
            dateOfBirth = m.DateOfBirth
        }, options: Json), true);

    public Task<ApiResult<ProfilePictureResponse>> UploadProfilePictureAsync(IFormFile file) =>
        SendAsync<ProfilePictureResponse>(HttpMethod.Post, "api/users/me/profilepicture", ImageContent(file), true);

    // ---------------- Events ----------------

    public Task<ApiResult<List<EventResponse>>> GetEventsAsync(string? eventType, string? location)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(eventType)) query.Add($"eventType={Uri.EscapeDataString(eventType)}");
        if (!string.IsNullOrWhiteSpace(location)) query.Add($"location={Uri.EscapeDataString(location)}");
        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return SendAsync<List<EventResponse>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventResponse>> GetEventAsync(int id) =>
        SendAsync<EventResponse>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<List<EventResponse>>> GetMyEventsAsync() =>
        SendAsync<List<EventResponse>>(HttpMethod.Get, "api/events/mine", null, true);

    public Task<ApiResult<EventResponse>> CreateEventAsync(EventFormModel m) =>
        SendAsync<EventResponse>(HttpMethod.Post, "api/events", EventBody(m), true);

    public Task<ApiResult<EventResponse>> UpdateEventAsync(int id, EventFormModel m) =>
        SendAsync<EventResponse>(HttpMethod.Put, $"api/events/{id}", EventBody(m), true);

    public Task<ApiResult<object>> DeleteEventAsync(int id) =>
        SendAsync<object>(HttpMethod.Delete, $"api/events/{id}", null, true);

    public Task<ApiResult<BannerResponse>> UploadBannerAsync(int eventId, IFormFile file) =>
        SendAsync<BannerResponse>(HttpMethod.Post, $"api/events/{eventId}/banner", ImageContent(file), true);

    // ---------------- Categories ----------------

    public Task<ApiResult<CategoryResponse>> CreateCategoryAsync(int eventId, CategoryFormModel m) =>
        SendAsync<CategoryResponse>(HttpMethod.Post, $"api/events/{eventId}/categories", CategoryBody(m), true);

    public Task<ApiResult<CategoryResponse>> UpdateCategoryAsync(int id, CategoryFormModel m) =>
        SendAsync<CategoryResponse>(HttpMethod.Put, $"api/categories/{id}", CategoryBody(m), true);

    public Task<ApiResult<object>> DeleteCategoryAsync(int id) =>
        SendAsync<object>(HttpMethod.Delete, $"api/categories/{id}", null, true);

    // ---------------- Enrolments ----------------

    public Task<ApiResult<EnrolmentResponse>> EnrolAsync(int eventId, int categoryId) =>
        SendAsync<EnrolmentResponse>(HttpMethod.Post, $"api/events/{eventId}/enrolments",
            JsonContent.Create(new { categoryId }, options: Json), true);

    public Task<ApiResult<List<EnrolmentResponse>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentResponse>>(HttpMethod.Get, "api/participants/me/enrolments", null, true);

    public Task<ApiResult<List<EnrolmentResponse>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentResponse>>(HttpMethod.Get, $"api/events/{eventId}/enrolments", null, true);

    public Task<ApiResult<EnrolmentResponse>> SetEnrolmentStatusAsync(int enrolmentId, string status) =>
        SendAsync<EnrolmentResponse>(HttpMethod.Put, $"api/enrolments/{enrolmentId}/status",
            JsonContent.Create(new { status }, options: Json), true);

    // ---------------- Results ----------------

    public Task<ApiResult<ResultResponse>> CaptureResultAsync(int enrolmentId, int seconds, int position, int total) =>
        SendAsync<ResultResponse>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/result",
            ResultBody(seconds, position, total), true);

    public Task<ApiResult<ResultResponse>> UpdateResultAsync(int resultId, int seconds, int position, int total) =>
        SendAsync<ResultResponse>(HttpMethod.Put, $"api/results/{resultId}",
            ResultBody(seconds, position, total), true);

    public Task<ApiResult<List<ResultResponse>>> GetMyResultsAsync() =>
        SendAsync<List<ResultResponse>>(HttpMethod.Get, "api/participants/me/results", null, true);

    public Task<ApiResult<List<ResultResponse>>> GetEventResultsAsync(int eventId) =>
        SendAsync<List<ResultResponse>>(HttpMethod.Get, $"api/events/{eventId}/results");

    // ---------------- plumbing ----------------

    private static JsonContent EventBody(EventFormModel m) => JsonContent.Create(new
    {
        name = m.Name,
        description = m.Description,
        eventDate = m.EventDate,
        location = m.Location,
        distanceKm = m.DistanceKm,
        eventType = m.EventType
    }, options: Json);

    private static JsonContent CategoryBody(CategoryFormModel m) => JsonContent.Create(new
    {
        name = m.Name,
        minAge = m.MinAge,
        maxAge = m.MaxAge,
        distanceKm = m.DistanceKm
    }, options: Json);

    private static JsonContent ResultBody(int seconds, int position, int total) => JsonContent.Create(new
    {
        finishTimeSeconds = seconds,
        finishPosition = position,
        totalFinishers = total
    }, options: Json);

    private static MultipartFormDataContent ImageContent(IFormFile file)
    {
        var content = new MultipartFormDataContent();
        var part = new StreamContent(file.OpenReadStream());
        part.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(part, "image", file.FileName);
        return content;
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null, bool requireAuth = false)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        var cookie = _accessor.HttpContext?.Session.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", $"{SessionKeys.ApiCookieName}={cookie}");
        else if (requireAuth)
            throw new ApiUnauthorizedException();

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiUnavailableException();
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                T? data = default;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try { data = JsonSerializer.Deserialize<T>(body, Json); }
                    catch (JsonException) { /* body wasn't the expected shape: treat as no data */ }
                }
                return ApiResult<T>.Ok(data, (int)response.StatusCode);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && requireAuth)
                throw new ApiUnauthorizedException();

            return ApiResult<T>.Fail((int)response.StatusCode, ExtractError(response.StatusCode, body));
        }
    }

    private static string? ReadAuthCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
        var prefix = SessionKeys.ApiCookieName + "=";
        foreach (var value in values)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var end = value.IndexOf(';');
            var pair = end > 0 ? value[..end] : value;
            return pair[prefix.Length..];
        }
        return null;
    }

    /// <summary>Turns the API's error body into a short, user-friendly sentence.</summary>
    private static string ExtractError(HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray() : Enumerable.Empty<JsonElement>())
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s) && !s!.Contains("JSON", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (messages.Count > 0) return string.Join(" ", messages);
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(detail.GetString()))
                return detail.GetString()!;

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
                && !title.GetString()!.StartsWith("One or more validation", StringComparison.OrdinalIgnoreCase))
                return title.GetString()!;
        }
        catch (JsonException) { /* empty or non-JSON body */ }

        return status switch
        {
            HttpStatusCode.BadRequest => "Please check the information you entered and try again.",
            HttpStatusCode.Forbidden => "You are not allowed to do that.",
            HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
            HttpStatusCode.Conflict => "That clashes with something that already exists.",
            _ => "Something went wrong. Please try again."
        };
    }
}
