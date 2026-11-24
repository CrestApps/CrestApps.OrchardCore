using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class QuestionPartDisplayDriver : ContentPartDisplayDriver<QuestionPart>
{
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly QuestionTypeOptions _questionTypes;

    internal readonly IStringLocalizer S;

    public QuestionPartDisplayDriver(IHtmlSanitizerService htmlSanitizer, IOptions<QuestionTypeOptions> questionTypes, IStringLocalizer<QuestionPartDisplayDriver> stringLocalizer)
    {
        _htmlSanitizer = htmlSanitizer;
        _questionTypes = questionTypes.Value;
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

            var type = _questionTypes.FindByContentType(part.ContentItem.ContentType);
            model.Informational = type?.Informational == true;
            model.MayBeUngraded = type?.MayBeUngraded == true;

            // A new survey question starts ungraded, and a description is never worth points.
            if (model.Informational || (context.IsNew && model.MayBeUngraded && part.Points == new QuestionPart().Points))
            {
                model.Points = 0;
            }
        });

    public override async Task<IDisplayResult> UpdateAsync(QuestionPart part, UpdatePartEditorContext context)
    {
        var model = new QuestionPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var type = _questionTypes.FindByContentType(part.ContentItem.ContentType);

        if (string.IsNullOrWhiteSpace(model.Prompt))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Prompt), type?.Informational == true ? S["Write the text."] : S["Write the question."]);
        }

        if (type?.Informational == true)
        {
            model.Points = 0;
        }
        else if (type?.MayBeUngraded == true ? model.Points < 0 : model.Points <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Points), type?.MayBeUngraded == true
                ? S["Points cannot be below zero."]
                : S["A question is worth more than zero points."]);
        }

        part.Prompt = string.IsNullOrWhiteSpace(model.Prompt) ? null : _htmlSanitizer.Sanitize(model.Prompt.Trim());
        part.Points = model.Points;
        part.Difficulty = Math.Clamp(model.Difficulty, 1, 5);
        part.Tags = (model.Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        part.GeneralFeedback = string.IsNullOrWhiteSpace(model.GeneralFeedback) ? null : _htmlSanitizer.Sanitize(model.GeneralFeedback.Trim());

        return Edit(part, context);
    }
}
