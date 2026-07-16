using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the per-template anti-spam throttle overrides.
/// Applies only to templates whose source is <see cref="AITemplateSources.Profile"/>.
/// </summary>
public sealed class AIProfileTemplatePromptSecurityDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IOptions<PromptSecurityOptions> _promptSecurityOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplatePromptSecurityDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptSecurityOptions">The site-level prompt security options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplatePromptSecurityDisplayDriver(
        IOptions<PromptSecurityOptions> promptSecurityOptions,
        IStringLocalizer<AIProfileTemplatePromptSecurityDisplayDriver> stringLocalizer)
    {
        _promptSecurityOptions = promptSecurityOptions;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfilePromptSecurityViewModel>("AIProfilePromptSecurity_Edit", model =>
        {
            AIProfilePromptSecurityMapper.PopulateSiteDefaults(model, _promptSecurityOptions.Value);

            if (template.Properties.ContainsKey(nameof(PromptSecurityProfileSettings)))
            {
                AIProfilePromptSecurityMapper.PopulateOverrides(model, template.GetOrCreate<PromptSecurityProfileSettings>());
            }
        }).Location("Content:1%Prompt Security;100")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new AIProfilePromptSecurityViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        AIProfilePromptSecurityMapper.Validate(model, context.Updater, Prefix, S);

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(template, context);
        }

        var settings = template.GetOrCreate<PromptSecurityProfileSettings>();
        AIProfilePromptSecurityMapper.ApplyOverrides(model, settings);
        template.Put(settings);

        return Edit(template, context);
    }
}
