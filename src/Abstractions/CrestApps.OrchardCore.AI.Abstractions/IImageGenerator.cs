namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a generator for creating images based on text prompts using AI models.
/// </summary>
public interface IImageGenerator
{
    /// <summary>
    /// Generates images based on the provided prompt.
    /// </summary>
    /// <param name="prompt">The text description of the image to generate.</param>
    /// <param name="options">Optional settings to control the image generation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the generated image result.</returns>
    Task<GeneratedImageResult> GenerateAsync(
        string prompt,
        ImageGenerationOptions options = null,
        CancellationToken cancellationToken = default);
}
