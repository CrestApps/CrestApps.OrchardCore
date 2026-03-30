using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// An AI tool that proxies execution to an agent (AI profile with <see cref="AIProfileType.Agent"/> type).
/// When the AI model invokes this tool, it delegates the task to the agent's completion service
/// using the agent profile's configuration (system message, tools, MCP connections, etc.).
/// </summary>
internal sealed class AgentProxyTool : AIFunction
{
    private readonly string _agentProfileName;
    private readonly string _description;

    public AgentProxyTool(string agentProfileName, string description)
    {
        _agentProfileName = agentProfileName;
        _description = description;
    }

    public override string Name => _agentProfileName;

    public override string Description => _description;

    public override JsonElement JsonSchema { get; } = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "prompt": {
                    "type": "string",
                    "description": "The prompt or message to send to the agent for processing."
                }
            },
            "required": ["prompt"]
        }
        """).RootElement;

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

        var logger = arguments.Services.GetRequiredService<ILogger<AgentProxyTool>>();

        if (!arguments.TryGetFirstString("prompt", out var task) || string.IsNullOrWhiteSpace(task))
        {
            return "No prompt was provided to the agent.";
        }

        try
        {
            var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();
            var profiles = await profileManager.GetAsync(AIProfileType.Agent);
            var agentProfile = profiles?.FirstOrDefault(p => string.Equals(p.Name, _agentProfileName, StringComparison.OrdinalIgnoreCase));

            if (agentProfile is null)
            {
                logger.LogWarning("Agent profile '{AgentName}' not found.", _agentProfileName);

                return $"Agent '{_agentProfileName}' is not available.";
            }

            var completionService = arguments.Services.GetRequiredService<IAICompletionService>();
            var contextBuilder = arguments.Services.GetRequiredService<IAICompletionContextBuilder>();
            var deploymentManager = arguments.Services.GetRequiredService<IAIDeploymentManager>();

            var context = await contextBuilder.BuildAsync(agentProfile);

            // Disable tools on the agent's context to prevent infinite recursion.
            context.DisableTools = true;

            var deployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentName: context.ChatDeploymentName)
                ?? throw new InvalidOperationException($"Unable to resolve a chat deployment for agent profile '{_agentProfileName}'.");

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, task),
            };

            var response = await completionService.CompleteAsync(
                deployment,
                messages,
                context,
                cancellationToken);

            var result = response?.Messages?.FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text;

            return result ?? "The agent did not produce a response.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute agent '{AgentName}'.", _agentProfileName);

            return $"An error occurred while executing agent '{_agentProfileName}'.";
        }
    }
}
