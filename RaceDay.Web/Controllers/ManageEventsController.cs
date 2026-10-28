namespace RaceDay.Web.Controllers;

public class ManageEventsController : OrganiserControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public ManageEventsController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Success) FlashError(result.Error ?? "We couldn't load your events.");
        return View((result.Data ?? new List<EventResponse>()).OrderBy(e => e.EventDate).ToList());
    }

    [HttpGet]
    public IActionResult Create() =>
        View(new EventFormModel { EventDate = DateTime.Today.AddDays(30).AddHours(8) });

    [HttpPost]
    public async Task<IActionResult> Create(EventFormModel model)
    {
        ValidateBanner(model);
        if (!ModelState.IsValid) return View(model);

        var created = await Api.CreateEventAsync(model);
        if (!created.Success || created.Data is null)
        {
            ModelState.AddModelError(string.Empty, created.Error ?? "We couldn't create the event. Please try again.");
            return View(model);
        }

        await SaveBannerAsync(created.Data.EventId, created.Data.Name, model.Banner, "created");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (ev, error) = await LoadOwnedEventAsync(id);
        if (ev is null) return error!;

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

    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventFormModel model)
    {
        model.EventId = id;
        ValidateBanner(model);
        if (!ModelState.IsValid) return View(model);

        var updated = await Api.UpdateEventAsync(id, model);
        if (!updated.Success || updated.Data is null)
        {
            ModelState.AddModelError(string.Empty, updated.Error ?? "We couldn't save your changes. Please try again.");
            return View(model);
        }

        await SaveBannerAsync(id, updated.Data.Name, model.Banner, "updated");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteEventAsync(id);
        if (result.Success) FlashSuccess("The event was deleted.");
        else FlashError(result.Error ?? "We couldn't delete the event.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateBanner(EventFormModel model)
    {
        var file = model.Banner;
        if (file is null || file.Length == 0) return;

        if (file.Length > MaxImageBytes)
            ModelState.AddModelError(nameof(model.Banner), "The banner must be 5 MB or smaller.");
        else if (!ImageExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            ModelState.AddModelError(nameof(model.Banner), "Use a JPG, PNG or WEBP image.");
    }

    // The event is already saved by the time we get here, so a failed upload is reported but never loses the event.
    private async Task SaveBannerAsync(int eventId, string name, IFormFile? banner, string verb)
    {
        if (banner is null || banner.Length == 0)
        {
            FlashSuccess($"'{name}' was {verb}.");
            return;
        }

        var upload = await Api.UploadBannerAsync(eventId, banner);
        if (upload.Success) FlashSuccess($"'{name}' was {verb}, and the banner was uploaded.");
        else FlashError($"'{name}' was {verb}, but the banner could not be uploaded: {upload.Error}");
    }
}
