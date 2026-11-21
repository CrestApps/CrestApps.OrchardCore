using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class OrderingQuestionPartDisplayDriver : ContentPartDisplayDriver<OrderingQuestionPart>
{
    internal readonly IStringLocalizer S;

    public OrderingQuestionPartDisplayDriver(IStringLocalizer<OrderingQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OrderingQuestionPart part, BuildPartEditorContext context)
        => Initialize<RowsQuestionViewModel>("RowsQuestion_Edit", model =>
        {
            model.Kind = "Ordering";
            model.PartialCredit = part.PartialCredit;
            model.Rows = part.Items.Select(item => new OptionRowViewModel { Id = item.Id, Text = item.Text }).ToList();

            while (model.Rows.Count < part.Items.Count + 3)
            {
                model.Rows.Add(new OptionRowViewModel());
            }
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(OrderingQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new RowsQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Items = model.Rows.Where(row => !string.IsNullOrWhiteSpace(row.Text))
            .Select(row => new ChoiceOption { Id = RowIds.Ensure(row.Id), Text = row.Text.Trim() })
            .ToList();
        part.PartialCredit = model.PartialCredit;

        if (part.Items.Count < 2)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Give at least two items, in their right order."]);
        }

        return Edit(part, context);
    }
}
