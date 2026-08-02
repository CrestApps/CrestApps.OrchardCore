using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Tools.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Display driver that contributes the AI tool instances selection to the
/// <see cref="AICompletionWithConfigTask"/> workflow activity Capabilities tab. The selection is stored
/// on the embedded interaction so the shared completion context builder resolves the instances at execution time.
/// </summary>
public sealed class AICompletionWithConfigToolInstancesDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly IAIToolInstanceAccessor _instanceAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    public AICompletionWithConfigToolInstancesDisplayDriver(IAIToolInstanceAccessor instanceAccessor)
    {
        _instanceAccessor = instanceAccessor;
    }

    public override async Task<IDisplayResult> EditAsync(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();

        if (accessibleInstances.Count == 0)
        {
            return null;
        }

        return Initialize<EditToolInstancesViewModel>("EditToolInstances_Edit", model =>
        {
            var interaction = activity.Interaction;

            var selectedNames = interaction.GetOrCreate<AIToolInstanceMetadata>().ToolInstanceNames ?? [];

            model.Instances = accessibleInstances
                .Select(instance => new ToolInstanceEntry
                {
                    Name = instance.Name,
                    Description = instance.Description,
                    IsSelected = selectedNames.Contains(instance.Name, StringComparer.OrdinalIgnoreCase),
                })
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }).Location("Content:4#Capabilities;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();

        if (accessibleInstances.Count == 0)
        {
            return null;
        }

        var model = new EditToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedNames = model.Instances?
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.Name);

        var interaction = activity.Interaction;

        var metadata = interaction.GetOrCreate<AIToolInstanceMetadata>();

        metadata.ToolInstanceNames = selectedNames is null
            ? []
            : accessibleInstances
                .Select(instance => instance.Name)
                .Intersect(selectedNames, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        interaction.Put(metadata);

        activity.Interaction = interaction;

        return await EditAsync(activity, context);
    }
}
