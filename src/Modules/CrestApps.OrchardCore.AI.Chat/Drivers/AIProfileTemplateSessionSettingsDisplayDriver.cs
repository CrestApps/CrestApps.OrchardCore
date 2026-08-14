using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI profile template session settings shape.
/// </summary>
public sealed class AIProfileTemplateSessionSettingsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateSessionSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateSessionSettingsDisplayDriver(
        IStringLocalizer<AIProfileTemplateSessionSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSessionSettingsViewModel>("AIProfileSessionSettings_Edit", model =>
        {
            var dataExtractionSettings = template.GetOrCreate<AIProfileDataExtractionSettings>();
            model.SessionInactivityTimeoutInMinutes = dataExtractionSettings.SessionInactivityTimeoutInMinutes;
        }).Location("Content:1#Data Processing & Metrics;10")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditAIProfileSessionSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.SessionInactivityTimeoutInMinutes < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionInactivityTimeoutInMinutes), S["Session Inactivity Timeout must be at least 1 minute."]);
        }

        var dataExtractionSettings = template.GetOrCreate<AIProfileDataExtractionSettings>();
        dataExtractionSettings.SessionInactivityTimeoutInMinutes = model.SessionInactivityTimeoutInMinutes;
        template.Put(dataExtractionSettings);

        return Edit(template, context);
    }
}
