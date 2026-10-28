using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    [StringLength(20)]
    public string? ContactNumber { get; set; }

    /// <summary>Organisers only.</summary>
    [StringLength(150)]
    public string? OrganisationName { get; set; }

    /// <summary>Participants only (format yyyy-MM-dd).</summary>
    public DateOnly? DateOfBirth { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? ContactNumber { get; set; }

    /// <summary>Organisers only.</summary>
    [StringLength(150)]
    public string? OrganisationName { get; set; }

    /// <summary>Participants only (format yyyy-MM-dd).</summary>
    public DateOnly? DateOfBirth { get; set; }
}

public class EventRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Range(0.01, 100000)]
    public decimal DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;
}

public class CategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    [Range(0.01, 100000)]
    public decimal? DistanceKm { get; set; }
}

public class EnrolRequest
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

public class UpdateStatusRequest
{
    /// <summary>Pending, Confirmed or Cancelled.</summary>
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class ResultRequest
{
    [Range(1, int.MaxValue)]
    public int FinishTimeSeconds { get; set; }

    [Range(1, int.MaxValue)]
    public int FinishPosition { get; set; }

    [Range(1, int.MaxValue)]
    public int TotalFinishers { get; set; }
}
