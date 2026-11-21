using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class FillInBlanksQuestionPartDisplayDriver : ContentPartDisplayDriver<FillInBlanksQuestionPart>
{
    internal readonly IStringLocalizer S;

    public FillInBlanksQuestionPartDisplayDriver(IStringLocalizer<FillInBlanksQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(FillInBlanksQuestionPart part, BuildPartEditorContext context)
        => Initialize<SimpleQuestionViewModel>("FillInBlanksQuestionPart_Edit", model =>
        {
            model.Text = part.Text;
            model.CaseSensitive = part.CaseSensitive;
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(FillInBlanksQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new SimpleQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        part.Text = model.Text?.Trim();
        part.CaseSensitive = model.CaseSensitive;

        if (string.IsNullOrEmpty(part.Text) || !part.Text.Contains("[[", StringComparison.Ordinal))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Text), S["Write the text with at least one blank, such as [[answer]]."]);
        }

        return Edit(part, context);
    }
}
