namespace RaceDay.Web.Controllers;

public class OrganiserController : OrganiserControllerBase
{
    public OrganiserController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Dashboard()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Success) FlashError(result.Error ?? "We couldn't load your events.");

        var events = result.Data ?? new List<EventResponse>();
        var now = DateTime.Now;
        return View(new DashboardViewModel
        {
            AllEvents = events.OrderByDescending(e => e.EventDate).ToList(),
            Upcoming = events.Where(e => e.EventDate >= now).OrderBy(e => e.EventDate).ToList(),
            TotalEnrolments = events.Sum(e => e.EnrolmentCount ?? 0)
        });
    }
}
