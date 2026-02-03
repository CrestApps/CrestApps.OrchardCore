using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Services;
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
        var prompts = await _store.GetAllAsync();

        var promptsData = new JsonArray();

        var promptIds = step.IncludeAll
            ? []
            : step.PromptIds ?? [];

        foreach (var prompt in prompts)
        {
            if (promptIds.Length > 0 && !promptIds.Contains(prompt.ItemId))
            {
                continue;
            }

            var argumentsArray = new JsonArray();
            foreach (var arg in prompt.Arguments ?? [])
            {
                argumentsArray.Add(new JsonObject
                {
                    { nameof(McpPromptArgument.Name), arg.Name },
                    { nameof(McpPromptArgument.Description), arg.Description },
                    { nameof(McpPromptArgument.IsRequired), arg.IsRequired },
                });
            }

            var messagesArray = new JsonArray();
            foreach (var msg in prompt.Messages ?? [])
            {
                messagesArray.Add(new JsonObject
                {
                    { nameof(McpPromptMessage.Role), msg.Role },
                    { nameof(McpPromptMessage.Content), msg.Content },
                });
            }

            var deploymentInfo = new JsonObject()
            {
                { nameof(McpPrompt.ItemId), prompt.ItemId },
                { nameof(McpPrompt.DisplayText), prompt.DisplayText },
                { nameof(McpPrompt.Name), prompt.Name },
                { nameof(McpPrompt.Description), prompt.Description },
                { nameof(McpPrompt.Author), prompt.Author },
                { nameof(McpPrompt.CreatedUtc), prompt.CreatedUtc },
                { nameof(McpPrompt.OwnerId), prompt.OwnerId },
                { nameof(McpPrompt.Arguments), argumentsArray },
                { nameof(McpPrompt.Messages), messagesArray },
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
