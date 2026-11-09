// Defines the namespace for the RaceDay web controllers.
namespace RaceDay.Web.Controllers;

// Requires users to be logged in before accessing this controller.
[Authorize]
public class ProfileController : AppController
{
    // Sets the maximum allowed profile picture size to 5 MB.
    private const long MaxImageBytes = 5 * 1024 * 1024;

    // Lists the image formats allowed for profile picture uploads.
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    // Stores the API client used to communicate with the RaceDay API.
    private readonly ApiClient _api;

    // Constructor that receives the API client through dependency injection.
    public ProfileController(ApiClient api) => _api = api;

    // Displays the current user's profile information.
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Request the user's profile details from the API.
        var result = await _api.GetProfileAsync();

        // Check whether the profile information was loaded successfully.
        if (!result.Success || result.Data is null)
        {
            // Display an error message if the profile could not be loaded.
            FlashError(result.Error ?? "We couldn't load your profile.");

            // Redirect the user to the home page.
            return RedirectToAction("Index", "Home");
        }

        // Convert the profile information into a form model and display it.
        return View(ToForm(result.Data));
    }

    // Handles the submission of updated profile information.
    [HttpPost]
    public async Task<IActionResult> Update(ProfileFormModel model)
    {
        // Check that the user's date of birth is not in the future.
        if (model.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");

        // Return to the profile page if the submitted information is invalid.
        if (!ModelState.IsValid) return View("Index", model);

        // Send the updated profile information to the API.
        var result = await _api.UpdateProfileAsync(model);

        // Display an error if the profile could not be updated.
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't save your profile.");
            return View("Index", model);
        }

        // Display a success message after updating the profile.
        FlashSuccess("Your profile was updated.");

        // Redirect back to the profile page.
        return RedirectToAction(nameof(Index));
    }

    // Allows participants to upload or change their profile picture.
    [HttpPost]
    [Authorize(Roles = "Participant")]
    public async Task<IActionResult> UploadPicture(IFormFile? image)
    {
        // Check whether the user selected an image to upload.
        if (image is null || image.Length == 0)
            FlashError("Choose an image to upload.");

        // Check that the selected image does not exceed 5 MB.
        else if (image.Length > MaxImageBytes)
            FlashError("The picture must be 5 MB or smaller.");

        // Check that the uploaded image uses an allowed file extension.
        else if (!ImageExtensions.Contains(Path.GetExtension(image.FileName).ToLowerInvariant()))
            FlashError("Use a JPG, PNG or WEBP image.");

        else
        {
            // Send the selected profile picture to the API for uploading.
            var result = await _api.UploadProfilePictureAsync(image);

            // Display a success message if the picture was uploaded.
            if (result.Success)
                FlashSuccess("Your profile picture was updated.");

            // Display an error message if the upload was unsuccessful.
            else
                FlashError(result.Error ?? "We couldn't upload that picture.");
        }

        // Redirect back to the profile page after the upload attempt.
        return RedirectToAction(nameof(Index));
    }

    // Converts the profile response received from the API into a form model.
    private static ProfileFormModel ToForm(ProfileResponse p) => new()
    {
        // Set the user's email address.
        Email = p.Email,

        // Set the user's account role.
        Role = p.Role,

        // Set the user's full name.
        FullName = p.FullName,

        // Set the user's contact number.
        ContactNumber = p.ContactNumber,

        // Set the organisation name if the user is an organiser.
        OrganisationName = p.OrganisationName,

        // Set the user's date of birth.
        DateOfBirth = p.DateOfBirth,

        // Set the current profile picture URL.
        ProfilePictureUrl = p.ProfilePictureUrl
    };
}