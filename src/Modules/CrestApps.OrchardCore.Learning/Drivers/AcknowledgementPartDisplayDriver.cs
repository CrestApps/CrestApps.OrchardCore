using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The editor of the acknowledgement part: the statement the learner confirms.
/// </summary>
internal sealed class AcknowledgementPartDisplayDriver : ContentPartDisplayDriver<AcknowledgementPart>
{
    internal readonly IStringLocalizer S;

    public AcknowledgementPartDisplayDriver(IStringLocalizer<AcknowledgementPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AcknowledgementPart part, BuildPartEditorContext context)
    {
        return Initialize<LearningPartTextViewModel>(GetEditorShapeType(context), model =>
        {
            model.Statement = context.IsNew && string.IsNullOrEmpty(part.Statement)
                ? S["I have read and understood this policy."].Value
                : part.Statement;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AcknowledgementPart part, UpdatePartEditorContext context)
    {
        var model = new LearningPartTextViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Statement))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Statement), S["Write the statement the learner confirms."]);
        }

        part.Statement = model.Statement?.Trim();

        return Edit(part, context);
    }
}
