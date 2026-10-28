namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class EnrolmentsController : ApiControllerBase
{
    public EnrolmentsController(ApplicationDBContext db) : base(db) { }

    /// <summary>Enrol myself in an event under a chosen category (Participants only).</summary>
    /// <remarks>
    /// Body: categoryId. The participant is taken from the session, never from the request.
    /// If the category has an age range, the participant's date of birth (profile) must fit it on the event date.
    /// </remarks>
    /// <response code="201">Enrolment created with status Pending.</response>
    /// <response code="400">Category belongs to another event, event already happened, or age not eligible.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="404">Event or category not found.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("events/{eventId:int}/enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(int eventId, [FromBody] EnrolRequest request)
    {
        var ev = await Db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(StatusCodes.Status404NotFound, "Event not found");

        var category = await Db.Categories.FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null) return Fail(StatusCodes.Status404NotFound, "Category not found");
        if (category.EventId != eventId)
            return Fail(StatusCodes.Status400BadRequest, "Invalid category", "This category does not belong to the selected event.");

        if (ev.EventDate < DateTime.Now)
            return Fail(StatusCodes.Status400BadRequest, "Event closed", "This event has already taken place.");

        var participantId = CurrentUserId;
        var participant = await Db.Participants.FirstOrDefaultAsync(p => p.UserId == participantId);
        if (participant is null) return Fail(StatusCodes.Status401Unauthorized, "Account no longer exists");

        if (await Db.Enrolments.AnyAsync(e => e.ParticipantId == participantId && e.EventId == eventId))
            return Fail(StatusCodes.Status409Conflict, "Already enrolled", "You are already enrolled in this event.");

        if (category.MinAge.HasValue || category.MaxAge.HasValue)
        {
            if (participant.DateOfBirth is null)
                return Fail(StatusCodes.Status400BadRequest, "Date of birth required",
                    "This category has an age range. Add your date of birth to your profile first.");

            var age = AgeOn(participant.DateOfBirth.Value, DateOnly.FromDateTime(ev.EventDate));
            if ((category.MinAge.HasValue && age < category.MinAge) || (category.MaxAge.HasValue && age > category.MaxAge))
                return Fail(StatusCodes.Status400BadRequest, "Not eligible", $"You will be {age} on the event date, which is outside this category's age range.");
        }

        var enrolment = new Enrolment
        {
            Participant = participant,
            Event = ev,
            Category = category,
            Status = EnrolmentStatuses.Pending,
            EnrolmentDate = DateTime.UtcNow
        };
        Db.Enrolments.Add(enrolment);
        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Fail(StatusCodes.Status409Conflict, "Already enrolled", "You are already enrolled in this event.");
        }

        return Created($"/api/participants/me/enrolments", enrolment.ToResponse());
    }

    /// <summary>List the events I have enrolled for, with status (Participants only).</summary>
    /// <response code="200">Array of my enrolments.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("participants/me/enrolments")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMyEnrolments()
    {
        var userId = CurrentUserId;
        var list = await Db.Enrolments.AsNoTracking().IncludeDetails()
            .Where(e => e.ParticipantId == userId)
            .OrderBy(e => e.Event.EventDate)
            .ToListAsync();
        return Ok(list.Select(e => e.ToResponse()));
    }

    /// <summary>List everyone enrolled in an event I own (Organisers only).</summary>
    /// <response code="200">Array of enrolments with participant and category detail.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetEventEnrolments(int eventId)
    {
        var (_, error) = await GetOwnedEventAsync(eventId);
        if (error is not null) return error;

        var list = await Db.Enrolments.AsNoTracking().IncludeDetails()
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(list.Select(e => e.ToResponse()));
    }

    /// <summary>Change an enrolment's status for an event I own (Organisers only).</summary>
    /// <remarks>Body: status (Pending, Confirmed or Cancelled).</remarks>
    /// <response code="200">Updated enrolment.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Enrolment not found.</response>
    [HttpPut("enrolments/{id:int}/status")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentResponse>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var status = Allowed.Match(request.Status, EnrolmentStatuses.All);
        if (status is null)
            return Fail(StatusCodes.Status400BadRequest, "Invalid status", "Status must be Pending, Confirmed or Cancelled.");

        var enrolment = await Db.Enrolments.IncludeDetails().FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return Fail(StatusCodes.Status404NotFound, "Enrolment not found");
        if (enrolment.Event.OrganiserId != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You do not own the event for this enrolment.");

        enrolment.Status = status;
        await Db.SaveChangesAsync();
        return Ok(enrolment.ToResponse());
    }

    private static int AgeOn(DateOnly dateOfBirth, DateOnly on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (dateOfBirth.AddYears(age) > on) age--;
        return age;
    }
}
