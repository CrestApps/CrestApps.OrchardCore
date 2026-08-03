using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Display driver that contributes the tools selection to the
/// <see cref="AICompletionWithConfigTask"/> workflow activity Capabilities tab.
/// </summary>
public sealed class AICompletionWithConfigToolsDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigToolsDisplayDriver"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The AI tool definition options.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AICompletionWithConfigToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IStringLocalizer<AICompletionWithConfigToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        return Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
        {
            var interaction = activity.Interaction;

            model.Tools = _toolDefinitions.GetSelectableTools()
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = interaction.ToolNames?.Contains(entry.Key) ?? false,
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }).Location("Content:1#Capabilities;3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var model = new EditProfileToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

        var interaction = activity.Interaction;

        interaction.ToolNames = selectedToolKeys is null || !selectedToolKeys.Any()
            ? []
            : _toolDefinitions.GetSelectableTools().Keys
                .Intersect(selectedToolKeys)
                .ToList();

        activity.Interaction = interaction;

        return Edit(activity, context);
    }
}
