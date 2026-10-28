using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class EnrolmentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public EnrolmentTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Enrol_AsParticipant_ReturnsCreatedAndEnrolmentIsRecorded()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal("Pending", created!.Status);

        // retrievable through the API...
        var mine = await participant.Client.GetFromJsonAsync<List<EnrolmentResponse>>("/api/participants/me/enrolments");
        Assert.Contains(mine!, e => e.EventId == ev.EventId && e.CategoryId == category.CategoryId);

        // ...and actually stored against the right participant, event and category
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        Assert.True(await db.Enrolments.AnyAsync(e =>
            e.ParticipantId == participant.UserId && e.EventId == ev.EventId && e.CategoryId == category.CategoryId));
    }

    [Fact]
    public async Task Enrol_TwiceInSameEvent_ReturnsConflict()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var second = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_AsOrganiser_ReturnsForbidden()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenNotLoggedIn_ReturnsUnauthorized()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_ReturnsBadRequest()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var eventA = await TestHelpers.CreateEventAsync(organiser.Client);
        var eventB = await TestHelpers.CreateEventAsync(organiser.Client);
        var categoryOfB = await TestHelpers.CreateCategoryAsync(organiser.Client, eventB.EventId);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{eventA.EventId}/enrolments", new { categoryId = categoryOfB.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InMissingEvent_ReturnsNotFound()
    {
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");

        var response = await participant.Client.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListEventEnrolments_AsOwningOrganiser_ReturnsParticipants()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);

        var list = await organiser.Client.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.EventId}/enrolments");

        Assert.Single(list!);
        Assert.Equal(participant.UserId, list![0].ParticipantId);
    }

    [Fact]
    public async Task ListEventEnrolments_AsParticipant_ReturnsForbidden()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);

        var response = await participant.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListEventEnrolments_AsDifferentOrganiser_ReturnsForbidden()
    {
        var owner = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(owner.Client);

        var response = await other.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
