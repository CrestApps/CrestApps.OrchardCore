using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileMenuDisplayDriver : DisplayDriver<AIProfile>
{
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

        }).Location("Content:10%General;1");
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
