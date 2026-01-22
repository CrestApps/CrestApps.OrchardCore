namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents the result of an image generation operation.
/// </summary>
public sealed class GeneratedImageResult
{
    /// <summary>
    /// Gets or sets the collection of generated images.
    /// </summary>
    public IReadOnlyList<GeneratedImage> Images { get; set; } = [];
}

/// <summary>
/// Represents a single generated image.
/// </summary>
public sealed class GeneratedImage
{
    /// <summary>
    /// Gets or sets the URL where the generated image can be accessed.
    /// This is set when the response format is "url".
    /// </summary>
    public Uri Url { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded image data.
    /// This is set when the response format is "b64_json".
    /// </summary>
    public string Base64Data { get; set; }

    /// <summary>
    /// Gets or sets the content type of the image (e.g., "image/png").
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the revised prompt that was actually used for generation.
    /// Some models revise the prompt before generating.
    /// </summary>
    public string RevisedPrompt { get; set; }
}
