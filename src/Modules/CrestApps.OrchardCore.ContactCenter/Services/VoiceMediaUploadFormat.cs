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

        var extension = Path.GetExtension(audio.FileName);

        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.TrimStart('.').ToLowerInvariant();
        }

        var contentType = audio.ContentType;

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
