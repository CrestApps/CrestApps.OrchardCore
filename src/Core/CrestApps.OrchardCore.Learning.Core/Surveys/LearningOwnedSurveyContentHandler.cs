using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Removes the survey a course item owns when the item is removed: deleted when nobody responded, archived otherwise so
/// its responses and results stay.
/// </summary>
public sealed class LearningOwnedSurveyContentHandler : ContentHandlerBase
{
    private readonly ISurveyManager _surveyManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningOwnedSurveyContentHandler"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    public LearningOwnedSurveyContentHandler(ISurveyManager surveyManager)
    {
        _surveyManager = surveyManager;
    }

    /// <inheritdoc />
    public override async Task RemovedAsync(RemoveContentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Removing one version of a draftable item leaves the item: only removing all of it removes its survey.
        if (!context.NoActiveVersionLeft)
        {
            return;
        }

        foreach (var survey in await _surveyManager.ListByOwnerAsync(LearningSurveyContexts.Owner(context.ContentItem.ContentItemId)))
        {
            if (await _surveyManager.DeleteSurveyAsync(survey.ItemId) == SurveyErrorCode.None)
            {
                continue;
            }

            if (survey.Status == SurveyStatus.Open)
            {
                await _surveyManager.ChangeStatusAsync(survey.ItemId, SurveyStatus.Closed);
            }

            if (survey.Status != SurveyStatus.Draft)
            {
                await _surveyManager.ChangeStatusAsync(survey.ItemId, SurveyStatus.Archived);
            }
        }
    }
}
