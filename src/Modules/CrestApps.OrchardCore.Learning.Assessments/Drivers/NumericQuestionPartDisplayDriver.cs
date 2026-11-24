using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class NumericQuestionPartDisplayDriver : ContentPartDisplayDriver<NumericQuestionPart>
{
    internal readonly IStringLocalizer S;

    public NumericQuestionPartDisplayDriver(IStringLocalizer<NumericQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(NumericQuestionPart part, BuildPartEditorContext context)
        => Initialize<SimpleQuestionViewModel>("NumericQuestionPart_Edit", model =>
        {
            model.NumberAnswer = part.Answer;
            model.Tolerance = part.Tolerance;
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(NumericQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new SimpleQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        part.Answer = model.NumberAnswer;
        part.Tolerance = Math.Abs(model.Tolerance);

        return Edit(part, context);
    }
}
