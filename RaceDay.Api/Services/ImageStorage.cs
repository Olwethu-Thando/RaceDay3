using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

public interface IImageStorage
{
    /// <summary>Stores the image and returns a URL the client can use to display it.</summary>
    Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct);
}

public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] Types = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "An image file is required.";
        if (file.Length > MaxBytes) return "Image must be 5 MB or smaller.";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Types.Contains(file.ContentType.ToLowerInvariant()) || !Extensions.Contains(ext))
            return "Only JPG, PNG or WEBP images are allowed.";
        return null;
    }
}

/// <summary>Used when no Azure connection string is configured. Saves into wwwroot/uploads.</summary>
public class LocalImageStorage : IImageStorage
{
    private readonly IWebHostEnvironment _env;
    public LocalImageStorage(IWebHostEnvironment env) => _env = env;

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct)
    {
        var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(root, "uploads", folder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = File.Create(Path.Combine(dir, name));
        await file.CopyToAsync(stream, ct);
        return $"/uploads/{folder}/{name}";
    }
}

/// <summary>Used when AzureBlob:ConnectionString is set (Part 1 plan: Azure Blob Storage).</summary>
public class AzureBlobImageStorage : IImageStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobImageStorage(string connectionString, string containerName) =>
        _container = new BlobContainerClient(connectionString, containerName);

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        var blob = _container.GetBlobClient($"{folder}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}");
        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType }, cancellationToken: ct);
        return blob.Uri.ToString();
    }
}
