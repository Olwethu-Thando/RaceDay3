using System.Security.Claims;

namespace RaceDay.Api.Common;

public static class UserClaimsExtensions
{
    /// <summary>Reads the logged-in user's id from the session cookie's claims (never from client input).</summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : 0;
    }
}
