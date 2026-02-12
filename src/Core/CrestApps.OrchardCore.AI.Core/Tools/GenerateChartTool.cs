using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Tools;

/// <summary>
/// System tool that generates Chart.js configuration JSON from a data description.
/// Returns the chart config in the <c>[chart:json]</c> format recognized by the client.
/// </summary>
public sealed class GenerateChartTool : AIFunction
{
    public const string TheName = SystemToolNames.GenerateChart;

    private const string ChartGenerationSystemPrompt = """
        You are a data visualization expert. Your task is to generate Chart.js configuration JSON based on the user's request and data.

        IMPORTANT RULES:
        1. Return ONLY valid JSON that can be used directly with Chart.js
        2. Do NOT include any explanation, markdown, or code blocks - just the raw JSON
        3. The JSON must be a valid Chart.js configuration object with 'type', 'data', and optionally 'options' properties
        4. Supported chart types: 'bar', 'line', 'pie', 'doughnut', 'radar', 'polarArea', 'scatter', 'bubble'
        5. Use appropriate colors from this palette: ['#4dc9f6', '#f67019', '#f53794', '#537bc4', '#acc236', '#166a8f', '#00a950', '#58595b', '#8549ba']
        6. Include responsive: true and maintainAspectRatio: true in options
        7. Extract and structure data from the conversation to create meaningful visualizations

        Example output format:
        {"type":"bar","data":{"labels":["Jan","Feb","Mar"],"datasets":[{"label":"Sales","data":[10,20,30],"backgroundColor":["#4dc9f6","#f67019","#f53794"]}]},"options":{"responsive":true,"maintainAspectRatio":true}}
        """;

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

    public override string Description => "Generates a Chart.js configuration from a data description. Returns chart configuration that can be rendered in the UI.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>() { ["Strict"] = false };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("data_description", out var dataDescription))
        {
            return "Unable to find a 'data_description' argument in the arguments parameter.";
        }

        var logger = arguments.Services.GetService<ILogger<GenerateChartTool>>();

        try
        {
            var providerOptions = arguments.Services.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            var aIClientFactory = arguments.Services.GetRequiredService<IAIClientFactory>();
            var context = arguments.Services.GetService<OrchestrationContext>();

            var providerName = context?.SourceName;
            var connectionName = context?.CompletionContext?.ConnectionName;

            if (string.IsNullOrEmpty(providerName) || !providerOptions.Providers.TryGetValue(providerName, out var provider))
            {
                return "Chart generation is not available. AI provider is not configured.";
            }

            if (string.IsNullOrEmpty(connectionName))
            {
                connectionName = provider.DefaultConnectionName;
            }

            if (string.IsNullOrEmpty(connectionName) || !provider.Connections.TryGetValue(connectionName, out var connection))
            {
                return "Chart generation is not available. No connection is configured.";
            }

            var deploymentName = connection.GetDefaultDeploymentName(throwException: false);

            if (string.IsNullOrEmpty(deploymentName))
            {
                return "Chart generation is not available. No chat model deployment is configured.";
            }

            var chatClient = await aIClientFactory.CreateChatClientAsync(providerName, connectionName, deploymentName);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, ChartGenerationSystemPrompt),
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
                return "Failed to generate chart configuration.";
            }

            var chartConfig = ExtractJsonFromResponse(response.Text);

            if (string.IsNullOrEmpty(chartConfig))
            {
                return "Failed to generate valid chart configuration.";
            }

            // Validate it's valid Chart.js JSON.
            try
            {
                using var doc = JsonDocument.Parse(chartConfig);

                if (!doc.RootElement.TryGetProperty("type", out _) || !doc.RootElement.TryGetProperty("data", out _))
                {
                    return "Generated chart configuration is missing required properties.";
                }
            }
            catch (JsonException)
            {
                return "Failed to generate valid chart configuration.";
            }

            return $"[chart:{chartConfig}]";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during chart generation.");
            return $"An error occurred while generating the chart: {ex.Message}";
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
