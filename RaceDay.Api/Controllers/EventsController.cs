namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ApiControllerBase
{
    private readonly IImageStorage _storage;

    public EventsController(ApplicationDBContext db, IImageStorage storage) : base(db) => _storage = storage;

    /// <summary>List upcoming events (public).</summary>
    /// <remarks>Optional filters: eventType (Run, Walk, Cycle), location (contains), includePast (default false).</remarks>
    /// <response code="200">Array of events ordered by date.</response>
    /// <response code="400">Unknown eventType.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetEvents(
        [FromQuery] string? eventType, [FromQuery] string? location, [FromQuery] bool includePast = false)
    {
        var query = Db.Events.AsNoTracking().AsQueryable();

        if (!includePast)
        {
            var now = DateTime.Now;
            query = query.Where(e => e.EventDate >= now);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = Allowed.Match(eventType, EventTypes.All);
            if (type is null) return Fail(StatusCodes.Status400BadRequest, "Invalid event type", "eventType must be Run, Walk or Cycle.");
            query = query.Where(e => e.EventType == type);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim();
            query = query.Where(e => e.Location.Contains(loc));
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToResponse()));
    }

    /// <summary>Get one event with its categories (public).</summary>
    /// <response code="200">The event, including its categories.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetEventById(int id)
    {
        var ev = await Db.Events.AsNoTracking().Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        return ev is null ? Fail(StatusCodes.Status404NotFound, "Event not found") : Ok(ev.ToResponse(includeCategories: true));
    }

    /// <summary>List the events I created, with enrolment totals (Organiser dashboard).</summary>
    /// <response code="200">Array of my events including enrolmentCount.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetMyEvents()
    {
        var userId = CurrentUserId;
        var rows = await Db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == userId)
            .OrderBy(e => e.EventDate)
            .Select(e => new { Event = e, Count = e.Enrolments.Count })
            .ToListAsync();
        return Ok(rows.Select(r => r.Event.ToResponse(r.Count)));
    }

    /// <summary>Create an event owned by the logged-in Organiser.</summary>
    /// <remarks>Body: name, description, eventDate, location, distanceKm, eventType (Run, Walk or Cycle).</remarks>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> CreateEvent([FromBody] EventRequest request)
    {
        var type = Allowed.Match(request.EventType, EventTypes.All);
        if (type is null) return Fail(StatusCodes.Status400BadRequest, "Invalid event type", "eventType must be Run, Walk or Cycle.");

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description,
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = type
        };
        Db.Events.Add(ev);
        await Db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEventById), new { id = ev.EventId }, ev.ToResponse());
    }

    /// <summary>Update an event I own.</summary>
    /// <response code="200">Updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> UpdateEvent(int id, [FromBody] EventRequest request)
    {
        var type = Allowed.Match(request.EventType, EventTypes.All);
        if (type is null) return Fail(StatusCodes.Status400BadRequest, "Invalid event type", "eventType must be Run, Walk or Cycle.");

        var (ev, error) = await GetOwnedEventAsync(id);
        if (error is not null) return error;

        ev!.Name = request.Name.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = type;
        await Db.SaveChangesAsync();
        return Ok(ev.ToResponse());
    }

    /// <summary>Delete an event I own (its categories, enrolments and results are removed too).</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var (ev, error) = await GetOwnedEventAsync(id);
        if (error is not null) return error;

        var enrolments = await Db.Enrolments.Include(e => e.Result).Where(e => e.EventId == id).ToListAsync();
        Db.Enrolments.RemoveRange(enrolments);
        Db.Events.Remove(ev!);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Upload a banner image for an event I own.</summary>
    /// <remarks>multipart/form-data with an "image" file (JPG, PNG or WEBP, max 5 MB).</remarks>
    /// <response code="200">Returns the banner URL.</response>
    /// <response code="400">Missing or invalid image.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BannerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BannerResponse>> UploadBanner(int id, [FromForm] IFormFile image, CancellationToken ct)
    {
        var (ev, error) = await GetOwnedEventAsync(id);
        if (error is not null) return error;

        var invalid = ImageRules.Validate(image);
        if (invalid is not null) return Fail(StatusCodes.Status400BadRequest, "Invalid image", invalid);

        ev!.BannerImageUrl = await _storage.SaveAsync(image, "banners", ct);
        await Db.SaveChangesAsync(ct);
        return Ok(new BannerResponse(ev.BannerImageUrl));
    }
}
