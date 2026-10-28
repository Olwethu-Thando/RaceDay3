namespace RaceDay.Web.Controllers;

public class CategoriesController : OrganiserControllerBase
{
    public CategoriesController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Index(int eventId)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (ev is null) return error!;
        return View(new CategoriesViewModel { Event = ev, NewCategory = new CategoryFormModel { EventId = eventId } });
    }

    [HttpPost]
    public async Task<IActionResult> Create(int eventId, CategoryFormModel model)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (ev is null) return error!;

        model.EventId = eventId;
        ValidateAges(model);
        if (!ModelState.IsValid)
            return View("Index", new CategoriesViewModel { Event = ev, NewCategory = model });

        var result = await Api.CreateCategoryAsync(eventId, model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't add the category.");
            return View("Index", new CategoriesViewModel { Event = ev, NewCategory = model });
        }

        FlashSuccess($"Category '{model.Name}' was added.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int eventId, int id)
    {
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (ev is null) return error!;

        var category = ev.Categories?.FirstOrDefault(c => c.CategoryId == id);
        if (category is null) return NotFound();

        ViewData["EventName"] = ev.Name;
        return View(new CategoryFormModel
        {
            CategoryId = category.CategoryId,
            EventId = eventId,
            Name = category.Name,
            MinAge = category.MinAge,
            MaxAge = category.MaxAge,
            DistanceKm = category.DistanceKm
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int eventId, int id, CategoryFormModel model)
    {
        model.CategoryId = id;
        model.EventId = eventId;
        ValidateAges(model);
        if (!ModelState.IsValid) return View(model);

        var result = await Api.UpdateCategoryAsync(id, model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't save the category.");
            return View(model);
        }

        FlashSuccess("The category was updated.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var result = await Api.DeleteCategoryAsync(id);
        if (result.Success) FlashSuccess("The category was removed.");
        else FlashError(result.Error ?? "We couldn't remove the category.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    private void ValidateAges(CategoryFormModel model)
    {
        if (model.MinAge.HasValue && model.MaxAge.HasValue && model.MinAge > model.MaxAge)
            ModelState.AddModelError(nameof(model.MaxAge), "Maximum age must be at least the minimum age.");
    }
}
