using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public record AuthUser(HttpClient Client, int UserId, string Email);

public static class TestHelpers
{
    public const string Password = "Passw0rd!123";

    public static string NewEmail() => $"{Guid.NewGuid():N}@test.raceday.co.za";

    public static object RegisterBody(string email, string role, string password = Password) => new
    {
        fullName = "Test User",
        email,
        password,
        role,
        contactNumber = "0821234567",
        organisationName = role == "Organiser" ? "Test Club" : (string?)null,
        dateOfBirth = role == "Participant" ? "1995-05-20" : (string?)null
    };

    /// <summary>Registers a new user and logs in. Each user gets their own HttpClient (own cookie jar).</summary>
    public static async Task<AuthUser> CreateLoggedInUserAsync(CustomWebApplicationFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email, role));
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        return new AuthUser(client, auth!.UserId, email);
    }

    public static object EventBody(string name = "Test 10K", string type = "Run") => new
    {
        name,
        description = "Test event",
        eventDate = DateTime.Now.AddDays(30),
        location = "Johannesburg",
        distanceKm = 10.0,
        eventType = type
    };

    public static async Task<EventResponse> CreateEventAsync(HttpClient organiser)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiser, int eventId, string name = "Open")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    public static async Task<EnrolmentResponse> EnrolAsync(HttpClient participant, int eventId, int categoryId)
    {
        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }
}
