using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.Extensions.Localization;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileMemoryDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileMemoryDisplayDriver(
        ISiteService siteService,
        IStringLocalizer<AIProfileMemoryDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileMemoryViewModel>("AIProfileMemory_Edit", async model =>
        {
            model.EnableUserMemory = profile.GetMemoryMetadata().EnableUserMemory ?? false;
            model.HasIndexProfile = !string.IsNullOrEmpty((await _siteService.GetSettingsAsync<AIMemorySettings>()).IndexProfileName);
        }).Location(l => l.Zone("Content", "1").Tab(S["Knowledge"], "2").Card(S["Memory"], "30"));
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
