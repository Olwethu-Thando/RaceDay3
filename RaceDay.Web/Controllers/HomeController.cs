namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index(string? eventType, string? location)
    {
        if (User.IsInRole("Organiser")) return RedirectToAction("Dashboard", "Organiser");

        var result = await _api.GetEventsAsync(eventType, location);
        return View(new HomeViewModel
        {
            Events = result.Data ?? new List<EventResponse>(),
            EventType = eventType,
            Location = location,
            Error = result.Success ? null : result.Error
        });
    }

    public IActionResult ServiceUnavailable() => View();

    [IgnoreAntiforgeryToken]
    public IActionResult Error() => View();

    [IgnoreAntiforgeryToken]
    public IActionResult Status(int id)
    {
        ViewData["Code"] = id;
        return View();
    }
}
