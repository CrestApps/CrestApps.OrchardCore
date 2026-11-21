using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class ChoiceBlanksQuestionPartDisplayDriver : ContentPartDisplayDriver<ChoiceBlanksQuestionPart>
{
    internal readonly IStringLocalizer S;

    public ChoiceBlanksQuestionPartDisplayDriver(IStringLocalizer<ChoiceBlanksQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChoiceBlanksQuestionPart part, BuildPartEditorContext context)
        => Initialize<ChoiceBlanksQuestionViewModel>("ChoiceBlanksQuestionPart_Edit", model =>
        {
            model.Text = part.Text;
            model.Distractors = string.Join(Environment.NewLine, part.Distractors ?? []);
            model.ShuffleChoices = context.IsNew || part.ShuffleChoices;
            model.DragIntoText = part.ContentItem.ContentType == AssessmentsConstants.ContentTypes.DragIntoTextQuestion;
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(ChoiceBlanksQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new ChoiceBlanksQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Text = model.Text?.Trim();
        part.Distractors = (model.Distractors ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        part.ShuffleChoices = model.ShuffleChoices;

        if (string.IsNullOrEmpty(part.Text) || !part.Text.Contains("[[", StringComparison.Ordinal))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Text), S["Write the text with at least one blank, such as [[word]]."]);
        }

        return Edit(part, context);
    }
}
