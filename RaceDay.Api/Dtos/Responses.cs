using System.Text.Json.Serialization;

namespace RaceDay.Api.Dtos;

public record AuthResponse(int UserId, string Role, string FullName);

public record ProfileResponse(
    int UserId, string Email, string Role, string FullName, string? ContactNumber,
    string? OrganisationName, DateOnly? DateOfBirth, string? ProfilePictureUrl, DateTime CreatedDate);

public record ProfilePictureResponse(string ProfilePictureUrl);
public record BannerResponse(string BannerImageUrl);

public record CategoryResponse(int CategoryId, int EventId, string Name, int? MinAge, int? MaxAge, decimal? DistanceKm);

public record EventResponse(
    int EventId, int OrganiserId, string Name, string? Description, DateTime EventDate,
    string Location, decimal DistanceKm, string EventType, string? BannerImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? EnrolmentCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CategoryResponse>? Categories = null);

public record EnrolmentResponse(
    int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    int CategoryId, string CategoryName, int ParticipantId, string ParticipantName,
    DateTime EnrolmentDate, string Status);

public record ResultResponse(
    int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    int CategoryId, string CategoryName, int ParticipantId, string ParticipantName,
    int FinishTimeSeconds, string FinishTime, int FinishPosition, int TotalFinishers, DateTime CapturedDate);
