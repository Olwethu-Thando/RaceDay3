namespace RaceDay.Web.Helpers;

public static class Format
{
    public static string Ordinal(int n)
    {
        var mod100 = n % 100;
        if (mod100 is >= 11 and <= 13) return $"{n}th";
        return (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" };
    }

    public static string DateTimeText(DateTime d) => d.ToString("ddd, d MMM yyyy 'at' HH:mm");
    public static string DateText(DateTime d) => d.ToString("d MMM yyyy");

    public static string AgeRange(int? min, int? max) => (min, max) switch
    {
        (null, null) => "All ages",
        (int a, null) => $"{a}+ years",
        (null, int b) => $"Up to {b} years",
        (int a, int b) => $"{a}-{b} years"
    };
}
