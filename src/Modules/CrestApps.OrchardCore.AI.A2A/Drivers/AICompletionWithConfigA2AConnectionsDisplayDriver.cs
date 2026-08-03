using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.A2A.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.A2A.Drivers;

/// <summary>
/// Contributes the A2A connections selection to the <see cref="AICompletionWithConfigTask"/>
/// workflow activity Capabilities tab.
/// </summary>
public sealed class AICompletionWithConfigA2AConnectionsDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly ICatalog<A2AConnection> _store;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigA2AConnectionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The A2A connection store.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AICompletionWithConfigA2AConnectionsDisplayDriver(
        ICatalog<A2AConnection> store,
        IStringLocalizer<AICompletionWithConfigA2AConnectionsDisplayDriver> stringLocalizer)
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

        return Initialize<ChatInteractionA2AConnectionsViewModel>("ChatInteractionA2AConnections_Edit", model =>
        {
            var interaction = activity.Interaction;

            model.Connections = connections
                .Select(entry => new ToolEntry
                {
                    ItemId = entry.ItemId,
                    DisplayText = entry.DisplayText,
                    IsSelected = interaction.A2AConnectionIds?.Contains(entry.ItemId) ?? false,
                }).OrderBy(entry => entry.DisplayText)
                .ToArray();
        }).Location("Content:3#Capabilities;3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var connections = await _store.GetAllAsync();

        if (connections.Count == 0)
        {
            return null;
        }

        var model = new ChatInteractionA2AConnectionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var ids = model.Connections?.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();

        var interaction = activity.Interaction;

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

        activity.Interaction = interaction;

        return Edit(activity, context);
    }
}
