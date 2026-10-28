namespace RaceDay.Api.Models;

public class Organiser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public string? OrganisationName { get; set; }

    public AppUser User { get; set; } = null!;
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
