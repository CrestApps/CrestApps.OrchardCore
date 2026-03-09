using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileTemplateMenuDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileMenuViewModel>("AIProfileMenu_Edit", model =>
        {
            if (template.Properties.ContainsKey(nameof(AIChatProfileSettings)))
            {
                var settings = template.As<AIChatProfileSettings>();
                model.IsOnAdminMenu = settings.IsOnAdminMenu;
            }
            else
            {
                model.IsOnAdminMenu = template.ProfileType == AIProfileType.Chat;
            }
        }).Location("Content:5.2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var model = new AIProfileMenuViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = template.As<AIChatProfileSettings>();
        settings.IsOnAdminMenu = model.IsOnAdminMenu;
        template.Put(settings);

        return Edit(template, context);
    }
}
