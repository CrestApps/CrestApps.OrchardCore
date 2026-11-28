using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Names the audio format of an uploaded file the way the voice media library records it.
/// </summary>
internal static class VoiceMediaUploadFormat
{
    /// <summary>
    /// Resolves the format (for example <c>mp3</c>) from the file's extension, or from its content type when the
    /// file has no extension.
    /// </summary>
    /// <param name="audio">The uploaded audio file.</param>
    /// <returns>The lower-case format, or <see langword="null"/> when neither names one.</returns>
    public static string Resolve(IFormFile audio)
    {
        ArgumentNullException.ThrowIfNull(audio);

        return Resolve(audio.FileName, audio.ContentType);
    }

    /// <summary>
    /// Resolves the format (for example <c>mp3</c>) from a file name's extension, or from the content type when the
    /// name has no extension.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type of the audio.</param>
    /// <returns>The lower-case format, or <see langword="null"/> when neither names one.</returns>
    public static string Resolve(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);

        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.TrimStart('.').ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var slashIndex = contentType.IndexOf('/');

            if (slashIndex >= 0 && slashIndex < contentType.Length - 1)
            {
                return contentType[(slashIndex + 1)..].Trim().ToLowerInvariant();
            }
        }

        return null;
    }
}
