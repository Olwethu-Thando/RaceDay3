namespace RaceDay.Api.Models;

public class Participant
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? ProfilePictureUrl { get; set; }

    public AppUser User { get; set; } = null!;
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
