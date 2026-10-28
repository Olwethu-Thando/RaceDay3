namespace RaceDay.Web.Controllers;

public class EnrolmentsController : OrganiserControllerBase
{
    public EnrolmentsController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Index(int eventId)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (ev is null) return error!;

        var result = await Api.GetEventEnrolmentsAsync(eventId);
        if (!result.Success) FlashError(result.Error ?? "We couldn't load the enrolments.");

        return View(new EnrolmentsViewModel { Event = ev, Enrolments = result.Data ?? new List<EnrolmentResponse>() });
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int eventId, int id, string status)
    {
        var result = await Api.SetEnrolmentStatusAsync(id, status);
        if (result.Success) FlashSuccess($"Enrolment marked as {result.Data?.Status ?? status}.");
        else FlashError(result.Error ?? "We couldn't update the enrolment.");
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
