using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Tools;

/// <summary>
/// System tool that generates images from text descriptions using DALL-E or compatible image generation models.
/// Returns markdown image syntax for inline rendering.
/// </summary>
public sealed class GenerateImageTool : AIFunction
{
    public const string TheName = SystemToolNames.GenerateImage;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "prompt": {
              "type": "string",
              "description": "A detailed description of the image to generate."
            }
          },
          "required": ["prompt"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Generates an image from a text description using an AI image generation model and returns it as markdown.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };


    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("prompt", out var prompt))
        {
            return "Unable to find a 'prompt' argument in the arguments parameter.";
        }

        var logger = arguments.Services.GetService<ILogger<GenerateImageTool>>();

        try
        {
            var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();

            var executionContext = httpContextAccessor.HttpContext?.Items[nameof(AIToolExecutionContext)] as AIToolExecutionContext;

            if (executionContext is null)
            {
                return $"Image generation is not available. The {nameof(AIToolExecutionContext)} is missing from the HttpContext.";
            }

            var providerName = executionContext.ProviderName;
            var connectionName = executionContext.ConnectionName;

            if (string.IsNullOrEmpty(providerName))
            {
                return "Image generation is not available. AI provider is not configured.";
            }

            var providerOptions = arguments.Services.GetRequiredService<IOptions<AIProviderOptions>>().Value;

            if (!providerOptions.Providers.TryGetValue(providerName, out var provider))
            {
                return "Image generation is not available. AI provider is invalid.";
            }

            if (string.IsNullOrEmpty(connectionName))
            {
                connectionName = provider.DefaultConnectionName;
            }

            if (string.IsNullOrEmpty(connectionName) || !provider.Connections.TryGetValue(connectionName, out var connection))
            {
                return "Image generation is not available. No connection is configured.";
            }

            var deploymentName = connection.GetDefaultImagesDeploymentName(throwException: false);

            if (string.IsNullOrEmpty(deploymentName))
            {
                return "Image generation is not available. No image model deployment is configured.";
            }

            var aIClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();

            var imageGenerator = await aIClientFactory.CreateImageGeneratorAsync(providerName, connectionName, deploymentName);

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var options = new ImageGenerationOptions
            {
                ImageSize = new System.Drawing.Size(1024, 1024),
                ResponseFormat = ImageGenerationResponseFormat.Uri,
            };

            var request = new ImageGenerationRequest
            {
                Prompt = prompt,
            };
#pragma warning restore MEAI001

            var result = await imageGenerator.GenerateAsync(request, options, cancellationToken);

            if (result?.Contents is null || result.Contents.Count == 0)
            {
                return "No images were generated.";
            }

            var builder = new StringBuilder();

            foreach (var contentItem in result.Contents)
            {
                var imageUri = ExtractImageUri(contentItem);

                if (!string.IsNullOrWhiteSpace(imageUri))
                {
                    builder.AppendLine($"![Generated Image]({imageUri})");
                    builder.AppendLine();
                }
            }

            return builder.Length > 0
                ? builder.ToString()
                : "No images were generated.";
        }
        catch (NotSupportedException ex)
        {
            logger?.LogWarning(ex, "Image generation is not supported by the configured provider.");
            return "Image generation is not supported by the configured AI provider.";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during image generation.");
            return $"An error occurred while generating the image: {ex.Message}";
        }
    }

    private static string ExtractImageUri(AIContent contentItem)
    {
        if (contentItem is UriContent uriContent)
        {
            return uriContent.Uri?.ToString();
        }

        if (contentItem is DataContent dataContent && dataContent.Uri is not null)
        {
            return dataContent.Uri.ToString();
        }

        return null;
    }
}
