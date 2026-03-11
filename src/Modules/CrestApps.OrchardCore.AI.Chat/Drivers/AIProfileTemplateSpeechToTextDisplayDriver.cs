using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIProfileTemplateSpeechToTextDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly ISiteService _siteService;

    public AIProfileTemplateSpeechToTextDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileSpeechToTextViewModel>("AIProfileSpeechToText_Edit", model =>
        {
            if (template.Properties.ContainsKey(nameof(SpeechToTextProfileSettings)))
            {
                var settings = template.As<SpeechToTextProfileSettings>();
                model.EnableSpeechToText = settings.EnableSpeechToText;
            }
        }).Location("Content:5.3")
        .RenderWhen(async () =>
        {
            if (template.Source != AITemplateSources.Profile)
            {
                return false;
            }

            var site = await _siteService.GetSiteSettingsAsync();
            var deploymentSettings = site.As<DefaultAIDeploymentSettings>();
            return !string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId);
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new AIProfileSpeechToTextViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = template.As<SpeechToTextProfileSettings>();
        settings.EnableSpeechToText = model.EnableSpeechToText;
        template.Put(settings);

        return Edit(template, context);
    }
}
