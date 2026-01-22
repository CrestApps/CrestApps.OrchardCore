namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Options for controlling image generation.
/// </summary>
public sealed class ImageGenerationOptions
{
    /// <summary>
    /// Gets or sets the desired width of the generated image in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the desired height of the generated image in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets the number of images to generate.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Gets or sets the quality level for the generated image (e.g., "standard", "hd").
    /// </summary>
    public string Quality { get; set; }

    /// <summary>
    /// Gets or sets the style of the generated image (e.g., "vivid", "natural").
    /// </summary>
    public string Style { get; set; }

    /// <summary>
    /// Gets or sets the response format (e.g., "url", "b64_json").
    /// </summary>
    public string ResponseFormat { get; set; }
}
