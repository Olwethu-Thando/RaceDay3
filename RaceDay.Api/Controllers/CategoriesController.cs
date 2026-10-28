namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class CategoriesController : ApiControllerBase
{
    public CategoriesController(ApplicationDBContext db) : base(db) { }

    /// <summary>List the categories available for an event (public).</summary>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetCategories(int eventId)
    {
        if (!await Db.Events.AnyAsync(e => e.EventId == eventId))
            return Fail(StatusCodes.Status404NotFound, "Event not found");

        var categories = await Db.Categories.AsNoTracking().Where(c => c.EventId == eventId).OrderBy(c => c.CategoryId).ToListAsync();
        return Ok(categories.Select(c => c.ToResponse()));
    }

    /// <summary>Add an age or distance category to an event I own.</summary>
    /// <remarks>Body: name (e.g. "Under 20", "21km"), minAge, maxAge, distanceKm (all optional except name).</remarks>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed (e.g. minAge greater than maxAge).</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(int eventId, [FromBody] CategoryRequest request)
    {
        var (_, error) = await GetOwnedEventAsync(eventId);
        if (error is not null) return error;

        var invalid = ValidateAges(request);
        if (invalid is not null) return invalid;

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            DistanceKm = request.DistanceKm
        };
        Db.Categories.Add(category);
        await Db.SaveChangesAsync();
        return Created($"/api/events/{eventId}/categories", category.ToResponse());
    }

    /// <summary>Update a category on an event I own.</summary>
    /// <response code="200">Updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Category not found.</response>
    [HttpPut("categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(int id, [FromBody] CategoryRequest request)
    {
        var (category, error) = await GetOwnedCategoryAsync(id);
        if (error is not null) return error;

        var invalid = ValidateAges(request);
        if (invalid is not null) return invalid;

        category!.Name = request.Name.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.DistanceKm = request.DistanceKm;
        await Db.SaveChangesAsync();
        return Ok(category.ToResponse());
    }

    /// <summary>Remove a category from an event I own.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="403">Caller is not the owning Organiser.</response>
    /// <response code="404">Category not found.</response>
    /// <response code="409">Participants are already enrolled in this category.</response>
    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var (category, error) = await GetOwnedCategoryAsync(id);
        if (error is not null) return error;

        if (await Db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Fail(StatusCodes.Status409Conflict, "Category in use", "Participants are already enrolled in this category.");

        Db.Categories.Remove(category!);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    private ObjectResult? ValidateAges(CategoryRequest r) =>
        r.MinAge.HasValue && r.MaxAge.HasValue && r.MinAge > r.MaxAge
            ? Fail(StatusCodes.Status400BadRequest, "Invalid age range", "minAge cannot be greater than maxAge.")
            : null;

    // Ownership is one step removed: Category -> Event -> OrganiserId.
    private async Task<(Category? Category, ObjectResult? Error)> GetOwnedCategoryAsync(int id)
    {
        var category = await Db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return (null, Fail(StatusCodes.Status404NotFound, "Category not found"));
        if (category.Event.OrganiserId != CurrentUserId)
            return (null, Fail(StatusCodes.Status403Forbidden, "Forbidden", "You do not own the event this category belongs to."));
        return (category, null);
    }
}
