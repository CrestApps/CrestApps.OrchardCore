using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Creates the survey a quiz owns when the quiz is first saved: a scored survey from the quiz template.
/// </summary>
public sealed class QuizPartHandler : ContentPartHandler<QuizPart>
{
    private readonly ILearningSurveyService _learningSurveys;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartHandler"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    public QuizPartHandler(ILearningSurveyService learningSurveys)
    {
        _learningSurveys = learningSurveys;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreateContentContext context, QuizPart part)
        => EnsureSurveyAsync(part);

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdateContentContext context, QuizPart part)
        => EnsureSurveyAsync(part);

    private async Task EnsureSurveyAsync(QuizPart part)
    {
        if (!string.IsNullOrEmpty(part.SurveyId))
        {
            return;
        }

        var survey = await _learningSurveys.CreateOwnedSurveyAsync(part.ContentItem, SurveyKinds.Quiz, SurveyTemplateProvider.Quiz);

        part.SurveyId = survey?.ItemId;
        part.ContentItem.Apply(part);
    }
}
