using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.ViewModels;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Surveys.Drivers;

/// <summary>
/// Shows, in the course item editor, the survey the item owns with links to design it and to its settings.
/// </summary>
internal sealed class CourseSurveyPartDisplayDriver : ContentPartDisplayDriver<CourseSurveyPart>
{
    private readonly ISurveyManager _surveyManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseSurveyPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    public CourseSurveyPartDisplayDriver(ISurveyManager surveyManager)
    {
        _surveyManager = surveyManager;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(CourseSurveyPart part, BuildPartEditorContext context)
        => Initialize<CourseSurveyPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Survey = string.IsNullOrEmpty(part.SurveyId) ? null : await _surveyManager.FindByIdAsync(part.SurveyId);
            model.SingleQuestion = context.TypePartDefinition?.GetSettings<CourseSurveyPartSettings>()?.SingleQuestion == true;
            model.QuestionCount = model.Survey?.Draft?.Pages.Sum(page => page.Questions.Count) ?? 0;
        });
}
