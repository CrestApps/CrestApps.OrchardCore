using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Edits whether an AI profile reads and writes contact memory.
/// </summary>
public sealed class AIProfileContactMemoryDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileContactMemoryDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileContactMemoryDisplayDriver(IStringLocalizer<AIProfileContactMemoryDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileContactMemoryViewModel>("AIProfileContactMemory_Edit", model =>
        {
            var settings = profile.GetSettings<AIProfileContactMemorySettings>();
            model.UseMemory = settings.UseMemory;
            model.SaveMemory = settings.SaveMemory;
        }).Location(l => l.Zone("Content", "1").Tab(S["Data Processing & Metrics"], "12").Card(S["Contact Memory"], "2"));
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
