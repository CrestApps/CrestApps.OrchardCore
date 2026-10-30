using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Edits whether an AI profile reads and writes contact memory.
/// </summary>
public sealed class AIProfileContactMemoryDisplayDriver : DisplayDriver<AIProfile>
{
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileContactMemoryViewModel>("AIProfileContactMemory_Edit", model =>
        {
            var settings = profile.GetSettings<AIProfileContactMemorySettings>();
            model.UseMemory = settings.UseMemory;
            model.SaveMemory = settings.SaveMemory;
        }).Location("Content:1#Data Processing & Metrics;12");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileContactMemoryViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<AIProfileContactMemorySettings>(settings =>
        {
            settings.UseMemory = model.UseMemory;
            settings.SaveMemory = model.SaveMemory;
        });

        return Edit(profile, context);
    }
}
