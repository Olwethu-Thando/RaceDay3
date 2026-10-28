namespace RaceDay.Web.Services;

public static class SessionKeys
{
    public const string ApiCookie = "ApiCookie";
    public const string ApiCookieName = "RaceDayAuth";
}

public class ApiUnauthorizedException : Exception { }
public class ApiUnavailableException : Exception { }

public class ApiResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T? data, int status = 200) => new() { Success = true, StatusCode = status, Data = data };
    public static ApiResult<T> Fail(int status, string error) => new() { Success = false, StatusCode = status, Error = error };
}

public record LoginOutcome(AuthResponse Auth, string ApiCookie);
