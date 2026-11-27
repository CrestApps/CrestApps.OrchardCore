using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI profile template menu shape.
/// </summary>
public sealed class AIProfileTemplateMenuDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateMenuDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateMenuDisplayDriver(IStringLocalizer<AIProfileTemplateMenuDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileMenuViewModel>("AIProfileMenu_Edit", model =>
        {
            if (template.TryGet<AIChatProfileSettings>(out var settings))
            {
                model.IsOnAdminMenu = settings.IsOnAdminMenu;
            }
            else
            {
                var profileMetadata = template.GetOrCreate<ProfileTemplateMetadata>();
                model.IsOnAdminMenu = profileMetadata.ProfileType == AIProfileType.Chat;
            }
        }).Location(l => l.Zone("Content", "7").Card(S["General"], "1"))
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

        var settings = template.GetOrCreate<AIChatProfileSettings>();
        settings.IsOnAdminMenu = model.IsOnAdminMenu;
        template.Put(settings);

        return Edit(template, context);
    }
}
