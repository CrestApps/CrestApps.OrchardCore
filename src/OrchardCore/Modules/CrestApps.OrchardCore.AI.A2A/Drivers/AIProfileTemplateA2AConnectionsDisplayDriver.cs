using CrestApps.AI;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.A2A.ViewModels;
using CrestApps.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using CrestApps;

namespace CrestApps.OrchardCore.AI.A2A.Drivers;

internal sealed class AIProfileTemplateA2AConnectionsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ICatalog<A2AConnection> _store;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateA2AConnectionsDisplayDriver(
        ICatalog<A2AConnection> store,
        IStringLocalizer<AIProfileTemplateA2AConnectionsDisplayDriver> stringLocalizer)
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

        return Initialize<EditProfileA2AConnectionsViewModel>("EditProfileA2AConnection_Edit", model =>
        {
            var a2aMetadata = template.As<AIProfileA2AMetadata>();

            model.Connections = connections
            .Select(entry => new ToolEntry
            {
                ItemId = entry.ItemId,
                DisplayText = entry.DisplayText,
                IsSelected = a2aMetadata.ConnectionIds?.Contains(entry.ItemId) ?? false,
            }).OrderBy(entry => entry.DisplayText)
        .ToArray();

        }).Location("Content:4#Capabilities;8")
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

        var model = new EditProfileA2AConnectionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var ids = model.Connections?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();

        var metadata = new AIProfileA2AMetadata();

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
