namespace RaceDay.Api.Models;

/// <summary>Base authentication record shared by both roles (table: Users).</summary>
public class AppUser
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public Organiser? Organiser { get; set; }
    public Participant? Participant { get; set; }
}
