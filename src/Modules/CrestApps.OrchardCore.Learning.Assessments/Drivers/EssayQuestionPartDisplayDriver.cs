using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class EssayQuestionPartDisplayDriver : ContentPartDisplayDriver<EssayQuestionPart>
{
    internal readonly IStringLocalizer S;

    public EssayQuestionPartDisplayDriver(IStringLocalizer<EssayQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(EssayQuestionPart part, BuildPartEditorContext context)
        => Initialize<SimpleQuestionViewModel>("EssayQuestionPart_Edit", model =>
        {
            model.MinWords = part.MinWords;
            model.MaxWords = part.MaxWords;
            model.GraderGuidance = part.GraderGuidance;
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(EssayQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new SimpleQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        part.MinWords = Math.Max(model.MinWords, 0);
        part.MaxWords = Math.Max(model.MaxWords, 0);
        part.GraderGuidance = string.IsNullOrWhiteSpace(model.GraderGuidance) ? null : model.GraderGuidance.Trim();

        if (part.MaxWords > 0 && part.MaxWords < part.MinWords)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.MaxWords), S["The most words must be at least the fewest words."]);
        }

        return Edit(part, context);
    }
}
