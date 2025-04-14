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
    private readonly IModelStore<McpConnection> _store;

    internal readonly IStringLocalizer S;

    public AIProfileMcpConnectionsDisplayDriver(
        IModelStore<McpConnection> store,
        IStringLocalizer<AIProfileMcpConnectionsDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileMcpConnectionsViewModel>("EditProfileMcpConnection_Edit", async model =>
        {
            var mcpMetadata = profile.As<AIProfileMcpMetadata>();

            model.Connections = (await _store.GetAllAsync())
            .Select(entry => new ToolEntry
            {
                Id = entry.Id,
                DisplayText = entry.DisplayText,
                IsSelected = mcpMetadata.ConnectionIds?.Contains(entry.Id) ?? false,
            }).OrderBy(entry => entry.DisplayText)
            .ToArray();

        }).Location("Content:8.5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileMcpConnectionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var ids = model.Connections?.Where(x => x.IsSelected).Select(x => x.Id).ToArray();

        var metadata = new AIProfileMcpMetadata();

        if (ids is null || ids.Length == 0)
        {
            metadata.ConnectionIds = [];
        }
        else
        {
            var connections = await _store.GetAllAsync();

            metadata.ConnectionIds = connections.Select(x => x.Id)
                .Intersect(ids)
                .ToArray();
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}

