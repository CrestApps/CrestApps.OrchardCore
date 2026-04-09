using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

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
                var profileMetadata = template.As<ProfileTemplateMetadata>();
                model.IsOnAdminMenu = profileMetadata.ProfileType == AIProfileType.Chat;
            }
        }).Location("Content:7%General;1")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new AIProfileMenuViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = template.As<AIChatProfileSettings>();
        settings.IsOnAdminMenu = model.IsOnAdminMenu;
        template.Put(settings);

        return Edit(template, context);
    }
}
