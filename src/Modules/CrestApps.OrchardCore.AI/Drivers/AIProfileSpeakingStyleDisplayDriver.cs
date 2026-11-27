using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits the speaking styles of an AI profile, chosen from the prompt templates in the speaking style category.
/// </summary>
public sealed class AIProfileSpeakingStyleDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ITemplateService _templateService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileSpeakingStyleDisplayDriver"/> class.
    /// </summary>
    /// <param name="templateService">The template service the styles are listed from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileSpeakingStyleDisplayDriver(
        ITemplateService templateService,
        IStringLocalizer<AIProfileSpeakingStyleDisplayDriver> stringLocalizer)
    {
        _templateService = templateService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSpeakingStyleViewModel>("AIProfileSpeakingStyle_Edit", async model =>
        {
            model.Styles = profile.TryGetSettings<AIProfileSpeakingStyleSettings>(out var settings)
                ? AISpeakingStyles.Normalize(settings.Styles)
                : [];
            model.AvailableStyles = await _templateService.ListOptionsByCategoryAsync(AISpeakingStyles.Category);
        }).Location(l => l.Zone("Content", "1").Card(S["Speaking Style"], "4.5"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileSpeakingStyleViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<AIProfileSpeakingStyleSettings>(settings => settings.Styles = AISpeakingStyles.Normalize(model.Styles));

        return Edit(profile, context);
    }
}
