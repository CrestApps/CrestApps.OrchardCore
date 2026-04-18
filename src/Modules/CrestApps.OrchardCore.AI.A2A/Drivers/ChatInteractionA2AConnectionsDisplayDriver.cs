using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.A2A.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.A2A.Drivers;

internal sealed class ChatInteractionA2AConnectionsDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly ICatalog<A2AConnection> _store;

    internal readonly IStringLocalizer S;

    public ChatInteractionA2AConnectionsDisplayDriver(
        ICatalog<A2AConnection> store,
        IStringLocalizer<ChatInteractionA2AConnectionsDisplayDriver> stringLocalizer)
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

        return Initialize<ChatInteractionA2AConnectionsViewModel>("ChatInteractionA2AConnections_Edit", model =>
        {
            model.Connections = connections
            .Select(entry => new ToolEntry
            {
                ItemId = entry.ItemId,
                DisplayText = entry.DisplayText,
                IsSelected = interaction.A2AConnectionIds?.Contains(entry.ItemId) ?? false,
            }).OrderBy(entry => entry.DisplayText)
        .ToArray();

        }).Location("Parameters:4#Capabilities;5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        var model = new ChatInteractionA2AConnectionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var ids = model.Connections?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();

        if (ids is null || ids.Length == 0)
        {
            interaction.A2AConnectionIds = [];
        }
        else
        {
            interaction.A2AConnectionIds = connections.Select(x => x.ItemId)
                .Intersect(ids)
                .ToList();
        }

        return Edit(interaction, context);
    }
}
