using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

/// <summary>
/// Display driver for the AI profile memory shape.
/// </summary>
public sealed class AIProfileMemoryDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileMemoryDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public AIProfileMemoryDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileMemoryViewModel>("AIProfileMemory_Edit", async model =>
        {
            model.EnableUserMemory = profile.GetMemoryMetadata().EnableUserMemory ?? false;
            model.HasIndexProfile = !string.IsNullOrEmpty((await _siteService.GetSettingsAsync<AIMemorySettings>()).IndexProfileName);
        }).Location("Content:1#Knowledge;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileMemoryViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterMemoryMetadata(settings =>
        {
            settings.EnableUserMemory = model.EnableUserMemory;
        });

        return Edit(profile, context);
    }
}
