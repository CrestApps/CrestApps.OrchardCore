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
/// Display driver for the per-profile anti-spam throttle overrides.
/// </summary>
public sealed class AIProfilePromptSecurityDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IOptions<PromptSecurityOptions> _promptSecurityOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfilePromptSecurityDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptSecurityOptions">The site-level prompt security options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfilePromptSecurityDisplayDriver(
        IOptions<PromptSecurityOptions> promptSecurityOptions,
        IStringLocalizer<AIProfilePromptSecurityDisplayDriver> stringLocalizer)
    {
        _promptSecurityOptions = promptSecurityOptions;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfilePromptSecurityViewModel>("AIProfilePromptSecurity_Edit", model =>
        {
            AIProfilePromptSecurityMapper.PopulateSiteDefaults(model, _promptSecurityOptions.Value);

            if (profile.TryGetSettings<PromptSecurityProfileSettings>(out var settings))
            {
                AIProfilePromptSecurityMapper.PopulateOverrides(model, settings);
            }
        }).Location("Content:20%Prompt Security;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfilePromptSecurityViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        AIProfilePromptSecurityMapper.Validate(model, context.Updater, Prefix, S);

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(profile, context);
        }

        profile.AlterSettings<PromptSecurityProfileSettings>(settings => AIProfilePromptSecurityMapper.ApplyOverrides(model, settings));

        return Edit(profile, context);
    }
}
