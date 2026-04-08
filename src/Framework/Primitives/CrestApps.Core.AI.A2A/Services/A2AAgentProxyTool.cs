using System.Text.Json;
using A2A;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.A2A.Services;

/// <summary>
/// An AI tool that proxies execution to a remote agent via the A2A protocol.
/// When the AI model invokes this tool, it sends a message to the remote agent
/// and returns the response.
/// </summary>
internal sealed class A2AAgentProxyTool : AIFunction
{
    private readonly string _agentName;
    private readonly string _description;
    private readonly string _endpoint;
    private readonly string _connectionId;

    public A2AAgentProxyTool(string agentName, string description, string endpoint, string connectionId)
    {
        _agentName = agentName;
        _description = description;
        _endpoint = endpoint;
        _connectionId = connectionId;
    }

    public override string Name => _agentName;

    public override string Description => _description;

    public override JsonElement JsonSchema { get; } = JsonElement.Parse(
        """
        {
          "type": "object",
          "properties": {
            "message": {
              "type": "string",
              "description": "The message or task to send to the remote agent for processing."
            },
            "contextId": {
              "type": "string",
              "description": "An optional context identifier to maintain conversation continuity with the remote agent."
            }
          },
          "required": ["message"]
        }

        """);
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,

    };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<A2AAgentProxyTool>>();

        if (!TryGetString(arguments, "message", out var message) || string.IsNullOrWhiteSpace(message))
        {
            return "No message was provided to the agent.";

        }

        TryGetString(arguments, "contextId", out var contextId);

        try
        {
            var httpClientFactory = arguments.Services.GetRequiredService<IHttpClientFactory>();

            var httpClient = httpClientFactory.CreateClient();

            var authService = arguments.Services.GetService<IA2AConnectionAuthService>();
            if (authService is not null)
            {
                var connectionStore = arguments.Services.GetRequiredService<ICatalog<A2AConnection>>();

                var connection = await connectionStore.FindByIdAsync(_connectionId);

                if (connection is not null)
                {
                    var metadata = connection.As<A2AConnectionMetadata>();
                    await authService.ConfigureHttpClientAsync(httpClient, metadata, cancellationToken);
                }

            }

            var client = new A2AClient(new Uri(_endpoint), httpClient);

            var agentMessage = new AgentMessage
            {
                Role = MessageRole.User,
                MessageId = Guid.NewGuid().ToString(),
                ContextId = contextId ?? Guid.NewGuid().ToString(),
                Parts = [new TextPart { Text = message }],
                Metadata = new Dictionary<string, JsonElement>
                {
                    ["agentName"] = JsonSerializer.SerializeToElement(_agentName),
                },

            };

            var sendParams = new MessageSendParams
            {
                Message = agentMessage,

            };

            var response = await client.SendMessageAsync(sendParams, cancellationToken);

            var responseText = ExtractTextFromResponse(response);

            return responseText ?? "The remote agent did not produce a text response.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {

            logger.LogError(ex, "Failed to communicate with remote A2A agent '{AgentName}' at '{Endpoint}'.", _agentName, _endpoint);

            return $"An error occurred while communicating with remote agent '{_agentName}'.";
        }

    }

    private static bool TryGetString(AIFunctionArguments arguments, string key, out string value)
    {

        value = null;

        if (arguments.TryGetValue(key, out var obj))
        {
            if (obj is string str)
            {
                value = str;
                return true;

            }

            if (obj is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString();
                return true;
            }

        }

        return false;

    }

    private static string ExtractTextFromResponse(A2AResponse response)
    {
        if (response is AgentMessage message)
        {

            var texts = message.Parts?.OfType<TextPart>().Select(p => p.Text);

            if (texts?.Any() == true)
            {
                return string.Join(string.Empty, texts);
            }
        }
        else if (response is AgentTask task)
        {
            if (task.Artifacts?.Count > 0)
            {
                var artifactTexts = task.Artifacts
                    .SelectMany(a => a.Parts?.OfType<TextPart>() ?? [])

                    .Select(p => p.Text);

                var combined = string.Join(string.Empty, artifactTexts);

                if (!string.IsNullOrEmpty(combined))
                {
                    return combined;
                }

            }

            if (task.Status.Message?.Parts is not null)
            {

                var statusTexts = task.Status.Message.Parts.OfType<TextPart>().Select(p => p.Text);

                var combined = string.Join(string.Empty, statusTexts);

                if (!string.IsNullOrEmpty(combined))
                {
                    return combined;
                }
            }

        }

        return null;
    }
}
