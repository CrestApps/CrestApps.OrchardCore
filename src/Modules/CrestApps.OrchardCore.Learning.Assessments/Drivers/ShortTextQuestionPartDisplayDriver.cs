using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class ShortTextQuestionPartDisplayDriver : ContentPartDisplayDriver<ShortTextQuestionPart>
{
    internal readonly IStringLocalizer S;

    public ShortTextQuestionPartDisplayDriver(IStringLocalizer<ShortTextQuestionPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShortTextQuestionPart part, BuildPartEditorContext context)
        => Initialize<SimpleQuestionViewModel>("ShortTextQuestionPart_Edit", model =>
        {
            model.AcceptedAnswers = string.Join("\n", part.AcceptedAnswers ?? []);
            model.CaseSensitive = part.CaseSensitive;
        }).Location("Content:3");

    public override async Task<IDisplayResult> UpdateAsync(ShortTextQuestionPart part, UpdatePartEditorContext context)
    {
        var model = new SimpleQuestionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        part.AcceptedAnswers = (model.AcceptedAnswers ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
        part.CaseSensitive = model.CaseSensitive;

        if (part.AcceptedAnswers.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.AcceptedAnswers), S["Give at least one accepted answer."]);
        }

        return Edit(part, context);
    }
}
