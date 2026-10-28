namespace RaceDay.Web.Controllers;

[Authorize]
public class ProfileController : AppController
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly ApiClient _api;

    public ProfileController(ApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetProfileAsync();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We couldn't load your profile.");
            return RedirectToAction("Index", "Home");
        }
        return View(ToForm(result.Data));
    }

    [HttpPost]
    public async Task<IActionResult> Update(ProfileFormModel model)
    {
        if (model.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        if (!ModelState.IsValid) return View("Index", model);

        var result = await _api.UpdateProfileAsync(model);
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't save your profile.");
            return View("Index", model);
        }

        FlashSuccess("Your profile was updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Participant")]
    public async Task<IActionResult> UploadPicture(IFormFile? image)
    {
        if (image is null || image.Length == 0)
            FlashError("Choose an image to upload.");
        else if (image.Length > MaxImageBytes)
            FlashError("The picture must be 5 MB or smaller.");
        else if (!ImageExtensions.Contains(Path.GetExtension(image.FileName).ToLowerInvariant()))
            FlashError("Use a JPG, PNG or WEBP image.");
        else
        {
            var result = await _api.UploadProfilePictureAsync(image);
            if (result.Success) FlashSuccess("Your profile picture was updated.");
            else FlashError(result.Error ?? "We couldn't upload that picture.");
        }
        return RedirectToAction(nameof(Index));
    }

    private static ProfileFormModel ToForm(ProfileResponse p) => new()
    {
        Email = p.Email,
        Role = p.Role,
        FullName = p.FullName,
        ContactNumber = p.ContactNumber,
        OrganisationName = p.OrganisationName,
        DateOfBirth = p.DateOfBirth,
        ProfilePictureUrl = p.ProfilePictureUrl
    };
}
