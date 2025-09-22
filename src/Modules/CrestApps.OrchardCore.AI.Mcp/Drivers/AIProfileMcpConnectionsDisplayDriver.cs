using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class AIProfileMcpConnectionsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ICatalog<McpConnection> _store;

    internal readonly IStringLocalizer S;

    public AIProfileMcpConnectionsDisplayDriver(
        ICatalog<McpConnection> store,
        IStringLocalizer<AIProfileMcpConnectionsDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        return Initialize<EditProfileMcpConnectionsViewModel>("EditProfileMcpConnection_Edit", model =>
        {
            var mcpMetadata = profile.As<AIProfileMcpMetadata>();

            model.Connections = connections
            .Select(entry => new ToolEntry
            {
                ItemId = entry.ItemId,
                DisplayText = entry.DisplayText,
                IsSelected = mcpMetadata.ConnectionIds?.Contains(entry.ItemId) ?? false,
            }).OrderBy(entry => entry.DisplayText)
            .ToArray();

        }).Location("Content:8.5#Capabilities:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        var model = new EditProfileMcpConnectionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var ids = model.Connections?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();

        var metadata = new AIProfileMcpMetadata();

        if (ids is null || ids.Length == 0)
        {
            metadata.ConnectionIds = [];
        }
        else
        {
            metadata.ConnectionIds = connections.Select(x => x.ItemId)
                .Intersect(ids)
                .ToArray();
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}

