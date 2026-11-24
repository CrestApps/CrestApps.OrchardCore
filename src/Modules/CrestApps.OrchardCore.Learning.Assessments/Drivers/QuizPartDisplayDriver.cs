using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys;
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
/// attempts combine into the grade. It warns when the quiz completes "When the learner passes" but its survey (the draft
/// or the published version) leaves some score with no pass decision: no pass mark, and a score band that neither passes
/// nor fails or no band at all for that score, so a graded attempt with that score completes it. It also warns when the
/// quiz counts toward the course grade but its survey is anonymous: an anonymous quiz records the completion only, so it
/// stays out of the course grade.
/// </summary>
internal sealed class QuizPartDisplayDriver : ContentPartDisplayDriver<QuizPart>
{
    private readonly ISurveyManager _surveyManager;
    private readonly ISurveyVersionCache _versions;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="versions">The survey versions, for the published one.</param>
    /// <param name="contentDefinitionManager">The content definition manager, for the quiz's default completion mode.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public QuizPartDisplayDriver(
        ISurveyManager surveyManager,
        ISurveyVersionCache versions,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<QuizPartDisplayDriver> stringLocalizer)
    {
        _surveyManager = surveyManager;
        _versions = versions;
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
            model.NoPassDecision = model.Survey is { } survey
                && string.Equals(await CompletionModeAsync(part), CompletionModes.Passed, StringComparison.Ordinal)
                && (!DecidesPass(survey.Draft) || (await PublishedDefinitionAsync(survey) is { } published && !DecidesPass(published)));
            model.AnonymousGradable = model.Survey?.Access?.Anonymity == AnonymityMode.Anonymous
                && part.ContentItem?.TryGet<CourseItemPart>(out var gradedItem) == true
                && gradedItem.Gradable;
            model.GradingMethods =
            [
                new SelectListItem(S["The highest score"], nameof(GradingMethod.Highest)),
                new SelectListItem(S["The average score"], nameof(GradingMethod.Average)),
                new SelectListItem(S["The first attempt"], nameof(GradingMethod.First)),
                new SelectListItem(S["The latest attempt"], nameof(GradingMethod.Latest)),
            ];
        });

    /// <summary>
    /// Gets whether a survey decides a pass for every score from 0 to 100, as <see cref="SurveyPassRule"/> decides it: it
    /// has a pass mark, or every score falls in a score band that passes or fails (a band starts at 0 or below, and no band
    /// a score can reach leaves the decision to the missing pass mark).
    /// </summary>
    /// <param name="definition">The survey definition.</param>
    /// <returns><see langword="true"/> when every score has a pass decision.</returns>
    internal static bool DecidesPass(SurveyDefinition definition)
    {
        if (definition?.Scoring?.PassMarkPercent is not null)
        {
            return true;
        }

        var bands = (definition?.ScoreBands ?? []).Where(band => band is not null && band.MinPercent <= 100).ToList();

        return bands.Any(band => band.MinPercent <= 0) && bands.All(band => band.Passing is not null);
    }

    private async Task<SurveyDefinition> PublishedDefinitionAsync(Survey survey)
        => (await _versions.GetPublishedAsync(survey))?.Definition;

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
