using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class QuestionPartDisplayDriver : ContentPartDisplayDriver<QuestionPart>
{
    private readonly IHtmlSanitizerService _htmlSanitizer;

    internal readonly IStringLocalizer S;

    public QuestionPartDisplayDriver(IHtmlSanitizerService htmlSanitizer, IStringLocalizer<QuestionPartDisplayDriver> stringLocalizer)
    {
        _htmlSanitizer = htmlSanitizer;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(QuestionPart part, BuildPartEditorContext context)
        => Initialize<QuestionPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Prompt = part.Prompt;
            model.Points = part.Points;
            model.Difficulty = part.Difficulty;
            model.Tags = string.Join(", ", part.Tags ?? []);
            model.GeneralFeedback = part.GeneralFeedback;
            model.SourceIdentifier = part.SourceIdentifier;
            model.SourceFile = part.SourceFile;
        });

    public override async Task<IDisplayResult> UpdateAsync(QuestionPart part, UpdatePartEditorContext context)
    {
        var model = new QuestionPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Prompt))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Prompt), S["Write the question."]);
        }

        if (model.Points <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Points), S["A question is worth more than zero points."]);
        }

        part.Prompt = string.IsNullOrWhiteSpace(model.Prompt) ? null : _htmlSanitizer.Sanitize(model.Prompt.Trim());
        part.Points = model.Points;
        part.Difficulty = Math.Clamp(model.Difficulty, 1, 5);
        part.Tags = (model.Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        part.GeneralFeedback = string.IsNullOrWhiteSpace(model.GeneralFeedback) ? null : _htmlSanitizer.Sanitize(model.GeneralFeedback.Trim());

        return Edit(part, context);
    }
}
