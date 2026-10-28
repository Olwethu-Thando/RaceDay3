using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class ResultTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public ResultTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(AuthUser Organiser, AuthUser Participant, EnrolmentResponse Enrolment)> SetupEnrolmentAsync()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInUserAsync(_factory, "Participant");
        var ev = await TestHelpers.CreateEventAsync(organiser.Client);
        var category = await TestHelpers.CreateCategoryAsync(organiser.Client, ev.EventId);
        var enrolment = await TestHelpers.EnrolAsync(participant.Client, ev.EventId, category.CategoryId);
        return (organiser, participant, enrolment);
    }

    private static object ResultBody(int position = 47) => new { finishTimeSeconds = 2745, finishPosition = position, totalFinishers = 312 };

    [Fact]
    public async Task CaptureResult_AsOwningOrganiser_ReturnsCreated_AndParticipantCanSeeIt()
    {
        var (organiser, participant, enrolment) = await SetupEnrolmentAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mine = await participant.Client.GetFromJsonAsync<List<ResultResponse>>("/api/participants/me/results");
        Assert.Contains(mine!, r => r.EnrolmentId == enrolment.EnrolmentId && r.FinishPosition == 47);
    }

    [Fact]
    public async Task CaptureResult_AsParticipant_ReturnsForbidden()
    {
        var (_, participant, enrolment) = await SetupEnrolmentAsync();

        var response = await participant.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_AsDifferentOrganiser_ReturnsForbidden()
    {
        var (_, _, enrolment) = await SetupEnrolmentAsync();
        var other = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");

        var response = await other.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_WithPositionAboveTotalFinishers_ReturnsBadRequest()
    {
        var (organiser, _, enrolment) = await SetupEnrolmentAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", ResultBody(position: 400));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_ReturnsConflict()
    {
        var (organiser, _, enrolment) = await SetupEnrolmentAsync();
        (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", ResultBody())).EnsureSuccessStatusCode();

        var second = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", ResultBody());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetMyResults_AsOrganiser_ReturnsForbidden()
    {
        var organiser = await TestHelpers.CreateLoggedInUserAsync(_factory, "Organiser");

        var response = await organiser.Client.GetAsync("/api/participants/me/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
