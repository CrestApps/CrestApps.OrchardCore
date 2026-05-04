using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Orchestration context handler that enriches the system message with descriptions
/// of available AI agents. This gives the model awareness of which agents exist and
/// what they can do, enabling it to make informed routing decisions.
/// <para>
/// This follows the industry-standard pattern used by OpenAI, LangChain, and CrewAI
/// where agent/tool descriptions are included in the system prompt so the model
/// can decide which capabilities to invoke.
/// </para>
/// </summary>
internal sealed class AgentOrchestrationContextBuilderHandler : IOrchestrationContextBuilderHandler
{
    private readonly IAIProfileManager _profileManager;
    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentOrchestrationContextBuilderHandler"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager for retrieving agent profiles.</param>
    /// <param name="templateService">The template service for rendering agent availability prompts.</param>
    /// <param name="logger">The logger instance.</param>
    public AgentOrchestrationContextBuilderHandler(
        IAIProfileManager profileManager,
        ITemplateService templateService,
        ILogger<AgentOrchestrationContextBuilderHandler> logger)
    {
        _profileManager = profileManager;
        _templateService = templateService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task BuildingAsync(OrchestrationContextBuildingContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public async Task BuiltAsync(OrchestrationContextBuiltContext context, CancellationToken cancellationToken = default)
    {
        var completionContext = context.OrchestrationContext.CompletionContext;

        if (completionContext is null)
        {
            return;
        }

        var requestedAgentNames = completionContext.AgentNames;
        var agents = await _profileManager.GetAsync(AIProfileType.Agent, cancellationToken);

        if (!agents.Any())
        {
            return;
        }

        var availableAgents = new List<AgentInfo>();

        foreach (var agent in agents)
        {
            if (string.IsNullOrEmpty(agent.Name) || string.IsNullOrEmpty(agent.Description))
            {
                continue;
            }

            var agentMetadata = agent.GetOrCreate<AgentMetadata>();
            var isAlwaysAvailable = agentMetadata?.Availability == AgentAvailability.AlwaysAvailable;

            if (isAlwaysAvailable ||
                (requestedAgentNames is { Length: > 0 } &&
                    requestedAgentNames.Contains(agent.Name, StringComparer.OrdinalIgnoreCase)))
            {
                availableAgents.Add(new AgentInfo
                {
                    Name = agent.Name,
                    Description = agent.Description,
                });
            }
        }

        if (availableAgents.Count == 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Enriching system message with {AgentCount} available agent(s).", availableAgents.Count);
        }

        var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["agents"] = availableAgents,
        };

        var header = await _templateService.RenderAsync(AITemplateIds.AgentAvailability, arguments, cancellationToken);

        if (!string.IsNullOrEmpty(header))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(header);
        }
    }

    private sealed class AgentInfo
    {
        /// <summary>
        /// Gets or sets the name of the agent.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the agent.
        /// </summary>
        public string Description { get; set; }
    }
}
