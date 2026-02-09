using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class ChatInteractionMcpConnectionsDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly ICatalog<McpConnection> _store;

    internal readonly IStringLocalizer S;

    public ChatInteractionMcpConnectionsDisplayDriver(
        ICatalog<McpConnection> store,
        IStringLocalizer<ChatInteractionMcpConnectionsDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        return Initialize<EditChatInteractionMcpConnectionsViewModel>("ChatInteractionMcpConnections_Edit", model =>
        {
            model.Connections = connections
            .Select(entry => new ToolEntry
            {
                ItemId = entry.ItemId,
                DisplayText = entry.DisplayText,
                IsSelected = interaction.McpConnectionIds?.Contains(entry.ItemId) ?? false,
            }).OrderBy(entry => entry.DisplayText)
            .ToArray();

        }).Location("Parameters:4#Capabilities:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        var model = new EditChatInteractionMcpConnectionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var ids = model.Connections?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();

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

        return Edit(interaction, context);
    }
}
