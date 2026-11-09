// Import authentication features for signing users in and out.
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace RaceDay.Web.Controllers;

// Handles user registration, login, logout and access permissions.
public class AccountController : AppController
{
    // Stores the API client used to communicate with the RaceDay API.
    private readonly ApiClient _api;

    // Constructor that receives the API client through dependency injection.
    public AccountController(ApiClient api) => _api = api;

    // Displays the login page.
    [HttpGet]
    public IActionResult Login(string? returnUrl, bool expired = false)
    {
        // If the user is already logged in, redirect them to the correct home page.
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToHome(User.FindFirst(ClaimTypes.Role)?.Value);

        // Display an error message if the user's session has expired.
        if (expired)
            FlashError("Your session has ended. Please log in again.");

        // Open the login page and keep the return URL if one was provided.
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // Handles the login form submission.
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Check whether the information entered by the user is valid.
        if (!ModelState.IsValid)
            return View(model);

        // Send the user's email and password to the API for authentication.
        var result = await _api.LoginAsync(model.Email, model.Password);

        // Display an error if the login was unsuccessful or no data was returned.
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        // Sign the user into the website after successful authentication.
        await SignInAsync(result.Data);

        // Return the user to the requested local page if a valid return URL exists.
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        // Otherwise, redirect the user according to their assigned role.
        return RedirectToHome(result.Data.Auth.Role);
    }

    // Displays the registration page.
    [HttpGet]
    public IActionResult Register(string? role)
    {
        // Redirect users who are already logged in to their correct home page.
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToHome(User.FindFirst(ClaimTypes.Role)?.Value);

        // Set the account role to Organiser or Participant.
        // Participant is selected by default if another role is provided.
        return View(new RegisterViewModel { Role = role == "Organiser" ? "Organiser" : "Participant" });
    }

    // Handles the registration form submission.
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Make sure the selected role is either Organiser or Participant.
        if (model.Role != "Organiser" && model.Role != "Participant")
            ModelState.AddModelError(nameof(model.Role), "Choose Organiser or Participant.");

        // Check that the date of birth is not a future date.
        if (model.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");

        // Return to the registration page if any validation errors are found.
        if (!ModelState.IsValid)
            return View(model);

        // Send the registration details to the API to create a new account.
        var registered = await _api.RegisterAsync(model);

        // Display an error if the account could not be created.
        if (!registered.Success)
        {
            ModelState.AddModelError(string.Empty, registered.Error ?? "We couldn't create your account. Please try again.");
            return View(model);
        }

        // Automatically log the user in after successful registration.
        var login = await _api.LoginAsync(model.Email, model.Password);

        // If automatic login fails, ask the user to log in manually.
        if (!login.Success || login.Data is null)
        {
            FlashSuccess("Your account was created. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        // Create the user's website session.
        await SignInAsync(login.Data);

        // Display a welcome message after successful registration and login.
        FlashSuccess($"Welcome to RaceDay, {login.Data.Auth.FullName}!");

        // Redirect the user according to their account role.
        return RedirectToHome(login.Data.Auth.Role);
    }

    // Handles the logout process when a user wants to leave their account.
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        try
        {
            // Send a logout request to the API.
            await _api.LogoutAsync();
        }
        // Ignore this exception if the user is already logged out on the API side.
        catch (ApiUnauthorizedException) { /* already logged out on the API side */ }

        // If the API cannot be reached, still end the local website session.
        catch (ApiUnavailableException) { /* API unreachable: still end our own session */ }

        // Clear all session information stored for the current user.
        HttpContext.Session.Clear();

        // Sign the user out of the website's cookie authentication system.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Display a confirmation message after logout.
        FlashSuccess("You have been logged out.");

        // Redirect the user back to the login page.
        return RedirectToAction(nameof(Login));
    }

    // Displays the access denied page when a user is not allowed to access a page.
    [HttpGet]
    public IActionResult AccessDenied() => View();

    // Creates the authenticated user session after a successful login.
    private async Task SignInAsync(LoginOutcome outcome)
    {
        // Clear any previous session information before starting a new session.
        HttpContext.Session.Clear();

        // Store the API authentication cookie in the current website session.
        HttpContext.Session.SetString(SessionKeys.ApiCookie, outcome.ApiCookie);

        // Create a list of claims containing the user's account information.
        var claims = new List<Claim>
        {
            // Store the user's unique ID.
            new(ClaimTypes.NameIdentifier, outcome.Auth.UserId.ToString()),

            // Store the user's full name.
            new(ClaimTypes.Name, outcome.Auth.FullName),

            // Store the user's role for access control.
            new(ClaimTypes.Role, outcome.Auth.Role)
        };

        // Create an identity using the cookie authentication scheme.
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Sign the user into the website using their authentication details.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    // Redirects the user to the correct page based on their account role.
    private IActionResult RedirectToHome(string? role) =>
        role == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
}