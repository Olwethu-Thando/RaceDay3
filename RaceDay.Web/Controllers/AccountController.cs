using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl, bool expired = false)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToHome(User.FindFirst(ClaimTypes.Role)?.Value);
        if (expired) FlashError("Your session has ended. Please log in again.");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.LoginAsync(model.Email, model.Password);
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        await SignInAsync(result.Data);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToHome(result.Data.Auth.Role);
    }

    [HttpGet]
    public IActionResult Register(string? role)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToHome(User.FindFirst(ClaimTypes.Role)?.Value);
        return View(new RegisterViewModel { Role = role == "Organiser" ? "Organiser" : "Participant" });
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role != "Organiser" && model.Role != "Participant")
            ModelState.AddModelError(nameof(model.Role), "Choose Organiser or Participant.");
        if (model.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        if (!ModelState.IsValid) return View(model);

        var registered = await _api.RegisterAsync(model);
        if (!registered.Success)
        {
            ModelState.AddModelError(string.Empty, registered.Error ?? "We couldn't create your account. Please try again.");
            return View(model);
        }

        var login = await _api.LoginAsync(model.Email, model.Password);
        if (!login.Success || login.Data is null)
        {
            FlashSuccess("Your account was created. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        await SignInAsync(login.Data);
        FlashSuccess($"Welcome to RaceDay, {login.Data.Auth.FullName}!");
        return RedirectToHome(login.Data.Auth.Role);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        try { await _api.LogoutAsync(); }
        catch (ApiUnauthorizedException) { /* already logged out on the API side */ }
        catch (ApiUnavailableException) { /* API unreachable: still end our own session */ }

        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        FlashSuccess("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task SignInAsync(LoginOutcome outcome)
    {
        HttpContext.Session.Clear();
        HttpContext.Session.SetString(SessionKeys.ApiCookie, outcome.ApiCookie);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, outcome.Auth.UserId.ToString()),
            new(ClaimTypes.Name, outcome.Auth.FullName),
            new(ClaimTypes.Role, outcome.Auth.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private IActionResult RedirectToHome(string? role) =>
        role == "Organiser" ? RedirectToAction("Dashboard", "Organiser") : RedirectToAction("Index", "Home");
}
