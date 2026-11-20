using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class SurveyQuestionPartDisplayDriver : ContentPartDisplayDriver<SurveyQuestionPart>
{
    internal readonly IStringLocalizer S;

    public SurveyQuestionPartDisplayDriver(IStringLocalizer<SurveyQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SurveyQuestionPart part, BuildPartEditorContext context)
        => Initialize<SurveyQuestionViewModel>("SurveyQuestionPart_Edit", model =>
        {
            model.Multiple = part.Multiple;
            model.Options = part.Options.Select(option => new OptionRowViewModel { Id = option.Id, Text = option.Text, Feedback = option.Feedback }).ToList();

            while (model.Options.Count < part.Options.Count + 3)
            {
                model.Options.Add(new OptionRowViewModel());
            }
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new SurveyQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Multiple = model.Multiple;
        part.Options = (model.Options ?? []).Where(row => !string.IsNullOrWhiteSpace(row?.Text))
            .Select(row => new ChoiceOption
            {
                Id = RowIds.Ensure(row.Id),
                Text = row.Text.Trim(),
                Feedback = string.IsNullOrWhiteSpace(row.Feedback) ? null : row.Feedback.Trim(),
            })
            .ToList();

        if (part.Options.Count < 2)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Options), S["Give at least two options."]);
        }

        return Edit(part, context);
    }
}
