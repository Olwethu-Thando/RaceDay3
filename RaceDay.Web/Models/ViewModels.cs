namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(150, MinimumLength = 2), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a password."), StringLength(72, MinimumLength = 8, ErrorMessage = "Password must be 8 to 72 characters."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password."), Compare(nameof(Password), ErrorMessage = "The passwords do not match."),
     DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a role.")]
    public string Role { get; set; } = "Participant";

    [RegularExpression(@"^[0-9+\s\-()]{7,20}$", ErrorMessage = "Enter a valid phone number."), Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [StringLength(150), Display(Name = "Organisation name")]
    public string? OrganisationName { get; set; }

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }
}

public class EventFormModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Give the event a name."), StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Keep the description under 2000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose a date and time."), Display(Name = "Date and time")]
    public DateTime EventDate { get; set; }

    [Required(ErrorMessage = "Enter the location."), StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "Distance must be greater than 0."), Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Required(ErrorMessage = "Choose an event type."), Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? CurrentBannerUrl { get; set; }
}

public class CategoryFormModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Give the category a name."), StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120, ErrorMessage = "Minimum age must be between 0 and 120."), Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Maximum age must be between 0 and 120."), Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Range(0.01, 100000, ErrorMessage = "Distance must be greater than 0."), Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }
}

public class ResultFormModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }

    [Range(0, 999, ErrorMessage = "Hours must be between 0 and 999.")]
    public int Hours { get; set; }

    [Range(0, 59, ErrorMessage = "Minutes must be between 0 and 59.")]
    public int Minutes { get; set; }

    [Range(0, 59, ErrorMessage = "Seconds must be between 0 and 59.")]
    public int Seconds { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Position must be 1 or more.")]
    public int Position { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Total finishers must be 1 or more.")]
    public int TotalFinishers { get; set; }

    public int TotalSeconds => Hours * 3600 + Minutes * 60 + Seconds;
}

public class ProfileFormModel
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your full name."), StringLength(150, MinimumLength = 2), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^[0-9+\s\-()]{7,20}$", ErrorMessage = "Enter a valid phone number."), Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [StringLength(150), Display(Name = "Organisation name")]
    public string? OrganisationName { get; set; }

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public string? ProfilePictureUrl { get; set; }
}

// ----- page models -----

public class HomeViewModel
{
    public List<EventResponse> Events { get; set; } = new();
    public string? EventType { get; set; }
    public string? Location { get; set; }
    public string? Error { get; set; }
}

public class EventDetailsViewModel
{
    public EventResponse Event { get; set; } = null!;
    public EnrolmentResponse? ExistingEnrolment { get; set; }
    public List<ResultResponse> Results { get; set; } = new();
}

public class DashboardViewModel
{
    public List<EventResponse> AllEvents { get; set; } = new();
    public List<EventResponse> Upcoming { get; set; } = new();
    public int TotalEnrolments { get; set; }
}

public class CategoriesViewModel
{
    public EventResponse Event { get; set; } = null!;
    public CategoryFormModel NewCategory { get; set; } = new();
}

public class EnrolmentsViewModel
{
    public EventResponse Event { get; set; } = null!;
    public List<EnrolmentResponse> Enrolments { get; set; } = new();
}

public class ResultRow
{
    public EnrolmentResponse Enrolment { get; set; } = null!;
    public ResultResponse? Result { get; set; }
}

public class ResultsViewModel
{
    public EventResponse Event { get; set; } = null!;
    public List<ResultRow> Rows { get; set; } = new();
}
