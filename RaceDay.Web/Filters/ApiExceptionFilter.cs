// Import authentication features for managing user sessions.
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

// Import filter features for handling application exceptions.
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Web.Filters;

/// <summary>
/// Handles API errors and redirects users to friendly pages instead of allowing the application to crash.
/// </summary>
public class ApiExceptionFilter : IAsyncExceptionFilter
{
    // Runs when an unhandled exception occurs in the application.
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        // Check which type of exception was returned by the API.
        switch (context.Exception)
        {
            case ApiUnauthorizedException:

                // Clear the current session if the API session has expired or is no longer valid.
                context.HttpContext.Session.Clear();

                // Sign the user out of the website's cookie authentication system.
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Redirect the user to the login page and indicate that the session has expired.
                context.Result = new RedirectToActionResult("Login", "Account", new { expired = true });

                // Mark the exception as handled so it does not cause further errors.
                context.ExceptionHandled = true;

                break;

            case ApiUnavailableException:

                // Redirect the user to the service unavailable page if the API cannot be reached.
                context.Result = new RedirectToActionResult("ServiceUnavailable", "Home", null);

                // Mark the exception as handled to prevent it from causing an application crash.
                context.ExceptionHandled = true;

                break;
        }
    }
}