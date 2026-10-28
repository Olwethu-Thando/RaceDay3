namespace RaceDay.Web.Controllers;

public class EventsController : AppController
{
    private readonly ApiClient _api;

    public EventsController(ApiClient api) => _api = api;

    // Public: anyone can read an event; enrolling needs a Participant login.
    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetEventAsync(id);
        if (!result.Success || result.Data is null) return NotFound();

        var vm = new EventDetailsViewModel { Event = result.Data };

        var results = await _api.GetEventResultsAsync(id);
        vm.Results = results.Data ?? new List<ResultResponse>();

        if (User.IsInRole("Participant"))
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            vm.ExistingEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id);
        }

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Participant")]
    public async Task<IActionResult> Enrol(int id, int categoryId)
    {
        if (categoryId <= 0)
        {
            FlashError("Please choose a category.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _api.EnrolAsync(id, categoryId);
        if (!result.Success)
        {
            FlashError(result.Error ?? "We couldn't enrol you. Please try again.");
            return RedirectToAction(nameof(Details), new { id });
        }

        FlashSuccess($"You're in! Your enrolment for {result.Data?.EventName ?? "the event"} is {result.Data?.Status ?? "Pending"}.");
        return RedirectToAction("MyEnrolments", "Participant");
    }
}
