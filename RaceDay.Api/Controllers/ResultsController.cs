namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class ResultsController : ApiControllerBase
{
    public ResultsController(ApplicationDBContext db) : base(db) { }

    /// <summary>Capture a participant's finish time and position (Organisers only, for events they own).</summary>
    /// <remarks>Body: finishTimeSeconds, finishPosition, totalFinishers.</remarks>
    /// <response code="201">Result captured.</response>
    /// <response code="400">Invalid numbers, or the enrolment was cancelled.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Enrolment not found.</response>
    /// <response code="409">A result already exists for this enrolment (use PUT /api/results/{id}).</response>
    [HttpPost("enrolments/{id:int}/result")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CaptureResult(int id, [FromBody] ResultRequest request)
    {
        var enrolment = await Db.Enrolments.IncludeDetails().Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return Fail(StatusCodes.Status404NotFound, "Enrolment not found");
        if (enrolment.Event.OrganiserId != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You do not own the event for this enrolment.");
        if (enrolment.Status == EnrolmentStatuses.Cancelled)
            return Fail(StatusCodes.Status400BadRequest, "Enrolment cancelled", "Results cannot be captured for a cancelled enrolment.");
        if (enrolment.Result is not null)
            return Fail(StatusCodes.Status409Conflict, "Result already captured", "Use PUT /api/results/{id} to correct it.");

        var invalid = ValidatePosition(request);
        if (invalid is not null) return invalid;

        var result = new Result
        {
            Enrolment = enrolment,
            FinishTimeSeconds = request.FinishTimeSeconds,
            FinishPosition = request.FinishPosition,
            TotalFinishers = request.TotalFinishers,
            CapturedDate = DateTime.UtcNow
        };
        Db.Results.Add(result);
        await Db.SaveChangesAsync();
        return Created($"/api/results/{result.ResultId}", result.ToResponse());
    }

    /// <summary>Correct a previously captured result (Organisers only, for events they own).</summary>
    /// <response code="200">Updated result.</response>
    /// <response code="400">Invalid numbers.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Result not found.</response>
    [HttpPut("results/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> UpdateResult(int id, [FromBody] ResultRequest request)
    {
        var result = await Db.Results.IncludeDetails().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return Fail(StatusCodes.Status404NotFound, "Result not found");
        if (result.Enrolment.Event.OrganiserId != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You do not own the event for this result.");

        var invalid = ValidatePosition(request);
        if (invalid is not null) return invalid;

        result.FinishTimeSeconds = request.FinishTimeSeconds;
        result.FinishPosition = request.FinishPosition;
        result.TotalFinishers = request.TotalFinishers;
        await Db.SaveChangesAsync();
        return Ok(result.ToResponse());
    }

    /// <summary>View my own race history (Participants only).</summary>
    /// <response code="200">My results across all events, newest first.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("participants/me/results")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMyResults()
    {
        var userId = CurrentUserId;
        var list = await Db.Results.AsNoTracking().IncludeDetails()
            .Where(r => r.Enrolment.ParticipantId == userId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .ToListAsync();
        return Ok(list.Select(r => r.ToResponse()));
    }

    /// <summary>Public leaderboard: all results for an event, ordered by finishing position.</summary>
    /// <response code="200">Array of results.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("events/{eventId:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetEventResults(int eventId)
    {
        if (!await Db.Events.AnyAsync(e => e.EventId == eventId))
            return Fail(StatusCodes.Status404NotFound, "Event not found");

        var list = await Db.Results.AsNoTracking().IncludeDetails()
            .Where(r => r.Enrolment.EventId == eventId)
            .OrderBy(r => r.FinishPosition)
            .ToListAsync();
        return Ok(list.Select(r => r.ToResponse()));
    }

    private ObjectResult? ValidatePosition(ResultRequest r) =>
        r.FinishPosition > r.TotalFinishers
            ? Fail(StatusCodes.Status400BadRequest, "Invalid position", "finishPosition cannot be greater than totalFinishers.")
            : null;
}
