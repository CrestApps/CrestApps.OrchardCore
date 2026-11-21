using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class ChoiceQuestionPartDisplayDriver : ContentPartDisplayDriver<ChoiceQuestionPart>
{
    internal readonly IStringLocalizer S;

    public ChoiceQuestionPartDisplayDriver(IStringLocalizer<ChoiceQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChoiceQuestionPart part, BuildPartEditorContext context)
        => Initialize<RowsQuestionViewModel>("RowsQuestion_Edit", model =>
        {
            model.Kind = "Choice";
            model.Multiple = part.ContentItem.ContentType == AssessmentsConstants.ContentTypes.MultipleChoiceQuestion;
            model.Shuffle = context.IsNew || part.ShuffleOptions;
            model.PartialCredit = part.PartialCredit;
            model.Rows = part.Options.Select(option => new OptionRowViewModel { Id = option.Id, Text = option.Text, Correct = option.Correct, Feedback = option.Feedback }).ToList();

            while (model.Rows.Count < part.Options.Count + 3)
            {
                model.Rows.Add(new OptionRowViewModel());
            }
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(ChoiceQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new RowsQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var options = model.Rows.Where(row => !string.IsNullOrWhiteSpace(row.Text))
            .Select(row => new ChoiceOption { Id = RowIds.Ensure(row.Id), Text = row.Text.Trim(), Correct = row.Correct, Feedback = string.IsNullOrWhiteSpace(row.Feedback) ? null : row.Feedback.Trim() })
            .ToList();
        var multiple = part.ContentItem.ContentType == AssessmentsConstants.ContentTypes.MultipleChoiceQuestion;
        var correct = options.Count(option => option.Correct);

        if (options.Count < 2)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Give at least two options."]);
        }
        else if (correct == 0 || (!multiple && correct > 1))
        {
            context.Updater.ModelState.AddModelError(Prefix, multiple ? S["Mark at least one option as right."] : S["Mark exactly one option as right."]);
        }

        part.Options = options;
        part.ShuffleOptions = model.Shuffle;
        part.PartialCredit = multiple && model.PartialCredit;

        return Edit(part, context);
    }
}
