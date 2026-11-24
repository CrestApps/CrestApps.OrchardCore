using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Creates the survey a quiz owns when the quiz is first saved: a scored survey from the quiz template. A copied quiz
/// gets a copy of the original's survey, never the original itself. A quiz whose survey is gone (such as one restored
/// from the audit trail after its removal purged the survey) gets a new one when it is next saved, so it never
/// points at nothing.
/// </summary>
public sealed class QuizPartHandler : ContentPartHandler<QuizPart>
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ISurveyManager _surveyManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartHandler"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="surveyManager">The survey manager.</param>
    public QuizPartHandler(ILearningSurveyService learningSurveys, ISurveyManager surveyManager)
    {
        _learningSurveys = learningSurveys;
        _surveyManager = surveyManager;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreateContentContext context, QuizPart part)
        => EnsureSurveyAsync(part, recreateMissing: false);

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdateContentContext context, QuizPart part)
        => EnsureSurveyAsync(part, recreateMissing: true);

    /// <inheritdoc />
    public override async Task CloningAsync(CloneContentContext context, QuizPart part)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CloneContentItem is null || !context.CloneContentItem.TryGet<QuizPart>(out var clone))
        {
            return;
        }

        var survey = await _learningSurveys.CopyOwnedSurveyAsync(context.CloneContentItem, part.SurveyId, SurveyKinds.Quiz, SurveyTemplateNames.Quiz);

        clone.SurveyId = survey?.ItemId;
        context.CloneContentItem.Apply(clone);
    }

    // A new item keeps the survey id it comes with (an import may bring the survey after the item); a saved item whose
    // survey is gone gets a new one.
    private async Task EnsureSurveyAsync(QuizPart part, bool recreateMissing)
    {
        if (!string.IsNullOrEmpty(part.SurveyId)
            && (!recreateMissing || await _surveyManager.FindByIdAsync(part.SurveyId) is not null))
        {
            return;
        }

        var survey = await _learningSurveys.CreateOwnedSurveyAsync(part.ContentItem, SurveyKinds.Quiz, SurveyTemplateNames.Quiz);

        part.SurveyId = survey?.ItemId;
        part.ContentItem.Apply(part);
    }
}
