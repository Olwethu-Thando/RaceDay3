// Defines the namespace for the RaceDay web controllers.
namespace RaceDay.Web.Controllers;

// Controller responsible for managing and publishing event results.
// Only organisers can access this controller.
public class ResultsController : OrganiserControllerBase
{
    // Constructor that receives the API client from the base controller.
    public ResultsController(ApiClient api) : base(api) { }

    // Displays participant enrolments and their results for a selected event.
    public async Task<IActionResult> Index(int eventId)
    {
        // Load the selected event and check that it belongs to the current organiser.
        var (ev, error) = await LoadOwnedEventAsync(eventId);

        // Return an error if the event cannot be found or accessed.
        if (ev is null) return error!;

        // Retrieve all participant enrolments for the selected event.
        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);

        // Retrieve the recorded results for the selected event.
        var results = await Api.GetEventResultsAsync(eventId);

        // Display an error message if the enrolments could not be loaded.
        if (!enrolments.Success)
            FlashError(enrolments.Error ?? "We couldn't load the enrolments.");

        // Create a dictionary to match each result with its enrolment ID.
        var byEnrolment = (results.Data ?? new List<ResultResponse>())
            .ToDictionary(r => r.EnrolmentId);

        // Create a list of result rows for participants who have not cancelled.
        var rows = (enrolments.Data ?? new List<EnrolmentResponse>())
            .Where(e => e.Status != "Cancelled")

            // Match each participant's enrolment with their recorded result.
            .Select(e => new ResultRow
            {
                Enrolment = e,
                Result = byEnrolment.GetValueOrDefault(e.EnrolmentId)
            })

            // Sort participants according to their finishing positions.
            // Participants without results are placed at the bottom.
            .OrderBy(r => r.Result?.FinishPosition ?? int.MaxValue)

            // Sort participants alphabetically when their positions are equal.
            .ThenBy(r => r.Enrolment.ParticipantName)
            .ToList();

        // Pass the event details and participant results to the view.
        return View(new ResultsViewModel { Event = ev, Rows = rows });
    }

    // Handles saving or updating a participant's event result.
    [HttpPost]
    public async Task<IActionResult> Save(ResultFormModel model)
    {
        // Check whether the submitted result information is valid.
        if (!ModelState.IsValid)
        {
            // Display the validation errors to the organiser.
            FlashError(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            // Redirect back to the results page.
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }

        // Make sure the participant's finishing time is greater than zero.
        if (model.TotalSeconds <= 0)
        {
            FlashError("Enter a finish time greater than zero.");
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }

        // Check that the finishing position does not exceed the total number of finishers.
        if (model.Position > model.TotalFinishers)
        {
            FlashError("The finishing position cannot be higher than the total number of finishers.");
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }

        // Check whether the result already exists.
        // If a result ID is available, update the existing result.
        // Otherwise, capture a new result for the participant.
        var result = model.ResultId.HasValue
            ? await Api.UpdateResultAsync(model.ResultId.Value, model.TotalSeconds, model.Position, model.TotalFinishers)
            : await Api.CaptureResultAsync(model.EnrolmentId, model.TotalSeconds, model.Position, model.TotalFinishers);

        // Display a success message if the result was saved successfully.
        if (result.Success)
            FlashSuccess("Result saved and published.");

        // Display an error message if the result could not be saved.
        else
            FlashError(result.Error ?? "We couldn't save that result.");

        // Redirect back to the results page for the selected event.
        return RedirectToAction(nameof(Index), new { eventId = model.EventId });
    }
}