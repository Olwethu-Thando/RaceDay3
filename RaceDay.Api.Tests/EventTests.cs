using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class EventTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public EventTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateEvent_AsOrganiser_ReturnsCreatedAndOwnedByCaller()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", TestHelpers.EventBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal(organiser.UserId, created!.OrganiserId);
        Assert.Equal("Run", created.EventType);
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_ReturnsForbidden()
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");

        var response = await participant.Client.PostAsJsonAsync("/api/events", TestHelpers.EventBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WhenNotLoggedIn_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", TestHelpers.EventBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_ReturnsBadRequest()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", TestHelpers.EventBody(type: "Swim"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_AsOwner_ReturnsOkWithNewValues()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.EventBody("Renamed Event", "Cycle"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("Renamed Event", updated!.Name);
        Assert.Equal("Cycle", updated.EventType);
    }

    [Fact]
    public async Task UpdateEvent_AsDifferentOrganiser_ReturnsForbidden()
    {
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(owner.Client);

        var response = await other.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", TestHelpers.EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsOwner_ReturnsNoContentAndEventIsGone()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var delete = await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");
        var get = await organiser.Client.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AsParticipant_ReturnsForbidden()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var response = await participant.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithoutLogin_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_WhenMissing_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_AsParticipant_ReturnsForbidden()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "Senior" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_AsOwner_ReturnsCreatedAndIsVisibleToEveryone()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId, "Senior");

        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        Assert.Contains(list!, c => c.Name == "Senior");
    }
}
