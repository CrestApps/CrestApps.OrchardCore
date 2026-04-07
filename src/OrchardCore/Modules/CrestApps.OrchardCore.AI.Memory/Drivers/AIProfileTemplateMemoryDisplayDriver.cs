using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Memory.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

public sealed class AIProfileTemplateMemoryDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ISiteService _siteService;

    public AIProfileTemplateMemoryDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditAIProfileMemoryViewModel>("AIProfileMemory_Edit", async model =>
        {
            model.EnableUserMemory = template.GetMemoryMetadata().EnableUserMemory ?? false;
            model.HasIndexProfile = !string.IsNullOrEmpty((await _siteService.GetSettingsAsync<AIMemorySettings>()).IndexProfileName);
        }).Location("Content:1#Knowledge;2")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditAIProfileMemoryViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        template.WithMemoryMetadata(new MemoryMetadata
        {
            EnableUserMemory = model.EnableUserMemory,
        });

        return Edit(template, context);
    }
}
