using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileSessionSettingsDisplayDriver : DisplayDriver<AIProfile>
{
    internal readonly IStringLocalizer S;

    public AIProfileSessionSettingsDisplayDriver(
        IStringLocalizer<AIProfileSessionSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSessionSettingsViewModel>("AIProfileSessionSettings_Edit", model =>
        {
            var dataExtractionSettings = profile.GetSettings<AIProfileDataExtractionSettings>();
            var analyticsMetadata = profile.As<AnalyticsMetadata>();

            model.SessionInactivityTimeoutInMinutes = dataExtractionSettings.SessionInactivityTimeoutInMinutes;
            model.EnableAIResolutionDetection = analyticsMetadata.EnableAIResolutionDetection;
        }).Location("Content:10#Data Processing & Metrics:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileSessionSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.SessionInactivityTimeoutInMinutes < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SessionInactivityTimeoutInMinutes), S["Session Inactivity Timeout must be at least 1 minute."]);
        }

        profile.AlterSettings<AIProfileDataExtractionSettings>(settings =>
        {
            settings.SessionInactivityTimeoutInMinutes = model.SessionInactivityTimeoutInMinutes;
        });

        var analyticsMetadata = profile.As<AnalyticsMetadata>();
        analyticsMetadata.EnableAIResolutionDetection = model.EnableAIResolutionDetection;
        profile.Put(analyticsMetadata);

        return Edit(profile, context);
    }
}
