namespace RaceDay.Api.Common;

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
    public static readonly string[] All = { Run, Walk, Cycle };
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = { Pending, Confirmed, Cancelled };
}

public static class Allowed
{
    /// <summary>Case-insensitive match against a fixed list. Returns the canonical value, or null if not allowed.</summary>
    public static string? Match(string? input, string[] options) =>
        options.FirstOrDefault(o => string.Equals(o, input?.Trim(), StringComparison.OrdinalIgnoreCase));
}
