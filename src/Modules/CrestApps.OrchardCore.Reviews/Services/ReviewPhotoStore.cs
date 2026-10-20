using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Reviews.Services;

/// <summary>
/// The photos buyers add to their reviews, kept in the site's media under <c>reviews/{productId}</c> when the Media
/// feature is on. A photo is a JPEG, PNG or WebP of at most 5 MB, checked by its first bytes and not only its name.
/// </summary>
public sealed class ReviewPhotoStore
{
    public const int MaxPhotos = 3;

    public const long MaxBytes = 5 * 1024 * 1024;

    private readonly IServiceProvider _serviceProvider;

    public ReviewPhotoStore(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets whether photos can be added: the Media feature is on.
    /// </summary>
    public bool IsAvailable
        => _serviceProvider.GetService<IMediaFileStore>() is not null;

    /// <summary>
    /// Returns a photo's address, or <see langword="null"/>.
    /// </summary>
    public string GetUrl(string path)
        => string.IsNullOrEmpty(path) ? null : _serviceProvider.GetService<IMediaFileStore>()?.MapPathToPublicUrl(path);

    /// <summary>
    /// Saves a review's photos and returns their media paths, or the problem with one of them.
    /// </summary>
    public async Task<(IReadOnlyList<string> Paths, string Problem)> SaveAsync(string productId, string reviewId, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken = default)
    {
        var photos = (files ?? []).Where(file => file is { Length: > 0 }).ToList();

        if (photos.Count == 0)
        {
            return ([], null);
        }

        if (_serviceProvider.GetService<IMediaFileStore>() is not { } media)
        {
            return ([], "unavailable");
        }

        if (photos.Count > MaxPhotos)
        {
            return ([], "too-many");
        }

        var saved = new List<string>();

        foreach (var (photo, index) in photos.Select((photo, index) => (photo, index)))
        {
            if (photo.Length > MaxBytes)
            {
                return ([], "too-big");
            }

            await using var stream = new MemoryStream();
            await photo.CopyToAsync(stream, cancellationToken);

            if (GetExtension(stream.GetBuffer().AsSpan(0, (int)Math.Min(stream.Length, 16))) is not { } extension)
            {
                return ([], "not-an-image");
            }

            stream.Position = 0;

            var path = $"reviews/{Safe(productId)}/{Safe(reviewId)}-{index + 1}-{Guid.NewGuid():N}{extension}";

            await media.CreateFileFromStreamAsync(path, stream, overwrite: true);
            saved.Add(path);
        }

        return (saved, null);
    }

    /// <summary>
    /// Deletes photos a review no longer has.
    /// </summary>
    public async Task DeleteAsync(IEnumerable<string> paths)
    {
        if (_serviceProvider.GetService<IMediaFileStore>() is not { } media)
        {
            return;
        }

        foreach (var path in paths ?? [])
        {
            if (!string.IsNullOrEmpty(path))
            {
                await media.TryDeleteFileAsync(path);
            }
        }
    }

    /// <summary>
    /// Returns the extension of an image by its first bytes: JPEG, PNG or WebP, or <see langword="null"/>.
    /// </summary>
    internal static string GetExtension(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return ".jpg";
        }

        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }

    // Ids are letters and digits; anything else is left out of a path.
    private static string Safe(string value)
        => new([.. (value ?? string.Empty).Where(char.IsAsciiLetterOrDigit)]);
}
