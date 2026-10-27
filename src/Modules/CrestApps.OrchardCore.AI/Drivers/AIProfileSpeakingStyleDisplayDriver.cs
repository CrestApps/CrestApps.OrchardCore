using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits the speaking styles of an AI profile, chosen from the prompt templates in the speaking style category.
/// </summary>
public sealed class AIProfileSpeakingStyleDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ITemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileSpeakingStyleDisplayDriver"/> class.
    /// </summary>
    /// <param name="templateService">The template service the styles are listed from.</param>
    public AIProfileSpeakingStyleDisplayDriver(ITemplateService templateService)
    {
        _templateService = templateService;
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
        }).Location("Content:20%Instructions;4");
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
