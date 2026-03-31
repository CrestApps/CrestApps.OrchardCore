using System.Text.Json;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Tools;

/// <summary>
/// System tool that generates Chart.js configuration JSON from a data description.
/// Returns the chart config in the <c>[chart:json]</c> format recognized by the client.
/// </summary>
public sealed class GenerateChartTool : AIFunction
{
    public const string TheName = SystemToolNames.GenerateChart;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "data_description": {
              "type": "string",
              "description": "A description of the data to visualize, including any specific chart type preferences."
            }
          },
          "required": ["data_description"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "REQUIRED for any chart or data visualization request. This is the ONLY way to render a visual chart in the UI. Do NOT generate chart JSON inline — it will not be rendered. Always call this tool instead. Returns a special [chart:JSON] marker that MUST be included exactly as-is in your response.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };


    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<GenerateChartTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("data_description", out var dataDescription))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument 'data_description'.", Name);
            return "Unable to find a 'data_description' argument in the arguments parameter.";
        }

        try
        {
            var executionContext = AIInvocationScope.Current?.ToolExecutionContext;

            if (executionContext is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: execution context is missing.", Name);
                return $"Chart generation is not available. The {nameof(AIToolExecutionContext)} is missing from the invocation context.";
            }

            var providerName = executionContext.ProviderName;
            var connectionName = executionContext.ConnectionName;

            if (string.IsNullOrEmpty(providerName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: AI provider is not configured.", Name);
                return "Chart generation is not available. AI provider is not configured.";
            }

            var deploymentManager = arguments.Services.GetRequiredService<IAIDeploymentManager>();
            var deployment = await deploymentManager.ResolveUtilityOrDefaultAsync(
                clientName: providerName,
                connectionName: connectionName);

            if (deployment == null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: no chat model deployment configured.", Name);
                return "Chart generation is not available. No chat model deployment is configured.";
            }

            if (string.IsNullOrEmpty(deployment.ConnectionName))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: chart deployment '{DeploymentName}' has no connection reference.", Name, deployment.Name);
                return "Chart generation is not available. The resolved deployment does not define a connection.";
            }

            var aIClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();

            var chatClient = await aIClientFactory.CreateChatClientAsync(deployment.ClientName, deployment.ConnectionName, deployment.ModelName);

            var promptService = arguments.Services.GetService<IAITemplateService>();
            var systemPrompt = promptService != null
                ? await promptService.RenderAsync(AITemplateIds.ChartGeneration)
                : string.Empty;

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt ?? string.Empty),
                new(ChatRole.User, dataDescription),
            };

            var chatOptions = new ChatOptions
            {
                Temperature = 0.3f,
                MaxOutputTokens = 2000,
            };

            var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);

            if (response is null || string.IsNullOrWhiteSpace(response.Text))
            {
                logger.LogWarning("AI tool '{ToolName}' received an empty response from the chat client.", Name);
                return "Failed to generate chart configuration.";
            }

            var chartConfig = ExtractJsonFromResponse(response.Text);

            if (string.IsNullOrEmpty(chartConfig))
            {
                logger.LogWarning("AI tool '{ToolName}' failed to extract valid JSON from the chat response.", Name);
                return "Failed to generate valid chart configuration.";
            }

            // Validate it's valid Chart.js JSON.
            try
            {
                using var doc = JsonDocument.Parse(chartConfig);

                if (!doc.RootElement.TryGetProperty("type", out _) || !doc.RootElement.TryGetProperty("data", out _))
                {
                    logger.LogWarning("AI tool '{ToolName}' generated chart config missing 'type' or 'data' properties.", Name);
                    return "Generated chart configuration is missing required properties.";
                }
            }
            catch (JsonException)
            {
                logger.LogWarning("AI tool '{ToolName}' generated invalid JSON for chart configuration.", Name);
                return "Failed to generate valid chart configuration.";
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return $"Chart generated successfully.Include the following marker exactly as-is in your response (do NOT modify, convert to an image, or replace it):\n\n[chart:{chartConfig}]";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during chart generation.");
            return "An error occurred while generating the chart.";
        }
    }

    private static string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var text = response.Trim();

        // If wrapped in markdown code blocks, extract the content.
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            var endIndex = text.IndexOf("```", 7);
            if (endIndex > 7)
            {
                text = text[7..endIndex].Trim();
            }
        }
        else if (text.StartsWith("```"))
        {
            var startIndex = text.IndexOf('\n');
            if (startIndex > 0)
            {
                var endIndex = text.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    text = text[(startIndex + 1)..endIndex].Trim();
                }
            }
        }

        var jsonStart = text.IndexOf('{');

        if (jsonStart < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = jsonStart; i < text.Length; i++)
        {
            var c = text[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text[jsonStart..(i + 1)];
                }
            }
        }

        return null;
    }
}
