using CrestApps.Core.AI;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

/// <summary>
/// Display driver for the AI profile template memory shape.
/// </summary>
public sealed class AIProfileTemplateMemoryDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateMemoryDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateMemoryDisplayDriver(
        ISiteService siteService,
        IStringLocalizer<AIProfileTemplateMemoryDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditAIProfileMemoryViewModel>("AIProfileMemory_Edit", async model =>
        {
            model.EnableUserMemory = template.GetMemoryMetadata().EnableUserMemory ?? false;
            model.HasIndexProfile = !string.IsNullOrEmpty((await _siteService.GetSettingsAsync<AIMemorySettings>()).IndexProfileName);
        }).Location(l => l.Zone("Content", "1").Tab(S["Knowledge"], "2").Card(S["Memory"], "30"))
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
