using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

public sealed class AIProfileToolsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IModelStore<AIToolInstance> _toolInstanceStore;

    internal readonly IStringLocalizer S;

    public AIProfileToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IModelStore<AIToolInstance> toolInstanceStore,
        IStringLocalizer<AIProfileToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _toolInstanceStore = toolInstanceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", async model =>
        {
            var toolMetadata = profile.As<AIProfileFunctionInvocationMetadata>();

            model.Tools = _toolDefinitions.Tools
            .Select(entry => new ToolEntry
            {
                Id = entry.Key,
                DisplayText = entry.Value.Title,
                Description = entry.Value.Description,
                IsSelected = toolMetadata.Names?.Contains(entry.Key) ?? false,
            }).OrderBy(entry => entry.DisplayText)
            .ToArray();

            var instances = await _toolInstanceStore.GetAllAsync();

            model.Instances = instances
            .Select(instance => new ToolEntry
            {
                Id = instance.Id,
                DisplayText = instance.DisplayText,
                Description = instance.As<InvokableToolMetadata>()?.Description,
                IsSelected = toolMetadata.InstanceIds?.Contains(instance.Id) ?? false,
            }).OrderBy(entry => entry.DisplayText)
            .ToArray();

        }).Location("Content:8");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Where(x => x.IsSelected).Select(x => x.Id).ToArray();

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

        metadata.InstanceIds = model.Instances?.Where(x => x.IsSelected).Select(x => x.Id).ToArray() ?? [];

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
