using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Creates the survey a quiz owns when the quiz is first saved: a scored survey from the quiz template. A copied quiz
/// gets a copy of the original's survey, never the original itself. A quiz whose survey is gone (such as one restored
/// from the audit trail after its removal purged the survey) gets a new one when it is next saved, so it never
/// points at nothing. An import keeps the survey id it brings, also when it updates a quiz the site has: a deployment
/// may run its content step before the Surveys step that brings the survey, and a new survey made then would leave the
/// quiz pointing at a blank survey while the imported one is orphaned.
/// </summary>
public sealed class QuizPartHandler : ContentPartHandler<QuizPart>
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ISurveyManager _surveyManager;
    private readonly ILogger _logger;
    private readonly HashSet<string> _importing = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizPartHandler"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="logger">The logger.</param>
    public QuizPartHandler(ILearningSurveyService learningSurveys, ISurveyManager surveyManager, ILogger<QuizPartHandler> logger)
    {
        _learningSurveys = learningSurveys;
        _surveyManager = surveyManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreateContentContext context, QuizPart part)
        => EnsureSurveyAsync(part, recreateMissing: false);

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdateContentContext context, QuizPart part)
        => EnsureSurveyAsync(part, recreateMissing: !_importing.Contains(part.ContentItem.ContentItemId));

    /// <inheritdoc />
    public override Task ImportingAsync(ImportContentContext context, QuizPart part)
    {
        if (!string.IsNullOrEmpty(part?.ContentItem?.ContentItemId))
        {
            _importing.Add(part.ContentItem.ContentItemId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ImportedAsync(ImportContentContext context, QuizPart part)
    {
        if (!string.IsNullOrEmpty(part?.ContentItem?.ContentItemId))
        {
            _importing.Remove(part.ContentItem.ContentItemId);
        }

        return Task.CompletedTask;
    }

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

        // A saved quiz whose survey is gone is healed with a new blank survey: the warning lets the lost survey be traced.
        if (!string.IsNullOrEmpty(part.SurveyId))
        {
            _logger.LogWarning("The survey '{SurveyId}' of the quiz '{ContentItemId}' was not found, so the quiz was given a new blank survey.", part.SurveyId, part.ContentItem.ContentItemId);
        }

        var survey = await _learningSurveys.CreateOwnedSurveyAsync(part.ContentItem, SurveyKinds.Quiz, SurveyTemplateNames.Quiz);

        part.SurveyId = survey?.ItemId;
        part.ContentItem.Apply(part);
    }
}
