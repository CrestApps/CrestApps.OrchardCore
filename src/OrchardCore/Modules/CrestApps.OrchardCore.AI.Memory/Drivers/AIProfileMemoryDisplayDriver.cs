using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Memory.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

public sealed class AIProfileMemoryDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;
    public AIProfileMemoryDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileMemoryViewModel>("AIProfileMemory_Edit", async model =>
        {
            model.EnableUserMemory = profile.GetSettings<AIProfileMemorySettings>().EnableUserMemory;
            model.HasIndexProfile = !string.IsNullOrEmpty((await _siteService.GetSettingsAsync<AIMemorySettings>()).IndexProfileName);
        }).Location("Content:15%Interactions;3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileMemoryViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        profile.AlterSettings<AIProfileMemorySettings>(settings =>
        {
            settings.EnableUserMemory = model.EnableUserMemory;
        });

        return Edit(profile, context);
    }
}
