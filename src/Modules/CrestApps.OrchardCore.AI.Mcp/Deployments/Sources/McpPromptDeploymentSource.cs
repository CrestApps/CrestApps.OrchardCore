using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Services;
using ModelContextProtocol.Protocol;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;

internal sealed class McpPromptDeploymentSource : DeploymentSourceBase<McpPromptDeploymentStep>
{
    private readonly ICatalog<McpPrompt> _store;

    public McpPromptDeploymentSource(ICatalog<McpPrompt> store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(McpPromptDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _store.GetAllAsync();

        var promptsData = new JsonArray();

        var promptIds = step.IncludeAll
            ? []
            : step.PromptIds ?? [];

        foreach (var entry in entries)
        {
            if (promptIds.Length > 0 && !promptIds.Contains(entry.ItemId))
            {
                continue;
            }

            var argumentsArray = new JsonArray();
            foreach (var arg in entry.Prompt?.Arguments ?? [])
            {
                argumentsArray.Add(new JsonObject
                {
                    { nameof(PromptArgument.Name), arg.Name },
                    { nameof(PromptArgument.Title), arg.Title },
                    { nameof(PromptArgument.Description), arg.Description },
                    { nameof(PromptArgument.Required), arg.Required },
                });
            }

            var promptData = new JsonObject
            {
                { nameof(Prompt.Name), entry.Prompt?.Name },
                { nameof(Prompt.Title), entry.Prompt?.Title },
                { nameof(Prompt.Description), entry.Prompt?.Description },
                { nameof(Prompt.Arguments), argumentsArray },
            };

            var deploymentInfo = new JsonObject()
            {
                { nameof(McpPrompt.ItemId), entry.ItemId },
                { nameof(McpPrompt.Name), entry.Name },
                { nameof(McpPrompt.Author), entry.Author },
                { nameof(McpPrompt.CreatedUtc), entry.CreatedUtc },
                { nameof(McpPrompt.OwnerId), entry.OwnerId },
                { nameof(McpPrompt.Prompt), promptData },
            };

            promptsData.Add(deploymentInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Prompts"] = promptsData,
        });
    }
}
