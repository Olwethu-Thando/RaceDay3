// Defines the namespace for the RaceDay web controllers.
namespace RaceDay.Web.Controllers;

// Controller responsible for creating, viewing, editing and deleting events.
// Only organisers can access this controller.
public class ManageEventsController : OrganiserControllerBase
{
    // Sets the maximum allowed banner image size to 5 MB.
    private const long MaxImageBytes = 5 * 1024 * 1024;

    // Lists the image formats allowed when uploading event banners.
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    // Constructor that receives the API client from the base controller.
    public ManageEventsController(ApiClient api) : base(api) { }

    // Displays all events created by the current organiser.
    public async Task<IActionResult> Index()
    {
        // Retrieve the organiser's events from the API.
        var result = await Api.GetMyEventsAsync();

        // Display an error message if the events could not be loaded.
        if (!result.Success)
            FlashError(result.Error ?? "We couldn't load your events.");

        // Sort the events by date before displaying them.
        return View((result.Data ?? new List<EventResponse>()).OrderBy(e => e.EventDate).ToList());
    }

    // Displays the form for creating a new event.
    [HttpGet]
    public IActionResult Create() =>
        // Set the default event date to 30 days from today at 8 AM.
        View(new EventFormModel { EventDate = DateTime.Today.AddDays(30).AddHours(8) });

    // Handles the submission of the new event form.
    [HttpPost]
    public async Task<IActionResult> Create(EventFormModel model)
    {
        // Check whether the uploaded banner image meets the requirements.
        ValidateBanner(model);

        // Return to the form if any validation errors are found.
        if (!ModelState.IsValid) return View(model);

        // Send the new event details to the API.
        var created = await Api.CreateEventAsync(model);

        // Display an error if the event could not be created.
        if (!created.Success || created.Data is null)
        {
            ModelState.AddModelError(string.Empty, created.Error ?? "We couldn't create the event. Please try again.");
            return View(model);
        }

        // Upload the banner image after the event has been successfully created.
        await SaveBannerAsync(created.Data.EventId, created.Data.Name, model.Banner, "created");

        // Redirect back to the organiser's event list.
        return RedirectToAction(nameof(Index));
    }

    // Displays the edit form for an existing event.
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        // Load the selected event and check that it belongs to the current organiser.
        var (ev, error) = await LoadOwnedEventAsync(id);

        // Return an error if the event cannot be found or accessed.
        if (ev is null) return error!;

        // Load the existing event details into the edit form.
        return View(new EventFormModel
        {
            EventId = ev.EventId,
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            CurrentBannerUrl = ev.BannerImageUrl
        });
    }

    // Handles the submission of updated event information.
    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventFormModel model)
    {
        // Assign the selected event ID to the form model.
        model.EventId = id;

        // Validate the uploaded banner image.
        ValidateBanner(model);

        // Return to the edit form if the information is invalid.
        if (!ModelState.IsValid) return View(model);

        // Send the updated event information to the API.
        var updated = await Api.UpdateEventAsync(id, model);

        // Display an error if the event could not be updated.
        if (!updated.Success || updated.Data is null)
        {
            ModelState.AddModelError(string.Empty, updated.Error ?? "We couldn't save your changes. Please try again.");
            return View(model);
        }

        // Upload a new banner if one was selected.
        await SaveBannerAsync(id, updated.Data.Name, model.Banner, "updated");

        // Redirect back to the organiser's event list.
        return RedirectToAction(nameof(Index));
    }

    // Handles the deletion of an event.
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        // Send a request to the API to delete the selected event.
        var result = await Api.DeleteEventAsync(id);

        // Display a success message if the event was deleted.
        if (result.Success)
            FlashSuccess("The event was deleted.");

        // Display an error message if the deletion was unsuccessful.
        else
            FlashError(result.Error ?? "We couldn't delete the event.");

        // Redirect back to the event list.
        return RedirectToAction(nameof(Index));
    }

    // Checks whether the uploaded banner image meets the required conditions.
    private void ValidateBanner(EventFormModel model)
    {
        // Get the banner image selected by the organiser.
        var file = model.Banner;

        // Stop checking if no image was uploaded.
        if (file is null || file.Length == 0) return;

        // Check that the image does not exceed the maximum file size of 5 MB.
        if (file.Length > MaxImageBytes)
            ModelState.AddModelError(nameof(model.Banner), "The banner must be 5 MB or smaller.");

        // Check that the uploaded image uses an allowed file extension.
        else if (!ImageExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            ModelState.AddModelError(nameof(model.Banner), "Use a JPG, PNG or WEBP image.");
    }

    // Uploads the event banner after the event has been saved.
    // If the upload fails, the event remains saved.
    private async Task SaveBannerAsync(int eventId, string name, IFormFile? banner, string verb)
    {
        // If no banner was selected, display a success message for the event.
        if (banner is null || banner.Length == 0)
        {
            FlashSuccess($"'{name}' was {verb}.");
            return;
        }

        // Send the selected banner image to the API for uploading.
        var upload = await Api.UploadBannerAsync(eventId, banner);

        // Display a success message if the banner was uploaded successfully.
        if (upload.Success)
            FlashSuccess($"'{name}' was {verb}, and the banner was uploaded.");

        // Display an error if the event was saved but the banner upload failed.
        else
            FlashError($"'{name}' was {verb}, but the banner could not be uploaded: {upload.Error}");
    }
}