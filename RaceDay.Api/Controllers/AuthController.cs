using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ApiControllerBase
{
    public AuthController(ApplicationDBContext db) : base(db) { }

    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Body: fullName, email, password (8-72 chars), role ("Organiser" or "Participant"), contactNumber,
    /// organisationName (Organisers) or dateOfBirth yyyy-MM-dd (Participants).
    /// The password is hashed with BCrypt before it is stored. The role is validated against the allowed list.
    /// </remarks>
    /// <response code="201">Account created; returns user id and role.</response>
    /// <response code="400">Validation failed (bad email, short password, invalid role...).</response>
    /// <response code="409">Email is already registered.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var role = Allowed.Match(request.Role, Roles.All);
        if (role is null)
            return Fail(StatusCodes.Status400BadRequest, "Invalid role", "Role must be either 'Organiser' or 'Participant'.");

        if (request.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.Today))
            return Fail(StatusCodes.Status400BadRequest, "Invalid date of birth", "Date of birth cannot be in the future.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await Db.Users.AnyAsync(u => u.Email == email))
            return Fail(StatusCodes.Status409Conflict, "Email already registered");

        var fullName = request.FullName.Trim();
        var user = new AppUser
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };

        if (role == Roles.Organiser)
            user.Organiser = new Organiser { FullName = fullName, ContactNumber = request.ContactNumber, OrganisationName = request.OrganisationName };
        else
            user.Participant = new Participant { FullName = fullName, ContactNumber = request.ContactNumber, DateOfBirth = request.DateOfBirth };

        Db.Users.Add(user);
        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two registrations with the same email at the same moment: the unique index wins.
            return Fail(StatusCodes.Status409Conflict, "Email already registered");
        }

        return Created("/api/users/me", new AuthResponse(user.UserId, role, fullName));
    }

    /// <summary>Log in and start a session.</summary>
    /// <remarks>Body: email and password. On success a session cookie (RaceDayAuth) storing the user id and role is issued.</remarks>
    /// <response code="200">Logged in; returns user id, role and name.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await Db.Users.Include(u => u.Organiser).Include(u => u.Participant)
            .FirstOrDefaultAsync(u => u.Email == email);

        // Same answer for "unknown email" and "wrong password" so accounts can't be probed.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Fail(StatusCodes.Status401Unauthorized, "Invalid credentials", "Email or password is incorrect.");

        var fullName = user.Organiser?.FullName ?? user.Participant?.FullName ?? user.Email;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Ok(new AuthResponse(user.UserId, user.Role, fullName));
    }

    /// <summary>End the current session.</summary>
    /// <response code="200">Session cleared.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out." });
    }
}
