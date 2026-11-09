namespace RaceDay.Web.Controllers;
// Controller responsible for displaying the home page and public events.
public class HomeController : AppController
{
    // Stores the API client used to communicate with the RaceDay API.
    private readonly ApiClient _api;

    // Constructor that receives the API client through dependency injection.
    public HomeController(ApiClient api) => _api = api;

    // Displays the home page and allows users to filter events by type and location.
    public async Task<IActionResult> Index(string? eventType, string? location)
    {
        // Redirect organisers to their dashboard when they access the home page.
        if (User.IsInRole("Organiser"))
            return RedirectToAction("Dashboard", "Organiser");

        // Retrieve events from the API using the selected filters.
        var result = await _api.GetEventsAsync(eventType, location);

        // Pass the event information and any error message to the home page.
        return View(new HomeViewModel
        {
            Events = result.Data ?? new List<EventResponse>(),
            EventType = eventType,
            Location = location,
            Error = result.Success ? null : result.Error
        });
    }

    // Displays a page when the service is temporarily unavailable.
    public IActionResult ServiceUnavailable() => View();

    // Displays the general error page without requiring an antiforgery token.
    [IgnoreAntiforgeryToken]
    public IActionResult Error() => View();

    // Displays a status page based on the HTTP status code received.
    [IgnoreAntiforgeryToken]
    public IActionResult Status(int id)
    {
        // Store the status code so it can be displayed in the view.
        ViewData["Code"] = id;

        return View();
    }
}
    }
}
