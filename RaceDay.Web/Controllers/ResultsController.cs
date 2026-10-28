namespace RaceDay.Web.Controllers;

public class ResultsController : OrganiserControllerBase
{
    public ResultsController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Index(int eventId)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (ev is null) return error!;

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        var results = await Api.GetEventResultsAsync(eventId);
        if (!enrolments.Success) FlashError(enrolments.Error ?? "We couldn't load the enrolments.");

        var byEnrolment = (results.Data ?? new List<ResultResponse>()).ToDictionary(r => r.EnrolmentId);
        var rows = (enrolments.Data ?? new List<EnrolmentResponse>())
            .Where(e => e.Status != "Cancelled")
            .Select(e => new ResultRow { Enrolment = e, Result = byEnrolment.GetValueOrDefault(e.EnrolmentId) })
            .OrderBy(r => r.Result?.FinishPosition ?? int.MaxValue)
            .ThenBy(r => r.Enrolment.ParticipantName)
            .ToList();

        return View(new ResultsViewModel { Event = ev, Rows = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Save(ResultFormModel model)
    {
        if (!ModelState.IsValid)
        {
            FlashError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }
        if (model.TotalSeconds <= 0)
        {
            FlashError("Enter a finish time greater than zero.");
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }
        if (model.Position > model.TotalFinishers)
        {
            FlashError("The finishing position cannot be higher than the total number of finishers.");
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }

        var result = model.ResultId.HasValue
            ? await Api.UpdateResultAsync(model.ResultId.Value, model.TotalSeconds, model.Position, model.TotalFinishers)
            : await Api.CaptureResultAsync(model.EnrolmentId, model.TotalSeconds, model.Position, model.TotalFinishers);

        if (result.Success) FlashSuccess("Result saved and published.");
        else FlashError(result.Error ?? "We couldn't save that result.");
        return RedirectToAction(nameof(Index), new { eventId = model.EventId });
    }
}
