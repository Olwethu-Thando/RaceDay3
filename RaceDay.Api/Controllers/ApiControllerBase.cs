namespace RaceDay.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ApiControllerBase(ApplicationDBContext db) => Db = db;

    protected ApplicationDBContext Db { get; }

    /// <summary>Id of the logged-in user, read from the session cookie.</summary>
    protected int CurrentUserId => User.GetUserId();

    protected ObjectResult Fail(int status, string title, string? detail = null) =>
        Problem(statusCode: status, title: title, detail: detail);

    /// <summary>Loads an event and checks the logged-in Organiser owns it (404 if missing, 403 if not the owner).</summary>
    protected async Task<(Event? Event, ObjectResult? Error)> GetOwnedEventAsync(int eventId)
    {
        var ev = await Db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return (null, Fail(StatusCodes.Status404NotFound, "Event not found"));
        if (ev.OrganiserId != CurrentUserId)
            return (null, Fail(StatusCodes.Status403Forbidden, "Forbidden", "You do not own this event."));
        return (ev, null);
    }
}
