using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public AuthTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ---------- Registration ----------

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_WithValidDetails_ReturnsCreatedWithRole(string role)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(TestHelpers.NewEmail(), role));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(role, body!.Role);
        Assert.True(body.UserId > 0);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Participant"))).EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Organiser"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidRole_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(TestHelpers.NewEmail(), "Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(TestHelpers.NewEmail(), "Participant", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotThePlainText()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Participant"))).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(TestHelpers.Password, user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt hash prefix
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsOkAndSessionCookie()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Organiser"))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("RaceDayAuth="));
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Organiser", body!.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = TestHelpers.NewEmail();
        (await client.PostAsJsonAsync("/api/auth/register", TestHelpers.RegisterBody(email, "Participant"))).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword!1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = TestHelpers.NewEmail(), password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Session behaviour ----------

    [Fact]
    public async Task GetProfile_WhenNotLoggedIn_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_WhenLoggedIn_ReturnsOwnProfileWithoutPasswordHash()
    {
        var user = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");

        var response = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(user.Email, json);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_ThenAccessProtectedEndpoint_ReturnsUnauthorized()
    {
        var user = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/users/me")).StatusCode);

        var logout = await user.Client.PostAsync("/api/auth/logout", null);
        var after = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }
}
