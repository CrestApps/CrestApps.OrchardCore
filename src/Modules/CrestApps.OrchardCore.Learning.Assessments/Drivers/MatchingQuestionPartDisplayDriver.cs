using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class MatchingQuestionPartDisplayDriver : ContentPartDisplayDriver<MatchingQuestionPart>
{
    internal readonly IStringLocalizer S;

    public MatchingQuestionPartDisplayDriver(IStringLocalizer<MatchingQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(MatchingQuestionPart part, BuildPartEditorContext context)
        => Initialize<RowsQuestionViewModel>("RowsQuestion_Edit", model =>
        {
            model.Kind = "Matching";
            model.PartialCredit = context.IsNew || part.PartialCredit;
            model.Rows = part.Pairs.Select(pair => new OptionRowViewModel { Id = pair.Id, Text = pair.Prompt, Answer = pair.Answer }).ToList();

            while (model.Rows.Count < part.Pairs.Count + 3)
            {
                model.Rows.Add(new OptionRowViewModel());
            }
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(MatchingQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new RowsQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Pairs = model.Rows.Where(row => !string.IsNullOrWhiteSpace(row.Text) && !string.IsNullOrWhiteSpace(row.Answer))
            .Select(row => new MatchingPair { Id = RowIds.Ensure(row.Id), Prompt = row.Text.Trim(), Answer = row.Answer.Trim() })
            .ToList();
        part.PartialCredit = model.PartialCredit;

        if (part.Pairs.Count < 2)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Give at least two pairs."]);
        }

        return Edit(part, context);
    }
}
