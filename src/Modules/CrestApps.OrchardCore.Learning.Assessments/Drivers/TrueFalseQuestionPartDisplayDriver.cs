using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class TrueFalseQuestionPartDisplayDriver : ContentPartDisplayDriver<TrueFalseQuestionPart>
{
    internal readonly IStringLocalizer S;

    public TrueFalseQuestionPartDisplayDriver(IStringLocalizer<TrueFalseQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TrueFalseQuestionPart part, BuildPartEditorContext context)
        => Initialize<SimpleQuestionViewModel>("TrueFalseQuestionPart_Edit", model =>
        {
            model.Answer = part.Answer;
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(TrueFalseQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new SimpleQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        part.Answer = model.Answer;

        return Edit(part, context);
    }
}
