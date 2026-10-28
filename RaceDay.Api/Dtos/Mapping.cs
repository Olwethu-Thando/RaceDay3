namespace RaceDay.Api.Dtos;

public static class Mapping
{
    public static CategoryResponse ToResponse(this Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm);

    public static EventResponse ToResponse(this Event e, int? enrolmentCount = null, bool includeCategories = false) =>
        new(e.EventId, e.OrganiserId, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm,
            e.EventType, e.BannerImageUrl, enrolmentCount,
            includeCategories ? e.Categories.OrderBy(c => c.CategoryId).Select(c => c.ToResponse()).ToList() : null);

    /// <summary>Requires Event, Category and Participant to be loaded.</summary>
    public static EnrolmentResponse ToResponse(this Enrolment e) =>
        new(e.EnrolmentId, e.EventId, e.Event.Name, e.Event.EventDate, e.CategoryId, e.Category.Name,
            e.ParticipantId, e.Participant.FullName, e.EnrolmentDate, e.Status);

    /// <summary>Requires Enrolment (with Event, Category and Participant) to be loaded.</summary>
    public static ResultResponse ToResponse(this Result r)
    {
        var en = r.Enrolment;
        var ts = TimeSpan.FromSeconds(r.FinishTimeSeconds);
        var formatted = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        return new ResultResponse(r.ResultId, r.EnrolmentId, en.EventId, en.Event.Name, en.Event.EventDate,
            en.CategoryId, en.Category.Name, en.ParticipantId, en.Participant.FullName,
            r.FinishTimeSeconds, formatted, r.FinishPosition, r.TotalFinishers, r.CapturedDate);
    }

    public static IQueryable<Enrolment> IncludeDetails(this IQueryable<Enrolment> q) =>
        q.Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant);

    public static IQueryable<Result> IncludeDetails(this IQueryable<Result> q) =>
        q.Include(r => r.Enrolment).ThenInclude(e => e.Event)
         .Include(r => r.Enrolment).ThenInclude(e => e.Category)
         .Include(r => r.Enrolment).ThenInclude(e => e.Participant);
}
