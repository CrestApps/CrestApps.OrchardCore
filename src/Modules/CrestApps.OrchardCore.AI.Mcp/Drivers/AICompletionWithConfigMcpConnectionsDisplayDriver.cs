using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

/// <summary>
/// Contributes the MCP connections selection to the <see cref="AICompletionWithConfigTask"/>
/// workflow activity Capabilities tab.
/// </summary>
public sealed class AICompletionWithConfigMcpConnectionsDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly ICatalog<McpConnection> _store;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigMcpConnectionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The MCP connection store.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AICompletionWithConfigMcpConnectionsDisplayDriver(
        ICatalog<McpConnection> store,
        IStringLocalizer<AICompletionWithConfigMcpConnectionsDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        return Initialize<ChatInteractionMcpConnectionsViewModel>("ChatInteractionMcpConnections_Edit", model =>
        {
            var interaction = activity.Interaction;

            model.Connections = connections
                .Select(entry => new ToolEntry
                {
                    ItemId = entry.ItemId,
                    DisplayText = entry.DisplayText,
                    IsSelected = interaction.McpConnectionIds?.Contains(entry.ItemId) ?? false,
                }).OrderBy(entry => entry.DisplayText)
                .ToArray();
        }).Location("Content:2#Capabilities;3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        var model = new ChatInteractionMcpConnectionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var ids = model.Connections?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();

        var interaction = activity.Interaction;

        if (ids is null || ids.Length == 0)
        {
            interaction.McpConnectionIds = [];
        }
        else
        {
            interaction.McpConnectionIds = connections.Select(x => x.ItemId)
                .Intersect(ids)
                .ToList();
        }

        activity.Interaction = interaction;

        return Edit(activity, context);
    }
}
