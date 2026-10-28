using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Web.Filters;

/// <summary>Turns API problems into friendly redirects instead of crashes.</summary>
public class ApiExceptionFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ApiUnauthorizedException:
                // The API session ended (or never started): clear ours and ask the user to log in again.
                context.HttpContext.Session.Clear();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = new RedirectToActionResult("Login", "Account", new { expired = true });
                context.ExceptionHandled = true;
                break;

            case ApiUnavailableException:
                context.Result = new RedirectToActionResult("ServiceUnavailable", "Home", null);
                context.ExceptionHandled = true;
                break;
        }
    }
}
