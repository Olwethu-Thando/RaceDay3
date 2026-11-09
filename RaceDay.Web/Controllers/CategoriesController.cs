// Defines the namespace for the RaceDay web controllers.
namespace RaceDay.Web.Controllers;

// Controller responsible for managing event categories.
// Only organisers can access this controller.
public class CategoriesController : OrganiserControllerBase
{
    // Constructor that receives the API client from the base controller.
    public CategoriesController(ApiClient api) : base(api) { }

    // Displays all categories belonging to a selected event.
    public async Task<IActionResult> Index(int eventId)
    {
        // Load the event and check whether it belongs to the current organiser.
        var (ev, error) = await LoadOwnedEventAsync(eventId);

        // Return an error if the event could not be found or accessed.
        if (ev is null) return error!;

        // Prepare the view model with event details and an empty category form.
        return View(new CategoriesViewModel
        {
            Event = ev,
            NewCategory = new CategoryFormModel { EventId = eventId }
        });
    }

    // Handles the creation of a new category for an event.
    [HttpPost]
    public async Task<IActionResult> Create(int eventId, CategoryFormModel model)
    {
        // Check that the selected event belongs to the current organiser.
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (ev is null) return error!;

        // Assign the event ID to the category being created.
        model.EventId = eventId;

        // Check that the minimum age is not greater than the maximum age.
        ValidateAges(model);

        // Return to the category page if the submitted information is invalid.
        if (!ModelState.IsValid)
            return View("Index", new CategoriesViewModel { Event = ev, NewCategory = model });

        // Send the new category details to the API.
        var result = await Api.CreateCategoryAsync(eventId, model);

        // Display an error if the category could not be created.
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't add the category.");
            return View("Index", new CategoriesViewModel { Event = ev, NewCategory = model });
        }

        // Display a success message after adding the category.
        FlashSuccess($"Category '{model.Name}' was added.");

        // Redirect back to the category page for the selected event.
        return RedirectToAction(nameof(Index), new { eventId });
    }

    // Displays the edit form for an existing event category.
    [HttpGet]
    public async Task<IActionResult> Edit(int eventId, int id)
    {
        // Load the event and confirm that it belongs to the current organiser.
        var (ev, error) = await LoadOwnedEventAsync(eventId);
        if (ev is null) return error!;

        // Find the category using its unique category ID.
        var category = ev.Categories?.FirstOrDefault(c => c.CategoryId == id);

        // Return a not found response if the category does not exist.
        if (category is null) return NotFound();

        // Store the event name so it can be displayed in the edit view.
        ViewData["EventName"] = ev.Name;

        // Load the existing category details into the edit form.
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

    // Handles the submission of updated category information.
    [HttpPost]
    public async Task<IActionResult> Edit(int eventId, int id, CategoryFormModel model)
    {
        // Set the category and event IDs before processing the form.
        model.CategoryId = id;
        model.EventId = eventId;

        // Validate the minimum and maximum age values.
        ValidateAges(model);

        // Return to the edit page if the submitted information is invalid.
        if (!ModelState.IsValid) return View(model);

        // Send the updated category details to the API.
        var result = await Api.UpdateCategoryAsync(id, model);

        // Display an error if the category could not be updated.
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't save the category.");
            return View(model);
        }

        // Display a success message after updating the category.
        FlashSuccess("The category was updated.");

        // Redirect back to the category list for the selected event.
        return RedirectToAction(nameof(Index), new { eventId });
    }

    // Handles the removal of a category from an event.
    [HttpPost]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        // Send a request to the API to delete the selected category.
        var result = await Api.DeleteCategoryAsync(id);

        // Display a success or error message depending on the API response.
        if (result.Success)
            FlashSuccess("The category was removed.");
        else
            FlashError(result.Error ?? "We couldn't remove the category.");

        // Return to the category page for the selected event.
        return RedirectToAction(nameof(Index), new { eventId });
    }

    // Checks whether the minimum age is greater than the maximum age.
    private void ValidateAges(CategoryFormModel model)
    {
        // Add a validation error if the age range is incorrect.
        if (model.MinAge.HasValue && model.MaxAge.HasValue && model.MinAge > model.MaxAge)
            ModelState.AddModelError(nameof(model.MaxAge), "Maximum age must be at least the minimum age.");
    }
}