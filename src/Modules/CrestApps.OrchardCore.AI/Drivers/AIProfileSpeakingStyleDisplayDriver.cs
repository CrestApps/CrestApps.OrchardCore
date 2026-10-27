using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits the speaking styles of an AI profile.
/// </summary>
public sealed class AIProfileSpeakingStyleDisplayDriver : DisplayDriver<AIProfile>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileSpeakingStyleViewModel>("AIProfileSpeakingStyle_Edit", model =>
        {
            model.Styles = profile.TryGetSettings<AIProfileSpeakingStyleSettings>(out var settings)
                ? AISpeakingStyles.Normalize(settings.Styles)
                : [];
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
