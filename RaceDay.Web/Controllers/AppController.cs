namespace RaceDay.Web.Controllers;

public abstract class AppController : Controller
{
    protected int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;
}

/// <summary>Base for every Organiser-only controller: role check plus "does this event belong to me?" helper.</summary>
[Authorize(Roles = "Organiser")]
public abstract class OrganiserControllerBase : AppController
{
    protected readonly ApiClient Api;

    protected OrganiserControllerBase(ApiClient api) => Api = api;

    protected async Task<(EventResponse? Event, IActionResult? Error)> LoadOwnedEventAsync(int eventId)
    {
        var result = await Api.GetEventAsync(eventId);
        if (!result.Success || result.Data is null) return (null, NotFound());
        if (result.Data.OrganiserId != CurrentUserId) return (null, RedirectToAction("AccessDenied", "Account"));
        return (result.Data, null);
    }
}
