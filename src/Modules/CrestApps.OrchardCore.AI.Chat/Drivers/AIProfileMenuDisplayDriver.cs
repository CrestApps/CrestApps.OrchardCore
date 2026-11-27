using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI profile menu shape.
/// </summary>
public sealed class AIProfileMenuDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileMenuDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileMenuDisplayDriver(IStringLocalizer<AIProfileMenuDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileMenuViewModel>("AIProfileMenu_Edit", model =>
        {
            if (profile.TryGetSettings<AIChatProfileSettings>(out var settings))
            {
                model.IsOnAdminMenu = settings.IsOnAdminMenu;
            }
            else
            {
                model.IsOnAdminMenu = profile.Type == AIProfileType.Chat && context.IsNew;
            }
        }).Location(l => l.Zone("Content", "7").Card(S["General"], "1"));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfileMenuViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<AIChatProfileSettings>(settings =>
        {
            settings.IsOnAdminMenu = model.IsOnAdminMenu;
        });

        return Edit(profile, context);
    }
}
