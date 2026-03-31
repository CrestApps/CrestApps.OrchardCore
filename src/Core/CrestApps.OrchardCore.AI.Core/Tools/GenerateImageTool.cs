using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Cysharp.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        var logger = arguments.Services.GetRequiredService<ILogger<GenerateImageTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("prompt", out var prompt))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument 'prompt'.", Name);
            return "Unable to find a 'prompt' argument in the arguments parameter.";
        }

        try
        {
            var executionContext = AIInvocationScope.Current?.ToolExecutionContext;

            if (executionContext is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: execution context is missing.", Name);
                return $"Image generation is not available. The {nameof(AIToolExecutionContext)} is missing from the invocation context.";
            }

            var providerName = executionContext.ProviderName;
            var connectionName = executionContext.ConnectionName;

            if (string.IsNullOrEmpty(providerName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: AI provider is not configured.", Name);
                return "Image generation is not available. AI provider is not configured.";
            }

            var deploymentManager = arguments.Services.GetRequiredService<IAIDeploymentManager>();
            var deployment = await deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentType.Image,
                clientName: providerName,
                connectionName: connectionName);

            if (deployment == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no image model deployment configured.", Name);
                return "Image generation is not available. No image model deployment is configured.";
            }

            if (string.IsNullOrEmpty(deployment.ConnectionName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: image deployment '{DeploymentName}' has no connection reference.", Name, deployment.Name);
                return "Image generation is not available. The resolved image deployment does not define a connection.";
            }

            var aIClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();

            var imageGenerator = await aIClientFactory.CreateImageGeneratorAsync(deployment.ClientName, deployment.ConnectionName, deployment.ModelName);

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
                logger.LogWarning("AI tool '{ToolName}' returned no images.", Name);
                return "No images were generated.";
            }

            using var builder = ZString.CreateStringBuilder();

            foreach (var contentItem in result.Contents)
            {
                var imageUri = ExtractImageUri(contentItem);

                if (!string.IsNullOrWhiteSpace(imageUri))
                {
                    builder.Append("![Generated Image](");
                    builder.Append(imageUri);
                    builder.AppendLine(")");
                    builder.AppendLine();
                }
            }

            if (builder.Length == 0)
            {
                logger.LogWarning("AI tool '{ToolName}' generated no usable image URIs.", Name);
                return "No images were generated.";
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return builder.ToString();
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "Image generation is not supported by the configured provider.");
            return "Image generation is not supported by the configured AI provider.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during image generation.");
            return "An error occurred while generating the image.";
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
