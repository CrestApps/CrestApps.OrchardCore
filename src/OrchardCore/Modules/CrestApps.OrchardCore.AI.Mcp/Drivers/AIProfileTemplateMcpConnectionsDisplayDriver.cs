using CrestApps.AI;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using CrestApps.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class AIProfileTemplateMcpConnectionsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ICatalog<McpConnection> _store;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateMcpConnectionsDisplayDriver(
        ICatalog<McpConnection> store,
        IStringLocalizer<AIProfileTemplateMcpConnectionsDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfileTemplate template, BuildEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        return Initialize<EditProfileMcpConnectionsViewModel>("EditProfileMcpConnection_Edit", model =>
        {
            var mcpMetadata = template.As<AIProfileMcpMetadata>();

            model.Connections = connections
            .Select(entry => new ToolEntry
            {
                ItemId = entry.ItemId,
                DisplayText = entry.DisplayText,
                IsSelected = mcpMetadata.ConnectionIds?.Contains(entry.ItemId) ?? false,
            }).OrderBy(entry => entry.DisplayText)
        .ToArray();

        }).Location("Content:3#Capabilities;8")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

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

        template.Put(metadata);

        return Edit(template, context);
    }
}
