using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Handlers;

internal sealed class AgentOrchestrationContextBuilderHandler : IOrchestrationContextBuilderHandler
{
    private readonly IAIProfileManager _profileManager;
    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;

    public AgentOrchestrationContextBuilderHandler(
        IAIProfileManager profileManager,
        ITemplateService templateService,
        ILogger<AgentOrchestrationContextBuilderHandler> logger)
    {
        _profileManager = profileManager;
        _templateService = templateService;
        _logger = logger;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        var completionContext = context.OrchestrationContext.CompletionContext;

        if (completionContext is null)
        {
            return;
        }

        var requestedAgentNames = completionContext.AgentNames;
        var agents = await _profileManager.GetAsync(AIProfileType.Agent);

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

            var agentMetadata = agent.As<AgentMetadata>();
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

        var header = await _templateService.RenderAsync(
            AITemplateIds.AgentAvailability,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["agents"] = availableAgents,
            });

        if (!string.IsNullOrEmpty(header))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(header);
        }
    }

    private sealed class AgentInfo
    {
        public string Name { get; set; }

        public string Description { get; set; }
    }
}
