namespace RaceDay.Api.Models;

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int FinishTimeSeconds { get; set; }
    public int FinishPosition { get; set; }
    public int TotalFinishers { get; set; }
    public DateTime CapturedDate { get; set; } = DateTime.UtcNow;

    public Enrolment Enrolment { get; set; } = null!;
}
