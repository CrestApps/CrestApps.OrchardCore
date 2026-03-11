using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileSpeechToTextDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;

    public AIProfileSpeechToTextDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileSpeechToTextViewModel>("AIProfileSpeechToText_Edit", model =>
        {
            if (profile.TryGetSettings<SpeechToTextProfileSettings>(out var settings))
            {
                model.EnableSpeechToText = settings.EnableSpeechToText;
            }
        }).Location("Content:5.3")
        .RenderWhen(async () =>
        {
            var site = await _siteService.GetSiteSettingsAsync();
            var deploymentSettings = site.As<DefaultAIDeploymentSettings>();
            return !string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId);
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfileSpeechToTextViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<SpeechToTextProfileSettings>(settings =>
        {
            settings.EnableSpeechToText = model.EnableSpeechToText;
        });

        return Edit(profile, context);
    }
}
