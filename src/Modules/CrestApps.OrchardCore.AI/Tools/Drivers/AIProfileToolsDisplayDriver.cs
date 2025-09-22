using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class AIProfileToolsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;

    internal readonly IStringLocalizer S;

    public AIProfileToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IStringLocalizer<AIProfileToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        return Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
        {
            var metadata = profile.As<AIProfileFunctionInvocationMetadata>();

            model.Tools = _toolDefinitions.Tools
            .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
            {
                ItemId = entry.Key,
                DisplayText = entry.Value.Title,
                Description = entry.Value.Description,
                IsSelected = metadata.Names?.Contains(entry.Key) ?? false,
            }).OrderBy(entry => entry.DisplayText).ToArray());

        }).Location("Content:8#Capabilities:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var model = new EditProfileToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();

        var metadata = new AIProfileFunctionInvocationMetadata();

        if (selectedToolKeys is null || selectedToolKeys.Length == 0)
        {
            metadata.Names = [];
        }
        else
        {
            metadata.Names = _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToArray();
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
