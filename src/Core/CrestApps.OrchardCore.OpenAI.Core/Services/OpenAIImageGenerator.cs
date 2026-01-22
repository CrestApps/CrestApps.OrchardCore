using CrestApps.OrchardCore.AI.Models;
using OpenAI.Images;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

/// <summary>
/// An IImageGenerator implementation that wraps OpenAI's ImageClient.
/// </summary>
public sealed class OpenAIImageGenerator : IImageGenerator
{
    private readonly ImageClient _imageClient;

    public OpenAIImageGenerator(ImageClient imageClient)
    {
        _imageClient = imageClient ?? throw new ArgumentNullException(nameof(imageClient));
    }

    public async Task<GeneratedImageResult> GenerateAsync(
        string prompt,
        ImageGenerationOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prompt);

        var requestOptions = BuildOptions(options);
        var response = await _imageClient.GenerateImageAsync(prompt, requestOptions, cancellationToken);

        return MapToResult(response.Value);
    }

    private static ImageGenerationOptions BuildOptions(ImageGenerationOptions options)
    {
        var requestOptions = new OpenAI.Images.ImageGenerationOptions();

        if (options == null)
        {
            return requestOptions;
        }

        if (options.Width.HasValue && options.Height.HasValue)
        {
            requestOptions.Size = new GeneratedImageSize(options.Width.Value, options.Height.Value);
        }

        if (!string.IsNullOrEmpty(options.Quality))
        {
            requestOptions.Quality = options.Quality.ToLowerInvariant() switch
            {
                "hd" => GeneratedImageQuality.High,
                "high" => GeneratedImageQuality.High,
                _ => GeneratedImageQuality.Standard,
            };
        }

        if (!string.IsNullOrEmpty(options.Style))
        {
            requestOptions.Style = options.Style.ToLowerInvariant() switch
            {
                "natural" => GeneratedImageStyle.Natural,
                _ => GeneratedImageStyle.Vivid,
            };
        }

        if (!string.IsNullOrEmpty(options.ResponseFormat))
        {
            requestOptions.ResponseFormat = options.ResponseFormat.ToLowerInvariant() switch
            {
                "b64_json" => GeneratedImageFormat.Bytes,
                "bytes" => GeneratedImageFormat.Bytes,
                _ => GeneratedImageFormat.Uri,
            };
        }

        return requestOptions;
    }

    private static GeneratedImageResult MapToResult(GeneratedImage generatedImage)
    {
        var images = new List<Models.GeneratedImage>();

        var image = new Models.GeneratedImage
        {
            Url = generatedImage.ImageUri,
            RevisedPrompt = generatedImage.RevisedPrompt,
            ContentType = "image/png",
        };

        // If bytes are available, convert to base64
        if (generatedImage.ImageBytes != null)
        {
            image.Base64Data = Convert.ToBase64String(generatedImage.ImageBytes.ToArray());
        }

        images.Add(image);

        return new GeneratedImageResult
        {
            Images = images,
        };
    }
}
