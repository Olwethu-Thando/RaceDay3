// Defines the namespace for the RaceDay web controllers.
namespace RaceDay.Web.Controllers;

// Controller responsible for managing participant enrolments.
// Only organisers can access this controller.
public class EnrolmentsController : OrganiserControllerBase
{
    // Constructor that receives the API client from the base controller.
    public EnrolmentsController(ApiClient api) : base(api) { }

    // Displays all enrolments for a selected event.
    public async Task<IActionResult> Index(int eventId)
    {
        // Load the selected event and check that it belongs to the current organiser.
        var (ev, error) = await LoadOwnedEventAsync(eventId);

        // Return an error if the event cannot be found or accessed.
        if (ev is null) return error!;

        // Retrieve all participant enrolments for the selected event.
        var result = await Api.GetEventEnrolmentsAsync(eventId);

        // Display an error message if the enrolments could not be loaded.
        if (!result.Success)
            FlashError(result.Error ?? "We couldn't load the enrolments.");

        // Pass the event details and enrolment list to the view.
        // An empty list is used if no enrolment data is returned.
        return View(new EnrolmentsViewModel
        {
            Event = ev,
            Enrolments = result.Data ?? new List<EnrolmentResponse>()
        });
    }

    // Updates the status of a participant's enrolment.
    [HttpPost]
    public async Task<IActionResult> SetStatus(int eventId, int id, string status)
    {
        // Send the enrolment ID and new status to the API.
        var result = await Api.SetEnrolmentStatusAsync(id, status);

        // Display a success message if the enrolment status was updated.
        if (result.Success)
            FlashSuccess($"Enrolment marked as {result.Data?.Status ?? status}.");

        // Display an error message if the status could not be updated.
        else
            FlashError(result.Error ?? "We couldn't update the enrolment.");

        // Redirect back to the enrolment list for the selected event.
        return RedirectToAction(nameof(Index), new { eventId });
    }
}