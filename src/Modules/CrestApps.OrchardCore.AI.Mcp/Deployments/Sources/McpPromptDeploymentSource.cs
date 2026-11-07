using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;

/// <summary>
/// Writes the selected MCP prompts to a deployment plan, in the shape <c>McpPromptStep</c> reads.
/// </summary>
internal sealed class McpPromptDeploymentSource : CatalogDeploymentSourceBase<McpPromptDeploymentStep, McpPrompt>
{
    private readonly INamedCatalog<McpPrompt> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpPromptDeploymentSource"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    public McpPromptDeploymentSource(INamedCatalog<McpPrompt> store)
        : base("Prompts")
    {
        _store = store;
    }

    protected override async Task<IEnumerable<McpPrompt>> GetEntriesAsync()
        => await _store.GetAllAsync();

    protected override bool IsSelected(McpPromptDeploymentStep step, McpPrompt entry)
        => IsSelectedByKey(step.IncludeAll, step.PromptIds, entry.ItemId);

    protected override JsonObject Export(McpPrompt entry)
    {
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

        var messagesArray = new JsonArray();

        foreach (var message in entry.Messages ?? [])
        {
            messagesArray.Add(new JsonObject
            {
                { nameof(McpPromptMessage.Role), message.Role },
                { nameof(McpPromptMessage.Content), message.Content },
            });
        }

        return new JsonObject()
        {
            { nameof(McpPrompt.ItemId), entry.ItemId },
            { nameof(McpPrompt.Name), entry.Name },
            { nameof(McpPrompt.Author), entry.Author },
            { nameof(McpPrompt.CreatedUtc), entry.CreatedUtc },
            { nameof(McpPrompt.OwnerId), entry.OwnerId },
            { nameof(McpPrompt.Prompt), promptData },
            { nameof(McpPrompt.Messages), messagesArray },
        };
    }
}
