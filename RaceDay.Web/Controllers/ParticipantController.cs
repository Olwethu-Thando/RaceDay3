namespace RaceDay.Web.Controllers;

[Authorize(Roles = "Participant")]
public class ParticipantController : AppController
{
    private readonly ApiClient _api;

    public ParticipantController(ApiClient api) => _api = api;

    public async Task<IActionResult> MyEnrolments()
    {
        var result = await _api.GetMyEnrolmentsAsync();
        if (!result.Success) FlashError(result.Error ?? "We couldn't load your enrolments.");
        return View((result.Data ?? new List<EnrolmentResponse>()).OrderBy(e => e.EventDate).ToList());
    }

    public async Task<IActionResult> MyResults()
    {
        var result = await _api.GetMyResultsAsync();
        if (!result.Success) FlashError(result.Error ?? "We couldn't load your results.");
        return View(result.Data ?? new List<ResultResponse>());
    }
}
