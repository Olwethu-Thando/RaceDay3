namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly IImageStorage _storage;

    public UsersController(ApplicationDBContext db, IImageStorage storage) : base(db) => _storage = storage;

    /// <summary>View my own profile (works for both roles).</summary>
    /// <response code="200">Profile merged with the role-specific fields. The password hash is never returned.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> GetMe()
    {
        var user = await LoadUserAsync();
        return user is null ? Fail(StatusCodes.Status401Unauthorized, "Account no longer exists") : Ok(ToProfile(user));
    }

    /// <summary>Update my own profile.</summary>
    /// <remarks>Body: fullName, contactNumber, organisationName (Organisers) or dateOfBirth (Participants). The user id always comes from the session, never from the request.</remarks>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await LoadUserAsync();
        if (user is null) return Fail(StatusCodes.Status401Unauthorized, "Account no longer exists");

        if (request.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.Today))
            return Fail(StatusCodes.Status400BadRequest, "Invalid date of birth", "Date of birth cannot be in the future.");

        if (user.Organiser is not null)
        {
            user.Organiser.FullName = request.FullName.Trim();
            user.Organiser.ContactNumber = request.ContactNumber;
            user.Organiser.OrganisationName = request.OrganisationName;
        }
        else if (user.Participant is not null)
        {
            user.Participant.FullName = request.FullName.Trim();
            user.Participant.ContactNumber = request.ContactNumber;
            user.Participant.DateOfBirth = request.DateOfBirth;
        }

        await Db.SaveChangesAsync();
        return Ok(ToProfile(user));
    }

    /// <summary>Upload my profile picture (Participants only).</summary>
    /// <remarks>multipart/form-data with an "image" file (JPG, PNG or WEBP, max 5 MB).</remarks>
    /// <response code="200">Returns the stored picture URL.</response>
    /// <response code="400">Missing or invalid image.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpPost("me/profilepicture")]
    [Authorize(Roles = Roles.Participant)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfilePictureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProfilePictureResponse>> UploadProfilePicture([FromForm] IFormFile image, CancellationToken ct)
    {
        var error = ImageRules.Validate(image);
        if (error is not null) return Fail(StatusCodes.Status400BadRequest, "Invalid image", error);

        var participant = await Db.Participants.FirstOrDefaultAsync(p => p.UserId == CurrentUserId, ct);
        if (participant is null) return Fail(StatusCodes.Status401Unauthorized, "Account no longer exists");

        participant.ProfilePictureUrl = await _storage.SaveAsync(image, "profiles", ct);
        await Db.SaveChangesAsync(ct);
        return Ok(new ProfilePictureResponse(participant.ProfilePictureUrl));
    }

    private Task<AppUser?> LoadUserAsync() =>
        Db.Users.Include(u => u.Organiser).Include(u => u.Participant).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);

    private static ProfileResponse ToProfile(AppUser u) => new(
        u.UserId, u.Email, u.Role,
        u.Organiser?.FullName ?? u.Participant?.FullName ?? string.Empty,
        u.Organiser?.ContactNumber ?? u.Participant?.ContactNumber,
        u.Organiser?.OrganisationName,
        u.Participant?.DateOfBirth,
        u.Participant?.ProfilePictureUrl,
        u.CreatedDate);
}
