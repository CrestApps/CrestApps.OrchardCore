using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// Shows, in the quiz editor, the survey the quiz owns with links to design it and to its settings, and edits how the
/// attempts combine into the grade. It warns when the quiz completes "When the learner passes" but its survey has
/// neither a pass mark nor a score band that passes or fails: nothing decides a pass, so any graded attempt completes it.
/// </summary>
internal sealed class QuizPartDisplayDriver : ContentPartDisplayDriver<QuizPart>
{
    private readonly ISurveyManager _surveyManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="contentDefinitionManager">The content definition manager, for the quiz's default completion mode.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public QuizPartDisplayDriver(ISurveyManager surveyManager, IContentDefinitionManager contentDefinitionManager, IStringLocalizer<QuizPartDisplayDriver> stringLocalizer)
    {
        _surveyManager = surveyManager;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(QuizPart part, BuildPartEditorContext context)
        => Initialize<QuizPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Survey = string.IsNullOrEmpty(part.SurveyId) ? null : await _surveyManager.FindByIdAsync(part.SurveyId);
            model.QuestionCount = model.Survey?.Draft?.Pages.Sum(page => page.Questions.Count(question => string.IsNullOrEmpty(question.PoolId)) + page.Pools.Sum(pool => pool.DrawCount)) ?? 0;
            model.GradingMethod = part.GradingMethod;
            model.NoPassDecision = model.Survey?.Draft is { } draft
                && !DecidesPass(draft)
                && string.Equals(await CompletionModeAsync(part), CompletionModes.Passed, StringComparison.Ordinal);
            model.GradingMethods =
            [
                new SelectListItem(S["The highest score"], nameof(GradingMethod.Highest)),
                new SelectListItem(S["The average score"], nameof(GradingMethod.Average)),
                new SelectListItem(S["The first attempt"], nameof(GradingMethod.First)),
                new SelectListItem(S["The latest attempt"], nameof(GradingMethod.Latest)),
            ];
        });

    /// <summary>
    /// Gets whether a survey can decide a pass: it has a pass mark or a score band that passes or fails.
    /// </summary>
    /// <param name="definition">The survey definition.</param>
    /// <returns><see langword="true"/> when something decides a pass.</returns>
    internal static bool DecidesPass(SurveyDefinition definition)
        => definition?.Scoring?.PassMarkPercent is not null
            || (definition?.ScoreBands ?? []).Any(band => band?.Passing is not null);

    private async Task<string> CompletionModeAsync(QuizPart part)
    {
        var mode = part.ContentItem?.TryGet<CourseItemPart>(out var courseItem) == true ? courseItem.CompletionMode : null;

        if (!string.IsNullOrEmpty(mode))
        {
            return mode;
        }

        var type = string.IsNullOrEmpty(part.ContentItem?.ContentType) ? null : await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
        var settings = type?.Parts
            .FirstOrDefault(candidate => string.Equals(candidate.PartDefinition?.Name, nameof(CourseItemPart), StringComparison.Ordinal))
            ?.GetSettings<CourseItemPartSettings>();

        // A quiz completes "When the learner passes" unless its type says otherwise.
        return settings?.DefaultCompletionMode ?? CompletionModes.Passed;
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(QuizPart part, UpdatePartEditorContext context)
    {
        var model = new QuizPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, item => item.GradingMethod);

        part.GradingMethod = Enum.IsDefined(model.GradingMethod) ? model.GradingMethod : GradingMethod.Highest;

        return Edit(part, context);
    }
}
