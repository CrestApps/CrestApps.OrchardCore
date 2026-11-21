using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// Shows, in the quiz editor, the survey the quiz owns with links to design it and to its settings, and edits how the
/// attempts combine into the grade.
/// </summary>
internal sealed class QuizPartDisplayDriver : ContentPartDisplayDriver<QuizPart>
{
    private readonly ISurveyManager _surveyManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public QuizPartDisplayDriver(ISurveyManager surveyManager, IStringLocalizer<QuizPartDisplayDriver> stringLocalizer)
    {
        _surveyManager = surveyManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(QuizPart part, BuildPartEditorContext context)
        => Initialize<QuizPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Survey = string.IsNullOrEmpty(part.SurveyId) ? null : await _surveyManager.FindByIdAsync(part.SurveyId);
            model.QuestionCount = model.Survey?.Draft?.Pages.Sum(page => page.Questions.Count(question => string.IsNullOrEmpty(question.PoolId)) + page.Pools.Sum(pool => pool.DrawCount)) ?? 0;
            model.GradingMethod = part.GradingMethod;
            model.GradingMethods =
            [
                new SelectListItem(S["The highest score"], nameof(GradingMethod.Highest)),
                new SelectListItem(S["The average score"], nameof(GradingMethod.Average)),
                new SelectListItem(S["The first attempt"], nameof(GradingMethod.First)),
                new SelectListItem(S["The latest attempt"], nameof(GradingMethod.Latest)),
            ];
        });

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(QuizPart part, UpdatePartEditorContext context)
    {
        var model = new QuizPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, item => item.GradingMethod);

        part.GradingMethod = Enum.IsDefined(model.GradingMethod) ? model.GradingMethod : GradingMethod.Highest;

        return Edit(part, context);
    }
}
